using UnityEngine;

public class CameraRingController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the GameObject holding the ManoLiveReceiver script here.")]
    public ManoLiveReceiver manoReceiver;

    [Header("Ring Settings")]
    // What the camera circles around
    public Vector3 centerPoint = new Vector3(1.0f, 0.0f, -1.0f);
    // How fast the camera moves around the ring
    public float moveSpeed = 100.0f;

    [Header("Grapple Camera Settings")]
    public bool isGrapplingMode = false;
    public Transform bearTransform;
    public Vector3 thirdPersonOffset = new Vector3(0, 0.3f, -0.8f);

    // These will be automatically calculated in Start()...
    private float radius;
    private float height;
    private float currentAngle;
    private float startingPitch; // Camera's initial X-axis rotation

    void Start()
    {
        // Grab the starting height
        height = transform.position.y;

        // Calculate the offset from the center point
        float dx = transform.position.x - centerPoint.x;
        float dz = transform.position.z - centerPoint.z;

        // Calculate the starting radius
        radius = Mathf.Sqrt(dx * dx + dz * dz);

        // Calculate the starting angle
        currentAngle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;

        startingPitch = transform.eulerAngles.x;
    }

    void Update()
    {
        if (manoReceiver == null) return; // Ensure we have the receiver linked

        // Logic for 3rd person camera (GRAPPLING MODE)
        if (isGrapplingMode && bearTransform != null)
        {
            Vector3 targetPosition = bearTransform.position + bearTransform.TransformDirection(thirdPersonOffset);
            transform.position     = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 5f);

            transform.LookAt(bearTransform.position + Vector3.up * 0.5f);

            return; // Skip the ring logic
        }

        // Fetch the current gestures
        string left  = manoReceiver.currentLeftGesture;
        string right = manoReceiver.currentRightGesture;

        // Determine movement based on gesture combinations
        if (left == "Pointing_Up" && right == "Closed_Fist")
            currentAngle += moveSpeed * Time.deltaTime;
        else if (left == "Closed_Fist" && right == "Pointing_Up")
            currentAngle -= moveSpeed * Time.deltaTime;
        float angleRad = currentAngle * Mathf.Deg2Rad;

        // Apply trigonometric formula for a circle on the X/Z plane
        float newX = centerPoint.x + radius * Mathf.Cos(angleRad);
        float newZ = centerPoint.z + radius * Mathf.Sin(angleRad);

        // Update the camera's position + look at the center point
        transform.position = new Vector3(newX, height, newZ);
        transform.LookAt(centerPoint);

        // Re-apply tilt to the X-axis
        Vector3 preservedRotation = transform.eulerAngles;
        preservedRotation.x       = startingPitch;
        transform.eulerAngles     = preservedRotation;
    }
}
