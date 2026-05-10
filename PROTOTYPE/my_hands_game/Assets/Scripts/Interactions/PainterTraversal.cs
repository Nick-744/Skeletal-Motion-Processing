using UnityEngine;

public class PainterTraversal : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("GameObject holding the ManoLiveReceiver.")]
    public ManoLiveReceiver manoReceiver;
    public Transform playerRig;

    [Header("Traverse Settings")]
    [Tooltip("Speed multiplier for movement.")]
    public float traverseMoveSpeed = 0.01f;
    
    [Tooltip("Deadzone threshold to prevent accidental drifting.")]
    public Vector3 traverseDeadzone = new Vector3(5.0f, 5.0f, 5.0f);

    private bool wasTraverseMoving = false;
    private Vector3 initialLeftHandPos;

    void Start() { if (playerRig == null) playerRig = transform; }

    void Update()
    {
        if (manoReceiver == null || playerRig == null) return;

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
