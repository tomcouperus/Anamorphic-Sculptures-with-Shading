using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class AnamorphicMapper : MonoBehaviour {
    private const string NORMAL_SHADER_MODE_PROP_NAME = "_Mode";
    private const string NORMAL_SHADER_RELATIVE_PLANE_PROP_NAME = "_RelativePlane";

    private enum OptimizerMode { XZPlane, TriangleNormals, IterativeDescent, OffsetTest }

    [Header("Settings")]
    [SerializeField]
    private Transform viewPosition;
    [SerializeField]
    private Mirror mirror;
    [SerializeField]
    private MappableObject[] originalObjects;
    [SerializeField]
    private int originalObjectIndex = 0;
    [SerializeField]
    [Range(0, 100)]
    private float maxRaycastDistance = 20f;
    [SerializeField]
    [Range(1, 7)]
    [Tooltip("Maximum amount of reflections allowed for recursive rays.")]
    private int maxReflections = 3;
    [SerializeField]
    [Min(0.00001f)]
    [Tooltip("Linearly scales the distance between mirror and mapped vertices.")]
    private float scale = 1.0f;
    [SerializeField]
    private OptimizerMode optimizer = OptimizerMode.IterativeDescent;

    public enum MappingStatus { None, Mapped, Optimized };
    public MappingStatus Status { get; private set; } = MappingStatus.None;

    private enum RenderMode { Texture, Normals, ObjectRelativeNormals };
    private enum RelativeMode { XYPlane, YZPlane, XZPlane, Total };
    [Header("Rendering")]
    [SerializeField]
    private Material objectMaterial;
    [SerializeField]
    private Material normalsMaterial;
    [SerializeField]
    private Material morphedNormalsMaterial;
    [SerializeField]
    private RenderMode renderMode = RenderMode.Texture;
    [SerializeField]
    [Tooltip("In render mode 'Relative' this determines the plane in which the angle is calculated.")]
    private RelativeMode relativeMode = RelativeMode.XZPlane;

    private const float GIZMO_SPHERE_RADIUS = 0.1f;
    [Header("Debug")]
    [SerializeField]
    private Vector3[] globalMeshVertices = null;
    [SerializeField]
    private Vector3[] meshNormals = null;
    [SerializeField]
    private Vector3[] meshTrianglePositions = null;
    [SerializeField]
    private Vector3[] meshTriangleNormals = null;
    [SerializeField]
    private Vector3[] raycastDirections = null;
    [SerializeField]
    private int[] numReflections = null;
    [SerializeField]
    private Vector3[,] mirrorHits = null;
    [SerializeField]
    private Vector3[,] mirrorNormals = null;
    [SerializeField]
    private Vector3[,] reflections = null;
    [SerializeField]
    private float[] vertexDistancesFromMirror = null;
    [SerializeField]
    private Vector3[] mappedVertices = null;
    [SerializeField]
    private Vector3[] mappedNormals = null;
    [SerializeField]
    private Vector3[] mappedTrianglePositions = null;
    [SerializeField]
    private Vector3[] mappedTriangleNormals = null;
    [SerializeField]
    private Vector3[] optimizedVertices = null;
    [SerializeField]
    private Vector3[] optimizedNormals = null;

    [SerializeField]
    private bool showMeshVertices = false;
    [SerializeField]
    private bool showMeshNormals = false;
    [SerializeField]
    private bool showMeshTriangleNormals = false;
    [SerializeField]
    private bool showRaycastDirections = false;
    [SerializeField]
    private bool showMirrorHits = false;
    [SerializeField]
    private bool showMirrorNormals = false;
    [SerializeField]
    private bool showReflections = false;
    [SerializeField]
    private float reflectionDistance = 1;
    [SerializeField]
    private bool showMappedVertices = false;
    [SerializeField]
    private bool showMappedNormals = false;
    [SerializeField]
    private bool showMappedTriangleNormals = false;
    [SerializeField]
    private bool showOptimizedVertices = false;
    [SerializeField]
    private bool showOptimizedNormals = false;

    public float showMin = 0;
    public float showMax = 0;
    public float ShowMinLimit { get; private set; } = 0;
    public float ShowMaxLimit { get; private set; } = 0;

    public void MapObject() {
        Debug.Log("Calculating anamorphic object mapping.");
        // Transform all vertices of target mesh to global space.
        MappableObject originalObject = originalObjects[originalObjectIndex];
        Transform originalTransform = originalObject.transform;
        Mesh originalMesh = originalObject.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = originalMesh.vertices;
        Vector3[] normals = originalMesh.normals;
        globalMeshVertices = new Vector3[vertices.Length];
        meshNormals = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) {
            globalMeshVertices[i] = originalTransform.TransformPoint(vertices[i]);
            meshNormals[i] = originalTransform.rotation * normals[i];
        }
        // Do the raycasting
        raycastDirections = new Vector3[vertices.Length];
        numReflections = new int[vertices.Length];
        mirrorHits = new Vector3[vertices.Length, maxReflections];
        mirrorNormals = new Vector3[vertices.Length, maxReflections];
        reflections = new Vector3[vertices.Length, maxReflections];
        vertexDistancesFromMirror = new float[vertices.Length];

        bool allCastsHit = true;
        for (int i = 0; i < vertices.Length; i++) {
            // Initial raycast
            // TODO possible optimisation is less normalising and multiplying by distance of the reflection vectors
            Vector3 origin = viewPosition.position; // Use as null value, as Vector3 is not nullable
            raycastDirections[i] = globalMeshVertices[i] - origin;
            Vector3 direction = raycastDirections[i].normalized;

            RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxRaycastDistance, LayerMask.GetMask("Mirror"));

            numReflections[i] = 0;
            if (hits.Length == 0) {
                allCastsHit = false;
                continue;
            }

            mirrorHits[i, 0] = hits[0].point;
            mirrorNormals[i, 0] = hits[0].normal;

            vertexDistancesFromMirror[i] = Vector3.Distance(globalMeshVertices[i], mirrorHits[i, 0]);
            reflections[i, 0] = Vector3.Reflect(direction, mirrorNormals[i, 0]);
            numReflections[i]++;

            // Reflections
            for (int r = 1; r < maxReflections; r++) {
                origin = mirrorHits[i, r - 1];
                direction = reflections[i, r - 1].normalized;
                hits = Physics.RaycastAll(origin, direction, maxRaycastDistance);

                if (hits.Length == 0) break;

                mirrorHits[i, r] = hits[0].point;
                mirrorNormals[i, r] = hits[0].normal;
                reflections[i, r] = Vector3.Reflect(direction, mirrorNormals[i, r]);
                reflections[i, r - 1] = direction * Vector3.Distance(origin, mirrorHits[i, r]);
                numReflections[i]++;
            }
        }
        if (!allCastsHit) {
            Debug.LogError("Some initial raycasts did not hit the mirror. Reposition the mirror or increase the maximum raycast distance.");
        }

        // Use the final reflections to create mesh
        Mesh mappedMesh = new Mesh();
        mappedVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) {
            int lastReflection = numReflections[i] - 1;
            if (lastReflection < 0) continue;
            mappedVertices[i] = mirrorHits[i, lastReflection] + scale * vertexDistancesFromMirror[i] * reflections[i, lastReflection];
        }
        int[] mappedTriangles = new int[originalMesh.triangles.Length];
        for (int i = 0; i < mappedTriangles.Length; i += 3) {
            mappedTriangles[i] = originalMesh.triangles[i + 2];
            mappedTriangles[i + 1] = originalMesh.triangles[i + 1];
            mappedTriangles[i + 2] = originalMesh.triangles[i];
        }

        mappedMesh.SetVertices(mappedVertices);
        mappedMesh.SetTriangles(mappedTriangles, 0);
        mappedMesh.SetUVs(0, originalMesh.uv);
        // Send the original normals to the shader as uv values;
        // Using uv 3, since unity doc says that 1 and 2 can be used for various lightmaps
        mappedMesh.SetUVs(3, meshNormals);
        RecalculateNormals(mappedMesh, vertices, originalObjects[originalObjectIndex].normalsAreContinuous);

        GetComponent<MeshFilter>().sharedMesh = mappedMesh;

        UpdateCollider();

        // Update last bits of debug gizmo variables
        mappedNormals = mappedMesh.normals;
        meshTrianglePositions = CalculateTrianglePositions(vertices, originalMesh.triangles);
        meshTriangleNormals = CalculateTriangleNormals(vertices, originalMesh.triangles);
        mappedTrianglePositions = CalculateTrianglePositions(mappedVertices, originalMesh.triangles);
        mappedTriangleNormals = CalculateTriangleNormals(mappedVertices, originalMesh.triangles);
        for (int i = 0; i < mappedTriangleNormals.Length; i++) {
            mappedTriangleNormals[i] *= -1;
        }
        ShowMaxLimit = vertices.Length - 1;
        Status = MappingStatus.Mapped;
    }

    /// <summary>
    /// Recalculates the normals of the mapped mesh. 
    /// If the vertices of the original object have duplicates at the same position for UV reasons but are still supposed to form a continuous surface, such as a cube-sphere, this method will account for that.
    /// </summary>
    /// <param name="mesh">Mesh of which the normals are to be calculated</param>
    /// <param name="vertices">Original object's vertices</param>
    private static void RecalculateNormals(Mesh mesh, Vector3[] vertices, bool continuousNormals) {
        // Recalculate the normals. 
        // If the original mesh is flagged as continuous, average the normals at those vertices that share positions with other vertices.
        mesh.RecalculateNormals();
        if (continuousNormals) {
            Vector3[] normals = mesh.normals;
            // First group the vertices by position
            Dictionary<Vector3, List<int>> positionVertexMap = GroupDuplicateVerticesByPosition(vertices);
            // Iterate over each position's indices and add the normals together to form a continuous surface.
            foreach (List<int> indices in positionVertexMap.Values) {
                Vector3 totalNormal = Vector3.zero;
                foreach (int i in indices) {
                    totalNormal += normals[i];
                }
                totalNormal.Normalize();
                foreach (int i in indices) {
                    normals[i] = totalNormal;
                }
            }
            mesh.SetNormals(normals);
        }
    }

    private static Dictionary<Vector3, List<int>> GroupDuplicateVerticesByPosition(Vector3[] vertices) {
        Dictionary<Vector3, List<int>> positionVertexMap = new();
        for (int i = 0; i < vertices.Length; i++) {
            Vector3 position = vertices[i];
            if (!positionVertexMap.ContainsKey(position)) {
                positionVertexMap.Add(position, new List<int>());
            }
            positionVertexMap[position].Add(i);
        }
        return positionVertexMap;
    }

    private static List<int>[] CreateVertexIdentityMap(Vector3[] vertices) {
        Dictionary<Vector3, List<int>> positionVertexMap = GroupDuplicateVerticesByPosition(vertices);
        List<int>[] vertexIdentityMap = new List<int>[vertices.Length];
        foreach (List<int> identicalVertices in positionVertexMap.Values) {
            foreach (int i in identicalVertices) {
                vertexIdentityMap[i] = new List<int>(identicalVertices);
            }
        }
        return vertexIdentityMap;
    }

    private void UpdateCollider() {
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
    }

    private bool OptimizeXZPlane() {
        Debug.Log("Applying XZ plane optimizer. Limited to simple planes morphed by a convex mirror with a vertical curve.");

        // Get some basic components into variables
        Mesh originalMesh = originalObjects[originalObjectIndex].GetComponent<MeshFilter>().sharedMesh;
        Vector3 originalRotation = originalObjects[originalObjectIndex].transform.rotation.eulerAngles;
        Debug.Log("Rotation " + originalRotation);
        Vector3[] originalVertices = originalMesh.vertices;
        Mesh mappedMesh = GetComponent<MeshFilter>().sharedMesh;
        Vector3[] verticesPrime = mappedMesh.vertices;

        // Calculate the angles of incident in the xz-plane and track the lowest.
        // No flipping required if using the angle of reflection, since that's the same.
        float[] xzAnglesIncident = new float[originalVertices.Length];
        float minAngle = float.PositiveInfinity;
        int minAngleIndex = -1;
        for (int i = 0; i < originalVertices.Length; i++) {
            int lastReflection = numReflections[i] - 1;
            if (lastReflection < 0) continue;
            Vector2 xzReflection = new(reflections[i, lastReflection].x, reflections[i, lastReflection].z);
            Vector2 xzMirrorNormal = new(mirrorNormals[i, lastReflection].x, mirrorNormals[i, lastReflection].z);
            xzAnglesIncident[i] = Vector2.Angle(xzReflection, xzMirrorNormal);
            if (xzAnglesIncident[i] < minAngle) {
                minAngle = xzAnglesIncident[i];
                minAngleIndex = i;
            }
        }
        // Only continue if the lowest angle of incident is actually 0, and thus coincident with the camera-vertex ray.
        Debug.Log("Minimum angle " + minAngle + " degrees at index " + minAngleIndex);
        if (minAngle != 0) {
            Debug.LogError("There is no angle of incident in the xz-plane of 0 degrees.");
            return false;
        }

        Debug.Log("Displacing vertices");
        Vector2 centralVertex = new(originalVertices[minAngleIndex].x, originalVertices[minAngleIndex].z);
        Vector2 centralVertexPrime = new(verticesPrime[minAngleIndex].x, verticesPrime[minAngleIndex].z);

        optimizedVertices = new Vector3[originalVertices.Length];
        for (int i = 0; i < originalVertices.Length; i++) {
            // If its the minimal angle, it's the central point and doesn't move
            if (i == minAngleIndex) {
                optimizedVertices[i] = verticesPrime[i];
                continue;
            }
            int lastReflection = numReflections[i] - 1;
            if (lastReflection < 0) continue;

            // Determine angle gamma
            Vector2 vertex = new(originalVertices[i].x, originalVertices[i].z);
            float angle = Vector2.SignedAngle(new Vector2(Mathf.Cos(originalRotation.y * Mathf.Deg2Rad), Mathf.Sin(originalRotation.y * Mathf.Deg2Rad)), vertex - centralVertex);
            float angleRad = angle * Mathf.Deg2Rad;

            Vector2 doublePrimeDirection = new(Mathf.Cos(-angleRad), Mathf.Sin(-angleRad));

            // Get the intersection point of the centralPrime to vertexDoublePrime vector with the reflection vector
            Vector2 xzMirrorHit = new(mirrorHits[i, lastReflection].x, mirrorHits[i, lastReflection].z);
            Vector2 xzReflection = new(reflections[i, lastReflection].x, reflections[i, lastReflection].z);
            bool doesIntersect = MathUtilities.LineLineIntersection(out Vector2 intersection, xzMirrorHit, xzReflection, centralVertexPrime, doublePrimeDirection);
            if (!doesIntersect) {
                Debug.LogError("Vertex " + i + " did not intersect");
                continue;
            }
            // Calculate the gamma
            // Has a bunch of checks for edge cases that haven't been fully worked out yet, just... patched.
            float gamma = (intersection.x - xzMirrorHit.x) / xzReflection.x;
            // TODO improve issue for NaN errors on the x axis. This just logs and patches it
            if (float.IsNaN(gamma)) {
                Debug.LogError("NaN error at index " + i);
                optimizedVertices[i] = mappedVertices[i];
                Debug.LogWarning("Gamma x: " + gamma);
                Debug.LogWarning("Gamma Nominator x: " + (intersection.x - xzMirrorHit.x));
                Debug.LogWarning("Gamma Denominator x: " + xzReflection.x);
                gamma = (intersection.y - xzMirrorHit.y) / xzReflection.y;
                Debug.LogWarning("Gamma z: " + gamma);
                Debug.LogWarning("Gamma Nominator z: " + (intersection.y - xzMirrorHit.y));
                Debug.LogWarning("Gamma Denominator z: " + xzReflection.y);
            } else {
                optimizedVertices[i] = mirrorHits[i, lastReflection] + reflections[i, lastReflection] * gamma;
                if (gamma > 3 || gamma < 0.4f) {
                    Debug.LogError("Index " + i + " has abnormal gamma: " + gamma);
                    Debug.LogWarning("Gamma x: " + gamma);
                    Debug.LogWarning("Gamma Nominator x: " + (intersection.x - xzMirrorHit.x));
                    Debug.LogWarning("Gamma Denominator x: " + xzReflection.x);
                }
            }

        }

        mappedMesh.SetVertices(optimizedVertices);
        RecalculateNormals(mappedMesh, originalVertices, originalObjects[originalObjectIndex].normalsAreContinuous);

        UpdateCollider();

        optimizedNormals = mappedMesh.normals;
        return true;
    }
    private Vector3[] CalculateTrianglePositions(Vector3[] vertices, int[] triangles) {
        Vector3[] trianglePositions = new Vector3[triangles.Length / 3];
        for (int i = 0; i < trianglePositions.Length; i++) {
            Vector3 a = vertices[triangles[3 * i]];
            Vector3 b = vertices[triangles[3 * i + 1]];
            Vector3 c = vertices[triangles[3 * i + 2]];
            trianglePositions[i] = (a + b + c) / 3;
        }
        return trianglePositions;
    }

    private Vector3[] CalculateTriangleNormals(Vector3[] vertices, int[] triangles) {
        Vector3[] triangleNormals = new Vector3[triangles.Length / 3];
        for (int i = 0; i < triangles.Length / 3; i++) {
            Vector3 a = vertices[triangles[3 * i]];
            Vector3 b = vertices[triangles[3 * i + 1]];
            Vector3 c = vertices[triangles[3 * i + 2]];

            triangleNormals[i] = Vector3.Cross(b - a, c - a).normalized;
        }
        return triangleNormals;
    }

    /// <summary>
    /// Creates a map to see which vertex is part of which triangles.
    /// The key of the map is the vertex index. These are mapped to a set of triangle indices.
    /// </summary>
    /// <param name="triangles"></param>
    /// <param name="vertices"></param>
    /// <returns></returns>
    private Dictionary<int, HashSet<int>> CreateVertexTriangleMap(int[] triangles, Vector3[] vertices) {
        // First initialize the vertex to triangle mapping
        Dictionary<int, HashSet<int>> vertexTriangleMap = new();
        for (int i = 0; i < vertices.Length; i++) {
            vertexTriangleMap.Add(i, new HashSet<int>());
        }
        // Then iterate over all the triangles, and add the triangle to the relevant vertex in the map
        for (int i = 0; i < triangles.Length / 3; i++) {
            vertexTriangleMap[triangles[3 * i]].Add(i);
            vertexTriangleMap[triangles[3 * i + 1]].Add(i);
            vertexTriangleMap[triangles[3 * i + 2]].Add(i);
        }
        return vertexTriangleMap;
    }

    private bool OptimizeTriangleNormals() {
        Debug.Log("Applying triangle normals optimizer. Limited to objects morphed by a convex mirror. Possible to work on more, but that is unverified.");
        if (!originalObjects[originalObjectIndex].meshIsContinuous) {
            Debug.LogError("This method only works on continuous meshes.");
            return false;
        }

        // Just get some values into vars
        Mesh originalMesh = originalObjects[originalObjectIndex].GetComponent<MeshFilter>().sharedMesh;
        Mesh mappedMesh = GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = originalMesh.vertices;
        Vector3[] normals = originalMesh.normals;
        int[] triangles = originalMesh.triangles;
        Vector3[] mappedVertices = mappedMesh.vertices;
        Vector3[] mappedNormals = mappedMesh.normals;

        // Calculate triangle normals
        Vector3[] optimizedTriangleNormals = mappedTriangleNormals;
        // Vector3[] optimizedTriangleNormals = new Vector3[triangleNormals.Length];

        // Find a rotation to base the other triangles on.
        // TODO improve selection routine. Currently just picks the first triangle, while there are probably more optimal choices. Or maybe just run an exhaustive search to minimize bounding box? Would likely require running everything below here N times.
        int initialTriangleIndex = -1;
        float maxZ = float.NegativeInfinity;
        for (int i = 0; i < mappedTriangleNormals.Length; i++) {
            float z = mappedTriangleNormals[i].z;
            if (z > maxZ) {
                maxZ = z;
                initialTriangleIndex = i;
            }
        }
        // Rotate all triangle relative to this initial triangle
        for (int i = 0; i < meshTriangleNormals.Length; i++) {
            Vector3 rotation = -Quaternion.FromToRotation(meshTriangleNormals[initialTriangleIndex], meshTriangleNormals[i]).eulerAngles;
            optimizedTriangleNormals[i] = Quaternion.Euler(rotation.x, rotation.y, rotation.z) * mappedTriangleNormals[initialTriangleIndex];
        }
        // return true;
        // Map which vertices are part of which triangles
        Dictionary<int, HashSet<int>> vertexTriangleMap = CreateVertexTriangleMap(triangles, vertices);
        List<int>[] vertexIdentityMap = CreateVertexIdentityMap(vertices);

        int[] optimizedVertexIsPlaced = new int[vertices.Length];
        optimizedVertices = new Vector3[vertices.Length];
        // Use triangle normals to find their plane and calculate intersections with the reflection rays, radiating outward from the triangle used to find the rotation.
        // Place initial vertices and queue up other vertices connected to these vertices
        List<int> initialVertices = new() {
            triangles[3*initialTriangleIndex],
            triangles[3*initialTriangleIndex+1],
            triangles[3*initialTriangleIndex+2]
        };
        // source vertex, triangle, placeable vertex
        Queue<(int, int, int)> placeableVertices = new();
        foreach (int v in initialVertices) {
            optimizedVertices[v] = mappedVertices[v];
            optimizedVertexIsPlaced[v]++;

            // Get connected triangles and vertices
            HashSet<int> vertexTriangles = vertexTriangleMap[v];
            foreach (int vt in vertexTriangles) {
                int v0 = triangles[3 * vt];
                int v1 = triangles[3 * vt + 1];
                int v2 = triangles[3 * vt + 2];
                // Only add vertex if not itself or a vertex connected to the initial triangle
                if (v0 != v && vt != initialTriangleIndex) placeableVertices.Enqueue((v, vt, v0));
                if (v1 != v && vt != initialTriangleIndex) placeableVertices.Enqueue((v, vt, v1));
                if (v2 != v && vt != initialTriangleIndex) placeableVertices.Enqueue((v, vt, v2));
            }
        }

        // Debug.LogWarning("Initial vertices");
        // foreach (int v in initialVertices) {
        //     print("(" + v + ", " + initialTriangleIndex + ")");
        // }

        // Debug.LogWarning("Placeable triangles");
        // foreach ((int sv, int t, int v) in placeableVertices) {
        //     print("(" + sv + ", " + t + ", " + v + ")");
        // }

        // Place vertices until no more are left
        int maxIterations = 20000;
        int iter = 0;
        while (placeableVertices.Count != 0 && iter < maxIterations) {
            // Track iterations and limit them just in case.
            iter++;

            // Get the source vertex, triangle, and target vertex
            (int sv, int t, int v) = placeableVertices.Dequeue();
            // Debug.LogWarning("Trying to place vertex " + v + " based on triangle " + t + " and source vertex " + sv);

            int lastReflection = numReflections[v] - 1;
            if (lastReflection < 0) {
                Debug.LogError("Vertex " + v + " did not have any reflections");
                continue;
            }
            // Calculate intersection of vertex's reflection ray with the plane it is to be placed in
            bool doesIntersect = MathUtilities.LinePlaneIntersection(out Vector3 intersection, mirrorHits[v, lastReflection], reflections[v, lastReflection], optimizedTriangleNormals[t], optimizedVertices[sv]);
            if (!doesIntersect) {
                // Debug.LogError("Vertex " + v + " has no intersection with the plane formed by triangle " + t + " and vertex " + sv);
                continue;
            }
            // print("Vertex " + v + " intersects its reflection ray at " + intersection);
            // Add the vertex if it hasn't been placed yet
            if (optimizedVertexIsPlaced[v] > 0) {
                float sqrDistance = Vector3.SqrMagnitude(intersection - optimizedVertices[v]);
                // Debug.Log("Vertex " + v + " is already placed at " + optimizedVertices[v] + ". Sqr distance between placements: " + sqrDistance);
                if (sqrDistance < 0.005f) {
                    // Debug.Log("Skipping duplicate placement");
                    continue;
                } else {
                    Debug.LogError("Distance too large for vertex " + v + ". Needs solution!");
                    Debug.Log("Current: " + optimizedVertices[v]);
                    Debug.Log("New: " + intersection);
                    // // Version 3: having a center of mass to decide which of the two options to keep. 
                    // // TODO: Not implemented yet. Ideas?
                    // // Version 2: averaging all optimized positions
                    // // TODO: also not optimal. Averaging the problematic vertices does bring in the high negative z values, but also moves the lower negative z values closer to 0, leading to vertices from the back in front of the front face, and vertices from the back/slide being moved very far back...
                    // intersection += optimizedVertexIsPlaced[v] * optimizedVertices[v];
                    // intersection /= optimizedVertexIsPlaced[v] + 1;
                    // Debug.Log("Using average: " + intersection);
                    // continue;
                    // // Version 1: based on distance to original mapped location
                    // // TODO look into better ways of picking the optimal location. This is a "decent" fix in the sense that the mesh is not all over the place, but it's still not good.
                    // float sqrDistanceOptimizedMapped = Vector3.SqrMagnitude(optimizedVertices[v] - mappedVertices[v]);
                    // float sqrDistanceIntersectMapped = Vector3.SqrMagnitude(intersection - mappedVertices[v]);
                    // Debug.Log("Old distance to mapped point: " + sqrDistanceOptimizedMapped);
                    // Debug.Log("New distance to mapped point: " + sqrDistanceIntersectMapped);
                    // if (sqrDistanceIntersectMapped > sqrDistanceOptimizedMapped) {
                    //     Debug.LogWarning("New distance larger than old. Skipping placement");
                    //     continue;
                    // }
                    // Old
                    continue;
                }
            }
            // // Old
            // optimizedVertices[v] = intersection;
            // optimizedVertexIsPlaced[v]++;
            // // Add the neighbouring vertices that haven't already been placed
            // HashSet<int> vertexTriangles = vertexTriangleMap[v];
            // foreach (int vt in vertexTriangles) {
            //     int v0 = triangles[3 * vt];
            //     int v1 = triangles[3 * vt + 1];
            //     int v2 = triangles[3 * vt + 2];
            //     // Only add vertex if not itself or a vertex connected to the initial triangle
            //     if (v0 != v && vt != t) placeableVertices.Enqueue((v, vt, v0));
            //     if (v1 != v && vt != t) placeableVertices.Enqueue((v, vt, v1));
            //     if (v2 != v && vt != t) placeableVertices.Enqueue((v, vt, v2));
            // }
            // New
            foreach (int vi in vertexIdentityMap[v]) {
                optimizedVertices[vi] = intersection;
                optimizedVertexIsPlaced[vi]++;
                // print("Vertex " + v + " with triangle normal " + optimizedTriangleNormals[t] + " has identical vertex " + vi + ", with triangle normal " + optimizedTriangleNormals[vertexTriangleMap[vi].First()]);
                HashSet<int> vertexTrianglesTest = vertexTriangleMap[vi];
                foreach (int vt in vertexTrianglesTest) {
                    int v0 = triangles[3 * vt];
                    int v1 = triangles[3 * vt + 1];
                    int v2 = triangles[3 * vt + 2];
                    // Only add vertex if not itself or a vertex connected to the initial triangle
                    if (v0 != vi && vt != t) placeableVertices.Enqueue((vi, vt, v0));
                    if (v1 != vi && vt != t) placeableVertices.Enqueue((vi, vt, v1));
                    if (v2 != vi && vt != t) placeableVertices.Enqueue((vi, vt, v2));
                }
            }
        }
        print(iter + "/" + maxIterations);

        // Set the vertices and update normals and colliders.
        mappedMesh.SetVertices(optimizedVertices);
        RecalculateNormals(mappedMesh, vertices, originalObjects[originalObjectIndex].normalsAreContinuous);

        UpdateCollider();

        optimizedNormals = mappedMesh.normals;

        return true;
    }

    private bool OptimizeIterativeDescent() {
        Debug.Log("Applying iterative descent method to minimize angles between original vertex normals and mapped vertex normals.");
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;

        // Initialise optimized vertices
        optimizedVertices = (Vector3[]) mappedVertices.Clone();

        // Get the ideal vertex normals
        Vector3[] idealVertexNormals = (Vector3[]) meshNormals.Clone();
        for (int i = 0; i < idealVertexNormals.Length; i++) {
            idealVertexNormals[i].z *= -1;
        }
        optimizedNormals = idealVertexNormals;

        // Calculate the initial angular deviation for each vertex
        Dictionary<int, float> normalAnglesFromIdeal = new();
        float totalAngleFromIdeal = 0;
        for (int i = 0; i < idealVertexNormals.Length; i++) {
            float angleFromIdeal = Vector3.Angle(idealVertexNormals[i], mappedNormals[i]);
            normalAnglesFromIdeal.Add(i, angleFromIdeal);
            totalAngleFromIdeal += angleFromIdeal;
        }
        Debug.LogWarning("Initial total angular deviation of vertex normals: " + totalAngleFromIdeal);

        // Sort the angles (and the indices)
        List<KeyValuePair<int, float>> sortedNormalAnglesFromIdeal = normalAnglesFromIdeal.ToList();
        static int largeToSmallSorter(KeyValuePair<int, float> pair1, KeyValuePair<int, float> pair2) {
            return pair2.Value.CompareTo(pair1.Value);
        }
        sortedNormalAnglesFromIdeal.Sort(largeToSmallSorter);
        // for (int i = 0; i < sortedNormalAnglesFromIdeal.Count; i++) {
        //     Debug.Log(sortedNormalAnglesFromIdeal[i]);
        // }

        List<int>[] vertexIdentityMap = CreateVertexIdentityMap(globalMeshVertices);
        int numIterations = 10000;
        int angleTooSmallRejections = 0;
        int rejections = 0;
        Random.InitState(0);
        float newTotalAngleFromIdeal = totalAngleFromIdeal;
        int offsetIfStuck = 0;
        for (int n = 0; n < numIterations; n++) {
            Mesh proposedMesh = new();
            Vector3[] proposedVertices = (Vector3[]) optimizedVertices.Clone();

            // If the stuck offset is larger than the amount of vertices we have, the algorithm cannot change any other vertices anymore.
            if (offsetIfStuck >= optimizedVertices.Length) {
                Debug.LogWarning("No more vertices to be changed. Stopping at iteration " + n);
                break;
            }

            // Pick a vertex from the list to mutate
            int chosenListIndex = 0 + offsetIfStuck;
            // int chosenListIndex = Random.Range(0, proposedVertices.Length);
            (int v, float angle) = sortedNormalAnglesFromIdeal[chosenListIndex];
            // If the angle is small enough, ignore and repeat
            if (angle < 0.2f) {
                angleTooSmallRejections++;
                offsetIfStuck++;
                continue;
            }

            // Pick a mutation
            float mutation = 0.01f;
            // mutation = vertexDistancesFromMirror[v] * (1 + mutation);
            // mutation = vertexDistancesFromMirror[v] * (1 - mutation);
            bool addMutation = Random.Range(0, 2) == 0;
            if (addMutation) mutation = vertexDistancesFromMirror[v] * (1 + mutation);
            else mutation = vertexDistancesFromMirror[v] * (1 - mutation);

            // Displace all identical vertices
            List<int> identicalVertices = vertexIdentityMap[v];
            for (int i = 0; i < identicalVertices.Count; i++) {
                int vi = identicalVertices[i];
                int lastReflection = numReflections[vi] - 1;
                if (lastReflection < 0) continue;
                proposedVertices[vi] = mirrorHits[vi, lastReflection] + scale * mutation * reflections[vi, lastReflection];
            }

            // Recalculate the normals
            // TODO figure out local calculation. First attempt did not match exactly, so postponed in favour of proof of concept
            proposedMesh.SetVertices(proposedVertices);
            proposedMesh.SetTriangles(mesh.triangles, 0);
            RecalculateNormals(proposedMesh, proposedVertices, originalObjects[originalObjectIndex].normalsAreContinuous);
            Vector3[] proposedNormals = proposedMesh.normals;
            float proposedTotalAngleFromIdeal = 0;
            for (int i = 0; i < idealVertexNormals.Length; i++) {
                float angleFromIdeal = Vector3.Angle(idealVertexNormals[i], proposedNormals[i]);
                normalAnglesFromIdeal[i] = angleFromIdeal;
                proposedTotalAngleFromIdeal += angleFromIdeal;
            }
            Debug.Log("Vertex " + v + " (" + angle + ") gives new total angular deviation: " + proposedTotalAngleFromIdeal);
            // If no reduction, reject and keep going
            if (proposedTotalAngleFromIdeal > newTotalAngleFromIdeal) {
                // Debug.LogWarning("Rejected");
                offsetIfStuck++;
                rejections++;
                continue;
            }
            // If accepted, reset stuck offset
            offsetIfStuck = 0;
            // If accepted, update all changed distances
            for (int i = 0; i < identicalVertices.Count; i++) {
                int vi = identicalVertices[i];
                vertexDistancesFromMirror[vi] = mutation;
            }
            // Update new total
            newTotalAngleFromIdeal = proposedTotalAngleFromIdeal;

            // Resort the list
            sortedNormalAnglesFromIdeal = normalAnglesFromIdeal.ToList();
            sortedNormalAnglesFromIdeal.Sort(largeToSmallSorter);

            // Update the actual mesh
            optimizedVertices = proposedVertices;
            mesh.SetVertices(proposedVertices);
            mesh.SetNormals(proposedMesh.normals);
        }
        Debug.Log("Angle too small rejections: " + angleTooSmallRejections + "/" + numIterations);
        Debug.Log("Other rejections: " + rejections + "/" + numIterations);
        Debug.Log("Change in total angular deviation: " + ((newTotalAngleFromIdeal - totalAngleFromIdeal) / totalAngleFromIdeal * 100) + "%");

        return false;
    }

    private bool OptimizeOffsetTest() {
        Debug.Log("Applying additional offset ");
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;

        // Calculate the vertex with minimum depth value
        float minDepth = float.PositiveInfinity;
        for (int i = 0; i < globalMeshVertices.Length; i++) {
            if (globalMeshVertices[i].z < minDepth) {
                minDepth = globalMeshVertices[i].z;
            }
        }
        // Calculate z-difference relative to this point
        float[] zOffsets = new float[globalMeshVertices.Length];
        for (int i = 0; i < zOffsets.Length; i++) {
            zOffsets[i] = globalMeshVertices[i].z - minDepth;
        }
        optimizedVertices = new Vector3[mappedVertices.Length];
        for (int i = 0; i < optimizedVertices.Length; i++) {
            int lastReflection = numReflections[i] - 1;
            if (lastReflection < 0) continue;
            optimizedVertices[i] = mirrorHits[i, lastReflection] + scale * (vertexDistancesFromMirror[i] + zOffsets[i]) * reflections[i, lastReflection];
        }
        mesh.SetVertices(optimizedVertices);
        RecalculateNormals(mesh, globalMeshVertices, originalObjects[originalObjectIndex].normalsAreContinuous);
        UpdateCollider();

        optimizedNormals = mesh.normals;
        return true;
    }

    public void Optimize() {
        switch (optimizer) {
            case OptimizerMode.XZPlane:
                if (!OptimizeXZPlane()) return;
                break;
            case OptimizerMode.TriangleNormals:
                if (!OptimizeTriangleNormals()) return;
                break;
            case OptimizerMode.IterativeDescent:
                if (!OptimizeIterativeDescent()) return;
                break;
            case OptimizerMode.OffsetTest:
                if (!OptimizeOffsetTest()) return;
                break;
            default:
                Debug.LogError("Optimization mode not implemented.");
                return;
        }
        Status = MappingStatus.Optimized;
    }

    public void Clear() {
        globalMeshVertices = null;
        meshNormals = null;
        meshTrianglePositions = null;
        meshTriangleNormals = null;
        raycastDirections = null;
        numReflections = null;
        mirrorHits = null;
        mirrorNormals = null;
        reflections = null;
        vertexDistancesFromMirror = null;
        mappedVertices = null;
        mappedNormals = null;
        mappedTrianglePositions = null;
        mappedTriangleNormals = null;
        optimizedVertices = null;
        optimizedNormals = null;
        GetComponent<MeshFilter>().sharedMesh = null;
        GetComponent<MeshCollider>().sharedMesh = null;

        Status = MappingStatus.None;
    }

    private void OnDrawGizmosSelected() {
        Vector3 origin = viewPosition.position;

        // Original mesh
        Gizmos.color = Color.white;
        if (showMeshVertices && globalMeshVertices != null && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax && i < globalMeshVertices.Length; i++) {
                if (numReflections[i] == 0) Gizmos.color = Color.red;
                Gizmos.DrawSphere(globalMeshVertices[i], GIZMO_SPHERE_RADIUS);
                if (numReflections[i] == 0) Gizmos.color = Color.white;
            }
        }
        // Original normals
        Gizmos.color = Color.green;
        if (showMeshNormals && globalMeshVertices != null && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax && i < globalMeshVertices.Length; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawLine(globalMeshVertices[i], globalMeshVertices[i] + meshNormals[i]);
                }
            }
        }
        // Original triangle normals
        Gizmos.color = Color.green;
        if (showMeshTriangleNormals && meshTriangleNormals != null && meshTrianglePositions != null && Status != MappingStatus.None) {
            Transform originalObjectTransform = originalObjects[originalObjectIndex].transform;
            for (int i = 0; i < meshTriangleNormals.Length; i++) {
                Vector3 globalTrianglePos = originalObjectTransform.TransformPoint(meshTrianglePositions[i]);
                Gizmos.DrawLine(globalTrianglePos, globalTrianglePos + meshTriangleNormals[i]);
            }
        }
        // Raycast directions
        Gizmos.color = Color.white;
        if (showRaycastDirections && raycastDirections != null && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax && i < raycastDirections.Length; i++) {
                if (numReflections[i] == 0) Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, origin + raycastDirections[i]);
                if (numReflections[i] == 0) Gizmos.color = Color.white;
            }
        }
        // Mirror intersections
        Gizmos.color = Color.white;
        if (showMirrorHits && mirrorHits != null && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax && i < mirrorHits.Length; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawSphere(mirrorHits[i, r], GIZMO_SPHERE_RADIUS);
                }
            }
        }
        // Mirror normals
        Gizmos.color = Color.green;
        if (showMirrorNormals && mirrorHits != null && mirrorNormals != null && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawLine(mirrorHits[i, r], mirrorHits[i, r] + mirrorNormals[i, r]);
                }
            }
        }
        // Reflections
        Gizmos.color = Color.blue;
        if (showReflections && mirrorHits != null && reflections != null && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax && i < reflections.Length; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Vector3 reflection = reflections[i, r];
                    if (r == numReflections[i] - 1) reflection *= vertexDistancesFromMirror[i];
                    Gizmos.DrawLine(mirrorHits[i, r], mirrorHits[i, r] + reflection * reflectionDistance);
                }
            }
        }
        // Mapped vertices
        Gizmos.color = Color.white;
        if (showMappedVertices && mappedVertices != null && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax && i < mappedVertices.Length; i++) {
                if (numReflections[i] == 0) continue;
                Gizmos.DrawSphere(mappedVertices[i], GIZMO_SPHERE_RADIUS);
            }
        }
        // Normals of mapped vertices
        Gizmos.color = Color.green;
        if (showMappedNormals && mappedVertices != null && mappedNormals != null && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax && i < mappedVertices.Length; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawLine(mappedVertices[i], mappedVertices[i] + mappedNormals[i]);
                }
            }
        }
        // Mapped triangle normals
        Gizmos.color = Color.green;
        if (showMappedTriangleNormals && mappedTriangleNormals != null && mappedTrianglePositions != null && Status != MappingStatus.None) {
            for (int i = 0; i < mappedTriangleNormals.Length; i++) {
                Gizmos.DrawLine(mappedTrianglePositions[i], mappedTrianglePositions[i] + mappedTriangleNormals[i]);
            }
        }
        // Mapped vertices after optimization
        Gizmos.color = Color.magenta;
        if (showOptimizedVertices && optimizedVertices != null && Status == MappingStatus.Optimized) {
            for (int i = (int) showMin; i <= showMax && i < optimizedVertices.Length; i++) {
                if (numReflections[i] == 0) continue;
                Gizmos.DrawSphere(optimizedVertices[i], GIZMO_SPHERE_RADIUS);
            }
        }
        // Normals of mapped vertices after optimization
        Gizmos.color = Color.green;
        if (showOptimizedNormals && optimizedVertices != null && optimizedNormals != null && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax && i < optimizedVertices.Length; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawLine(optimizedVertices[i], optimizedVertices[i] + optimizedNormals[i]);
                }
            }
        }
    }

    private void UpdateMaterials() {
        if (originalObjects[originalObjectIndex] == null) return;
        MeshRenderer anamorphMeshRenderer = originalObjects[originalObjectIndex].GetComponent<MeshRenderer>();
        MeshRenderer mappedMeshRenderer = GetComponent<MeshRenderer>();
        switch (renderMode) {
            case RenderMode.Texture:
                anamorphMeshRenderer.material = objectMaterial;
                mappedMeshRenderer.material = objectMaterial;
                break;
            case RenderMode.Normals:
                anamorphMeshRenderer.material = normalsMaterial;
                mappedMeshRenderer.material = morphedNormalsMaterial;
                morphedNormalsMaterial.SetInteger(NORMAL_SHADER_MODE_PROP_NAME, 1);
                break;
            case RenderMode.ObjectRelativeNormals:
                anamorphMeshRenderer.material = normalsMaterial;
                mappedMeshRenderer.material = morphedNormalsMaterial;
                morphedNormalsMaterial.SetInteger(NORMAL_SHADER_MODE_PROP_NAME, 2);
                break;
        }
    }

    private void UpdateShaderRelativePlane() {
        if (morphedNormalsMaterial == null) return;
        morphedNormalsMaterial.SetInteger(NORMAL_SHADER_RELATIVE_PLANE_PROP_NAME, (int) relativeMode + 1);
    }

    private void OnValidate() {
        if (originalObjectIndex < 0) originalObjectIndex = 0;
        if (originalObjectIndex >= originalObjects.Length) originalObjectIndex = originalObjects.Length - 1;
        for (int i = 0; i < originalObjects.Length; i++) {
            originalObjects[i].gameObject.SetActive(i == originalObjectIndex);
        }
        UpdateShaderRelativePlane();
        UpdateMaterials();
    }
}
