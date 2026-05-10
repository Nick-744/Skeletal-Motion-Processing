using UnityEngine;

[RequireComponent(typeof(HandPointer))]
public class BuildingGrabber : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Enter the name (or part of the name) of the objects able to grab.")]
    public string targetObjectName = "cube";

    public float followSmoothSpeed = 12f; // Smooth follow for the grabbed building

    private HandPointer handPointer;
    private GameObject grabbedBuilding;

    // Track how the building was grabbed
    private bool grabbedInPalmMode = false;

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

                grabbedInPalmMode = handPointer.usePalmRepulsor;
            }
        }

        // Move and hold the building...
        if (grabbedBuilding != null)
        {
            // As long as the left hand holds the Victory sign...
            if (handPointer.IsConfirming)
            {
                if (grabbedInPalmMode)
                {
                    // PALM MODE GRAB
                    Vector3 targetPos = handPointer.PalmPosition;
                    targetPos.y       = grabbedBuildingStartPos.y;

                    grabbedBuilding.transform.position = Vector3.Lerp(grabbedBuilding.transform.position, targetPos, Time.deltaTime * followSmoothSpeed);

                    handPointer.forceDisableLaser = true; // Hide laser while in palm mode grab
                }
                else
                {
                    // Drag the building around - LASER MODE GRAB
                    Vector3 laserDelta = handPointer.CurrentTargetPosition - grabLaserStartPos;
                    laserDelta.y       = 0; // Don't allow vertical movement

                    // Apply laser delta to building's original position
                    Vector3 targetPos = grabbedBuildingStartPos + laserDelta;
                    targetPos.y       = grabbedBuildingStartPos.y; // Lock Y

                    grabbedBuilding.transform.position = Vector3.Lerp(grabbedBuilding.transform.position, targetPos, Time.deltaTime * followSmoothSpeed);
                }
            }
            else
            {
                grabbedBuilding = null;

                // Re-enable laser when not grabbing
                handPointer.forceDisableLaser = false;
            }
        }
    }

    // Cleanup - If the script is disabled while holding a building, drop it safely
    void OnDisable()
    {
        if (grabbedBuilding != null) grabbedBuilding = null;
        if (handPointer     != null) handPointer.forceDisableLaser = false;
    }
}
