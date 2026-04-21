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
    public float ropeShootSpeed     = 50f;
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

    [HideInInspector] public Vector3 leftLaserEnd  => leftArmState.laserEnd;
    [HideInInspector] public Vector3 rightLaserEnd => rightArmState.laserEnd;

    [HideInInspector] public SpringJoint leftJoint  => leftArmState.joint;
    [HideInInspector] public SpringJoint rightJoint => rightArmState.joint;

    public class GrappleState
    {
        // The automatically generated visuals
        public Transform arm;
        public Transform grappleBall;
        public GameObject targetMarker;
        public LineRenderer aimLaser;

        // Ropes and Joints
        public SpringJoint joint;
        public LineRenderer rope;

        public Vector3 laserEnd;
        public float detachTimer       = 0f;
        public float initialRopeLength = 0f; // Remember how long the rope was when we shot it!
        public float animProgress      = 0f;
        public string prevGesture      = "";

        public void CleanUp()
        {
            if (grappleBall  != null && grappleBall.gameObject.activeSelf) grappleBall.gameObject.SetActive(false);
            if (targetMarker != null && targetMarker.activeSelf) targetMarker.SetActive(false);

            // Clean up ropes and joints
            if (joint        != null) Destroy(joint);
            if (rope         != null) rope.enabled     = false;
            if (aimLaser     != null) aimLaser.enabled = false;

            prevGesture  = "";
            detachTimer  = 0f;
            animProgress = 0f;
        }
    }

    private GrappleState leftArmState  = new GrappleState();
    private GrappleState rightArmState = new GrappleState();

    private GameObject virtualCenter;

    private Rigidbody bearRb;
    private float originalDrag;

    void Start()
    {
        // INDEPENDENT virtual center
        virtualCenter = new GameObject("VirtualGrappleCenter");

        leftArmState.arm  = leftArm;
        rightArmState.arm = rightArm;

        GameObject rightGrappleBall = CreateDebugBall("RightGrappleBall", Color.red);
        GameObject leftGrappleBall  = CreateDebugBall("LeftGrappleBall",  Color.blue);

        // Parent to the independent virtual center
        rightGrappleBall.transform.SetParent(virtualCenter.transform);
        leftGrappleBall.transform.SetParent(virtualCenter.transform);

        leftArmState.grappleBall  = leftGrappleBall.transform;
        rightArmState.grappleBall = rightGrappleBall.transform;

        leftArmState.targetMarker  = CreateDebugBall("LeftTargetMarker", Color.yellow);
        rightArmState.targetMarker = CreateDebugBall("RightTargetMarker", Color.yellow);
        leftArmState.targetMarker.SetActive(false);
        rightArmState.targetMarker.SetActive(false);

        leftArmState.aimLaser  = CreateLaser("LeftAimLaser", Color.yellow);
        rightArmState.aimLaser = CreateLaser("RightAimLaser", Color.yellow);

        // Setup Visual Ropes
        leftArmState.rope  = CreateRope("LeftRope", Color.blue);
        rightArmState.rope = CreateRope("RightRope", Color.red);

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
            leftArmState.CleanUp();
            rightArmState.CleanUp();

            if (bearRb != null) bearRb.linearDamping = originalDrag;

            return;
        }

        // Toggle debug visibility based on the checkbox
        leftArmState.grappleBall.GetComponent<Renderer>().enabled  = showDebugBalls;
        rightArmState.grappleBall.GetComponent<Renderer>().enabled = showDebugBalls;

        if (!leftArmState.grappleBall.gameObject.activeSelf)  leftArmState.grappleBall.gameObject.SetActive(true);
        if (!rightArmState.grappleBall.gameObject.activeSelf) rightArmState.grappleBall.gameObject.SetActive(true);

        // Position the virtual center based on the bear's root
        Quaternion cleanRotation         = Quaternion.Euler(0, bearRoot.eulerAngles.y, 0);
        virtualCenter.transform.position = bearRoot.position + (cleanRotation * centerOffset);
        virtualCenter.transform.rotation = cleanRotation;

        // Apply scaled hand offsets...
        if (manoReceiver.leftHandRoot != null)
        {
            Vector3 rawOffset = manoReceiver.leftHandRoot.localPosition;
            rawOffset.Scale(movementScale);
            leftArmState.grappleBall.localPosition = rawOffset;
        }

        if (manoReceiver.rightHandRoot != null)
        {
            Vector3 rawOffset = manoReceiver.rightHandRoot.localPosition;
            rawOffset.Scale(movementScale);
            rightArmState.grappleBall.localPosition = rawOffset;
        }

        // Arm aiming logic
        if (leftArm != null)
        {
            leftArm.LookAt(leftArmState.grappleBall.position);
            leftArm.Rotate(90f, 0f, 0f, Space.Self);
        }

        if (rightArm != null)
        {
            rightArm.LookAt(rightArmState.grappleBall.position);
            rightArm.Rotate(90f, 0f, 0f, Space.Self);
        }

        // Grappling logic
        string currentLeftGesture  = manoReceiver.currentLeftGesture;
        string currentRightGesture = manoReceiver.currentRightGesture;

        HandleGrapple(currentLeftGesture, leftArmState, rightArmState.joint);
        HandleGrapple(currentRightGesture, rightArmState, leftArmState.joint);

        if (bearRb != null)
        {
            if (leftArmState.joint != null || rightArmState.joint != null)
                // Grappling...
                bearRb.linearDamping = grappleAirDrag;
            else
                // NOT grappling...
                bearRb.linearDamping = originalDrag;
        }
    }

    private void HandleGrapple(string currentGesture, GrappleState state, SpringJoint otherJoint)
    {
        Vector3 visualStartPos = state.arm != null ? state.arm.position : bearRoot.position;
        Vector3 aimDirection   = (state.grappleBall.position - visualStartPos).normalized;

        bool hitSomething      = Physics.SphereCast(state.grappleBall.position, aimAssistRadius, aimDirection, out RaycastHit hit, maxGrappleDistance);
        bool isLookingAtTarget = hitSomething && (hit.collider.CompareTag(graspableTag) || hit.collider.gameObject.name.ToLower().Contains(targetObjectName.ToLower()));
        state.laserEnd         = hitSomething ? hit.point : visualStartPos + (aimDirection * maxGrappleDistance);

        UpdateAimVisuals(state.joint == null, visualStartPos, state.laserEnd, isLookingAtTarget, hit.point, state.aimLaser, state.targetMarker);

        if (state.joint == null) TryAttachGrapple(currentGesture, state, isLookingAtTarget, hit.point);
        else
        {
            TryDetachGrapple(currentGesture, otherJoint, state);
            if (state.joint != null) UpdateRopeVisuals(state, visualStartPos, aimDirection);
        }

        if (currentGesture != "None") state.prevGesture = currentGesture;
    }

    private void UpdateAimVisuals(bool isAimPhase, Vector3 startPos, Vector3 endPos, bool hasTarget, Vector3 hitPoint, LineRenderer aimLaser, GameObject targetMarker)
    {
        aimLaser.enabled = isAimPhase;
        targetMarker.SetActive(isAimPhase && hasTarget);

        if (isAimPhase)
        {
            aimLaser.SetPosition(0, startPos);
            aimLaser.SetPosition(1, endPos);
            if (hasTarget) targetMarker.transform.position = hitPoint;
        }
    }

    private void TryAttachGrapple(string currentGesture, GrappleState state, bool hasTarget, Vector3 hitPoint)
    {
        if (currentGesture == grabGesture && (state.prevGesture == swingGesture || state.prevGesture == "Open_Palm" || state.prevGesture == "Open_Hand"))
        {
            if (hasTarget)
            {
                state.joint = bearRoot.gameObject.AddComponent<SpringJoint>();
                state.joint.autoConfigureConnectedAnchor = false;
                state.joint.connectedAnchor              = hitPoint;

                state.initialRopeLength = Vector3.Distance(bearRoot.position, hitPoint);
                state.joint.maxDistance = state.initialRopeLength * 0.6f;
                state.joint.minDistance = state.initialRopeLength * 0.3f;

                state.joint.spring    = 30f;
                state.joint.damper    = 20f;
                state.joint.massScale = 3.0f;

                state.rope.enabled = true;
                state.detachTimer  = 0f;
                state.animProgress = 0f;
            }
        }
    }

    private void TryDetachGrapple(string currentGesture, SpringJoint otherJoint, GrappleState state)
    {
        if (currentGesture == grabGesture) state.detachTimer = 0f;
        else if (otherJoint != null)
        {
            state.detachTimer += Time.deltaTime;
            if (state.detachTimer >= detachDelay)
            {
                Destroy(state.joint);
                state.rope.enabled = false;
                state.detachTimer  = 0f;
            }
        }
        else state.detachTimer = 0f;
    }

    private void UpdateRopeVisuals(GrappleState state, Vector3 startPos, Vector3 aimDirection)
    {
        if (state.rope.enabled)
        {
            state.animProgress  += Time.deltaTime * (ropeShootSpeed / Mathf.Max(0.1f, state.initialRopeLength));
            Vector3 currentStart = startPos + (aimDirection * 0.15f);
            Vector3 currentEnd   = Vector3.Lerp(currentStart, state.joint.connectedAnchor, Mathf.Clamp01(state.animProgress));

            state.rope.SetPosition(0, currentStart);
            state.rope.SetPosition(1, currentEnd);
        }
    }
}
