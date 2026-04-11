using UnityEngine;

public class HandPointer : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the GameObject holding the ManoLiveReceiver script here.")]
    public ManoLiveReceiver manoReceiver;

    [Header("Settings")]
    public string confirmGesture = "Victory";
    [Tooltip("The layers that the hand pointer can interact with.")]
    public LayerMask interactableLayers = ~0; // Default to everything

    [Header("Aiming Style (Iron Man style or finger pointing)")]
    public bool usePalmRepulsor = false;
    
    [Header("Bone Indices (for palm calculation)")]
    public int wristIndex     = 0;
    public int indexBaseIndex = 1; // Index base joint
    public int pinkyBaseIndex = 7; // Pinky base joint

    public Vector3 CurrentTargetPosition { get; private set; }
    public bool HasValidTarget           { get; private set; }
    public bool IsConfirming             { get; private set; }
    public RaycastHit CurrentHit         { get; private set; }

    [Header("Grappling Mode")]
    public bool isGrapplingMode = false;

    // Visual markers
    private GameObject laserPointerMarker;
    private LineRenderer laserBeam;

    void Start()
    {
        // Red Dot Marker
        laserPointerMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        laserPointerMarker.transform.localScale = Vector3.one * 0.1f;
        Destroy(laserPointerMarker.GetComponent<Collider>());
        laserPointerMarker.GetComponent<Renderer>().material.color = Color.red;
        laserPointerMarker.SetActive(false);

        // Laser Beam
        GameObject laserObj  = new GameObject("LaserBeam");
        laserBeam            = laserObj.AddComponent<LineRenderer>();
        laserBeam.startWidth = 0.02f;
        laserBeam.endWidth   = 0.02f;
        laserBeam.material   = new Material(Shader.Find("Sprites/Default"));
        laserBeam.startColor = Color.red;
        laserBeam.endColor   = Color.red;
        laserBeam.enabled    = false;
    }

    void Update()
    {
        if (manoReceiver == null) return;

        if (isGrapplingMode) { ResetLaser(); return; }

        string physicalLeftGesture     = manoReceiver.currentLeftGesture;
        Transform[] physicalRightBones = manoReceiver.rightBones;

        // Aiming Logic (Physical Right Hand)
        Vector3 rayOrigin    = Vector3.zero;
        Vector3 rayDirection = Vector3.zero;
        bool validAimBones   = false;

        // Determine Origin and Direction based on mode
        if (usePalmRepulsor)
        {
            // Iron Man style
            Transform wrist     = physicalRightBones[wristIndex];
            Transform indexBase = physicalRightBones[indexBaseIndex];
            Transform pinkyBase = physicalRightBones[pinkyBaseIndex];

            if (wrist != null && indexBase != null && pinkyBase != null)
            {
                // Approximate the center of the palm
                rayOrigin = (wrist.position + indexBase.position + pinkyBase.position) / 3f;

                // Cross product
                Vector3 toIndex = indexBase.position - wrist.position;
                Vector3 toPinky = pinkyBase.position - wrist.position;
                rayDirection    = Vector3.Cross(toPinky, toIndex).normalized;

                validAimBones = true;
            }
        }
        else
        {
            // Finger pointing style
            Transform indexMid = physicalRightBones[2];
            Transform indexTip = physicalRightBones[3];

            if (indexMid != null && indexTip != null)
            {
                rayOrigin = indexTip.position;

                // Calculate direction from the mid joint to the tip
                rayDirection = (indexTip.position - indexMid.position).normalized;

                validAimBones = true;
            }
        }

        // Execute Raycast
        if (validAimBones)
        {
            if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, 20f, interactableLayers))
            {
                laserBeam.enabled = true;

                laserBeam.SetPosition(0, rayOrigin);
                laserBeam.SetPosition(1, hit.point);

                // Update properties
                CurrentHit            = hit; 
                CurrentTargetPosition = hit.point;
                HasValidTarget        = true;

                laserPointerMarker.transform.position = CurrentTargetPosition;
                laserPointerMarker.SetActive(true);
            }
            else ResetLaser();
        }
        else ResetLaser();

        // Confirm Logic (Physical Left Hand)
        IsConfirming = (physicalLeftGesture == confirmGesture);
    }

    // ---< Helper function >--- //
    private void ResetLaser()
    {
        laserBeam.enabled = false;
        HasValidTarget    = false;
        laserPointerMarker.SetActive(false);

        // Clear the hit struct
        CurrentHit = new RaycastHit(); 
    }
}
