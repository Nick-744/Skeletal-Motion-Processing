using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

public partial class LaserPainter
{
    // ---< SAVING FUNCTIONALITY >--- //

    public void SaveDrawing()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string timestamp   = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        if (showRotationAxis)
        {
            string filePath = Path.Combine(desktopPath, "LatheDrawing_" + timestamp + ".ply");
            ExportLathePLY(filePath);
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
        GameObject camObj         = new GameObject("TempRenderCam");
        Camera renderCam          = camObj.AddComponent<Camera>();
        renderCam.orthographic    = true;
        renderCam.clearFlags      = CameraClearFlags.SolidColor;
        renderCam.backgroundColor = Color.clear;
        
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
        
        Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGBA32, false);
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

    private void ExportLathePLY(string filePath)
    {
        // Generate a point cloud by rotating the 2D ink lines 360 degrees
        int radialSegments = 120;
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

        // Write PLY
        using (StreamWriter sw = new StreamWriter(filePath))
        {
            // Write PLY Header
            sw.WriteLine("ply");
            sw.WriteLine("format ascii 1.0");
            sw.WriteLine($"element vertex {allPoints.Count}");
            sw.WriteLine("property float x");
            sw.WriteLine("property float y");
            sw.WriteLine("property float z");
            sw.WriteLine("end_header");

            // Write Point Data
            foreach (Vector3 p in allPoints) sw.WriteLine(System.FormattableString.Invariant($"{p.x} {p.y} {p.z}"));
        }

        ShowFeedback("Saved PLY");
    }
}
