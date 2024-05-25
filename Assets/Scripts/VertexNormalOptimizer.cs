using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VertexNormalOptimizer : MonoBehaviour {
    [Header("Settings")]
    [SerializeField]
    private MappableObject originalObject;
    [SerializeField]
    private Transform viewTransform;
    [SerializeField]
    private float minOptimizeOffset = -5;
    [SerializeField]
    private float maxOptimizeOffset = 5;
    [SerializeField]
    private float optimizeOffsetStep = 0.1f;

    private const int DEFORM_INDEX = 3;

    [Header("Status: Initialized -- variables")]
    private Mesh originalMesh;
    [SerializeField]
    private Vector3[] originalVertices;
    private Vector3[] originalNormals;
    private Vector3[] adjustmentRays;
    private float[] originalAdjustmentDistances;

    [Header("Status: Deformed -- variables")]
    private Mesh deformedMesh;
    private Vector3[] deformedVertices;
    private Vector3[] deformedNormals;
    private float[] deformedAdjustmentDistances;
    [SerializeField]
    private float[] deformedAngularDeviations;

    [Header("Status: Optimized -- variables")]
    private Mesh optimizedMesh;
    private Vector3[] optimizedVertices;
    private Vector3[] optimizedNormals;
    private float[] optimizedAdjustmentDistances;
    private float[] optimizedAngularDeviations;
    private Dictionary<float, float> offsetDeviationMap;


    private const float GIZMO_SPHERE_RADIUS = 0.05f;
    [Header("Debug")]
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
    private bool showOffsetDeviationMap = false;

    public enum OptimizerStatus { None, Initialized, Deformed, Optimized };
    public OptimizerStatus Status { get; private set; } = OptimizerStatus.None;

    // MAIN METHODS
    public void Initialize() {
        Debug.Log("Initializing");

        // Translate the original points to global space and obtain the adjustment rays
        originalMesh = originalObject.GetComponent<MeshFilter>().sharedMesh;
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
        RecalculateNormals(originalMesh, originalObject.useSmoothShading);
        originalNormals = originalMesh.normals;

        // Update status
        Status = OptimizerStatus.Initialized;
        SwitchMesh();
    }
    public void Deform() {
        if (Status != OptimizerStatus.Initialized) return;
        Debug.Log("Deforming mesh");

        // Apply a deformation to the original mesh by adjusting the distance along the rays
        deformedVertices = new Vector3[adjustmentRays.Length];
        deformedAdjustmentDistances = new float[adjustmentRays.Length];
        Vector3 viewPosition = viewTransform.position;
        // But only deform the unique vertices
        Dictionary<Vector3, List<int>> verticesByPosition = GroupVerticesByLocation(originalVertices);
        int i = 0;
        foreach ((Vector3 position, List<int> identicalVertices) in verticesByPosition) {
            // Determine the new distance
            float newDistance = originalAdjustmentDistances[identicalVertices[0]];
            if (i == DEFORM_INDEX) {
                newDistance *= 1.1f;
            }
            // Apply it to all vertices
            foreach (int vi in identicalVertices) {
                deformedVertices[vi] = viewPosition + adjustmentRays[vi] * newDistance;
                deformedAdjustmentDistances[vi] = newDistance;
            }
            i++;
        }

        // Apply it to a new mesh
        deformedMesh = new();
        deformedMesh.SetVertices(deformedVertices);
        deformedMesh.SetTriangles(originalMesh.triangles, 0);
        RecalculateNormals(deformedMesh, originalObject.useSmoothShading);
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
        if (Status != OptimizerStatus.Deformed) return;
        Debug.Log("Optimizing vertex normals");

        // Make a list of various offsets
        offsetDeviationMap = new();
        float minOffset = -5;
        float maxOffset = 5;
        float offsetStep = 0.1f;
        for (float offset = minOffset; offset <= maxOffset; offset += offsetStep) {
            offsetDeviationMap.Add(offset, -1);
        }
        List<float> offsets = new(offsetDeviationMap.Keys);
        offsets.Sort();


        // Optimize the deformed mesh by adjusting the distance along the rays
        optimizedVertices = new Vector3[adjustmentRays.Length];
        optimizedAdjustmentDistances = new float[adjustmentRays.Length];

        // Initialize the mesh
        optimizedMesh = new();
        optimizedMesh.SetVertices(optimizedVertices);
        optimizedMesh.SetTriangles(originalMesh.triangles, 0);

        Vector3 viewPosition = viewTransform.position;
        // But only optimize the unique vertices
        Dictionary<Vector3, List<int>> verticesByPosition = GroupVerticesByLocation(originalVertices);

        // Loop over all the offsets, and adjust the chosen vertex
        foreach (float offset in offsets) {
            int i = 0;
            foreach ((Vector3 position, List<int> identicalVertices) in verticesByPosition) {
                // Determine the new distance
                float newDistance = deformedAdjustmentDistances[identicalVertices[0]];
                if (i == DEFORM_INDEX) {
                    newDistance += offset;
                }
                // Apply it to all vertices
                foreach (int vi in identicalVertices) {
                    optimizedVertices[vi] = viewPosition + adjustmentRays[vi] * newDistance;
                    optimizedAdjustmentDistances[vi] = newDistance;
                }
                i++;
            }
            // Update the vertices in the mesh
            optimizedMesh.SetVertices(optimizedVertices);
            RecalculateNormals(optimizedMesh, originalObject.useSmoothShading);
            optimizedNormals = optimizedMesh.normals;
            // Calculate the new deviation and store it
            float[] deviations = CalculateAngularDeviation(originalNormals, optimizedNormals);
            offsetDeviationMap[offset] = Enumerable.Sum(deviations);
        }

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

        // Status: Optimized -- variables
        optimizedMesh = null;
        optimizedVertices = null;
        optimizedNormals = null;
        optimizedAdjustmentDistances = null;
        optimizedAngularDeviations = null;
        offsetDeviationMap = null;

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
        Gizmos.color = Color.white;

        if (showOriginalVertices) {
            for (int i = 0; i < originalVertices.Length; i++) {
                Gizmos.DrawSphere(originalVertices[i], GIZMO_SPHERE_RADIUS);
            }
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
        Gizmos.color = Color.blue;
        if (showDeformedVertices) {
            for (int i = 0; i < deformedVertices.Length; i++) {
                Gizmos.DrawSphere(deformedVertices[i], GIZMO_SPHERE_RADIUS * 1.5f);
            }
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
        Gizmos.color = Color.green;
        if (showOffsetDeviationMap) {
            foreach ((float offset, float deviation) in offsetDeviationMap) {
                Gizmos.DrawSphere(new Vector3(5, deviation / 20, offset), GIZMO_SPHERE_RADIUS * 3);
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
        if (minOptimizeOffset >= maxOptimizeOffset) minOptimizeOffset = maxOptimizeOffset - optimizeOffsetStep;
    }
#endif
}
