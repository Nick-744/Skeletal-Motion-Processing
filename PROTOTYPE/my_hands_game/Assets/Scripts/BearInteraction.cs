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
    public Transform head;
    public Transform leftShoulder;
    public Transform rightShoulder;
    public Transform leftLeg;
    public Transform rightLeg;

    [Header("Interaction Settings")]
    public bool isGrapplingMode       = false;
    public string waveGesture         = "Thumb_Up";
    public Vector3 leftArmUpRotation  = new Vector3(0, 0, -30f);
    public Vector3 rightArmUpRotation = new Vector3(0, 0,  30f);
    public float animationSpeed       = 8f;

    [Header("Wave Settings")]
    [Tooltip("How fast the bear waves its arm.")]
    public float waveSpeed = 12.5f;
    [Tooltip("How far the arm swings up and down during the wave.")]
    public float waveAngle = 40f;
    [Tooltip("How much the head tilts when waving.")]
    public float headTiltAngle = 10f;

    [Header("Walk & Navigation Settings")]
    public float walkSpeed         = 0.8f;
    public float rotationSpeed     = 5f;
    public float walkBounceHeight  = 0.05f;
    public float walkArmSwingAngle = 40f;
    public float walkStepFrequency = 6f;
    public float walkSwayAngle     = 4f; // Side-to-side waddle angle
    public float walkLegSwingAngle = 35f;

    public float walkingHeightOffset = 95f; // How much should be above the ground

    private Quaternion headStartRot;
    private Quaternion leftStartRot;
    private Quaternion rightStartRot;
    private Quaternion leftLegStartRot;
    private Quaternion rightLegStartRot;

    private Vector3 targetPosition;
    private bool isWalking = false;

    void Start()
    {
        // Save default T-pose rotations
        if (head != null) headStartRot = head.localRotation;

        if (leftShoulder  != null) leftStartRot  = leftShoulder.localRotation;
        if (rightShoulder != null) rightStartRot = rightShoulder.localRotation;

        if (leftLeg  != null) leftLegStartRot  = leftLeg.localRotation;
        if (rightLeg != null) rightLegStartRot = rightLeg.localRotation;

        // Hide the bubble
        if (dialogueBubble != null) dialogueBubble.SetActive(false);

        targetPosition = transform.position;
    }

    void Update()
    {
        if (manoReceiver == null || handPointer == null) return;

        if (isGrapplingMode) return; // Grapple Controller

        // Check target from the hand pointer
        if (handPointer.IsConfirming && handPointer.HasValidTarget && !isWalking)
        {
            targetPosition = handPointer.CurrentTargetPosition;
            isWalking      = true;
        }

        // Movement and Animation Logic
        Quaternion headTarget     = headStartRot;
        Quaternion leftArmTarget  = leftStartRot;
        Quaternion rightArmTarget = rightStartRot;
        Quaternion leftLegTarget  = leftLegStartRot;
        Quaternion rightLegTarget = rightLegStartRot;

        if (isWalking)
        {
            Vector3 moveDir = (targetPosition - transform.position);
            moveDir.y       = 0;

            if (moveDir.magnitude > 0.2f)
            {
                float cycleTime = Time.time * walkStepFrequency; // Synchronized time

                // Rotate to face target + natural side-to-side sway
                if (moveDir != Vector3.zero)
                {
                    Quaternion baseLookRot = Quaternion.LookRotation(moveDir);
                    float swayZ            = Mathf.Cos(cycleTime) * walkSwayAngle;
                    Quaternion swayRot     = Quaternion.Euler(0, 0, swayZ);

                    transform.rotation = Quaternion.Slerp(transform.rotation, baseLookRot * swayRot, Time.deltaTime * rotationSpeed);
                }

                // Move forward
                Vector3 targetPosFlat = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
                Vector3 nextPos       = Vector3.MoveTowards(transform.position, targetPosFlat, walkSpeed * Time.deltaTime);

                // Surface snapping
                if (Physics.Raycast(nextPos + Vector3.up * walkingHeightOffset, Vector3.down, out RaycastHit groundHit, 20f))
                {
                    float bobY         = groundHit.point.y + Mathf.Abs(Mathf.Sin(cycleTime)) * walkBounceHeight;
                    transform.position = new Vector3(nextPos.x, bobY, nextPos.z);
                }
                else transform.position = nextPos;

                // Arm swing animation
                float armSwing = Mathf.Sin(cycleTime) * walkArmSwingAngle;
                // The following order of operations applies the swing
                // applies the swing in the bone's tilted local space.
                leftArmTarget  = leftStartRot  * Quaternion.Euler( armSwing, 0, 0);
                rightArmTarget = rightStartRot * Quaternion.Euler(-armSwing, 0, 0);

                // Leg swing animation
                float legSwing = Mathf.Sin(cycleTime) * walkLegSwingAngle;
                // The following order of operations applies the swing
                // in the parent's straight coordinate space (the bear's body).
                leftLegTarget  = Quaternion.Euler( legSwing, 0, 0) * leftLegStartRot;
                rightLegTarget = Quaternion.Euler(-legSwing, 0, 0) * rightLegStartRot;
            }
            else
            {
                isWalking = false;

                // Reset upright rotation when stopping
                Vector3 flatEuler  = transform.rotation.eulerAngles;
                flatEuler.z        = 0;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(flatEuler), Time.deltaTime * rotationSpeed);
            }
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
                leftArmTarget          = leftStartRot * Quaternion.Euler(wavingRotation);

                headTarget = headStartRot * Quaternion.Euler(0, 0, -headTiltAngle);
            }

            if (isRightWaving)
            {
                Vector3 wavingRotation = rightArmUpRotation + new Vector3(0, 0, -waveOffset);
                rightArmTarget         = rightStartRot * Quaternion.Euler(wavingRotation);

                headTarget = headStartRot * Quaternion.Euler(0, 0, headTiltAngle);
            }

            // If both hands are waving, reset head to neutral...
            if (isLeftWaving && isRightWaving) headTarget = headStartRot;

            if (dialogueBubble != null) dialogueBubble.SetActive(isLeftWaving || isRightWaving);
        }

        // Apply head rotation
        if (head != null)
            head.localRotation = Quaternion.Slerp(head.localRotation, headTarget, Time.deltaTime * animationSpeed);

        // Apply arm rotations
        if (leftShoulder != null)
            leftShoulder.localRotation = Quaternion.Slerp(leftShoulder.localRotation, leftArmTarget, Time.deltaTime * animationSpeed);

        if (rightShoulder != null)
            rightShoulder.localRotation = Quaternion.Slerp(rightShoulder.localRotation, rightArmTarget, Time.deltaTime * animationSpeed);

        // Apply leg rotations
        if (leftLeg != null)
            leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, leftLegTarget, Time.deltaTime * animationSpeed);

        if (rightLeg != null)
            rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, rightLegTarget, Time.deltaTime * animationSpeed);
    }
}
