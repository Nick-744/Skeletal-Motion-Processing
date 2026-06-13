using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class PCDImporter : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("The HandPointer whose laser decides where the cloud is placed.")]
    public HandPointer handPointer;

    [Header("Import Settings")]
    [Tooltip("Overall size of the imported cloud in the world.")]
    public float placementScale = 0.12f;
    [Tooltip("Edge length of each point cube (local space).")]
    public float pointSize = 0.01f;
    [Tooltip("Colour of the imported points.")]
    public Color pointColor = Color.white;

    private GameObject currentPCD;
    private bool isPlacing = false;

    public void CheckAndStartImport()
    {
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string[] files     = Directory.GetFiles(desktopPath, "*.ply");

        if (files.Length == 0) return; // No PLY found...

        // Pick the latest one by write time.
        System.Array.Sort(files, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

        if (!ImportPLY(files[0])) return;

        // Switch the laser on so the cloud can be aimed onto a surface...
        if (handPointer != null) handPointer.enabled = true;

        isPlacing = true;
    }

    // ---< Cleanup >--- //
    public void StopImport()
    {
        // An already-placed cloud is not affected!
        if (isPlacing)
        {
            if (handPointer != null) handPointer.enabled = false;

            if (currentPCD != null)
            {
                Destroy(currentPCD);
                currentPCD = null;
            }
        }

        isPlacing = false;
    }

    private bool ImportPLY(string filePath)
    {
        List<Vector3> points = ParsePLY(filePath);
        if (points.Count == 0) return false;

        // Anchor the cloud so its BOTTOM-CENTRE is
        // at the local origin - the laser hit point...
        Vector3 min = points[0];
        Vector3 max = points[0];
        for (int i = 1; i < points.Count; i++)
        {
            min = Vector3.Min(min, points[i]);
            max = Vector3.Max(max, points[i]);
        }

        Vector3 anchor = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
        for (int i = 0; i < points.Count; i++) points[i] -= anchor;

        if (currentPCD != null) Destroy(currentPCD);

        Mesh cloudMesh = BuildCubeCloud(points, pointSize);

        currentPCD                      = new GameObject("Imported_PCD");
        currentPCD.transform.localScale = Vector3.one * placementScale;
        currentPCD.transform.rotation   = Quaternion.identity; // As imported - No rotation!

        MeshFilter mf   = currentPCD.AddComponent<MeshFilter>();
        MeshRenderer mr = currentPCD.AddComponent<MeshRenderer>();
        mf.mesh         = cloudMesh;

        // Colour...
        mr.material       = new Material(Shader.Find("Sprites/Default"));
        mr.material.color = pointColor;

        currentPCD.SetActive(false); // Hidden until the laser finds a surface

        return true;
    }

    private List<Vector3> ParsePLY(string filePath)
    {
        var points = new List<Vector3>();
        var ci     = System.Globalization.CultureInfo.InvariantCulture;

        string[] lines = File.ReadAllLines(filePath);
        bool inBody    = false;

        foreach (string raw in lines)
        {
            string line = raw.Trim();

            if (!inBody)
            {
                if (line == "end_header") inBody = true;

                continue;
            }

            string[] parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            if (float.TryParse(parts[0], System.Globalization.NumberStyles.Float, ci, out float x) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, ci, out float y) &&
                float.TryParse(parts[2], System.Globalization.NumberStyles.Float, ci, out float z))
                points.Add(new Vector3(x, y, z));
        }

        return points;
    }

    private Mesh BuildCubeCloud(List<Vector3> points, float size)
    {
        int n        = points.Count;
        var vertices = new Vector3[n * 8];
        var tris     = new int[n * 36];

        float h = size * 0.5f;
        Vector3[] corners =
        {
            new Vector3(-h, -h, -h), new Vector3( h, -h, -h), new Vector3( h,  h, -h), new Vector3(-h,  h, -h),
            new Vector3(-h, -h,  h), new Vector3( h, -h,  h), new Vector3( h,  h,  h), new Vector3(-h,  h,  h),
        };

        // Cube triangle index list - Graphics 101 lab!!!
        int[] cubeTris =
        {
            0, 2, 1, 0, 3, 2, // -Z
            4, 5, 6, 4, 6, 7, // +Z
            0, 1, 5, 0, 5, 4, // -Y
            2, 3, 7, 2, 7, 6, // +Y
            0, 4, 7, 0, 7, 3, // -X
            1, 2, 6, 1, 6, 5  // +X
        };

        for (int p = 0; p < n; p++)
        {
            Vector3 c = points[p];
            int vBase = p * 8;
            int tBase = p * 36;

            for (int k = 0; k < 8;  k++) vertices[vBase + k] = c + corners[k];
            for (int k = 0; k < 36; k++) tris[tBase + k]     = vBase + cubeTris[k];
        }

        Mesh mesh        = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices    = vertices;
        mesh.triangles   = tris;
        mesh.RecalculateBounds();

        return mesh;
    }

    void Update()
    {
        if (!isPlacing || currentPCD == null || handPointer == null) return;

        bool hasTarget = handPointer.HasValidTarget;

        // Only show the cloud while the laser is actually hitting a surface
        currentPCD.SetActive(hasTarget);
        if (!hasTarget) return;

        currentPCD.transform.position = handPointer.CurrentTargetPosition;

        // Confirm with HandPointer's confirm gesture!
        if (handPointer.IsConfirming)
        {
            isPlacing           = false;
            handPointer.enabled = false; // laser off once placed
        }
    }
}
