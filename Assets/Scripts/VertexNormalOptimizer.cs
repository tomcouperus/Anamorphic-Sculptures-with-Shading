using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VertexNormalOptimizer : MonoBehaviour {
    [Header("General Settings")]
    [SerializeField]
    private MeshFilter originalObject;
    [SerializeField]
    private bool useSmoothShading;
    [SerializeField]
    private Transform viewTransform;
    [SerializeField]
    private int seed = 0;
    [Header("Optimizer Settings")]
    [SerializeField]
    private int iterations = 1;
    private const int MAX_ITERATIONS = 100;
    [SerializeField]
    private float minOptimizeOffset = -5;
    [SerializeField]
    private float maxOptimizeOffset = 5;
    [SerializeField]
    private float optimizeOffsetStep = 0.1f;

    [Header("Status: Initialized -- variables")]
    private Mesh originalMesh;
    [SerializeField]
    private Vector3[] originalVertices;
    private Vector3[] originalNormals;
    private Vector3[] adjustmentRays;
    private float[] originalAdjustmentDistances;
    private readonly Color GIZMOS_INITIALIZED_COLOR = Color.white;

    [Header("Status: Deformed -- variables")]
    private Mesh deformedMesh;
    private Vector3[] deformedVertices;
    private Vector3[] deformedNormals;
    [SerializeField]
    private float[] deformedAdjustmentDistances;
    [SerializeField]
    private float[] deformedAngularDeviations;
    private readonly Color GIZMOS_DEFORMED_COLOR = Color.blue;

    [Header("Status: Optimized -- variables")]
    private Mesh optimizedMesh;
    private Vector3[] optimizedVertices;
    private Vector3[] optimizedNormals;
    [SerializeField]
    private float[] optimizedAdjustmentDistances;
    [SerializeField]
    private float[] optimizedAngularDeviations;
    private readonly Color GIZMOS_OPTIMIZED_COLOR = Color.magenta;


    private const float GIZMO_SPHERE_RADIUS = 0.05f;
    [Header("Debug")]
    [SerializeField]
    private int selectedVertex = 0;
    private readonly Color GIZMOS_SELECTED_COLOR = Color.green;
    [SerializeField]
    private bool showOriginalVertices = false;
    [SerializeField]
    private bool showOriginalNormals = false;
    [SerializeField]
    private bool showAdjustmentRays = false;
    [SerializeField]
    private float adjustmentRaysScale = 10;

    [SerializeField]
    private bool showDeformedVertices = false;
    [SerializeField]
    private bool showDeformedNormals = false;

    [SerializeField]
    private bool showOptimizedVertices = false;
    [SerializeField]
    private bool showOptimizedNormals = false;

    public enum OptimizerStatus { None, Initialized, Deformed, Optimized };
    public OptimizerStatus Status { get; private set; } = OptimizerStatus.None;

    // MAIN METHODS
    public void Initialize() {
        Debug.Log("Initializing");
        // Initialize the random number generator
        UnityEngine.Random.InitState(seed);

        // Translate the original points to global space and obtain the adjustment rays
        originalMesh = originalObject.mesh;
        Vector3[] localOriginalVertices = originalMesh.vertices;
        originalVertices = new Vector3[localOriginalVertices.Length];
        Vector3 viewPosition = viewTransform.position;
        adjustmentRays = new Vector3[originalVertices.Length];
        originalAdjustmentDistances = new float[originalVertices.Length];
        for (int i = 0; i < localOriginalVertices.Length; i++) {
            originalVertices[i] = originalObject.transform.TransformPoint(localOriginalVertices[i]);
            Vector3 ray = originalVertices[i] - viewPosition;
            adjustmentRays[i] = ray.normalized;
            originalAdjustmentDistances[i] = ray.magnitude;
        }

        // Forcibly recalculate normals with the method we will use.
        // Blender exports them slightly differently, and I cannot figure out the difference.
        RecalculateNormals(originalMesh, useSmoothShading);
        originalNormals = originalMesh.normals;

        // Update status
        Status = OptimizerStatus.Initialized;
        SwitchMesh();
    }
    public void Deform() {
        // if (Status != OptimizerStatus.Initialized) return;
        Debug.Log("Deforming mesh");

        // Apply a deformation to the original mesh by adjusting the distance along the rays
        deformedVertices = new Vector3[adjustmentRays.Length];
        deformedAdjustmentDistances = new float[adjustmentRays.Length];
        Vector3 viewPosition = viewTransform.position;
        // But only deform the unique vertices
        Dictionary<Vector3, List<int>> verticesByPosition = GroupVerticesByLocation(originalVertices);
        foreach ((Vector3 position, List<int> identicalVertices) in verticesByPosition) {
            // Determine the new distance
            float offset = UnityEngine.Random.Range(-1f, 1f);
            float newDistance = originalAdjustmentDistances[identicalVertices[0]] + offset;
            // Apply it to all identical vertices
            foreach (int vi in identicalVertices) {
                deformedVertices[vi] = viewPosition + adjustmentRays[vi] * newDistance;
                deformedAdjustmentDistances[vi] = newDistance;
            }
        }

        // Apply it to a new mesh
        deformedMesh = new();
        deformedMesh.SetVertices(deformedVertices);
        deformedMesh.SetTriangles(originalMesh.triangles, 0);
        RecalculateNormals(deformedMesh, useSmoothShading);
        GetComponent<MeshFilter>().sharedMesh = deformedMesh;
        deformedNormals = deformedMesh.normals;

        // Calculate deviation
        deformedAngularDeviations = CalculateAngularDeviation(originalNormals, deformedNormals);
        Debug.Log("Angular deviation: " + Enumerable.Sum(deformedAngularDeviations));

        // Update status
        Status = OptimizerStatus.Deformed;
        SwitchMesh();
    }

    public void Optimize() {
        // if (Status != OptimizerStatus.Deformed) return;
        Debug.Log("Optimizing vertex normals");

        // Make a list of various offsets
        Dictionary<float, float> offsetTotalDeviationMap = new();
        float minOffset = -5;
        float maxOffset = 5;
        float offsetStep = 0.1f;
        List<float> offsets = new();
        for (float offset = minOffset; offset <= maxOffset; offset += offsetStep) {
            offsets.Add(offset);
            offsetTotalDeviationMap.Add(offset, -1);
        }

        // Optimize the deformed mesh by adjusting the distance along the rays
        // Initialize variables
        optimizedVertices = (Vector3[]) deformedVertices.Clone();
        optimizedNormals = (Vector3[]) deformedNormals.Clone();
        optimizedAdjustmentDistances = (float[]) deformedAdjustmentDistances.Clone();
        optimizedAngularDeviations = (float[]) deformedAngularDeviations.Clone();

        Vector3 viewPosition = viewTransform.position;

        // Initialize mesh
        optimizedMesh = new();
        optimizedMesh.SetVertices(optimizedVertices);
        optimizedMesh.SetTriangles(originalMesh.triangles, 0);

        // Only optimize the unique vertices
        Dictionary<Vector3, List<int>> verticesByPosition = GroupVerticesByLocation(originalVertices);

        // Sort the optimized angular deviations
        Dictionary<int, float> optimizedAngularDeviationsMap = new();
        for (int i = 0; i < optimizedVertices.Length; i++) {
            optimizedAngularDeviationsMap.Add(i, deformedAngularDeviations[i]);
        }
        List<KeyValuePair<int, float>> sortedOptimizedAngularDeviations = optimizedAngularDeviationsMap.ToList();
        sortedOptimizedAngularDeviations.Sort(SortFunctions.largeToSmallValueSorter);
        // foreach ((int v, float deviation) in sortedOptimizedAngularDeviations) {
        //     Debug.Log(v + ": " + deviation);
        // }

        // For a number of iterations, optimize the vertex that has the largest angular deviation
        int skipAmount = 0;
        for (int i = 0; i < iterations; i++) {
            if (skipAmount >= sortedOptimizedAngularDeviations.Count) {
                Debug.LogWarning("Skipped all vertices. Halting optimization");
                break;
            }
            int v = sortedOptimizedAngularDeviations[skipAmount].Key;
            Debug.Log("Iteration: " + i + ", vertex: " + v);
            Vector3[] newVertices = (Vector3[]) optimizedVertices.Clone();

            // Find the set of identical vertices containing v
            List<int> identicalVertices = null;
            foreach ((Vector3 _, List<int> ivs) in verticesByPosition) {
                if (ivs.Contains(v)) {
                    identicalVertices = ivs;
                    break;
                }
            }

            // Go over all the offsets
            foreach (float offset in offsets) {
                // Determine the new distance and position
                float newDistance = optimizedAdjustmentDistances[v] + offset;
                Vector3 newVertexPosition = viewPosition + adjustmentRays[v] * newDistance;
                // Update all identical vertices
                foreach (int vi in identicalVertices) {
                    newVertices[vi] = newVertexPosition;
                }

                // Recalculate the normals for this offset
                optimizedMesh.SetVertices(newVertices);
                RecalculateNormals(optimizedMesh, useSmoothShading);
                // Calculate the new total deviation and store it
                float[] deviations = CalculateAngularDeviation(originalNormals, optimizedMesh.normals);
                offsetTotalDeviationMap[offset] = Enumerable.Sum(deviations);
            }

            // Determine the offset with minimum total deviation
            List<KeyValuePair<float, float>> sortedOffsetTotalDeviations = offsetTotalDeviationMap.ToList();
            sortedOffsetTotalDeviations.Sort(SortFunctions.smallToLargeValueSorter);

            // Apply this optimal offset to all identical vertices
            float optimalDistance = optimizedAdjustmentDistances[v] + sortedOffsetTotalDeviations[0].Key;
            Vector3 optimalVertexPosition = viewPosition + adjustmentRays[v] * optimalDistance;
            bool skip = false;
            if (optimalVertexPosition == optimizedVertices[v]) {
                Debug.Log("Optimal position already attained");
                skip = true;
            } else {
                skip = false;
                foreach (int vi in identicalVertices) {
                    optimizedVertices[vi] = optimalVertexPosition;
                    optimizedAdjustmentDistances[vi] = optimalDistance;
                }
            }
            optimizedMesh.SetVertices(optimizedVertices);
            RecalculateNormals(optimizedMesh, useSmoothShading);
            if (skip) {
                skipAmount++;
                continue;
            } else {
                skipAmount = 0;
            }
            optimizedNormals = optimizedMesh.normals;
            // Resort the vertices according to their new deviations
            optimizedAngularDeviations = CalculateAngularDeviation(originalNormals, optimizedNormals);
            for (int vi = 0; vi < optimizedVertices.Length; vi++) {
                optimizedAngularDeviationsMap[vi] = optimizedAngularDeviations[vi];
            }
            sortedOptimizedAngularDeviations = optimizedAngularDeviationsMap.ToList();
            sortedOptimizedAngularDeviations.Sort(SortFunctions.largeToSmallValueSorter);
            // foreach ((int vi, float deviation) in sortedOptimizedAngularDeviations) {
            //     Debug.Log(vi + ": " + deviation);
            // }

        }
        Debug.Log("Angular deviation: " + Enumerable.Sum(optimizedAngularDeviations));

        // Update status
        Status = OptimizerStatus.Optimized;
        SwitchMesh();
    }

    public void Reset() {
        Debug.Log("Resetting");
        // Status: Initialized -- variables
        originalMesh = null;
        originalVertices = null;
        originalNormals = null;
        adjustmentRays = null;
        originalAdjustmentDistances = null;

        // Status: Deformed -- variables
        deformedMesh = null;
        GetComponent<MeshFilter>().sharedMesh = null;
        deformedVertices = null;
        deformedNormals = null;
        deformedAdjustmentDistances = null;
        deformedAngularDeviations = null;

        // Status: Optimized -- variables
        optimizedMesh = null;
        optimizedVertices = null;
        optimizedNormals = null;
        optimizedAdjustmentDistances = null;
        optimizedAngularDeviations = null;

        // Update status
        Status = OptimizerStatus.None;
        SwitchMesh();
    }

    public void SwitchMesh() {
        bool noneOrInit = Status == OptimizerStatus.None || Status == OptimizerStatus.Initialized;
        originalObject.gameObject.SetActive(noneOrInit);
        GetComponent<MeshRenderer>().enabled = !noneOrInit;
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (Status == OptimizerStatus.Deformed) meshFilter.sharedMesh = deformedMesh;
        else if (Status == OptimizerStatus.Optimized) meshFilter.sharedMesh = optimizedMesh;
    }

    // HELPER METHODS
    private static void RecalculateNormals(Mesh mesh, bool useSmoothShading) {

        if (!useSmoothShading) {
            mesh.RecalculateNormals();
        } else {
            // Calculate triangle normals and add them to the vertices that make it up (and their identicals)
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            List<int>[] vertexIdentityMap = CreateVertexIdentityMap(vertices);
            Vector3[] normals = new Vector3[vertices.Length];
            for (int i = 0; i < triangles.Length / 3; i++) {
                int a = triangles[3 * i];
                int b = triangles[3 * i + 1];
                int c = triangles[3 * i + 2];
                Vector3 va = vertices[a];
                Vector3 vb = vertices[b];
                Vector3 vc = vertices[c];
                // Unity has clockwise winding order, so to get the normal facing outward, take the cross product of AB and AC
                Vector3 normal = Vector3.Cross(vb - va, vc - va).normalized;

                // Find all the equivs of each vertex
                HashSet<int> verticesToAddNormalTo = new();
                verticesToAddNormalTo.AddRange(vertexIdentityMap[a]);
                verticesToAddNormalTo.AddRange(vertexIdentityMap[b]);
                verticesToAddNormalTo.AddRange(vertexIdentityMap[c]);
                foreach (int vi in verticesToAddNormalTo) {
                    normals[vi] += normal;
                }

            }
            // Normalize the normals
            for (int i = 0; i < normals.Length; i++) {
                normals[i].Normalize();
            }
            mesh.SetNormals(normals);
        }
    }

    private static Dictionary<Vector3, List<int>> GroupVerticesByLocation(Vector3[] vertices) {
        // Group vertices by position
        Dictionary<Vector3, List<int>> verticesByPosition = new();
        for (int i = 0; i < vertices.Length; i++) {
            Vector3 position = vertices[i];
            // If position is new, add it to the map
            if (!verticesByPosition.ContainsKey(position)) {
                verticesByPosition.Add(position, new());
            }
            // Add vertex index to grouping
            verticesByPosition[position].Add(i);
        }
        return verticesByPosition;
    }

    private static List<int>[] CreateVertexIdentityMap(Vector3[] vertices) {
        // Group vertices by position
        Dictionary<Vector3, List<int>> verticesByPosition = GroupVerticesByLocation(vertices);

        // Assign the list of identical vertices to each vertex in the list
        List<int>[] vertexIdentityMap = new List<int>[vertices.Length];
        foreach (List<int> identicalVertices in verticesByPosition.Values) {
            foreach (int i in identicalVertices) {
                vertexIdentityMap[i] = new List<int>(identicalVertices);
            }
        }
        return vertexIdentityMap;
    }

    private static float[] CalculateAngularDeviation(Vector3[] normals1, Vector3[] normals2) {
        if (normals1.Length != normals2.Length) {
            throw new ArgumentException("Arrays should have same length");
        }
        float[] angularDeviations = new float[normals1.Length];
        for (int i = 0; i < angularDeviations.Length; i++) {
            angularDeviations[i] = Vector3.Angle(normals1[i], normals2[i]);
        }
        return angularDeviations;
    }

    // DEBUG METHODS
#if UNITY_EDITOR
    private void DrawInitializedGizmos() {
        // Every debug feature that requires Initialized or higher status
        if (Status < OptimizerStatus.Initialized) return;
        Gizmos.color = GIZMOS_INITIALIZED_COLOR;

        if (showOriginalVertices) {
            for (int i = 0; i < originalVertices.Length; i++) {
                if (i == selectedVertex) continue;
                Gizmos.DrawSphere(originalVertices[i], GIZMO_SPHERE_RADIUS);
            }
            Gizmos.color = GIZMOS_SELECTED_COLOR;
            Gizmos.DrawSphere(originalVertices[selectedVertex], GIZMO_SPHERE_RADIUS);
            Gizmos.color = GIZMOS_INITIALIZED_COLOR;
        }
        if (showOriginalNormals) {
            for (int i = 0; i < originalNormals.Length; i++) {
                Gizmos.DrawLine(originalVertices[i], originalVertices[i] + originalNormals[i]);
            }
        }
        if (showAdjustmentRays) {
            for (int i = 0; i < adjustmentRays.Length; i++) {
                Gizmos.DrawLine(viewTransform.position, viewTransform.position + (adjustmentRaysScale * adjustmentRays[i]));
            }
        }
    }

    private void DrawDeformedGizmos() {
        // Every debug feature that requires Deformed or higher status
        if (Status < OptimizerStatus.Deformed) return;
        Gizmos.color = GIZMOS_DEFORMED_COLOR;
        if (showDeformedVertices) {
            for (int i = 0; i < deformedVertices.Length; i++) {
                if (i == selectedVertex) continue;
                Gizmos.DrawSphere(deformedVertices[i], GIZMO_SPHERE_RADIUS * 1.5f);
            }
            Gizmos.color = GIZMOS_SELECTED_COLOR;
            Gizmos.DrawSphere(deformedVertices[selectedVertex], GIZMO_SPHERE_RADIUS * 1.5f);
            Gizmos.color = GIZMOS_DEFORMED_COLOR;
        }
        if (showDeformedNormals) {
            for (int i = 0; i < deformedNormals.Length; i++) {
                Gizmos.DrawLine(deformedVertices[i], deformedVertices[i] + deformedNormals[i]);
            }
        }
    }

    private void DrawOptimizedGizmos() {
        // Every debug feature that requires Optimized or higher status
        if (Status < OptimizerStatus.Optimized) return;
        Gizmos.color = GIZMOS_OPTIMIZED_COLOR;
        if (showOptimizedVertices) {
            for (int i = 0; i < optimizedVertices.Length; i++) {
                if (i == selectedVertex) continue;
                Gizmos.DrawSphere(optimizedVertices[i], GIZMO_SPHERE_RADIUS * 1.5f);
            }
            Gizmos.color = GIZMOS_SELECTED_COLOR;
            Gizmos.DrawSphere(optimizedVertices[selectedVertex], GIZMO_SPHERE_RADIUS * 1.5f);
            Gizmos.color = GIZMOS_OPTIMIZED_COLOR;
        }
        if (showOptimizedNormals) {
            for (int i = 0; i < optimizedNormals.Length; i++) {
                Gizmos.DrawLine(optimizedVertices[i], optimizedVertices[i] + optimizedNormals[i]);
            }
        }
    }

    private void OnDrawGizmos() {
        DrawInitializedGizmos();
        DrawDeformedGizmos();
        DrawOptimizedGizmos();
    }

    // INPUT CHECKER
    private void OnValidate() {
        if (iterations < 1) iterations = 1;
        if (iterations > MAX_ITERATIONS) iterations = MAX_ITERATIONS;
        if (originalMesh != null && selectedVertex >= originalMesh.vertexCount) selectedVertex = originalMesh.vertexCount - 1;
        if (selectedVertex < 0) selectedVertex = 0;
        if (minOptimizeOffset >= maxOptimizeOffset) minOptimizeOffset = maxOptimizeOffset - optimizeOffsetStep;
    }
#endif
}
