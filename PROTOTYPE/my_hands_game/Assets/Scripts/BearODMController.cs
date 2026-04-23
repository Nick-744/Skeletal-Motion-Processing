using UnityEngine;
using Bhaptics.SDK2;

public class BearODMController : BearGrappleBase
{
    [Header("Controller Settings")]
    public bool isODMMode = false;

    [Header("ODM Mechanics")]
    public float winchingSpeed          = 5f;
    public float odmSteeringSensitivity = 2f;
    public float odmSlowMotionScale     = 0.3f;
    public float odmBoostForce          = 15f;
    
    private bool wasODMRotating = false;
    private bool wasODMHooked   = false;
    private Vector3 initialODMHandVector;
    private Quaternion initialBearRot;

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
            {
                // Grappling...
                bearRb.linearDamping = grappleAirDrag;

                // Figure out which arm is currently pulling the bear
                GrappleState activeState = leftArmState.joint != null ? leftArmState : rightArmState;

                // Force the bear to face the locked direction
                if (activeState.lockedForward != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(activeState.lockedForward);
                    bearRoot.rotation    = Quaternion.Slerp(bearRoot.rotation, targetRot, Time.deltaTime * 10f);
                }
            }
            else
                // NOT grappling...
                bearRb.linearDamping = originalDrag;
        }
    }

    private void HandleODMGrapple(string leftGesture, string rightGesture)
    {
        bool bothFists = (leftGesture == "Closed_Fist" && rightGesture == "Closed_Fist");
        bool bothOpen  = (leftGesture == "Open_Palm"   && rightGesture == "Open_Palm");

        UpdateODMAimPhase(leftArmState);
        UpdateODMAimPhase(rightArmState);
        Vector3 leftAnchorWorld  = leftArmState.arm != null ? leftArmState.arm.position + (leftArmState.arm.up * 0.15f) : bearRoot.position;
        Vector3 rightAnchorWorld = rightArmState.arm != null ? rightArmState.arm.position + (rightArmState.arm.up * 0.15f) : bearRoot.position;

        if (bothFists)
        {
            wasODMRotating = false;

            Time.timeScale      = Mathf.Lerp(Time.timeScale, 1f, Time.unscaledDeltaTime * 8f);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            if (leftArmState.joint  == null)  TryAttachGrapple(grabGesture, leftArmState, leftArmState.targetMarker.activeSelf, leftArmState.laserEnd, leftAnchorWorld);
            if (rightArmState.joint == null) TryAttachGrapple(grabGesture, rightArmState, rightArmState.targetMarker.activeSelf, rightArmState.laserEnd, rightAnchorWorld);

            if (leftArmState.joint  != null)  leftArmState.joint.maxDistance  = Mathf.Max(0.1f, leftArmState.joint.maxDistance - winchingSpeed * Time.deltaTime);
            if (rightArmState.joint != null) rightArmState.joint.maxDistance = Mathf.Max(0.1f, rightArmState.joint.maxDistance - winchingSpeed * Time.deltaTime);

            if (leftArmState.joint != null || rightArmState.joint != null)
                wasODMHooked = true;
        }
        else
        {
            if (leftArmState.joint  != null)  TryDetachGrapple("Drop", null, leftArmState);
            if (rightArmState.joint != null) TryDetachGrapple("Drop", null, rightArmState);

            if (bothOpen)
            {
                if (wasODMHooked && bearRb != null)
                {
                    Vector3 boostDir = bearRoot.forward + Vector3.up * 0.4f;
                    bearRb.AddForce(boostDir.normalized * odmBoostForce, ForceMode.Impulse);
                    wasODMHooked = false;
                }

                Time.timeScale      = Mathf.Lerp(Time.timeScale, odmSlowMotionScale, Time.unscaledDeltaTime * 8f);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;

                Vector3 leftPos    = manoReceiver.leftHandRoot.position;
                Vector3 rightPos   = manoReceiver.rightHandRoot.position;
                Vector3 handVector = bearRoot.InverseTransformDirection(rightPos - leftPos);

                if (!wasODMRotating)
                {
                    wasODMRotating       = true;
                    initialBearRot       = bearRoot.rotation;
                    initialODMHandVector = handVector;
                }
                else
                {
                    float initialYaw = Mathf.Atan2(initialODMHandVector.z, initialODMHandVector.x) * Mathf.Rad2Deg;
                    float currentYaw = Mathf.Atan2(handVector.z, handVector.x) * Mathf.Rad2Deg;
                    float deltaYaw   = Mathf.DeltaAngle(initialYaw, currentYaw);

                    bearRoot.rotation = initialBearRot * Quaternion.Euler(0, -deltaYaw * odmSteeringSensitivity, 0);
                }
            }
            else
            {
                wasODMRotating = false;
                wasODMHooked   = false;

                Time.timeScale      = Mathf.Lerp(Time.timeScale, 1f, Time.unscaledDeltaTime * 8f);
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
            }
        }

        if (leftArmState.joint  != null)  UpdateRopeVisuals(leftArmState, leftAnchorWorld, Vector3.zero);
        if (rightArmState.joint != null) UpdateRopeVisuals(rightArmState, rightAnchorWorld, Vector3.zero);

        if (!bothOpen)
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
