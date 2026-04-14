using UnityEngine;

[RequireComponent(typeof(HandPointer))]
public class BuildingGrabber : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Enter the name (or part of the name) of the objects you want to be able to grab.")]
    public string targetObjectName = "cube";

    public float followSmoothSpeed = 12f; // Smooth follow for the grabbed building

    private HandPointer handPointer;
    private GameObject grabbedBuilding;

    // World-space offset from BUILDING'S ROOT to initial grab point
    private Vector3 grabOffsetLocal;

    // Laser hit point at the moment of grab
    private Vector3 grabbedBuildingStartPos;
    private Vector3 grabLaserStartPos;

    void Start() { handPointer = GetComponent<HandPointer>(); }

    void Update()
    {
        // Attempt to grab a building...
        if (handPointer.IsConfirming && handPointer.HasValidTarget && grabbedBuilding == null)
        {
            GameObject hitObject = handPointer.CurrentHit.collider.gameObject;

            // Check if the hit object is the target building (by name)
            if (hitObject.name.ToLower().Contains(targetObjectName.ToLower()))
            {
                grabbedBuilding = hitObject;

                // Snapshot positions at the moment of grab
                grabLaserStartPos       = handPointer.CurrentTargetPosition;
                grabbedBuildingStartPos = grabbedBuilding.transform.position;
            }
        }

        // Move and hold the building...
        if (grabbedBuilding != null)
        {
            if (handPointer.IsConfirming)
            {
                // Drag the building around
                Vector3 laserDelta = handPointer.CurrentTargetPosition - grabLaserStartPos;
                laserDelta.y       = 0; // Don't allow vertical movement

                // Apply laser delta to building's original position
                Vector3 targetPos = grabbedBuildingStartPos + laserDelta;
                targetPos.y       = grabbedBuilding.transform.position.y; // Lock Y

                grabbedBuilding.transform.position = Vector3.Lerp(grabbedBuilding.transform.position, targetPos, Time.deltaTime * followSmoothSpeed);
            }
            else { grabbedBuilding = null; }
        }
    }

    // Cleanup - If the script is disabled while holding a building, drop it safely
    void OnDisable() { if (grabbedBuilding != null) grabbedBuilding = null; }
}
