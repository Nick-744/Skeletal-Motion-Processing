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

    [Header("Grapple Physics & Ropes")]
    public float maxGrappleDistance = 25f;
    public float reelInSpeed        = 8f;
    public string graspableTag      = "Graspable";

    // The automatically generated visuals
    private GameObject leftGrappleBall;
    private GameObject rightGrappleBall;
    private GameObject virtualCenter;

    // Ropes and Joints
    private SpringJoint leftJoint;
    private SpringJoint rightJoint;
    private LineRenderer leftRope;
    private LineRenderer rightRope;
    private bool wasLeftFist  = false;
    private bool wasRightFist = false;

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

        // Setup Visual Ropes
        leftRope  = CreateRope("LeftRope", Color.blue);
        rightRope = CreateRope("RightRope", Color.red);
    }

    private LineRenderer CreateRope(string name, Color color)
    {
        GameObject ropeObj = new GameObject(name);
        ropeObj.transform.SetParent(transform);
        LineRenderer lr    = ropeObj.AddComponent<LineRenderer>();
        lr.startWidth      = 0.05f;
        lr.endWidth        = 0.05f;
        lr.material        = new Material(Shader.Find("Sprites/Default"));
        lr.startColor      = color;
        lr.endColor        = color;
        lr.enabled         = false;

        return lr;
    }

    void Update()
    {
        if (!isGrapplingMode || manoReceiver == null || bearRoot == null)
        {
            if (leftGrappleBall.activeSelf)  leftGrappleBall.SetActive(false);
            if (rightGrappleBall.activeSelf) rightGrappleBall.SetActive(false);

            // Clean up ropes and joints
            if (leftJoint  != null) Destroy(leftJoint);
            if (rightJoint != null) Destroy(rightJoint);
            if (leftRope   != null) leftRope.enabled  = false;
            if (rightRope  != null) rightRope.enabled = false;
            wasLeftFist  = false;
            wasRightFist = false;

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

        // Grappling logic
        bool isLeftFist  = manoReceiver.currentRightGesture == "Closed_Fist";
        bool isRightFist = manoReceiver.currentLeftGesture  == "Closed_Fist";

        HandleGrapple(ref isLeftFist, ref wasLeftFist, leftGrappleBall.transform, leftArm, ref leftJoint, leftRope);
        HandleGrapple(ref isRightFist, ref wasRightFist, rightGrappleBall.transform, rightArm, ref rightJoint, rightRope);
    }

    private void HandleGrapple(ref bool isFist, ref bool wasFist, Transform grappleBall, Transform arm, ref SpringJoint joint, LineRenderer rope)
    {
        if (isFist && !wasFist)
        {
            Vector3 aimDirection = (grappleBall.position - bearRoot.position).normalized;

            if (Physics.Raycast(bearRoot.position, aimDirection, out RaycastHit hit, maxGrappleDistance))
            {
                if (hit.collider.CompareTag(graspableTag))
                {
                    joint = bearRoot.gameObject.AddComponent<SpringJoint>();
                    joint.autoConfigureConnectedAnchor = false;
                    joint.connectedAnchor = hit.point;

                    // Milder spring settings to prevent clipping through the floor
                    float distance    = Vector3.Distance(bearRoot.position, hit.point);
                    joint.maxDistance = distance;
                    joint.minDistance = 0.5f;
                    joint.spring      = 4f;
                    joint.damper      = 1f;

                    rope.enabled = true;
                }
            }
        }
        else if (isFist && joint != null)
        {
            joint.maxDistance -= reelInSpeed * Time.deltaTime;
            joint.maxDistance  = Mathf.Max(joint.maxDistance, joint.minDistance);

            rope.SetPosition(0, arm != null ? arm.position : bearRoot.position);
            rope.SetPosition(1, joint.connectedAnchor);
        }
        else if (!isFist && wasFist)
        {
            if (joint != null) Destroy(joint);
            rope.enabled = false;
        }

        wasFist = isFist;
    }
}
