using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

public class ManoLiveReceiver : MonoBehaviour
{
    [Header("UDP Settings")]
    public int port = 5052;

    [Header("Hand Models")]
    [Tooltip("The left MANO mesh")]
    public GameObject manoLeftModel;
    [Tooltip("The right MANO mesh")]
    public GameObject manoRightModel;

    [Header("Left Hand Bones")]
    public Transform leftHandRoot;
    [Tooltip("Order: 0:Wrist, 1-3:Index, 4-6:Middle, 7-9:Pinky, 10-12:Ring, 13-15:Thumb")]
    public Transform[] leftBones     = new Transform[16];
    public string currentLeftGesture = "None";

    [Header("Right Hand Bones")]
    public Transform rightHandRoot;
    [Tooltip("Order: 0:Wrist, 1-3:Index, 4-6:Middle, 7-9:Pinky, 10-12:Ring, 13-15:Thumb")]
    public Transform[] rightBones     = new Transform[16];
    public string currentRightGesture = "None";

    [Header("Anchor Settings")]
    [Tooltip("Scale/flip axes")]
    public Vector3 anchorMultiplier = new Vector3(0.01f, 0.01f, -0.005f);

    [Header("Global Game State")]
    public bool isGrapplingMode   = false;
    private bool wasGrapplingMode = false; // Tracks changes

    private Thread receiveThread;
    private UdpClient client;
    private string latestJson = "";
    private readonly object lockObject = new object();

    // Data structure -> match Python JSON payload
    public class ManoPayload
    {
        public float[][] left_pose  { get; set; }
        public float[][] right_pose { get; set; }

        public string left_gesture  { get; set; }
        public string right_gesture { get; set; }

        public float[] left_anchor  { get; set; }
        public float[] right_anchor { get; set; }
    }

    void Start()
    {
        receiveThread = new Thread(new ThreadStart(ReceiveData)) { IsBackground = true };

        receiveThread.Start();
        Debug.Log($"Listening for MANO data on port {port}...");
    }

    private void ReceiveData()
    {
        client           = new UdpClient(port);
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] data = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);

                // Thread safety: lock before updating the shared string
                lock (lockObject) latestJson = text;
            }
            catch (System.Exception e) { Debug.LogWarning(e.ToString()); }
        }
    }

    void Update()
    {
        // Toggle hand visibility (Grappling Mode)
        if (isGrapplingMode != wasGrapplingMode)
        {
            if (manoLeftModel  != null) manoLeftModel.SetActive(!isGrapplingMode);
            if (manoRightModel != null) manoRightModel.SetActive(!isGrapplingMode);

            wasGrapplingMode = isGrapplingMode;
        }

        string currentJson;
        lock (lockObject)
        {
            if (string.IsNullOrEmpty(latestJson)) return;
            currentJson = latestJson;
            latestJson  = ""; // Consume the data
        }

        try
        {
            ManoPayload payload = JsonConvert.DeserializeObject<ManoPayload>(currentJson);

            // ---< CROSS-MAPPING >--- //
            
            // Apply Poses
            if (payload.right_pose != null) ApplyPose(leftBones,  payload.right_pose);
            if (payload.left_pose  != null) ApplyPose(rightBones, payload.left_pose);

            // Update Gestures
            if (payload.right_gesture != null) currentLeftGesture  = payload.right_gesture;
            if (payload.left_gesture  != null) currentRightGesture = payload.left_gesture;

            // Apply Anchors
            if (payload.right_anchor != null && leftHandRoot  != null) ApplyAnchor(leftHandRoot,  payload.right_anchor);
            if (payload.left_anchor  != null && rightHandRoot != null) ApplyAnchor(rightHandRoot, payload.left_anchor);
        }
        catch (System.Exception e) { Debug.LogError($"Error parsing JSON: {e.Message}"); }
    }

    private void ApplyPose(Transform[] bones, float[][] poseData)
    {
        // MANO sends 16 joints
        for (int i = 0; i < poseData.Length && i < bones.Length; i++)
        {
            if (bones[i] == null) continue;

            float x = poseData[i][0];
            float y = poseData[i][1];
            float z = poseData[i][2];

            // ==============================================================================
            // The Python script (MANO) compresses the rotation into a single 3D vector [x, y, z].
            // Rather than using Euler angles (pitch/yaw/roll) or Quaternions, it uses "Axis-Angle":
            //
            // 1. THE AXIS (Direction): If you draw a line from (0,0,0) to this (x, y, z) coordinate, 
            //    that line acts as the physical "axle" or hinge that the bone spins around.
            // 2. THE ANGLE (Magnitude): The physical length of this vector tells us exactly *how much*
            //    the bone rotates around that axle, measured in radians.
            //
            // To use this in Unity, we have to unpack it: we calculate the vector's length to get 
            // the rotation amount, and then normalize the vector (set its length to 1) to get the 
            // pure directional axis for Unity's Quaternion.AngleAxis() function.
            // ==============================================================================
            Vector3 axisAngle = new Vector3(x, y, z);
            float angleRad    = axisAngle.magnitude;
            
            Vector3 axis   = axisAngle / angleRad; // Normalize to get the direction
            float angleDeg = angleRad * Mathf.Rad2Deg;
            
            Quaternion rotation    = Quaternion.AngleAxis(angleDeg, axis);
            bones[i].localRotation = rotation;
        }
    }

    private void ApplyAnchor(Transform root, float[] anchorData)
    {
        if (anchorData.Length < 3) return;

        float x = anchorData[0] * anchorMultiplier.x;
        float y = anchorData[1] * anchorMultiplier.y;
        float z = anchorData[2] * anchorMultiplier.z;
        
        root.localPosition = new Vector3(x, y, z);
    }

    void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (client != null) client.Close();
    }
}
