using UnityEngine;

public class CameraRingController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the GameObject holding the ManoLiveReceiver script here.")]
    public ManoLiveReceiver manoReceiver;
    
    [Tooltip("Drag the Bear holding the BearGrappleController script here.")]
    public BearGrappleController grappleController;

    [Header("Ring Settings")]
    // What the camera circles around
    public Vector3 centerPoint = new Vector3(1.0f, 0.0f, -1.0f);
    // How fast the camera moves around the ring
    public float moveSpeed = 45.0f;

    [Header("Grapple Camera Settings")]
    public bool isGrapplingMode = false;
    public Transform bearTransform;
    public Vector3 thirdPersonOffset = new Vector3(0, 0.3f, -1.0f);
    
    [Header("Grapple Steering Settings")]
    public float steeringSmoothTime = 0.1f; 

    // These will be automatically calculated in Start()...
    private float radius;
    private float height;
    private float currentAngle;
    private float startingPitch; // Camera's initial X-axis rotation

    private float grappleYaw    = 0f; 
    private float currentSteeringVelocity;
    private float lockedBaseYaw = 0f;

    private Vector3 smoothedLookTarget;
    private Vector3 lookTargetVelocity;

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

        if (bearTransform != null) 
        {
            grappleYaw         = bearTransform.eulerAngles.y;
            lockedBaseYaw      = grappleYaw;
            smoothedLookTarget = bearTransform.position + Vector3.up * 0.5f; 
        }
    }

    void LateUpdate()
    {
        if (manoReceiver == null || grappleController == null) return; // Ensure we have the receiver linked

        // Logic for 3rd person camera (GRAPPLING MODE)
        if (isGrapplingMode && bearTransform != null)
        {
            Transform physicalLeftHand = manoReceiver.rightHandRoot;
            string physicalLeftGesture = manoReceiver.currentRightGesture;

            Transform physicalRightHand = manoReceiver.leftHandRoot;
            string physicalRightGesture = manoReceiver.currentLeftGesture;

            bool leftGrabbing  = (grappleController.leftJoint  != null && physicalLeftGesture  == "Closed_Fist");
            bool rightGrabbing = (grappleController.rightJoint != null && physicalRightGesture == "Closed_Fist");

            Transform steeringHand = null;
            Vector3 targetLaserEnd = Vector3.zero;

            if (leftGrabbing && !rightGrabbing) 
            {
                steeringHand   = physicalRightHand; 
                targetLaserEnd = grappleController.rightLaserEnd; // Left is grabbing, right is free
            }
            else if (rightGrabbing && !leftGrabbing) 
            {
                steeringHand   = physicalLeftHand;  
                targetLaserEnd = grappleController.leftLaserEnd; // Right is grabbing, left is free
            }

            bool isSteering = (steeringHand != null);

            if (!isSteering)
            {
                // BEHAVIOR standing
                transform.position = bearTransform.position + bearTransform.TransformDirection(thirdPersonOffset); // No smooth transition...

                transform.LookAt(bearTransform.position + Vector3.up * 0.5f);

                lockedBaseYaw = bearTransform.eulerAngles.y;
                grappleYaw    = bearTransform.eulerAngles.y;
            }
            else
            {
                // BEHAVIOR holding the rope
                Vector3 rawOffset = steeringHand.localPosition;
                rawOffset.Scale(grappleController.movementScale);

                Quaternion lockedRotation = Quaternion.Euler(0, lockedBaseYaw, 0);
                Vector3 virtualCenterPos  = bearTransform.position + (lockedRotation * grappleController.centerOffset);
                Vector3 grappleBallPos    = virtualCenterPos + (lockedRotation * rawOffset);

                Vector3 aimDirection = (grappleBallPos - bearTransform.position).normalized;
                aimDirection.y       = 0; 

                if (aimDirection.sqrMagnitude > 0.001f)
                {
                    float targetGrappleYaw = Quaternion.LookRotation(aimDirection).eulerAngles.y;
                    grappleYaw             = Mathf.SmoothDampAngle(grappleYaw, targetGrappleYaw, ref currentSteeringVelocity, steeringSmoothTime);
                }

                // Apply the dynamic rotation to the camera offset
                Quaternion dynamicRotation = Quaternion.Euler(0, grappleYaw, 0);
                Vector3 rotatedOffset      = dynamicRotation * thirdPersonOffset;
                transform.position         = bearTransform.position + rotatedOffset;

                // Lock the camera to the END OF THE HELP LASER
                smoothedLookTarget = Vector3.SmoothDamp(smoothedLookTarget, targetLaserEnd, ref lookTargetVelocity, steeringSmoothTime);
                transform.LookAt(smoothedLookTarget);

                // Make the bear to face the direction the camera turned
                bearTransform.rotation = Quaternion.Euler(0, grappleYaw, 0);
            }

            return; // Skip the ring logic
        }

        // Fetch the current gestures - BEHAVIOR ring
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
