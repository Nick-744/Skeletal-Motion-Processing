using UnityEngine;

public class Painter3D : MonoBehaviour
{
    [Header("Dependencies")]
    public ManoLiveReceiver manoReceiver;
    public ParticleSystem pointCloudParticles;

    [Header("Paint Settings")]
    [Tooltip("Minimum distance between points to maintain a sparse point cloud.")]
    public float minimumDistance = 0.05f; 
    
    // Internal tracking
    private Vector3 lastPointPosition;
    private bool isCurrentlyPainting = false;

    void Update()
    {
        if (manoReceiver == null || pointCloudParticles == null) return;

        if (manoReceiver.currentRightGesture == "Pointing_Up")
        {
            Transform indexTip = manoReceiver.rightBones[3]; // Brush tip (index finger tip)
            if (indexTip == null) return;

            Vector3 currentFingertipPos = indexTip.position;

            // Sparse Point Cloud Logic
            if (!isCurrentlyPainting)
            {
                // First point of the new stroke
                DrawPoint(currentFingertipPos);
                isCurrentlyPainting = true;
            }
            else
            {
                float distance = Vector3.Distance(currentFingertipPos, lastPointPosition);
                if (distance >= minimumDistance) DrawPoint(currentFingertipPos);
            }
        }
        else isCurrentlyPainting = false; // Lift the brush
    }

    private void DrawPoint(Vector3 position)
    {
        // Setup the particle parameters
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
        emitParams.position                  = position;
        
        pointCloudParticles.Emit(emitParams, 1);
        
        // Update the tracker
        lastPointPosition = position;
    }
}
