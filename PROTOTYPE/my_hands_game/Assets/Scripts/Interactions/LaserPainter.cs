using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.IO;
using System;

public class LaserPainter : MonoBehaviour
{
    public enum PaintMode { Paint, Erase }

    [Header("Dependencies")]
    public ManoLiveReceiver manoReceiver;
    [Tooltip("The Transform of the 'Paper' in scene.")]
    public Transform paperTransform;
    [Tooltip("BlackInk - Prefab")]
    public GameObject linePrefab;

    [Header("Accuracy & Drawing Settings")]
    [Tooltip("Filter out the jitter from hand tracking. Higher = Smoother + delayed.")]
    public float smoothTime       = 0.1f;
    [Tooltip("Minimum distance between points to optimize the line.")]
    public float minPointDistance = 0.01f;
    [Tooltip("How close the laser needs to be to a line to erase it.")]
    public float eraseRadius      = 0.03f;

    [Header("Current State")]
    public PaintMode currentMode = PaintMode.Paint;

    // Internal tracking
    private LineRenderer laserBeam; 
    private LineRenderer currentLine;
    private GameObject currentLineObj;
    private Collider paperCollider; 
    private Vector3 smoothedPosition;
    private Vector3 velocity         = Vector3.zero;
    private bool isCurrentlyPainting = false;

    // Lathe Mechanics
    private bool _showRotationAxis = false;
    public bool showRotationAxis
    {
        get { return _showRotationAxis; }
        set // Event Trigger...
        {
            if (_showRotationAxis == value) return;
            _showRotationAxis = value;

            if (paperTransform != null)
            {
                if (_showRotationAxis)
                {
                    ShrinkAndMovePaper();
                    CreateVisibleAxis();
                }
                else
                {
                    RestorePaper();
                    if (axisObj != null) axisObj.SetActive(false);
                }
            }
        }
    }
    
    private GameObject axisObj;
    private Vector3 originalPaperScale;
    private Vector3 originalPaperPos;
    private bool hasSavedOriginalPaper = false;

    // Undo/Redo - Erasing System
    private class PaintAction
    {
        public enum ActionType { Draw, Erase }
        public ActionType type;
        public GameObject lineObj;
    }
    
    private List<GameObject> activeLines = new List<GameObject>();
    private Stack<PaintAction> undoStack = new Stack<PaintAction>();
    private Stack<PaintAction> redoStack = new Stack<PaintAction>();
    private bool hasReleasedUndoRedo     = true;

    // Feedback Text
    private GameObject feedbackTextObject;
    private TextMeshPro feedbackText;
    private float feedbackTimer = 0f;

    void Start()
    {
        if (paperTransform != null) paperCollider = paperTransform.GetComponent<Collider>();

        // Generate Red Laser Beam
        GameObject laserObj     = new GameObject("GeneratedLaserBeam");
        laserBeam               = laserObj.AddComponent<LineRenderer>();
        laserBeam.positionCount = 2;
        laserBeam.useWorldSpace = true;
        laserBeam.startWidth    = 0.02f;
        laserBeam.endWidth      = 0.02f;
        laserBeam.material      = new Material(Shader.Find("Sprites/Default"));
        laserBeam.startColor    = Color.red;
        laserBeam.endColor      = Color.red;
        laserBeam.enabled       = false;

        CreateFeedbackText();
    }

    private void CreateVisibleAxis()
    {
        if (axisObj != null) { axisObj.SetActive(true); return; }

        axisObj = new GameObject("PaperRotationAxis");
        axisObj.transform.SetParent(paperTransform);
        axisObj.transform.localPosition = Vector3.zero;
        axisObj.transform.localRotation = Quaternion.identity;

        LineRenderer axisLr  = axisObj.AddComponent<LineRenderer>();
        axisLr.useWorldSpace = false;
        axisLr.positionCount = 2;
        axisLr.startWidth    = 0.05f;
        axisLr.endWidth      = 0.05f;
        axisLr.material      = new Material(Shader.Find("Sprites/Default"));
        axisLr.startColor    = Color.blue;
        axisLr.endColor      = Color.blue;
        
        // Original center axis -> Left edge of the paper - Local Space (shift to my right + halve the width)
        MeshFilter mf   = paperTransform.GetComponent<MeshFilter>();
        float leftEdgeX = (mf != null && mf.sharedMesh != null) ? -mf.sharedMesh.bounds.extents.x : -0.5f;

        // Draw the axis line along the (new) left edge
        axisLr.SetPosition(0, new Vector3(leftEdgeX, 0, -0.5f));
        axisLr.SetPosition(1, new Vector3(leftEdgeX, 0,  0.5f));
    }

    private void ShrinkAndMovePaper()
    {
        MeshFilter mf = paperTransform.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        if (!hasSavedOriginalPaper)
        {
            originalPaperScale    = paperTransform.localScale;
            originalPaperPos      = paperTransform.localPosition;
            hasSavedOriginalPaper = true;
        }

        float unscaledWidth = mf.sharedMesh.bounds.size.x;

        // Halve the local scale X
        Vector3 newScale          = paperTransform.localScale;
        newScale.x               *= 0.5f;
        paperTransform.localScale = newScale;

        // Shift paper to my right (half the new scaled width) -> New left edge = Original center
        float offset             = (unscaledWidth * newScale.x) * 0.5f; 
        paperTransform.position += paperTransform.right * offset;
    }

    private void RestorePaper()
    {
        if (hasSavedOriginalPaper && paperTransform != null)
        {
            paperTransform.localScale    = originalPaperScale;
            paperTransform.localPosition = originalPaperPos;
        }
    }

    private void CreateFeedbackText()
    {
        feedbackTextObject     = new GameObject("UndoRedo_FeedbackText");
        feedbackText = feedbackTextObject.AddComponent<TextMeshPro>();
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.fontSize  = 4f;
        feedbackText.color     = Color.black;
        feedbackTextObject.SetActive(false);
    }

    void Update()
    {
        if (manoReceiver == null || paperCollider == null || linePrefab == null) return;

        HandleUndoRedoGestures();
        HandleFeedbackText();

        Transform indexTip = manoReceiver.rightBones[3];

        if (indexTip != null)
        {
            Vector3 laserOrigin = indexTip.position;

            // Projection: Force the coordinate perfectly flat onto the paper's plane...
            Vector3 infinitePlanePoint = Vector3.ProjectOnPlane(laserOrigin - paperTransform.position, paperTransform.up) + paperTransform.position;

            // Ensure the point doesn't leave the physical edges of the paper
            Vector3 nearestPointOnPaper = paperCollider.ClosestPoint(infinitePlanePoint);

            // Check if aiming ON the paper or OFF the paper...
            bool isAimingAtPaper = Vector3.Distance(infinitePlanePoint, nearestPointOnPaper) < 0.005f;

            // ALWAYS show the laser beam from the finger to the paper!
            laserBeam.enabled = true;
            laserBeam.SetPosition(0, laserOrigin);
            laserBeam.SetPosition(1, nearestPointOnPaper);

            // Lift the ink so it rests ON TOP of the paper mesh...
            Vector3 targetInkPos = nearestPointOnPaper + (paperTransform.up * 0.002f);

            // Action ONLY when pointing up
            if (manoReceiver.currentRightGesture == "Pointing_Up" && isAimingAtPaper)
            {
                // Smooth the hit point to prevent hand jitter
                smoothedPosition  = Vector3.SmoothDamp(smoothedPosition, targetInkPos, ref velocity, smoothTime);
                smoothedPosition  = paperCollider.ClosestPoint(smoothedPosition);
                smoothedPosition += (paperTransform.up * 0.002f); // Clamp...

                if (currentMode == PaintMode.Paint)
                {
                    if (!isCurrentlyPainting) StartNewLine(smoothedPosition);
                    else                      UpdateCurrentLine(smoothedPosition);
                }
                else if (currentMode == PaintMode.Erase) TryEraseLine(smoothedPosition);
            }
            else
            {
                // Lift the brush / Stop erasing
                if (isCurrentlyPainting)
                {
                    isCurrentlyPainting = false;
                    FinalizeCurrentLine();
                }
                
                // Snap the smoothed position to the laser dot when NOT active
                smoothedPosition = targetInkPos;
                velocity         = Vector3.zero;
            }
        }
        else
        {
            // Hand is totally lost
            if (isCurrentlyPainting) FinalizeCurrentLine();
            isCurrentlyPainting = false;
            if (laserBeam != null) laserBeam.enabled = false;
        }
    }

    private void StartNewLine(Vector3 startPos)
    {
        currentLineObj = Instantiate(linePrefab, Vector3.zero, Quaternion.identity);
        currentLine    = currentLineObj.GetComponent<LineRenderer>();
        
        currentLine.positionCount = 1;
        currentLine.SetPosition(0, startPos);
        isCurrentlyPainting       = true;
    }

    private void UpdateCurrentLine(Vector3 newPos)
    {
        if (currentLine == null) return;

        Vector3 lastPos = currentLine.GetPosition(currentLine.positionCount - 1);
        if (Vector3.Distance(lastPos, newPos) >= minPointDistance)
        {
            currentLine.positionCount++;
            currentLine.SetPosition(currentLine.positionCount - 1, newPos);
        }
    }

    // ---< UNDO/REDO - ERASING SYSTEM FUNCTIONS >--- //

    private void FinalizeCurrentLine()
    {
        if (currentLineObj != null)
        {
            activeLines.Add(currentLineObj);
            undoStack.Push(new PaintAction { type = PaintAction.ActionType.Draw, lineObj = currentLineObj });
            redoStack.Clear(); // Clear redo history when a new action is made
            currentLineObj = null;
            currentLine    = null;
        }
    }

    private void TryEraseLine(Vector3 erasePos)
    {
        // Iterate backwards - safely remove from the list
        for (int i = activeLines.Count - 1; i >= 0; i--)
        {
            GameObject lineObj = activeLines[i];
            LineRenderer lr    = lineObj.GetComponent<LineRenderer>();
            
            Vector3[] positions = new Vector3[lr.positionCount];
            lr.GetPositions(positions);

            foreach (Vector3 point in positions)
            {
                if (Vector3.Distance(point, erasePos) <= eraseRadius)
                {
                    // Erase it!
                    lineObj.SetActive(false);
                    activeLines.RemoveAt(i);
                    
                    undoStack.Push(new PaintAction { type = PaintAction.ActionType.Erase, lineObj = lineObj });
                    redoStack.Clear();
                    break; 
                }
            }
        }
    }

    private void HandleUndoRedoGestures()
    {
        string rightGesture = manoReceiver.currentRightGesture;

        if (rightGesture == "Thumb_Down")
        {
            if (hasReleasedUndoRedo)
            {
                PerformUndo();
                hasReleasedUndoRedo = false;
            }
        }
        else if (rightGesture == "Thumb_Up")
        {
            if (hasReleasedUndoRedo)
            {
                PerformRedo();
                hasReleasedUndoRedo = false;
            }
        }
        else hasReleasedUndoRedo = true; // Requires release before firing again
    }

    private void PerformUndo()
    {
        if (undoStack.Count == 0) return;

        PaintAction action = undoStack.Pop();
        redoStack.Push(action);

        if (action.type == PaintAction.ActionType.Draw)
        {
            action.lineObj.SetActive(false);
            activeLines.Remove(action.lineObj);
        }
        else if (action.type == PaintAction.ActionType.Erase)
        {
            action.lineObj.SetActive(true);
            activeLines.Add(action.lineObj);
        }

        ShowFeedback("Undo");
    }

    private void PerformRedo()
    {
        if (redoStack.Count == 0) return;

        PaintAction action = redoStack.Pop();
        undoStack.Push(action);

        if (action.type == PaintAction.ActionType.Draw)
        {
            action.lineObj.SetActive(true);
            activeLines.Add(action.lineObj);
        }
        else if (action.type == PaintAction.ActionType.Erase)
        {
            action.lineObj.SetActive(false);
            activeLines.Remove(action.lineObj);
        }

        ShowFeedback("Redo");
    }

    private void ShowFeedback(string message)
    {
        feedbackText.text = message;
        feedbackTimer     = 1.0f;
        feedbackTextObject.SetActive(true);
        
        // Place near the wrist...
        if (manoReceiver.rightBones != null && manoReceiver.rightBones.Length > 0 && manoReceiver.rightBones[0] != null)
            feedbackTextObject.transform.position = manoReceiver.rightBones[0].position - new Vector3(0, 0.8f, 0);
    }

    private void HandleFeedbackText()
    {
        if (feedbackTimer > 0)
        {
            feedbackTimer -= Time.deltaTime;
            
            // Billboard text - always faces the player's camera
            if (Camera.main != null && feedbackTextObject.activeSelf)
            {
                Vector3 lookDirection                 = feedbackTextObject.transform.position - Camera.main.transform.position;
                feedbackTextObject.transform.rotation = Quaternion.LookRotation(lookDirection, Camera.main.transform.up);
            }
            
            if (feedbackTimer <= 0) feedbackTextObject.SetActive(false);
        }
    }

    // ---< SAVING FUNCTIONALITY >--- //

    public void SaveDrawing()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string timestamp   = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        if (showRotationAxis)
        {
            string filePath = Path.Combine(desktopPath, "LatheDrawing_" + timestamp + ".obj");
            ExportLatheOBJ(filePath);
        }
        else
        {
            string filePath = Path.Combine(desktopPath, "PaperDrawing_" + timestamp + ".png");
            StartCoroutine(ExportPaperPNG(filePath));
        }
    }

    private System.Collections.IEnumerator ExportPaperPNG(string filePath)
    {
        yield return new WaitForEndOfFrame();

        if (paperTransform == null) yield break;

        // Create temporary orthographic camera
        GameObject camObj      = new GameObject("TempRenderCam");
        Camera renderCam       = camObj.AddComponent<Camera>();
        renderCam.orthographic = true;
        
        MeshRenderer paperRenderer = paperTransform.GetComponent<MeshRenderer>();
        if (paperRenderer != null)
            renderCam.orthographicSize = paperRenderer.bounds.extents.x;
        else
            renderCam.orthographicSize = 0.5f;

        renderCam.transform.position = paperTransform.position + paperTransform.up * 1f;
        renderCam.transform.LookAt(paperTransform.position, paperTransform.forward);

        int resWidth            = 1024;
        int resHeight           = 1024;
        RenderTexture rt        = new RenderTexture(resWidth, resHeight, 24);
        renderCam.targetTexture = rt;
        
        Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
        renderCam.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        screenShot.Apply();
        
        renderCam.targetTexture = null;
        RenderTexture.active    = null;
        Destroy(rt);
        Destroy(camObj);

        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);
        
        ShowFeedback("Saved PNG");
    }

    private void ExportLatheOBJ(string filePath)
    {
        // Generate a point cloud by rotating the 2D ink lines 360 degrees
        int radialSegments = 36;
        float angleStep    = 360f / radialSegments;

        List<Vector3> allPoints = new List<Vector3>();

        // Rotation axis definition - Blue line (left edge of the paper)
        Vector3 axisOrigin    = paperTransform.position;
        Vector3 axisDirection = paperTransform.forward;

        if (axisObj != null)
        {
            MeshFilter mf   = paperTransform.GetComponent<MeshFilter>();
            float leftEdgeX = (mf != null && mf.sharedMesh != null) ? -mf.sharedMesh.bounds.extents.x : -0.5f;
            axisOrigin      = paperTransform.TransformPoint(new Vector3(leftEdgeX, 0, 0));
            axisDirection   = axisObj.transform.forward;
        }

        // Gather all line vertices
        List<Vector3> lineVertices = new List<Vector3>();
        foreach (GameObject lineObj in activeLines)
        {
            if (lineObj.activeSelf)
            {
                LineRenderer lr = lineObj.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    Vector3[] pos = new Vector3[lr.positionCount];
                    lr.GetPositions(pos);
                    lineVertices.AddRange(pos);
                }
            }
        }

        // Spin points
        for (int i = 0; i < radialSegments; i++)
        {
            float angle         = i * angleStep;
            Quaternion rotation = Quaternion.AngleAxis(angle, axisDirection);

            foreach (Vector3 p in lineVertices)
            {
                Vector3 localizedPoint = p - axisOrigin;
                Vector3 rotatedPoint   = rotation * localizedPoint;
                Vector3 finalPoint     = rotatedPoint + axisOrigin;
                allPoints.Add(finalPoint);
            }
        }

        // Write OBJ
        using (StreamWriter sw = new StreamWriter(filePath))
        {
            sw.WriteLine("# Lathe Point Cloud");
            foreach (Vector3 p in allPoints) sw.WriteLine(System.FormattableString.Invariant($"v {p.x} {p.y} {p.z}"));
        }

        ShowFeedback("Saved OBJ");
    }
    
    void OnDisable()
    {
        if (laserBeam != null) laserBeam.enabled = false;
        if (isCurrentlyPainting) FinalizeCurrentLine();
        isCurrentlyPainting = false;
    }
}
