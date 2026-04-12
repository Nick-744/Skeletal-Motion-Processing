using UnityEngine;

[RequireComponent(typeof(HandPointer))]
public class BuildingGrabber : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Enter the name (or part of the name) of the objects you want to be able to grab.")]
    public string targetObjectName = "cube";

    private HandPointer handPointer;
    private GameObject grabbedBuilding;
    private Vector3 grabOffset;

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
                
                // Calculate the distance between where the laser hit and the building's center!
                grabOffset = grabbedBuilding.transform.position - handPointer.CurrentTargetPosition;
            }
        }

        // Move and hold the building...
        if (grabbedBuilding != null)
        {
            if (handPointer.IsConfirming)
            {
                // Drag the building around
                Vector3 targetPos = handPointer.CurrentTargetPosition + grabOffset;
                targetPos.y       = grabbedBuilding.transform.position.y; // Keep the building at the same height

                grabbedBuilding.transform.position = targetPos;
            }
            else { grabbedBuilding = null; }
        }
    }

    // Cleanup - If the script is disabled while holding a building, drop it safely
    void OnDisable() { if (grabbedBuilding != null) grabbedBuilding = null; }
}
