using UnityEngine;

public class BearODMController : BearGrappleBase
{
    [Header("Controller Settings")]
    public bool isODMMode = false;

    [Header("ODM Mechanics")]
    public float winchingSpeed          = 10f;
    public float odmBoostForce          = 15f;
    public float odmSteeringSensitivity = 1.5f;
    public float odmSlowMotionScale     = 0.01f;
    public float hookReleaseDelay       = 0.25f;
    
    private bool wasODMHooked   = false;
    private bool isSlowMoActive = false;
    private bool canShootHooks  = true;
    private Vector3 hookWallPoint;
    private Vector3 hookWallNormal;
    private float hookReleaseTimer = 0f;

    void Update()
    {
        if (!isODMMode || manoReceiver == null || bearRoot == null)
        {
            leftArmState.CleanUp();
            rightArmState.CleanUp();

            if (bearRb != null) bearRb.linearDamping = originalDrag;

            // Failsafe for Slow Motion
            if (Time.timeScale != 1f)
            {
                Time.timeScale      = 1f;
                Time.fixedDeltaTime = 0.02f;
            }
            return;
        }

        UpdateArmAimingVisuals();

        string currentLeftGesture  = manoReceiver.currentLeftGesture;
        string currentRightGesture = manoReceiver.currentRightGesture;

        HandleODMGrapple(currentLeftGesture, currentRightGesture);

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

    private void HandleODMGrapple(string leftGesture, string rightGesture)
    {
        if (leftGesture == "Pointing_Up" || rightGesture == "Pointing_Up") canShootHooks = true;

        bool isDetectedBothFists = (leftGesture == "Closed_Fist" && rightGesture == "Closed_Fist");
        
        // ---< Lost Grab Problem Solution >--- //
        if (isDetectedBothFists) hookReleaseTimer = 0f;
        bool bothFists = isDetectedBothFists;
        if (wasODMHooked && !isDetectedBothFists)
        {
            hookReleaseTimer += Time.deltaTime;
            if (hookReleaseTimer < hookReleaseDelay) bothFists = true;
        }

        UpdateODMAimPhase(leftArmState);
        UpdateODMAimPhase(rightArmState);
        Vector3 leftAnchorWorld  = leftArmState.arm != null ? leftArmState.arm.position + (leftArmState.arm.up * 0.15f) : bearRoot.position;
        Vector3 rightAnchorWorld = rightArmState.arm != null ? rightArmState.arm.position + (rightArmState.arm.up * 0.15f) : bearRoot.position;

        bool passedWall = false;
        if (wasODMHooked && leftArmState.joint != null && rightArmState.joint != null)
        {
            Vector3 toBear = bearRoot.position - hookWallPoint;
            toBear.y       = 0;

            // Check if the bear has swung past the wall enough to warrant unhooking...
            if (Vector3.Dot(toBear, hookWallNormal) > 0.4f) passedWall = true;
        }

        bool unhookTrigger = passedWall || (!bothFists && wasODMHooked);

        if (bothFists && canShootHooks && !passedWall)
        {
            isSlowMoActive = false;

            Time.timeScale      = Mathf.Lerp(Time.timeScale, 1f, Time.unscaledDeltaTime * 8f);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            bool newlyAttached = (leftArmState.joint == null || rightArmState.joint == null);

            if (leftArmState.joint  == null) TryAttachGrapple(grabGesture, leftArmState, leftArmState.targetMarker.activeSelf, leftArmState.laserEnd, leftAnchorWorld);
            if (rightArmState.joint == null) TryAttachGrapple(grabGesture, rightArmState, rightArmState.targetMarker.activeSelf, rightArmState.laserEnd, rightAnchorWorld);

            if (leftArmState.joint  != null) leftArmState.joint.maxDistance  = Mathf.Max(0.1f, leftArmState.joint.maxDistance - winchingSpeed * Time.deltaTime);
            if (rightArmState.joint != null) rightArmState.joint.maxDistance = Mathf.Max(0.1f, rightArmState.joint.maxDistance - winchingSpeed * Time.deltaTime);

            if (!wasODMHooked && (leftArmState.joint != null || rightArmState.joint != null)) wasODMHooked = true;

            if (newlyAttached && leftArmState.joint != null && rightArmState.joint != null)
            {
                Vector3 center   = (leftArmState.joint.connectedAnchor + rightArmState.joint.connectedAnchor) / 2f;
                hookWallPoint    = center;
                hookWallNormal   = (center - bearRoot.position);
                hookWallNormal.y = 0;
                hookWallNormal.Normalize();
            }
        }
        else if (unhookTrigger)
        {
            if (leftArmState.joint  != null) TryDetachGrapple("Drop", null, leftArmState);
            if (rightArmState.joint != null) TryDetachGrapple("Drop", null, rightArmState);

            if (wasODMHooked && bearRb != null)
            {
                Vector3 boostDir = bearRoot.forward + Vector3.up * 0.4f;
                bearRb.AddForce(boostDir.normalized * odmBoostForce, ForceMode.Impulse);
                wasODMHooked   = false;
                isSlowMoActive = true;
                canShootHooks  = false;
            }
        }

        if (isSlowMoActive)
        {
            Time.timeScale      = Mathf.Lerp(Time.timeScale, odmSlowMotionScale, Time.unscaledDeltaTime * 8f);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            float rotationSpeed = 50f * odmSteeringSensitivity;

            if (leftGesture == "Pointing_Up")
                bearRoot.Rotate(0, -rotationSpeed * Time.unscaledDeltaTime, 0, Space.Self);
            else if (rightGesture == "Pointing_Up")
                bearRoot.Rotate(0, rotationSpeed * Time.unscaledDeltaTime, 0, Space.Self);
        }

        if (leftArmState.joint  != null) UpdateRopeVisuals(leftArmState, leftAnchorWorld, Vector3.zero);
        if (rightArmState.joint != null) UpdateRopeVisuals(rightArmState, rightAnchorWorld, Vector3.zero);

        if (!unhookTrigger)
        {
            leftArmState.detachTimer  = 0f;
            rightArmState.detachTimer = 0f;
        }
    }

    private void UpdateODMAimPhase(GrappleState state)
    {
        Vector3 visualStartPos = state.arm != null ? state.arm.position : bearRoot.position;
        Vector3 aimDirection   = (state.grappleBall.position - visualStartPos).normalized;

        bool hitSomething      = Physics.SphereCast(state.grappleBall.position, aimAssistRadius, aimDirection, out RaycastHit hit, maxGrappleDistance);
        bool isLookingAtTarget = hitSomething && (hit.collider.CompareTag(graspableTag) || hit.collider.gameObject.name.ToLower().Contains(targetObjectName.ToLower()));
        state.laserEnd         = hitSomething ? hit.point : visualStartPos + (aimDirection * maxGrappleDistance);

        UpdateAimVisuals(state.joint == null, visualStartPos, state.laserEnd, isLookingAtTarget, hit.point, state.aimLaser, state.targetMarker);
    }
}
