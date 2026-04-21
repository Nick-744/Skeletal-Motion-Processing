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
    public float ballSize       = 0.15f;

    public Vector3 centerOffset = new Vector3(-0.5f, 1.0f, -0.7f);

    [Tooltip("Multiplier for the hand movement.")]
    public Vector3 movementScale = new Vector3(90f, -90f, -90f);

    [Header("Grapple Physics & Ropes")]
    public float maxGrappleDistance = 6f;
    public string graspableTag      = "Graspable";
    public string targetObjectName  = "cube";

    [Tooltip("Makes the raycast thicker (Aim Assist)")]
    public float aimAssistRadius = 0.8f;

    [Header("Rigid Rope Physics")]
    [Tooltip("Air resistance to apply while holding a rope.")]
    public float grappleAirDrag = 30f;

    [Header("Gestures")]
    public string grabGesture  = "Closed_Fist";
    public string swingGesture = "Open_Hand";

    [Header("Detach Settings")]
    public float detachDelay = 0.3f;

    [HideInInspector] public Vector3 leftLaserEnd;
    [HideInInspector] public Vector3 rightLaserEnd;

    private float leftDetachTimer  = 0f;
    private float rightDetachTimer = 0f;
    
    // Remember how long the rope was when we shot it!
    private float leftInitialLength  = 0f;
    private float rightInitialLength = 0f;

    // The automatically generated visuals
    private GameObject leftGrappleBall;
    private GameObject rightGrappleBall;
    private GameObject virtualCenter;
    private GameObject leftTargetMarker;
    private GameObject rightTargetMarker;
    private LineRenderer leftAimLaser;
    private LineRenderer rightAimLaser;

    // Ropes and Joints
    [HideInInspector] public SpringJoint leftJoint;
    [HideInInspector] public SpringJoint rightJoint;
    private LineRenderer leftRope;
    private LineRenderer rightRope;

    private string prevLeftGesture  = "";
    private string prevRightGesture = "";

    private Rigidbody bearRb;
    private float originalDrag;

    void Start()
    {
        // INDEPENDENT virtual center
        virtualCenter = new GameObject("VirtualGrappleCenter");

        rightGrappleBall = CreateDebugBall("RightGrappleBall", Color.red);
        leftGrappleBall  = CreateDebugBall("LeftGrappleBall",  Color.blue);

        // Parent to the independent virtual center
        rightGrappleBall.transform.SetParent(virtualCenter.transform);
        leftGrappleBall.transform.SetParent(virtualCenter.transform);

        leftTargetMarker  = CreateDebugBall("LeftTargetMarker",  Color.yellow);
        rightTargetMarker = CreateDebugBall("RightTargetMarker", Color.yellow);
        leftTargetMarker.SetActive(false);
        rightTargetMarker.SetActive(false);

        leftAimLaser  = CreateLaser("LeftAimLaser",  Color.yellow);
        rightAimLaser = CreateLaser("RightAimLaser", Color.yellow);

        // Setup Visual Ropes
        leftRope  = CreateRope("LeftRope",  Color.blue);
        rightRope = CreateRope("RightRope", Color.red);

        if (bearRoot != null)
        {
            bearRb = bearRoot.GetComponent<Rigidbody>();
            if (bearRb != null) originalDrag = bearRb.linearDamping;
        }
    }

    // ---< Helper Function >--- // Visual debugging balls
    private GameObject CreateDebugBall(string name, Color color)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name       = name;
        ball.transform.localScale                    = Vector3.one * ballSize;
        ball.GetComponent<Renderer>().material.color = color;
        Destroy(ball.GetComponent<Collider>());

        return ball;
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

    private LineRenderer CreateLaser(string name, Color color)
    {
        GameObject laserObj = new GameObject(name);
        laserObj.transform.SetParent(transform);
        LineRenderer lr     = laserObj.AddComponent<LineRenderer>();
        lr.startWidth       = 0.01f;
        lr.endWidth         = 0.01f;
        lr.material         = new Material(Shader.Find("Sprites/Default"));
        lr.startColor       = color;
        lr.endColor         = color;
        lr.enabled          = false;

        return lr;
    }

    void Update()
    {
        if (!isGrapplingMode || manoReceiver == null || bearRoot == null)
        {
            if (leftGrappleBall.activeSelf)  leftGrappleBall.SetActive(false);
            if (rightGrappleBall.activeSelf) rightGrappleBall.SetActive(false);
            if (leftTargetMarker.activeSelf)  leftTargetMarker.SetActive(false);
            if (rightTargetMarker.activeSelf) rightTargetMarker.SetActive(false);

            // Clean up ropes and joints
            if (leftJoint  != null) Destroy(leftJoint);
            if (rightJoint != null) Destroy(rightJoint);
            if (leftRope   != null) leftRope.enabled  = false;
            if (rightRope  != null) rightRope.enabled = false;

            if (leftAimLaser  != null) leftAimLaser.enabled  = false;
            if (rightAimLaser != null) rightAimLaser.enabled = false;

            prevLeftGesture  = "";
            prevRightGesture = "";
            leftDetachTimer  = 0f;
            rightDetachTimer = 0f;

            if (bearRb != null) bearRb.linearDamping = originalDrag;

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
            leftGrappleBall.transform.localPosition = rawOffset;
        }

        if (manoReceiver.rightHandRoot != null)
        {
            Vector3 rawOffset = manoReceiver.rightHandRoot.localPosition;
            rawOffset.Scale(movementScale);
            rightGrappleBall.transform.localPosition = rawOffset;
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
        string currentLeftGesture  = manoReceiver.currentLeftGesture;
        string currentRightGesture = manoReceiver.currentRightGesture;

        HandleGrapple(
            currentLeftGesture,
            ref prevLeftGesture,
            leftGrappleBall.transform,
            leftArm,
            ref leftJoint,
            leftRope,
            leftTargetMarker,
            leftAimLaser,
            ref leftDetachTimer,
            rightJoint,
            ref leftLaserEnd,
            ref leftInitialLength
        );
        HandleGrapple(
            currentRightGesture,
            ref prevRightGesture,
            rightGrappleBall.transform,
            rightArm,
            ref rightJoint,
            rightRope,
            rightTargetMarker,
            rightAimLaser,
            ref rightDetachTimer,
            leftJoint,
            ref rightLaserEnd,
            ref rightInitialLength
        );

        if (bearRb != null)
        {
            if (leftJoint != null || rightJoint != null)
            {
                // Grappling...
                bearRb.linearDamping  = grappleAirDrag;
                bearRb.freezeRotation = false;
            }
            else
            {
                // NOT grappling...
                bearRb.linearDamping = originalDrag;

                // Freeze X and Z
                bearRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }
    }

    private void HandleGrapple(string currentGesture, ref string previousGesture, Transform grappleBall, Transform arm, ref SpringJoint joint, LineRenderer rope, GameObject targetMarker, LineRenderer aimLaser, ref float detachTimer, SpringJoint otherJoint, ref Vector3 laserEndOut, ref float initialRopeLength)
    {
        Vector3 visualStartPos = arm != null ? arm.position : bearRoot.position;
        Vector3 aimDirection   = (grappleBall.position - visualStartPos).normalized;

        bool hitSomething      = Physics.SphereCast(grappleBall.position, aimAssistRadius, aimDirection, out RaycastHit hit, maxGrappleDistance);
        bool isLookingAtTarget = hitSomething && (hit.collider.CompareTag(graspableTag) || hit.collider.gameObject.name.ToLower().Contains(targetObjectName.ToLower()));

        laserEndOut = hitSomething ? hit.point : visualStartPos + (aimDirection * maxGrappleDistance);

        if (joint == null)
        {
            aimLaser.enabled = true;
            aimLaser.SetPosition(0, visualStartPos);
            aimLaser.SetPosition(1, laserEndOut);

            if (isLookingAtTarget)
            {
                targetMarker.SetActive(true);
                targetMarker.transform.position = hit.point;
            }
            else targetMarker.SetActive(false);
        }
        else
        {
            aimLaser.enabled = false;
            targetMarker.SetActive(false);
        }

        if (joint == null)
        {
            if (currentGesture == grabGesture && (previousGesture == swingGesture || previousGesture == "Open_Palm" || previousGesture == "Open_Hand"))
            {
                if (isLookingAtTarget)
                {
                    joint = bearRoot.gameObject.AddComponent<SpringJoint>();
                    joint.autoConfigureConnectedAnchor = false;
                    joint.connectedAnchor              = hit.point;

                    // Save the length when we first hit the target
                    initialRopeLength = Vector3.Distance(bearRoot.position, hit.point);

                    joint.maxDistance = initialRopeLength * 0.6f;
                    joint.minDistance = initialRopeLength * 0.3f;

                    // Spring settings
                    joint.spring    = 30f;
                    joint.damper    = 20f;
                    joint.massScale = 3.0f;

                    rope.enabled = true;
                    detachTimer  = 0f;
                }
            }
        }
        else
        {
            if (currentGesture == grabGesture) detachTimer = 0f;
            else
            {
                if (otherJoint != null)
                {
                    detachTimer += Time.deltaTime;
                    if (detachTimer >= detachDelay)
                    {
                        Destroy(joint);
                        rope.enabled = false;
                        detachTimer  = 0f;
                    }
                }
                else detachTimer = 0f;
            }

            if (rope.enabled)
            {
                rope.SetPosition(0, visualStartPos + (aimDirection * 0.15f));
                rope.SetPosition(1, joint.connectedAnchor);
            }
        }

        if (currentGesture != "None") previousGesture = currentGesture;
    }
}
