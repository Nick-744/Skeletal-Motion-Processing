using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Globalization;

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
    private bool hasSaved            = false; // Prevent saving every frame while holding the gesture

    void Update()
    {
        if (manoReceiver == null || pointCloudParticles == null) return;

        // PAINTING LOGIC - RIGHT HAND
        if (manoReceiver.currentRightGesture == "Pointing_Up")
        {
            Transform indexTip = manoReceiver.rightBones[3]; // Brush tip (index finger tip)
            if (indexTip != null)
            {
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
        }
        else isCurrentlyPainting = false; // Lift the brush

        // SAVING LOGIC - LEFT HAND
        if (manoReceiver.currentLeftGesture == "Victory")
        {
            if (!hasSaved)
            {
                SavePointCloudToOBJ();
                hasSaved = true; // Lock saving until gesture is released
            }
        }
        else hasSaved = false; // Reset lock when gesture changes
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

    private void SavePointCloudToOBJ()
    {
        int particleCount = pointCloudParticles.particleCount;
        if (particleCount == 0)
        {
            Debug.LogWarning("No points to save!");
            return;
        }

        // Extract particles from the Particle System
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[particleCount];
        pointCloudParticles.GetParticles(particles);

        // Determine if the particle system simulates in local space
        bool isLocalSpace     = pointCloudParticles.main.simulationSpace == ParticleSystemSimulationSpace.Local;
        Transform psTransform = pointCloudParticles.transform;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("# Point Cloud generated from Unity");
        sb.AppendLine($"# Total Points: {particleCount}");
        sb.AppendLine("o PointCloud");

        // Format points as OBJ vertices
        for (int i = 0; i < particleCount; i++)
        {
            // Convert to world space...
            Vector3 pos = isLocalSpace
                ? psTransform.TransformPoint(particles[i].position)
                : particles[i].position;

            // Force '.' as decimal separator
            string x = pos.x.ToString(CultureInfo.InvariantCulture);
            string y = pos.y.ToString(CultureInfo.InvariantCulture);
            // Invert the Z-axis: left handed system -> right handed system
            string z = (-pos.z).ToString(CultureInfo.InvariantCulture);

            sb.AppendLine($"v {x} {y} {z}");
        }

        // Single point element listing all vertices
        sb.Append("p");
        for (int i = 1; i <= particleCount; i++) sb.Append($" {i}"); // Add point elements
        sb.AppendLine();

        // Set up the Desktop file path with a timestamp
        string timestamp   = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string filePath    = Path.Combine(desktopPath, $"PointCloud_{timestamp}.obj");

        try
        {
            // Save the file
            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"<color=green>Point cloud successfully saved to:</color> {filePath}");
        }
        catch (Exception e) { Debug.LogError($"Failed to save point cloud: {e.Message}"); }
    }
}
