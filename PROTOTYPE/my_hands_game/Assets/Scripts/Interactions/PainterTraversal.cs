using UnityEngine;

public class PainterTraversal : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("GameObject holding the ManoLiveReceiver.")]
    public ManoLiveReceiver manoReceiver;
    public Transform playerRig;
    
    [Header("3D Mode Settings")]
    [Tooltip("The Transform of the 'Paper' in scene.")]
    public Transform paperTransform;
    [Tooltip("Sensitivity multiplier (XYZ) - ROTATION")]
    public Vector3 traverseSensitivity = new Vector3(-0.25f, 2.0f, 2.0f);

    [Header("Traverse Settings")]
    [Tooltip("Speed multiplier for movement.")]
    public float traverseMoveSpeed = 0.01f;
    
    [Tooltip("Deadzone threshold to prevent accidental drifting.")]
    public Vector3 traverseDeadzone = new Vector3(5.0f, 5.0f, 5.0f);

    [HideInInspector]
    public bool is3DMode             = false;
    private float modeToggleCooldown = 0f;

    // Traverse logic state
    private bool wasTraverseMoving = false;
    private Vector3 initialLeftHandPos;
    
    // 3D Traverse Variables (Rotation)
    private bool wasTraverseRotating = false;
    private Quaternion initialCameraRot;
    private Vector3 initialLocalHandVector;
    private float initialLocalMidpointY;

    // 3D Traverse Variables (Movement)
    private Vector3 initialSingleHandPos;

    // Storing initial relative position/rotation of the paper to the playerRig
    private Vector3 initialRelativePos;
    private Quaternion initialRelativeRot;
    private Transform initialParent;

    // Paper rendering
    private Renderer paperRenderer;
    private Color originalPaperColor;

    void Start() 
    { 
        if (playerRig == null) playerRig = transform;
        
        if (paperTransform != null)
        {
            initialRelativePos = playerRig.InverseTransformPoint(paperTransform.position);
            initialRelativeRot = Quaternion.Inverse(playerRig.rotation) * paperTransform.rotation;
            initialParent      = paperTransform.parent;

            paperRenderer = paperTransform.GetComponent<Renderer>();
            if (paperRenderer != null && paperRenderer.material != null) originalPaperColor = paperRenderer.material.color;
        }
    }

    void Update()
    {
        if (manoReceiver == null || playerRig == null) return;

        // Toggle 3D mode logic
        if (modeToggleCooldown > 0f) modeToggleCooldown -= Time.deltaTime;

        if (manoReceiver.currentLeftGesture == "Thumb_Down" && manoReceiver.currentRightGesture == "Thumb_Down")
        {
            if (modeToggleCooldown <= 0f)
            {
                is3DMode           = !is3DMode;
                modeToggleCooldown = 1.0f; // 1 second cooldown
                
                if (is3DMode && paperTransform != null)
                {
                    // Parent the paper to the player rig
                    paperTransform.SetParent(playerRig);

                    // Position the paper based on starting relative positions
                    paperTransform.localPosition = initialRelativePos;
                    paperTransform.localRotation = initialRelativeRot;

                    if (paperRenderer != null && paperRenderer.material != null)
                    {
                        Color c = paperRenderer.material.color;
                        c.a     = originalPaperColor.a * 0.5f; // Make it a bit transparent
                        paperRenderer.material.color = c;
                    }
                }
                else if (!is3DMode && paperTransform != null) 
                {
                    paperTransform.SetParent(initialParent);
                    if (paperRenderer != null && paperRenderer.material != null) paperRenderer.material.color = originalPaperColor;
                }
            }
        }

        if (is3DMode) Handle3DTraverseMode();
        else          Handle2DTraverseMode();
    }

    private void Handle3DTraverseMode()
    {
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

                // Apply the sensitivity multiplier and quantize to 90 degree increments
                float finalPitch = Mathf.Round((activePitch * traverseSensitivity.x) / 90f) * 90f;
                float finalYaw   = Mathf.Round((activeYaw   * traverseSensitivity.y) / 90f) * 90f;
                float finalRoll  = Mathf.Round((activeRoll  * traverseSensitivity.z) / 90f) * 90f;

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

    private void Handle2DTraverseMode()
    {
        if (manoReceiver.currentLeftGesture == "Closed_Fist")
        {
            Transform leftHand = manoReceiver.leftHandRoot;
            if (leftHand == null) return;

            // Convert world hand position to local space relative to the rig
            Vector3 localHandPos = playerRig.InverseTransformPoint(leftHand.position);

            if (!wasTraverseMoving)
            {
                // Initial grab for movement
                wasTraverseMoving  = true;
                initialLeftHandPos = localHandPos;
            }
            else
            {
                // Movement

                float rawDeltaX = (localHandPos.x - initialLeftHandPos.x) * 100f;
                float rawDeltaY = (localHandPos.y - initialLeftHandPos.y) * 100f;
                float rawDeltaZ = (localHandPos.z - initialLeftHandPos.z) * 100f;

                // Apply Deadzone filter
                float activeX = ApplyDeadzone(rawDeltaX, traverseDeadzone.x);
                float activeY = ApplyDeadzone(rawDeltaY, traverseDeadzone.y);
                float activeZ = ApplyDeadzone(rawDeltaZ, traverseDeadzone.z);
                
                // Only X and Y movement for traversal - Parallel to the painting plane
                Vector3 moveVelocity = new Vector3(activeX, activeY, 0) * traverseMoveSpeed;

                // Apply
                playerRig.Translate(moveVelocity * Time.deltaTime, Space.Self);
            }
        }
        // Reset state as soon as the fist is released
        else wasTraverseMoving = false;
    }

    // ---< Helper Function >--- //
    private float ApplyDeadzone(float rawDelta, float deadzone)
    {
        if (Mathf.Abs(rawDelta) <= deadzone) return 0f;
        
        // Subtract the deadzone amount so the movement starts smoothly from 0
        return Mathf.Sign(rawDelta) * (Mathf.Abs(rawDelta) - deadzone);
    }
}
