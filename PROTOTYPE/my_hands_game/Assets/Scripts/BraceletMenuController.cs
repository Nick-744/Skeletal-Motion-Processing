using UnityEngine;

public class ProceduralBracelet : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Drag the GameObject holding the ManoLiveReceiver script here.")]
    public ManoLiveReceiver manoReceiver;

    [Header("Bracelet Settings")]
    [Tooltip("Scale")]
    public Vector3 braceletScale          = new Vector3(0.6f, 0.06f, 0.7f);
    [Tooltip("Rotation Offset")]
    public Vector3 braceletRotationOffset = new Vector3(0f, 0f, 90f);
    [Tooltip("Position Offset")]
    public Vector3 braceletPositionOffset = new Vector3(-0.15f, 0f, 0f);

    private GameObject braceletVisual;
    private GameObject band;

    void Start() { CreateYellowBracelet(); }

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
    }

    void Update()
    {
        if (manoReceiver == null || manoReceiver.rightBones == null || manoReceiver.rightBones.Length == 0) return;

        // Wrist
        Transform rightWrist = manoReceiver.rightBones[0];
        if (rightWrist == null) return;

        // Snap!
        braceletVisual.transform.position = rightWrist.position;
        braceletVisual.transform.rotation = rightWrist.rotation;
    }
}
