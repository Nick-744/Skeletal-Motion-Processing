using UnityEngine;

public class BraceletMenuController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the GameObject holding the ManoLiveReceiver script here.")]
    public ManoLiveReceiver manoReceiver;

    [Header("Bracelet Settings")]
    [Tooltip("Scale")]
    public Vector3 braceletScale          = new Vector3(0.6f, 0.06f, 0.7f);
    [Tooltip("Rotation Offset")]
    public Vector3 braceletRotationOffset = new Vector3(0f, 0f, 90f);
    [Tooltip("Position Offset")]
    public Vector3 braceletPositionOffset = new Vector3(-0.15f, 0f, 0f);

    [Header("Balls Settings")]
    [Tooltip("Distance of the balls from the center of the bracelet")]
    public float ballRadius = 0.5f;
    [Tooltip("Size of each ball")]
    public float ballScale  = 0.2f;

    [Header("Active Ball Settings")]
    [Tooltip("Size of the active ball below the bracelet")]
    public float activeBallScale = 0.4f;
    [Tooltip("Color of the normal balls")]
    public Color defaultColor    = Color.green;
    [Tooltip("Color of the active ball")]
    public Color activeColor     = Color.purple;
    [Tooltip("Local position to move the highest ball below the center of the bracelet")]
    public Vector3 belowBraceletLocalPosition = new Vector3(-0.5f, 0f, 0f);

    private float transitionSpeed = 5f;

    private GameObject braceletVisual;
    private GameObject band;
    private GameObject[] balls = new GameObject[3];
    
    // Cache original local positions to prevent infinite loops
    private Vector3[] defaultLocalPositions = new Vector3[3];

    void Start() { CreateYellowBracelet(); }

    private void CreateYellowBracelet()
    {
        braceletVisual = new GameObject("Yellow_Wristband");

        band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        band.transform.SetParent(braceletVisual.transform);
        
        // Scale, rotation and position offset
        band.transform.localScale       = braceletScale;
        band.transform.localEulerAngles = braceletRotationOffset;
        band.transform.localPosition    = braceletPositionOffset;
        
        band.GetComponent<Renderer>().material.color = Color.yellow;
        
        // Remove physics collider
        Destroy(band.GetComponent<Collider>());

        // Create the 3 Balls wrapped around
        for (int i = 0; i < 3; i++)
        {
            balls[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            balls[i].transform.SetParent(braceletVisual.transform);

            // Calculate angle
            float angleDegrees = i * 90f;
            float angleRadians = angleDegrees * Mathf.Deg2Rad;

            Vector3 cylinderSpacePos = new Vector3(Mathf.Cos(angleRadians) * ballRadius, 0f, Mathf.Sin(angleRadians) * ballRadius);
            Vector3 rotatedOrbitPos  = Quaternion.Euler(braceletRotationOffset) * cylinderSpacePos;
            
            defaultLocalPositions[i]         = rotatedOrbitPos + braceletPositionOffset; // Store the default local position
            balls[i].transform.localPosition = defaultLocalPositions[i];
            
            balls[i].transform.localScale    = Vector3.one * ballScale;
            balls[i].GetComponent<Renderer>().material.color = defaultColor;
            
            // Remove physics collider
            Destroy(balls[i].GetComponent<Collider>()); 
        }
    }

    void Update()
    {
        if (manoReceiver == null || manoReceiver.rightBones == null || manoReceiver.rightBones.Length == 0) return;

        // Wrist
        Transform rightWrist = manoReceiver.rightBones[0];
        if (rightWrist == null) return;

        // Snap!
        braceletVisual.transform.position = rightWrist.position;
        braceletVisual.transform.rotation = rightWrist.rotation;
        
        int highestBallIndex = 0;
        float maxY           = float.MinValue;

        for (int i = 0; i < 3; i++)
        {
            // Calculate where the ball WOULD be in world space if it were in its default position...
            Vector3 theoreticalWorldPos = braceletVisual.transform.TransformPoint(defaultLocalPositions[i]);

            if (theoreticalWorldPos.y > maxY)
            {
                maxY             = theoreticalWorldPos.y;
                highestBallIndex = i;
            }
        }

        // Update ball positions...
        for (int i = 0; i < 3; i++)
        {
            Vector3 targetLocalPosition;
            Vector3 targetScale;
            Color targetColor;

            if (i == highestBallIndex)
            {
                targetLocalPosition = belowBraceletLocalPosition;
                targetScale         = Vector3.one * activeBallScale;
                targetColor         = activeColor;
            }
            else
            {
                targetLocalPosition = defaultLocalPositions[i];
                targetScale         = Vector3.one * ballScale;
                targetColor         = defaultColor;
            }

            // Smoothly interpolate position, scale, and color
            balls[i].transform.localPosition = Vector3.Lerp(balls[i].transform.localPosition, targetLocalPosition, Time.deltaTime * transitionSpeed);
            balls[i].transform.localScale    = Vector3.Lerp(balls[i].transform.localScale, targetScale, Time.deltaTime * transitionSpeed);

            Material mat = balls[i].GetComponent<Renderer>().material;
            mat.color    = Color.Lerp(mat.color, targetColor, Time.deltaTime * transitionSpeed);
        }
    }
}
