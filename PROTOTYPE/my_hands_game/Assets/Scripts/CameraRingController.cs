using UnityEngine;

public class CameraRingController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("GameObject holding the ManoLiveReceiver script.")]
    public ManoLiveReceiver manoReceiver;
    
    [Tooltip("Bear holding the BearGrappleCore script.")]
    public BearGrappleCore grappleController;

    [Header("Ring Settings")]
    public bool isRingMode = true;
    [Tooltip("True -> Player Rig (camera + hands) orbits the center point.")]
    public bool movePlayerRig = false;
    // What the camera circles around
    public Vector3 centerPoint = new Vector3(1.0f, 0.0f, -1.0f);
    // How fast the camera moves around the ring
    public float moveSpeed = 45.0f;
    [Tooltip("The gesture name used to rotate the ring")]
    public string pointingUpGesture = "Pointing_Up";

    [Header("Grapple Camera Settings")]
    public bool isGrapplingMode = false;
    public bool isODMMode       = false;
    public Transform bearTransform;
    public Vector3 thirdPersonOffset = new Vector3(0, 0.3f, -1.0f);
    
    [Header("Grapple Steering Settings")]
    public float steeringSmoothTime = 0.1f;

    [Header("Traverse Settings")]
    public bool isTraverseMode = false;
    [Tooltip("Sensitivity multiplier (XYZ) - ROTATION")]
    public Vector3 traverseSensitivity = new Vector3(-0.25f, 2.0f, 2.0f);
    [Tooltip("Speed multiplier - MOVEMENT")]
    public float traverseMoveSpeed = 0.008f;
    [Tooltip("Deadzone threshold.")]
    public Vector3 traverseDeadzone = new Vector3(5.0f, 5.0f, 5.0f);
    [Tooltip("Parent object containing both the Camera and Hands.")]
    public Transform playerRig;

    public bool IsActivelyRotatingRing { get; private set; } // State Tracking for external scripts

    // Ring variables
    // These will be automatically calculated in Start()...
    private float radius;
    private float height;
    private float currentAngle;
    private float startingPitch; // Camera's initial X-axis rotation

    // Grapple variables
    private float grappleYaw    = 0f; 
    private float currentSteeringVelocity;
    private float lockedBaseYaw = 0f;

    private Vector3 smoothedLookTarget;
    private Vector3 lookTargetVelocity;

    // Traverse Mode Variables (Rotation)
    private bool wasTraverseRotating = false;
    private Quaternion initialCameraRot;
    private Vector3 initialLocalHandVector;
    private float initialLocalMidpointY;

    // Traverse Mode Variables (Movement)
    private bool wasTraverseMoving = false;
    private Vector3 initialSingleHandPos;

    // State Tracking
    private bool wasInSpecialMode = false;
    private Vector3 initialRigPosition;
    private Quaternion initialRigRotation;

    void Start()
    {
        Transform targetTransform = (movePlayerRig && playerRig != null) ? playerRig : transform;

        // Grab the starting height
        height = targetTransform.position.y;

        // Calculate the offset from the center point
        float dx = targetTransform.position.x - centerPoint.x;
        float dz = targetTransform.position.z - centerPoint.z;

        // Calculate the starting radius
        radius = Mathf.Sqrt(dx * dx + dz * dz);

        // Calculate the starting angle
        currentAngle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;

        startingPitch = targetTransform.eulerAngles.x;

        if (bearTransform != null) 
        {
            grappleYaw         = bearTransform.eulerAngles.y;
            lockedBaseYaw      = grappleYaw;
            smoothedLookTarget = bearTransform.position + Vector3.up * 0.5f; 
        }

        // Save default state of Player Rig
        if (playerRig != null)
        {
            initialRigPosition = playerRig.position;
            initialRigRotation = playerRig.rotation;
        }
    }

    void LateUpdate()
    {
        if (manoReceiver == null) return; // Ensure we have the receiver linked

        // Reset the state every frame
        IsActivelyRotatingRing = false;

        // Logic for 3rd person camera (GRAPPLING MODE)
        if (isGrapplingMode && bearTransform != null && grappleController != null)
        {
            HandleThirdPersonCamera(false);
            return; // Skip the ring logic
        }

        // Logic for 3rd person camera (ODM MODE)
        if (isODMMode && bearTransform != null && grappleController != null)
        {
            HandleThirdPersonCamera(true);
            return; // Skip the ring logic
        }

        // TRAVERSE MODE
        if (isTraverseMode)
        {
            HandleTraverseMode();
            return; // Skip the ring logic
        }

        // ---< RING MODE >--- //
        if (isRingMode) HandleRingMode();
    }

    private void HandleThirdPersonCamera(bool isODM)
    {
        wasInSpecialMode = true;

        Transform physicalLeftHand = manoReceiver.leftHandRoot;
        string physicalLeftGesture = manoReceiver.currentLeftGesture;

        Transform physicalRightHand = manoReceiver.rightHandRoot;
        string physicalRightGesture = manoReceiver.currentRightGesture;

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

        if (steeringHand == null || isODM)
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
            // bearTransform.rotation = Quaternion.Euler(0, grappleYaw, 0);
            // I have spent a ridiculous amount of time trying to figure out why the f*** the rotation
            // of the bear was all over the place while updating the BearGrappleController.cs script...
        }
    }

    private void HandleTraverseMode()
    {
        wasInSpecialMode = true;

        if (playerRig == null)
        {
            Debug.LogWarning("playerRig is not assigned...");
            return;
        }

        bool isLeftFist  = (manoReceiver.currentLeftGesture  == "Closed_Fist");
        bool isRightFist = (manoReceiver.currentRightGesture == "Closed_Fist");

        bool isRotating = isLeftFist && isRightFist;
        bool isMoving   = isLeftFist ^ isRightFist; // XOR

        if (isRotating)
        {
            wasTraverseMoving = false; // Reset movement state

            // World space
            Vector3 leftPos  = manoReceiver.leftHandRoot.position;
            Vector3 rightPos = manoReceiver.rightHandRoot.position;

            Vector3 handVector = rightPos - leftPos;
            Vector3 midpoint   = (leftPos + rightPos) / 2f;

            // Convert to Player Rig's Local Space
            Vector3 localHandVector = playerRig.InverseTransformDirection(handVector);
            Vector3 localMidpoint   = playerRig.InverseTransformPoint(midpoint);

            if (!wasTraverseRotating)
            {
                // Initial grab
                wasTraverseRotating    = true;
                initialCameraRot       = playerRig.rotation;
                initialLocalHandVector = localHandVector;
                initialLocalMidpointY  = localMidpoint.y;
            }
            else
            {
                // Rotation

                float initialYaw  = Mathf.Atan2(initialLocalHandVector.z, initialLocalHandVector.x) * Mathf.Rad2Deg;
                float currentYaw  = Mathf.Atan2(localHandVector.z, localHandVector.x) * Mathf.Rad2Deg;
                float rawDeltaYaw = Mathf.DeltaAngle(initialYaw, currentYaw);

                float initialRoll  = Mathf.Atan2(initialLocalHandVector.y, initialLocalHandVector.x) * Mathf.Rad2Deg;
                float currentRoll  = Mathf.Atan2(localHandVector.y, localHandVector.x) * Mathf.Rad2Deg;
                float rawDeltaRoll = Mathf.DeltaAngle(initialRoll, currentRoll);

                float rawDeltaPitch = (localMidpoint.y - initialLocalMidpointY) * 100f;

                // Apply Deadzone filter
                float activePitch = ApplyDeadzone(rawDeltaPitch, traverseDeadzone.x);
                float activeYaw   = ApplyDeadzone(rawDeltaYaw,   traverseDeadzone.y);
                float activeRoll  = ApplyDeadzone(rawDeltaRoll,  traverseDeadzone.z);

                // Apply the sensitivity multiplier
                float finalPitch = activePitch * traverseSensitivity.x;
                float finalYaw   = activeYaw   * traverseSensitivity.y;
                float finalRoll  = activeRoll  * traverseSensitivity.z;

                // Apply the combined rotation to the level Rig
                Quaternion steering3D = Quaternion.Euler(finalPitch, -finalYaw, finalRoll);
                playerRig.rotation    = initialCameraRot * steering3D;
            }
        }
        else if (isMoving)
        {
            wasTraverseRotating = false; // Reset rotation state

            // Determine hand
            Transform activeHand = isLeftFist ? manoReceiver.leftHandRoot : manoReceiver.rightHandRoot;
            Vector3 localHandPos = playerRig.InverseTransformPoint(activeHand.position);

            if (!wasTraverseMoving)
            {
                // Initial grab for movement
                wasTraverseMoving    = true;
                initialSingleHandPos = localHandPos;
            }
            else
            {
                // Movement

                float rawDeltaX = (localHandPos.x - initialSingleHandPos.x) * 100f;
                float rawDeltaY = (localHandPos.y - initialSingleHandPos.y) * 100f;
                float rawDeltaZ = (localHandPos.z - initialSingleHandPos.z) * 100f;

                // Apply Deadzone filter
                float activeX = ApplyDeadzone(rawDeltaX, traverseDeadzone.x);
                float activeY = ApplyDeadzone(rawDeltaY, traverseDeadzone.y);
                float activeZ = ApplyDeadzone(rawDeltaZ, traverseDeadzone.z);
                
                Vector3 moveVelocity = new Vector3(activeX, activeY, activeZ) * traverseMoveSpeed;

                // Apply
                playerRig.Translate(moveVelocity * Time.deltaTime, Space.Self);
            }
        }
        else 
        {
            // Reset states
            wasTraverseRotating = false;
            wasTraverseMoving   = false;
        }
    }

    private void HandleRingMode()
    {
        // Reset any special mode...
        if (wasInSpecialMode)
        {
            playerRig.position = initialRigPosition;
            playerRig.rotation = initialRigRotation;

            wasTraverseRotating = false;
            wasTraverseMoving   = false;

            if (bearTransform != null)
            {
                grappleYaw    = bearTransform.eulerAngles.y;
                lockedBaseYaw = grappleYaw;
            }

            wasInSpecialMode = false;
        }

        // Fetch the current gestures - BEHAVIOR ring
        string left  = manoReceiver.currentLeftGesture;
        string right = manoReceiver.currentRightGesture;

        // Determine movement based on gesture combinations
        if (left == pointingUpGesture && right == "Closed_Fist")
        {
            currentAngle          -= moveSpeed * Time.deltaTime;
            IsActivelyRotatingRing = true;
        }
        else if (left == "Closed_Fist" && right == pointingUpGesture)
        {
            currentAngle          += moveSpeed * Time.deltaTime;
            IsActivelyRotatingRing = true;
        }
        float angleRad = currentAngle * Mathf.Deg2Rad;

        // Apply trigonometric formula for a circle on the X/Z plane
        float newX = centerPoint.x + radius * Mathf.Cos(angleRad);
        float newZ = centerPoint.z + radius * Mathf.Sin(angleRad);
        Vector3 calculatedPosition = new Vector3(newX, height, newZ);

        // Apply movement to the Rig or just the Camera
        if (movePlayerRig && playerRig != null)
        {
            playerRig.position = calculatedPosition;
            playerRig.LookAt(centerPoint);

            // Re-apply tilt to the X-axis for the rig
            Vector3 preservedRotation = playerRig.eulerAngles;
            preservedRotation.x       = startingPitch;
            playerRig.eulerAngles     = preservedRotation;
        }
        else
        {
            // Update the camera's position + look at the center point
            transform.position = calculatedPosition;
            transform.LookAt(centerPoint);

            // Re-apply tilt to the X-axis
            Vector3 preservedRotation = transform.eulerAngles;
            preservedRotation.x       = startingPitch;
            transform.eulerAngles     = preservedRotation;
        }
    }

    // ---< Helper Function >--- //
    private float ApplyDeadzone(float rawDelta, float deadzone)
    {
        if (Mathf.Abs(rawDelta) <= deadzone) return 0f;
        
        // Subtract the deadzone amount so the movement starts smoothly from 0
        return Mathf.Sign(rawDelta) * (Mathf.Abs(rawDelta) - deadzone);
    }
}
