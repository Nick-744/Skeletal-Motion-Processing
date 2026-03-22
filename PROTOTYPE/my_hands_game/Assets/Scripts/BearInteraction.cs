using UnityEngine;

public class BearInteraction : MonoBehaviour
{
    [Header("Connections")]
    [Tooltip("Drag the GameObject holding the ManoLiveReceiver script here.")]
    public ManoLiveReceiver manoReceiver;
    [Tooltip("Drag the Canvas/Dialogue Bubble GameObject here.")]
    public GameObject dialogueBubble;

    [Header("Bear Armature")]
    public Transform leftShoulder;
    public Transform rightShoulder;

    [Header("Interaction Settings")]
    public string targetGesture       = "Thumb_Up";
    public Vector3 leftArmUpRotation  = new Vector3(0, 0, -30f);
    public Vector3 rightArmUpRotation = new Vector3(0, 0, 30f);
    public float animationSpeed       = 8f;

    [Header("Wave Settings")]
    [Tooltip("How fast the bear waves its arm.")]
    public float waveSpeed = 12.5f;
    [Tooltip("How far the arm swings up and down during the wave.")]
    public float waveAngle = 40f;

    private Quaternion leftStartRot;
    private Quaternion rightStartRot;

    void Start()
    {
        // Save default T-pose rotations
        if (leftShoulder  != null) leftStartRot  = leftShoulder.localRotation;
        if (rightShoulder != null) rightStartRot = rightShoulder.localRotation;

        // Hide the bubble
        if (dialogueBubble != null) dialogueBubble.SetActive(false);
    }

    void Update()
    {
        if (manoReceiver == null) return;

        // Check left + right hands independently
        bool isLeftThumbsUp  = (manoReceiver.currentLeftGesture  == targetGesture);
        bool isRightThumbsUp = (manoReceiver.currentRightGesture == targetGesture);

        float waveOffset = Mathf.Sin(Time.time * waveSpeed) * waveAngle;



        // Waving Logic
        Quaternion leftTarget;
        if (isLeftThumbsUp)
        {
            Vector3 wavingRotation = leftArmUpRotation + new Vector3(0, 0, waveOffset);
            leftTarget             = leftStartRot * Quaternion.Euler(wavingRotation);
        }
        // Return to default resting pose
        else leftTarget = leftStartRot;

        Quaternion rightTarget;
        if (isRightThumbsUp)
        {
            Vector3 wavingRotation = rightArmUpRotation + new Vector3(0, 0, -waveOffset);
            rightTarget            = rightStartRot * Quaternion.Euler(wavingRotation);
        }
        else rightTarget = rightStartRot;

        if (leftShoulder != null)
            leftShoulder.localRotation = Quaternion.Slerp(leftShoulder.localRotation, leftTarget, Time.deltaTime * animationSpeed);

        if (rightShoulder != null)
            rightShoulder.localRotation = Quaternion.Slerp(rightShoulder.localRotation, rightTarget, Time.deltaTime * animationSpeed);



        if (dialogueBubble != null) dialogueBubble.SetActive(isLeftThumbsUp || isRightThumbsUp);
    }
}
