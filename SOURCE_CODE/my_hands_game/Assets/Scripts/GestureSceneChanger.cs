using UnityEngine;
using UnityEngine.SceneManagement;

public class CircularSceneChanger : MonoBehaviour
{
    [Header("Dependencies")]
    public ManoLiveReceiver manoReceiver;

    [Header("Scene Cycle (In Order)")]
    public string[] sceneCycle = new string[] 
    {
        "GrapplingBear",
        "Mario_N64",
        "City_Sandbox",
        "Mano_Paint_3D"
    };

    [Header("Gesture Combination")]
    public string handGestureUp   = "Thumb_Up";
    public string handGestureDown = "Thumb_Down";

    [Header("Settings")]
    public float cooldownDuration            = 2f;
    private static float lastSceneChangeTime = -10f;

    void Update()
    {
        if (manoReceiver == null) return;

        if (Time.time - lastSceneChangeTime < cooldownDuration) return;

        bool forwardGesture  = manoReceiver.currentLeftGesture  == handGestureDown &&
                               manoReceiver.currentRightGesture == handGestureUp;
        bool backwardGesture = manoReceiver.currentLeftGesture  == handGestureUp &&
                               manoReceiver.currentRightGesture == handGestureDown;

        // Check if both hands match the required gestures
        if      (forwardGesture)  TriggerNextScene();
        else if (backwardGesture) TriggerNextScene(-1);
    }

    private void TriggerNextScene(int direction = 1)
    {
        lastSceneChangeTime = Time.time; // Prevent rapid-fire loading

        // Get the name of the current active scene
        string currentScene = SceneManager.GetActiveScene().name;
        int currentIndex    = -1;

        // Find where this scene is in the cycle list
        for (int i = 0; i < sceneCycle.Length; i++)
        {
            if (sceneCycle[i] == currentScene)
            {
                currentIndex = i;
                break;
            }
        }

        // Calculate the next index
        int nextIndex = 0; // Default to the first scene just in case...
        if (currentIndex != -1) nextIndex = (currentIndex + direction + sceneCycle.Length) % sceneCycle.Length;

        string nextSceneToLoad = sceneCycle[nextIndex];
        
        Debug.Log($"Gesture detected! Cycling from {currentScene} to -> {nextSceneToLoad}");
        SceneManager.LoadScene(nextSceneToLoad);
    }
}
