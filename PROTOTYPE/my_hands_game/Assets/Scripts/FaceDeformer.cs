using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class FaceDeformer : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("GameObject holding the ManoLiveReceiver script.")]
    public ManoLiveReceiver manoReceiver;

    [Header("Physics Settings")]
    public float springForce = 150f;
    [Tooltip("higher = less jiggly")]
    public float damping = 10f;

    [Header("Grab Settings")]
    [Tooltip("How quickly the pulling effect fades away as you get further from the exact point you grabbed.")]
    public float grabFalloffPower = 25f; // Gaussian Falloff
    [Tooltip("Minimum influence even at max distance.")]
    public float minInfluence     = 0.1f;
    public float grabReleaseDelay = 0.25f; // Solve the lost grab problem...

    private float resolvedGrabRadius;

    // Mesh Data
    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] displacedVertices;
    private Vector3[] vertexVelocities;
    private Vector3[] targetVertices;

    private float[] leftGrabWeights;
    private float[] rightGrabWeights;
    private int[][] weldedGroups;

    // Left Hand State
    private bool isLeftGrabbing = false;
    private int leftGrabIndex   = -1;
    private Vector3 leftGrabOffset;
    private Vector3 leftInitialPos;
    private float leftReleaseTimer = 0f;

    // Right Hand State
    private bool isRightGrabbing = false;
    private int rightGrabIndex   = -1;
    private Vector3 rightGrabOffset;
    private Vector3 rightInitialPos;
    private float rightReleaseTimer = 0f;

    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        mesh                  = Instantiate(meshFilter.sharedMesh);
        meshFilter.mesh       = mesh;

        originalVertices  = mesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];
        vertexVelocities  = new Vector3[originalVertices.Length];
        targetVertices    = new Vector3[originalVertices.Length];
        leftGrabWeights   = new float[originalVertices.Length];
        rightGrabWeights  = new float[originalVertices.Length];

        for (int i = 0; i < originalVertices.Length; i++)
        {
            displacedVertices[i] = originalVertices[i];
            targetVertices[i]    = originalVertices[i];
        }

        BuildWeldMap();
        
        resolvedGrabRadius = mesh.bounds.size.magnitude;
    }

    // Solve the split vertex problem...
    // Prevent the mesh from tearing apart!
    private void BuildWeldMap()
    {
        var positionToIndices = new System.Collections.Generic.Dictionary<Vector3, System.Collections.Generic.List<int>>();

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 key = RoundVector(originalVertices[i], 4);
            if (!positionToIndices.ContainsKey(key))
                positionToIndices[key] = new System.Collections.Generic.List<int>();
            positionToIndices[key].Add(i);
        }

        // Jagged array
        /* ------------
         * Vertex 5 and Vertex 12 share the exact same physical spot,
         * weldedGroups[5] and weldedGroups[12] will both return the array [5, 12].
         */
        weldedGroups = new int[originalVertices.Length][];
        foreach (var kvp in positionToIndices)
        {
            int[] group = kvp.Value.ToArray();
            foreach (int idx in group) weldedGroups[idx] = group;
        }
    }

    // ---< Helper >--- //
    private Vector3 RoundVector(Vector3 v, int decimals)
    {
        float m = Mathf.Pow(10, decimals);

        return new Vector3(
            Mathf.Round(v.x * m) / m,
            Mathf.Round(v.y * m) / m,
            Mathf.Round(v.z * m) / m);
    }

    void Update()
    {
        if (manoReceiver == null) return;

        // Reset target shapes to the resting face (every frame)
        for (int i = 0; i < targetVertices.Length; i++) targetVertices[i] = originalVertices[i];

        HandleHand(
            manoReceiver.currentLeftGesture, manoReceiver.leftHandRoot,
            ref isLeftGrabbing, ref leftGrabIndex, ref leftGrabOffset, ref leftInitialPos,
            leftGrabWeights, ref leftReleaseTimer
        );

        HandleHand(
            manoReceiver.currentRightGesture, manoReceiver.rightHandRoot,
            ref isRightGrabbing, ref rightGrabIndex, ref rightGrabOffset, ref rightInitialPos,
            rightGrabWeights, ref rightReleaseTimer
        );

        UpdateMeshPhysics();
    }

    // ---< Core Logic >--- //
    private void HandleHand(
        string gesture, Transform handTransform,
        ref bool isGrabbing, ref int grabIndex, ref Vector3 grabOffset, ref Vector3 initialGrabPos,
        float[] grabWeights, ref float releaseTimer)
    {
        if (handTransform == null) return;

        bool isDetectedFist = (gesture == "Closed_Fist");

        // ---< Lost Grab Problem Solution >--- //
        if (isDetectedFist) releaseTimer = 0f;
        bool isEffectivelyFist = isDetectedFist;
        
        if (isGrabbing && !isDetectedFist)
        {
            releaseTimer += Time.deltaTime; // Tracker lost - start counting...
            if (releaseTimer < grabReleaseDelay) isEffectivelyFist = true;
        }

        if (isEffectivelyFist && !isGrabbing)
        {
            // GRAB START
            isGrabbing = true;
            grabIndex  = GetNearestVertex(handTransform.position);

            Vector3 localHandPos = transform.InverseTransformPoint(handTransform.position);
            initialGrabPos       = displacedVertices[grabIndex];
            grabOffset           = initialGrabPos - localHandPos;

            ComputeGrabWeights(grabIndex, grabWeights);
        }
        else if (isEffectivelyFist && isGrabbing)
        {
            // HOLDING
            Vector3 localHandPos       = transform.InverseTransformPoint(handTransform.position);
            Vector3 currentTargetLocal = localHandPos + grabOffset;
            
            // How far the hand has pulled the exact grabbed point
            Vector3 pullVector = currentTargetLocal - initialGrabPos;

            // Shift the target positions for the physics engine to pull toward
            for (int i = 0; i < targetVertices.Length; i++)
                if (grabWeights[i] > 0f) targetVertices[i] += pullVector * grabWeights[i];
        }
        else if (!isEffectivelyFist && isGrabbing)
        {
            // RELEASE
            isGrabbing   = false;
            grabIndex    = -1;
            releaseTimer = 0f; // Clean up
        }
    }

    private void ComputeGrabWeights(int centerIndex, float[] weights)
    {
        Vector3 centerPos = originalVertices[weldedGroups[centerIndex][0]];

        for (int i = 0; i < originalVertices.Length; i++)
        {
            float dist = Vector3.Distance(centerPos, originalVertices[weldedGroups[i][0]]);

            float t      = dist / resolvedGrabRadius;
            float smooth = Mathf.Exp(-grabFalloffPower * t * t); // Gaussian Falloff (Smooth curve)

            weights[i] = Mathf.Max(smooth, minInfluence);
        }

        float centerWeight = weights[centerIndex];
        if (centerWeight > 0f)
            for (int i = 0; i < weights.Length; i++) weights[i] /= centerWeight;
    }

    private void UpdateMeshPhysics()
    {
        for (int i = 0; i < displacedVertices.Length; i++)
        {
            // Only calculate physics for the absolute root of each welded group...
            if (weldedGroups[i][0] != i) continue;

            Vector3 displacement = displacedVertices[i] - targetVertices[i];

            vertexVelocities[i] -= displacement * springForce * Time.deltaTime;
            vertexVelocities[i] *= Mathf.Exp(-damping * Time.deltaTime);

            displacedVertices[i] += vertexVelocities[i] * Time.deltaTime;

            // Sync all UV duplicates to the exact same physical coordinates!
            foreach (int dup in weldedGroups[i])
            {
                if (dup == i) continue;
                displacedVertices[dup] = displacedVertices[i];
                vertexVelocities[dup]  = vertexVelocities[i];
            }
        }

        mesh.vertices = displacedVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private int GetNearestVertex(Vector3 worldHandPosition)
    {
        int nearestIndex     = -1;
        float minDistanceSqr = float.MaxValue;
        Vector3 localHandPos = transform.InverseTransformPoint(worldHandPosition);

        for (int i = 0; i < displacedVertices.Length; i++)
        {
            float distSqr = (displacedVertices[i] - localHandPos).sqrMagnitude;
            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                nearestIndex   = i;
            }
        }

        return nearestIndex;
    }
}
