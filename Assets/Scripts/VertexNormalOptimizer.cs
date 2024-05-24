using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VertexNormalOptimizer : MonoBehaviour {
    [Header("Settings")]
    [SerializeField]
    private MappableObject originalObject;
    [SerializeField]
    private Transform viewTransform;


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

        // Switch mesh visibility
        originalObject.gameObject.SetActive(true);
        GetComponent<MeshRenderer>().enabled = false;

        // Update status
        Status = OptimizerStatus.Initialized;
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
            if (i == 3) {
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

        // Switch mesh visibility
        originalObject.gameObject.SetActive(false);
        GetComponent<MeshRenderer>().enabled = true;

        // Update status
        Status = OptimizerStatus.Deformed;
    }

    public void Optimize() {
        if (Status != OptimizerStatus.Deformed) return;
        Debug.Log("Optimizing vertex normals");

        // Update status
        Status = OptimizerStatus.Optimized;
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

        // Switch mesh visibility
        originalObject.gameObject.SetActive(true);
        GetComponent<MeshRenderer>().enabled = false;

        // Update status
        Status = OptimizerStatus.None;
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

    // DEBUG METHODS
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

    private void OnDrawGizmos() {
        DrawInitializedGizmos();
        DrawDeformedGizmos();
    }
}
