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

    public Vector3 CurrentTargetPosition { get; private set; }
    public bool HasValidTarget           { get; private set; }
    public bool IsConfirming             { get; private set; }
    public RaycastHit CurrentHit         { get; private set; }

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
        laserBeam.startWidth = 0.01f;
        laserBeam.endWidth   = 0.01f;
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
        // Index finger's mid and tip joints... (ManoLiveReceiver)
        Transform indexMid = physicalRightBones[2];
        Transform indexTip = physicalRightBones[3];

        if (indexMid != null && indexTip != null)
        {
            // Calculate direction from the mid joint to the tip
            Vector3 pointDirection = (indexTip.position - indexMid.position).normalized;

            if (Physics.Raycast(indexTip.position, pointDirection, out RaycastHit hit, 20f, interactableLayers))
            {
                laserBeam.enabled = true;

                laserBeam.SetPosition(0, indexTip.position);
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
