using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Global Game State")]
    public bool isGrapplingMode = false;

    [Header("Script References")]
    public BearGrappleController grappleController;
    public BearInteraction bearInteraction;
    public CameraRingController cameraController;
    public HandPointer handPointer;

    [Header("Dependencies")]
    [Tooltip("GameObject holding the ManoLiveReceiver script.")]
    public ManoLiveReceiver manoReceiver;

    [Header("Gesture Settings")]
    public string grappleToggleGesture = "Thumb_Down";
    [Tooltip("How many seconds to wait before you can toggle it again.")]
    public float toggleCooldown        = 1.5f;

    private float lastToggleTime = -10f; // Stores the exact second the toggle happened

    void Update()
    {
        if (manoReceiver != null)
        {
            // If BOTH hands are doing Thumb_Down, toggle the grappling mode...
            if (manoReceiver.currentLeftGesture  == grappleToggleGesture &&
                manoReceiver.currentRightGesture == grappleToggleGesture)
            {
                // Only flip the switch if enough time has passed
                if (Time.time - lastToggleTime > toggleCooldown)
                {
                    isGrapplingMode = !isGrapplingMode;
                    lastToggleTime  = Time.time; // Reset the stopwatch
                    Debug.Log("Grappling Mode toggled to: " + isGrapplingMode);
                }
            }
        }

        if (grappleController != null) grappleController.isGrapplingMode = isGrapplingMode;
        if (bearInteraction   != null) bearInteraction.isGrapplingMode   = isGrapplingMode;
        if (cameraController  != null) cameraController.isGrapplingMode  = isGrapplingMode;
        if (handPointer       != null) handPointer.isGrapplingMode       = isGrapplingMode;
        if (manoReceiver      != null) manoReceiver.isGrapplingMode      = isGrapplingMode;
    }
}
