using UnityEngine;

public class LaserPainter : MonoBehaviour
{
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

    // Internal tracking
    private LineRenderer laserBeam; 
    private LineRenderer currentLine;
    private Collider paperCollider; 
    private Vector3 smoothedPosition;
    private Vector3 velocity         = Vector3.zero;
    private bool isCurrentlyPainting = false;

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
    }

    void Update()
    {
        if (manoReceiver == null || paperCollider == null || linePrefab == null) return;

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

            // Draw ONLY when pointing up
            if (manoReceiver.currentRightGesture == "Pointing_Up" && isAimingAtPaper)
            {
                // Smooth the hit point to prevent hand jitter
                smoothedPosition  = Vector3.SmoothDamp(smoothedPosition, targetInkPos, ref velocity, smoothTime);
                smoothedPosition  = paperCollider.ClosestPoint(smoothedPosition);
                smoothedPosition += (paperTransform.up * 0.002f); // Clamp...

                if (!isCurrentlyPainting) StartNewLine(smoothedPosition);
                else                      UpdateCurrentLine(smoothedPosition);
            }
            else
            {
                // Lift the brush
                isCurrentlyPainting = false;
                
                // Snap the smoothed position to the laser dot when NOT drawing
                smoothedPosition = targetInkPos;
                velocity         = Vector3.zero;
            }
        }
        else
        {
            // Hand is totally lost
            isCurrentlyPainting = false;
            if (laserBeam != null) laserBeam.enabled = false;
        }
    }

    private void StartNewLine(Vector3 startPos)
    {
        GameObject newLineObj = Instantiate(linePrefab, Vector3.zero, Quaternion.identity);
        currentLine           = newLineObj.GetComponent<LineRenderer>();
        
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
    
    void OnDisable()
    {
        if (laserBeam != null) laserBeam.enabled = false;
        isCurrentlyPainting = false;
    }
}
