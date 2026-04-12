using UnityEngine;
using TMPro;

public class BraceletMenuController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the GameObject holding the ManoLiveReceiver script here.")]
    public ManoLiveReceiver manoReceiver;

    [Header("Mode Controllers")]
    [Tooltip("Drag the GameObject holding the CameraRingController here.")]
    public CameraRingController cameraController;
    [Tooltip("Drag the GameObject holding the BuildingGrabber here.")]
    public BuildingGrabber buildingGrabber;
    [Tooltip("Drag the GameObject holding the HandPointer here.")]
    public HandPointer handPointer;

    [Header("Bracelet Settings")]
    [Tooltip("Scale")]
    public Vector3 braceletScale          = new Vector3(0.6f, 0.06f, 0.7f);
    [Tooltip("Rotation Offset")]
    public Vector3 braceletRotationOffset = new Vector3(0f, 0f, 90f);
    [Tooltip("Position Offset")]
    public Vector3 braceletPositionOffset = new Vector3(-0.15f, 0f, 0f);

    [Header("Balls Settings")]
    [Tooltip("Distance of the balls from the center of the bracelet")]
    public float ballRadius = 0.5f;
    [Tooltip("Size of each ball")]
    public float ballScale  = 0.2f;

    [Header("Active Ball Settings")]
    [Tooltip("Size of the active ball below the bracelet")]
    public float activeBallScale = 0.4f;
    [Tooltip("Color of the normal inactive balls")]
    public Color defaultColor    = Color.green;
    [Tooltip("Color of the ball currently hovering over to select")]
    public Color activeColor     = Color.purple;
    [Tooltip("Color of the ball that represents the CURRENTLY RUNNING mode")]
    public Color selectedColor   = Color.red;
    [Tooltip("Local position to move the highest ball below the center of the bracelet")]
    public Vector3 belowBraceletLocalPosition = new Vector3(-0.5f, 0f, 0f);

    [Header("Text Settings")]
    public string[] menuNames = { "Option 1", "Option 2", "Option 3" };
    [Tooltip("How far below the active ball the text should float")]
    public Vector3 textOffset = new Vector3(-0.6f, 0f, 0f);
    [Tooltip("Size of the floating text")]
    public float fontSize     = 3f;

    [Header("Interaction Settings")]
    [Tooltip("Gesture for right hand to toggle the menu")]
    public string rightActivationGesture = "Victory";
    [Tooltip("Gesture for left hand to select the active item")]
    public string leftSelectionGesture   = "Thumb_Up";

    private float transitionSpeed = 5f;

    private GameObject braceletVisual;
    private GameObject band;
    private GameObject[] balls = new GameObject[3];

    // Text Components
    private GameObject textObject;
    private TextMeshPro floatingText;

    // Cache original local positions to prevent infinite loops
    private Vector3[] defaultLocalPositions = new Vector3[3];

    // Interaction states
    private bool isMenuActive            = false;
    private bool hasReleasedRightGesture = true;
    private bool hasReleasedLeftGesture  = true;

    // Tracker for currently selected mode (-1: Default)
    private int currentActiveMode = -1; 

    void Start() 
    { 
        CreateYellowBracelet();
        CreateMenuText();
        
        // Hide the menu by default until activated
        if (braceletVisual != null) braceletVisual.SetActive(isMenuActive);

        // Ensure app starts in the default state
        ApplyCurrentMode(); 
    }

    private void CreateYellowBracelet()
    {
        braceletVisual = new GameObject("Yellow_Wristband");

        band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        band.transform.SetParent(braceletVisual.transform);
        
        // Scale, rotation and position offset
        band.transform.localScale       = braceletScale;
        band.transform.localEulerAngles = braceletRotationOffset;
        band.transform.localPosition    = braceletPositionOffset;
        
        band.GetComponent<Renderer>().material.color = Color.yellow;
        
        // Remove physics collider
        Destroy(band.GetComponent<Collider>());

        // Create the 3 Balls wrapped around
        for (int i = 0; i < 3; i++)
        {
            balls[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            balls[i].transform.SetParent(braceletVisual.transform);

            // Calculate angle
            float angleDegrees = i * 90f;
            float angleRadians = angleDegrees * Mathf.Deg2Rad;

            Vector3 cylinderSpacePos = new Vector3(Mathf.Cos(angleRadians) * ballRadius, 0f, Mathf.Sin(angleRadians) * ballRadius);
            Vector3 rotatedOrbitPos  = Quaternion.Euler(braceletRotationOffset) * cylinderSpacePos;
            
            defaultLocalPositions[i]         = rotatedOrbitPos + braceletPositionOffset; // Store the default local position
            balls[i].transform.localPosition = defaultLocalPositions[i];
            
            balls[i].transform.localScale    = Vector3.one * ballScale;
            balls[i].GetComponent<Renderer>().material.color = defaultColor;
            
            // Remove physics collider
            Destroy(balls[i].GetComponent<Collider>()); 
        }
    }

    private void CreateMenuText()
    {
        textObject = new GameObject("ActiveItem_Text");
        textObject.transform.SetParent(braceletVisual.transform);

        // Configuration
        floatingText           = textObject.AddComponent<TextMeshPro>();
        floatingText.alignment = TextAlignmentOptions.Center;
        floatingText.fontSize  = fontSize;
        floatingText.color     = Color.white;

        // Start hidden
        floatingText.text = "";
    }

    void Update()
    {
        if (manoReceiver == null) return;

        // Menu activation - Right hand
        if (manoReceiver.currentRightGesture == rightActivationGesture)
        {
            if (hasReleasedRightGesture)
            {
                isMenuActive            = !isMenuActive;
                braceletVisual.SetActive(isMenuActive);
                hasReleasedRightGesture = false;
            }
        }
        else hasReleasedRightGesture = true;

        if (!isMenuActive) return; // Skip the rest if menu is not active...

        // Wrist tracking - Right hand
        if (manoReceiver.rightBones == null || manoReceiver.rightBones.Length == 0) return;

        // Wrist
        Transform rightWrist = manoReceiver.rightBones[0];
        if (rightWrist == null) return;

        // Snap!
        braceletVisual.transform.position = rightWrist.position;
        braceletVisual.transform.rotation = rightWrist.rotation;
        
        int highestBallIndex = 0;
        float maxY           = float.MinValue;

        for (int i = 0; i < 3; i++)
        {
            // Calculate where the ball WOULD be in world space if it were in its default position...
            Vector3 theoreticalWorldPos = braceletVisual.transform.TransformPoint(defaultLocalPositions[i]);

            if (theoreticalWorldPos.y > maxY)
            {
                maxY             = theoreticalWorldPos.y;
                highestBallIndex = i;
            }
        }

        // Update ball positions...
        for (int i = 0; i < 3; i++)
        {
            Vector3 targetLocalPosition;
            Vector3 targetScale;
            Color targetColor;

            if (i == highestBallIndex)
            {
                targetLocalPosition = belowBraceletLocalPosition;
                targetScale         = Vector3.one * activeBallScale;
                targetColor         = activeColor;
            }
            else
            {
                targetLocalPosition = defaultLocalPositions[i];
                targetScale         = Vector3.one * ballScale;

                // Active -> red, Inactive -> green
                if (i == currentActiveMode)
                    targetColor = selectedColor;
                else
                    targetColor = defaultColor;
            }

            // Smoothly interpolate position, scale, and color
            balls[i].transform.localPosition = Vector3.Lerp(balls[i].transform.localPosition, targetLocalPosition, Time.deltaTime * transitionSpeed);
            balls[i].transform.localScale    = Vector3.Lerp(balls[i].transform.localScale, targetScale, Time.deltaTime * transitionSpeed);

            Material mat = balls[i].GetComponent<Renderer>().material;
            mat.color    = Color.Lerp(mat.color, targetColor, Time.deltaTime * transitionSpeed);
        }

        // Update Text String
        if (highestBallIndex >= 0 && highestBallIndex < menuNames.Length) floatingText.text = menuNames[highestBallIndex];

        Vector3 targetTextPos              = belowBraceletLocalPosition + textOffset;
        textObject.transform.localPosition = Vector3.Lerp(textObject.transform.localPosition, targetTextPos, Time.deltaTime * transitionSpeed);

        // Billboard text - always faces the player's camera + matches head tilt
        if (Camera.main != null)
        {
            Vector3 lookDirection         = textObject.transform.position - Camera.main.transform.position;
            textObject.transform.rotation = Quaternion.LookRotation(lookDirection, Camera.main.transform.up);
        }

        // Menu selection - Left hand
        if (manoReceiver.currentLeftGesture == leftSelectionGesture)
        {
            if (hasReleasedLeftGesture)
            {
                hasReleasedLeftGesture = false;
                isMenuActive           = false;
                braceletVisual.SetActive(isMenuActive);
                ExecuteSelection(highestBallIndex);
            }
        }
        else hasReleasedLeftGesture = true;
    }

    private void ExecuteSelection(int index)
    {
        // Toggle logic: If the selected option is already active, turn it off...
        if (currentActiveMode == index) currentActiveMode = -1; // Default
        else
        {
            currentActiveMode = index;
            Debug.Log(menuNames[index] + " selected.");
        }

        ApplyCurrentMode();
    }

    private void ApplyCurrentMode()
    {
        // Reset everything
        if (cameraController != null) cameraController.isGrapplingMode = false;
        if (buildingGrabber  != null) buildingGrabber.enabled          = false;
        if (handPointer      != null) handPointer.enabled              = false;

        switch (currentActiveMode)
        {
            case -1:
                // Default Mode: Basic ring camera only...
                break;
            
            case 0:
                // TODO: Implement traverse mode here later...
                break;
            
            case 1:
                if (buildingGrabber != null) buildingGrabber.enabled = true;
                if (handPointer     != null) handPointer.enabled     = true;
                break;
            
            case 2:
                // Nothing yet...
                break;
        }
    }
}
