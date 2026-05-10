using UnityEngine;
using Bhaptics.SDK2;

public class BearGrappleCore : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("GameObject holding the ManoLiveReceiver script.")]
    public ManoLiveReceiver manoReceiver;

    [Tooltip("Bear's root Transform.")]
    public Transform bearRoot;

    [Header("Armature Targets")]
    [Tooltip("Bear's Left Arm bone.")]
    public Transform leftArm;
    [Tooltip("Bear's Right Arm bone.")]
    public Transform rightArm;

    [Header("Base Settings")]
    // public bool isGrapplingMode = false; // Moved to derived class...
    [Tooltip("Check this to render the balls for debugging.")]
    public bool showDebugBalls = false;
    public float ballSize      = 0.15f;

    public Vector3 centerOffset = new Vector3(-0.5f, 1.0f, -0.7f);

    [Tooltip("Multiplier for the hand movement.")]
    public Vector3 movementScale = new Vector3(90f, -90f, -90f);

    [Header("Grapple Physics & Ropes")]
    public float maxGrappleDistance = 6f;
    public float ropeShootSpeed     = 10f;
    public string graspableTag      = "Graspable";
    public string targetObjectName  = "cube";

    [Tooltip("Makes the raycast thicker (Aim Assist)")]
    public float aimAssistRadius = 0.8f;

    [Header("Rigid Rope Physics")]
    [Tooltip("Air resistance to apply while holding a rope.")]
    public float grappleAirDrag = 0.1f;

    [Header("Gestures")]
    public string grabGesture = "Closed_Fist";

    [Header("Detach Settings")]
    public float detachDelay = 0.3f;

    // Expose for external use...
    [HideInInspector] public Vector3 leftLaserEnd  => leftArmState.laserEnd;
    [HideInInspector] public Vector3 rightLaserEnd => rightArmState.laserEnd;
    [HideInInspector] public SpringJoint leftJoint  => leftArmState.joint;
    [HideInInspector] public SpringJoint rightJoint => rightArmState.joint;

    public class GrappleState
    {
        // The automatically generated visuals
        public Transform arm;
        public Quaternion startRot;
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
        public Vector3 lockedForward   = Vector3.zero;

        public void CleanUp()
        {
            if (grappleBall  != null && grappleBall.gameObject.activeSelf) grappleBall.gameObject.SetActive(false);
            if (targetMarker != null && targetMarker.activeSelf) targetMarker.SetActive(false);

            // Clean up ropes and joints
            if (joint    != null) Destroy(joint);
            if (rope     != null) rope.enabled     = false;
            if (aimLaser != null) aimLaser.enabled = false;

            detachTimer   = 0f;
            animProgress  = 0f;
            lockedForward = Vector3.zero;
        }
    }

    protected GrappleState leftArmState  = new GrappleState();
    protected GrappleState rightArmState = new GrappleState();

    protected GameObject virtualCenter;

    protected Rigidbody bearRb;
    protected float originalDrag;

    protected virtual void Start()
    {
        // INDEPENDENT virtual center
        virtualCenter = new GameObject("VirtualGrappleCenter_" + this.GetType().Name);

        leftArmState.arm  = leftArm;
        rightArmState.arm = rightArm;

        if (leftArm  != null) leftArmState.startRot  = leftArm.localRotation;
        if (rightArm != null) rightArmState.startRot = rightArm.localRotation;

        GameObject rightGrappleBall = CreateDebugBall("RightGrappleBall", Color.red);
        GameObject leftGrappleBall  = CreateDebugBall("LeftGrappleBall",  Color.blue);

        // Parent to the independent virtual center
        rightGrappleBall.transform.SetParent(virtualCenter.transform);
        leftGrappleBall.transform.SetParent(virtualCenter.transform);

        leftArmState.grappleBall  = leftGrappleBall.transform;
        rightArmState.grappleBall = rightGrappleBall.transform;

        leftArmState.targetMarker  = CreateDebugBall("LeftTargetMarker",  Color.yellow);
        rightArmState.targetMarker = CreateDebugBall("RightTargetMarker", Color.yellow);
        leftArmState.targetMarker.SetActive(false);
        rightArmState.targetMarker.SetActive(false);

        leftArmState.aimLaser = CreateLaser("LeftAimLaser",   Color.yellow);
        rightArmState.aimLaser = CreateLaser("RightAimLaser", Color.yellow);

        // Setup Visual Ropes
        leftArmState.rope = CreateRope("LeftRope",   Color.blue);
        rightArmState.rope = CreateRope("RightRope", Color.red);

        if (bearRoot != null)
        {
            bearRb = bearRoot.GetComponent<Rigidbody>();
            if (bearRb != null) originalDrag = bearRb.linearDamping;
        }
    }

    // ---< Helper Function >--- // Visual debugging balls
    protected GameObject CreateDebugBall(string name, Color color)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name       = name;
        ball.transform.localScale                    = Vector3.one * ballSize;
        ball.GetComponent<Renderer>().material.color = color;
        Destroy(ball.GetComponent<Collider>());

        return ball;
    }

    protected LineRenderer CreateRope(string name, Color color)
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

    protected LineRenderer CreateLaser(string name, Color color)
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

    protected void UpdateArmAimingVisuals()
    {
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
            if (leftArmState.joint != null)
                leftArm.localRotation = Quaternion.Lerp(leftArm.localRotation, leftArmState.startRot * Quaternion.Euler(0, 0, -80f), Time.deltaTime * 8f);
            else
            {
                leftArm.LookAt(leftArmState.grappleBall.position);
                leftArm.Rotate(90f, 0f, 0f, Space.Self);
            }
        }

        if (rightArm != null)
        {
            if (rightArmState.joint != null)
                rightArm.localRotation = Quaternion.Lerp(rightArm.localRotation, rightArmState.startRot * Quaternion.Euler(0, 0, 80f), Time.deltaTime * 8f);
            else
            {
                rightArm.LookAt(rightArmState.grappleBall.position);
                rightArm.Rotate(90f, 0f, 0f, Space.Self);
            }
        }
    }

    protected void UpdateAimVisuals(bool isAimPhase, Vector3 startPos, Vector3 endPos, bool hasTarget, Vector3 hitPoint, LineRenderer aimLaser, GameObject targetMarker)
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

    protected void TryAttachGrapple(string currentGesture, GrappleState state, bool hasTarget, Vector3 hitPoint, Vector3 anchorWorldPos)
    {
        if (currentGesture == grabGesture && hasTarget)
        {
            // Calculate and LOCK the "forward" direction
            Vector3 lookDirection = hitPoint - bearRoot.position;
            lookDirection.y       = 0f;
            if (lookDirection.sqrMagnitude > 0.001f) state.lockedForward = lookDirection.normalized;

            // Setup Spring Joint
            state.joint = bearRoot.gameObject.AddComponent<SpringJoint>();
            state.joint.autoConfigureConnectedAnchor = false;
            state.joint.connectedAnchor              = hitPoint;
            state.joint.anchor                       = bearRoot.InverseTransformPoint(anchorWorldPos);

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

    protected void TryDetachGrapple(string currentGesture, SpringJoint otherJoint, GrappleState state)
    {
        if (currentGesture == grabGesture) state.detachTimer = 0f;
        else if (otherJoint != null || currentGesture == "Drop")
        {
            state.detachTimer += Time.deltaTime;
            if (state.detachTimer >= detachDelay || currentGesture == "Drop")
            {
                Destroy(state.joint);
                state.rope.enabled = false;
                state.detachTimer  = 0f;
            }
        }
        else state.detachTimer = 0f;
    }

    protected void UpdateRopeVisuals(GrappleState state, Vector3 startPos, Vector3 aimDirection)
    {
        if (state.rope.enabled)
        {
            state.animProgress += Time.deltaTime * (ropeShootSpeed / Mathf.Max(0.1f, state.initialRopeLength));
            Vector3 currentEnd  = Vector3.Lerp(startPos, state.joint.connectedAnchor, Mathf.Clamp01(state.animProgress));

            state.rope.SetPosition(0, startPos);
            state.rope.SetPosition(1, currentEnd);
        }
    }

    public void ForceCleanUp()
    {
        leftArmState?.CleanUp();
        rightArmState?.CleanUp();
    }

    protected virtual void OnDisable() { ForceCleanUp(); }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            Transform root       = bearRoot != null ? bearRoot : transform;

            // Calculate the direction from bear's center to point of contact
            Vector3 hitDirection = (contact.point - root.position).normalized;

            // Compare hit direction against bear's local forward, right, and up axes
            float dotForward = Vector3.Dot(root.forward, hitDirection);
            float dotRight   = Vector3.Dot(root.right, hitDirection);
            float dotUp      = Vector3.Dot(root.up, hitDirection);

            string hitCategory = "";
            if (Mathf.Abs(dotUp) > Mathf.Abs(dotForward) && Mathf.Abs(dotUp) > Mathf.Abs(dotRight))
                hitCategory = dotUp > 0 ? "Top hit" : "Bottom hit";
            else if (Mathf.Abs(dotForward) > Mathf.Abs(dotRight))
                hitCategory = dotForward > 0 ? "Front hit" : "Back hit";
            else
                hitCategory = dotRight > 0 ? "Right side hit" : "Left side hit";

            Debug.Log($"Detect Hit: {hitCategory} against {collision.gameObject.name}");

            // Only trigger haptic feedback for side/front/back hits
            if (hitCategory != "Top hit" && hitCategory != "Bottom hit") TriggerHapticFeedback(hitCategory);
        }
    }

    protected void TriggerHapticFeedback(string hitCategory)
    {
        // Indices 0-19 are the Front motors
        // Indices 20-39 are the Back motors
        int[] vestMotors = new int[40];
        int intensity    = 6;
        int duration     = 300; // milliseconds

        switch (hitCategory)
        {
            case "Front hit":
                for (int i = 0; i < 20; i++) vestMotors[i] = intensity;
                break;

            case "Back hit":
                for (int i = 20; i < 40; i++) vestMotors[i] = intensity;
                break;

            case "Left side hit":
                // Left edge motors
                for (int r = 0; r < 5; r++)
                {
                    vestMotors[r * 4]      = intensity; // Front left edge
                    vestMotors[20 + r * 4] = intensity; // Back left edge
                }
                break;

            case "Right side hit":
                // Right edge motors
                for (int r = 0; r < 5; r++)
                {
                    vestMotors[(r * 4) + 3]      = intensity; // Front right edge
                    vestMotors[20 + (r * 4) + 3] = intensity; // Back right edge
                }
                break;
        }

        BhapticsLibrary.PlayMotors((int)PositionType.Vest, vestMotors, duration);
    }
}
