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

    [Header("Left Hand Bones (drives RIGHT hand skeleton)")]
    [Tooltip("Order: 0:Wrist, 1-3:Index, 4-6:Middle, 7-9:Pinky, 10-12:Ring, 13-15:Thumb")]
    public Transform[] leftBones = new Transform[16];

    [Header("Right Hand Bones (drives LEFT hand skeleton)")]
    [Tooltip("Order: 0:Wrist, 1-3:Index, 4-6:Middle, 7-9:Pinky, 10-12:Ring, 13-15:Thumb")]
    public Transform[] rightBones = new Transform[16];

    private Thread receiveThread;
    private UdpClient client;
    private string latestJson = "";
    private readonly object lockObject = new object();

    // Data structure -> match Python JSON payload
    public class ManoPayload
    {
        public float[][] left_pose  { get; set; }
        public float[][] right_pose { get; set; }
    }

    void Start()
    {
        receiveThread = new Thread(new ThreadStart(ReceiveData))
        {
            IsBackground = true
        };
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
        string currentJson;
        lock (lockObject)
        {
            if (string.IsNullOrEmpty(latestJson)) return;
            currentJson = latestJson;
            latestJson = ""; // Consume the data
        }

        try
        {
            ManoPayload payload = JsonConvert.DeserializeObject<ManoPayload>(currentJson);

            if (payload.left_pose != null)  ApplyPose(leftBones, payload.left_pose);
            
            if (payload.right_pose != null) ApplyPose(rightBones, payload.right_pose);
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

    void OnApplicationQuit()
    {
        if (receiveThread != null) receiveThread.Abort();
        if (client != null) client.Close();
    }
}
