using UnityEngine;
using UnityEngine.SceneManagement;

public class CircularSceneChanger : MonoBehaviour
{
    [Header("Dependencies")]
    public ManoLiveReceiver manoReceiver;

    [Header("Scene Cycle (In Order)")]
    public string[] sceneCycle = new string[] 
    {
        "SampleScene",
        "Mario_N64Test",
        "City_Sandbox",
        "Mano_Paint_3D"
    };

    [Header("Gesture Combination")]
    public string leftHandGesture  = "Thumb_Up";
    public string rightHandGesture = "Thumb_Down";

    private bool isLoading = false;

    void Update()
    {
        if (manoReceiver == null || isLoading) return;

        // Check if both hands match the required gestures
        if (manoReceiver.currentLeftGesture  == leftHandGesture && 
            manoReceiver.currentRightGesture == rightHandGesture)
            TriggerNextScene();
    }

    private void TriggerNextScene()
    {
        isLoading = true; // Prevent rapid-fire loading

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
        if (currentIndex != -1) nextIndex = (currentIndex + 1) % sceneCycle.Length;

        string nextSceneToLoad = sceneCycle[nextIndex];
        
        Debug.Log($"Gesture detected! Cycling from {currentScene} to -> {nextSceneToLoad}");
        SceneManager.LoadScene(nextSceneToLoad);
    }
}
