using UnityEngine;

public class BearGrappleController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the GameObject holding the ManoLiveReceiver script here.")]
    public ManoLiveReceiver manoReceiver;

    [Tooltip("Drag the Bear's root Transform here.")]
    public Transform bearRoot;

    [Header("Armature Targets")]
    [Tooltip("Drag the bear's Left Arm bone here.")]
    public Transform leftArm;
    [Tooltip("Drag the bear's Right Arm bone here.")]
    public Transform rightArm;

    [Header("Settings")]
    public bool isGrapplingMode = false;
    [Tooltip("Check this to render the balls for debugging.")]
    public bool showDebugBalls  = false;
    public float ballSize       = 0.1f;

    public Vector3 centerOffset = new Vector3(-0.5f, 1.0f, -0.8f);

    [Tooltip("Multiplier for the hand movement.")]
    public Vector3 movementScale = new Vector3(100f, -100f, -100f);

    // The automatically generated visuals
    private GameObject leftGrappleBall;
    private GameObject rightGrappleBall;
    private GameObject virtualCenter;

    void Start()
    {
        // INDEPENDENT virtual center
        virtualCenter = new GameObject("VirtualGrappleCenter");

        // Right Grapple Ball
        rightGrappleBall      = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rightGrappleBall.name = "RightGrappleBall";
        rightGrappleBall.transform.localScale                    = Vector3.one * ballSize;
        rightGrappleBall.GetComponent<Renderer>().material.color = Color.red;
        Destroy(rightGrappleBall.GetComponent<Collider>());

        // Left Grapple Ball
        leftGrappleBall      = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leftGrappleBall.name = "LeftGrappleBall";
        leftGrappleBall.transform.localScale                    = Vector3.one * ballSize;
        leftGrappleBall.GetComponent<Renderer>().material.color = Color.blue;
        Destroy(leftGrappleBall.GetComponent<Collider>());

        // Parent to the independent virtual center
        rightGrappleBall.transform.SetParent(virtualCenter.transform);
        leftGrappleBall.transform.SetParent(virtualCenter.transform);
    }

    void Update()
    {
        if (!isGrapplingMode || manoReceiver == null || bearRoot == null)
        {
            if (leftGrappleBall.activeSelf)  leftGrappleBall.SetActive(false);
            if (rightGrappleBall.activeSelf) rightGrappleBall.SetActive(false);

            return;
        }

        // Toggle debug visibility based on the checkbox
        leftGrappleBall.GetComponent<Renderer>().enabled  = showDebugBalls;
        rightGrappleBall.GetComponent<Renderer>().enabled = showDebugBalls;

        if (!leftGrappleBall.activeSelf)  leftGrappleBall.SetActive(true);
        if (!rightGrappleBall.activeSelf) rightGrappleBall.SetActive(true);

        // Position the virtual center based on the bear's root
        Quaternion cleanRotation         = Quaternion.Euler(0, bearRoot.eulerAngles.y, 0);
        virtualCenter.transform.position = bearRoot.position + (cleanRotation * centerOffset);
        virtualCenter.transform.rotation = cleanRotation;

        // Apply scaled hand offsets...
        if (manoReceiver.leftHandRoot != null)
        {
            Vector3 rawOffset = manoReceiver.leftHandRoot.localPosition;
            rawOffset.Scale(movementScale);
            rightGrappleBall.transform.localPosition = rawOffset;
        }

        if (manoReceiver.rightHandRoot != null)
        {
            Vector3 rawOffset = manoReceiver.rightHandRoot.localPosition;
            rawOffset.Scale(movementScale);
            leftGrappleBall.transform.localPosition = rawOffset;
        }

        // Arm aiming logic
        if (leftArm != null)
        {
            leftArm.LookAt(leftGrappleBall.transform.position);
            leftArm.Rotate(90f, 0f, 0f, Space.Self);
        }

        if (rightArm != null)
        {
            rightArm.LookAt(rightGrappleBall.transform.position);
            rightArm.Rotate(90f, 0f, 0f, Space.Self);
        }
    }
}
