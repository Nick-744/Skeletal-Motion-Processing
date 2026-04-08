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

    private GameObject braceletVisual;
    private GameObject band;
    private GameObject[] balls = new GameObject[3];

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
            
            balls[i].transform.localPosition = rotatedOrbitPos + braceletPositionOffset;
            balls[i].transform.localScale    = Vector3.one * ballScale;
            balls[i].GetComponent<Renderer>().material.color = Color.green;
            
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
    }
}
