using UnityEngine;

public class BearGrappleController : BearGrappleBase
{
    [Header("Controller Settings")]
    public bool isGrapplingMode = false;

    void Update()
    {
        if (!isGrapplingMode || manoReceiver == null || bearRoot == null)
        {
            leftArmState.CleanUp();
            rightArmState.CleanUp();

            if (bearRb != null) bearRb.linearDamping = originalDrag;

            return;
        }

        UpdateArmAimingVisuals();

        // Grappling logic
        string currentLeftGesture  = manoReceiver.currentLeftGesture;
        string currentRightGesture = manoReceiver.currentRightGesture;

        HandleGrapple(currentLeftGesture, leftArmState, rightArmState.joint);
        HandleGrapple(currentRightGesture, rightArmState, leftArmState.joint);

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

    private void HandleGrapple(string currentGesture, GrappleState state, SpringJoint otherJoint)
    {
        Vector3 visualStartPos = state.arm != null ? state.arm.position : bearRoot.position;
        Vector3 aimDirection   = (state.grappleBall.position - visualStartPos).normalized;

        bool hitSomething      = Physics.SphereCast(state.grappleBall.position, aimAssistRadius, aimDirection, out RaycastHit hit, maxGrappleDistance);
        bool isLookingAtTarget = hitSomething && (hit.collider.CompareTag(graspableTag) || hit.collider.gameObject.name.ToLower().Contains(targetObjectName.ToLower()));
        state.laserEnd         = hitSomething ? hit.point : visualStartPos + (aimDirection * maxGrappleDistance);

        UpdateAimVisuals(state.joint == null, visualStartPos, state.laserEnd, isLookingAtTarget, hit.point, state.aimLaser, state.targetMarker);

        Vector3 anchorWorldPos = visualStartPos + (state.arm.up * 0.15f); // Tip of the bear's hand!

        if (state.joint == null)
            TryAttachGrapple(currentGesture, state, isLookingAtTarget, hit.point, anchorWorldPos);
        else
        {
            TryDetachGrapple(currentGesture, otherJoint, state);
            if (state.joint != null) UpdateRopeVisuals(state, anchorWorldPos, aimDirection);
        }
    }
}
