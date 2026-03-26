using UnityEngine;

public class BearInteraction : MonoBehaviour
{
    [Header("Connections")]
    [Tooltip("Drag the GameObject holding the ManoLiveReceiver script here.")]
    public ManoLiveReceiver manoReceiver;
    [Tooltip("Drag the HandController object here.")]
    public HandPointer handPointer;
    [Tooltip("Drag the Canvas/Dialogue Bubble GameObject here.")]
    public GameObject dialogueBubble;

    [Header("Bear Armature")]
    public Transform leftShoulder;
    public Transform rightShoulder;

    [Header("Interaction Settings")]
    public string waveGesture         = "Thumb_Up";
    public Vector3 leftArmUpRotation  = new Vector3(0, 0, -30f);
    public Vector3 rightArmUpRotation = new Vector3(0, 0,  30f);
    public float animationSpeed       = 8f;

    [Header("Wave Settings")]
    [Tooltip("How fast the bear waves its arm.")]
    public float waveSpeed = 12.5f;
    [Tooltip("How far the arm swings up and down during the wave.")]
    public float waveAngle = 40f;

    [Header("Walk & Navigation Settings")]
    public float walkSpeed         = 1.0f;
    public float rotationSpeed     = 6f;
    public float walkBounceHeight  = 0.15f;
    public float walkArmSwingAngle = 40f;

    private Quaternion leftStartRot;
    private Quaternion rightStartRot;

    private Vector3 targetPosition;
    private bool isWalking = false;

    void Start()
    {
        // Save default T-pose rotations
        if (leftShoulder  != null) leftStartRot  = leftShoulder.localRotation;
        if (rightShoulder != null) rightStartRot = rightShoulder.localRotation;

        // Hide the bubble
        if (dialogueBubble != null) dialogueBubble.SetActive(false);

        targetPosition = transform.position;
    }

    void Update()
    {
        if (manoReceiver == null || handPointer == null) return;

        // Check target from the hand pointer
        if (handPointer.IsConfirming && handPointer.HasValidTarget && !isWalking)
        {
            targetPosition = handPointer.CurrentTargetPosition;
            isWalking      = true;
        }

        // Movement and Animation Logic
        Quaternion leftTarget  = leftStartRot;
        Quaternion rightTarget = rightStartRot;

        if (isWalking)
        {
            Vector3 moveDir = (targetPosition - transform.position);
            moveDir.y       = 0;

            if (moveDir.magnitude > 0.2f)
            {
                // Rotate to face target
                if (moveDir != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                    transform.rotation        = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
                }

                // Move forward
                Vector3 targetPosFlat = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
                Vector3 nextPos       = Vector3.MoveTowards(transform.position, targetPosFlat, walkSpeed * Time.deltaTime);

                // Surface snapping
                if (Physics.Raycast(nextPos + Vector3.up * 5f, Vector3.down, out RaycastHit groundHit, 20f))
                {
                    float bobY         = groundHit.point.y + Mathf.Abs(Mathf.Sin(Time.time * walkSpeed)) * walkBounceHeight;
                    transform.position = new Vector3(nextPos.x, bobY, nextPos.z);
                }
                else transform.position = nextPos;

                // Arm swing animation
                float armSwing = Mathf.Sin(Time.time * walkSpeed) * walkArmSwingAngle;
                leftTarget     = leftStartRot  * Quaternion.Euler( armSwing, 0, 0);
                rightTarget    = rightStartRot * Quaternion.Euler(-armSwing, 0, 0);
            }
            else isWalking = false;
        }
        else
        {
            // Waving Logic

            // Check left + right hands independently (from the bear's perspective!)
            bool isLeftWaving  = (manoReceiver.currentLeftGesture  == waveGesture);
            bool isRightWaving = (manoReceiver.currentRightGesture == waveGesture);
            
            float waveOffset = Mathf.Sin(Time.time * waveSpeed) * waveAngle;

            if (isLeftWaving)
            {
                Vector3 wavingRotation = leftArmUpRotation + new Vector3(0, 0, waveOffset);
                leftTarget             = leftStartRot * Quaternion.Euler(wavingRotation);
            }

            if (isRightWaving)
            {
                Vector3 wavingRotation = rightArmUpRotation + new Vector3(0, 0, -waveOffset);
                rightTarget            = rightStartRot * Quaternion.Euler(wavingRotation);
            }

            if (dialogueBubble != null) dialogueBubble.SetActive(isLeftWaving || isRightWaving);
        }

        // Apply arm rotations
        if (leftShoulder != null)
            leftShoulder.localRotation = Quaternion.Slerp(leftShoulder.localRotation, leftTarget, Time.deltaTime * animationSpeed);

        if (rightShoulder != null)
            rightShoulder.localRotation = Quaternion.Slerp(rightShoulder.localRotation, rightTarget, Time.deltaTime * animationSpeed);
    }
}
