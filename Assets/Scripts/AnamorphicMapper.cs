using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class AnamorphicMapper : MonoBehaviour {
    private const string NORMAL_SHADER_MODE_PROP_NAME = "_Mode";
    private const string NORMAL_SHADER_RELATIVE_PLANE_PROP_NAME = "_RelativePlane";

    [Header("Settings")]
    [SerializeField]
    private Transform viewPosition;
    [SerializeField]
    private Mirror mirror;
    [SerializeField]
    private GameObject anamorphObject;
    [SerializeField]
    [Range(0, 100)]
    private float maxRaycastDistance = 20f;
    [SerializeField]
    [Range(1, 7)]
    [Tooltip("Maximum amount of reflections allowed for recursive rays.")]
    private int maxReflections = 3;
    [SerializeField]
    [Min(0.00001f)]
    [Tooltip("Minimum distance between mirror and mapped vertices as multiple of anamorph object bounding box.")]
    private float minDistance = 1.0f;
    [SerializeField]
    [Min(0.00001f)]
    [Tooltip("Linearly scales the distance between mirror and mapped vertices.")]
    private float scale = 1.0f;

    public enum MappingStatus { None, Mapped, Optimized };
    public MappingStatus Status { get; private set; } = MappingStatus.None;

    private enum RenderMode { Texture, Normals, ObjectRelativeNormals };
    private enum RelativePlane { XY, YZ, XZ };
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
    private RelativePlane relativePlane = RelativePlane.XZ;

    [Header("Debug")]
    [SerializeField]
    private Vector3[] globalMeshVertices = null;
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
    private Vector3[] mappedVertices = null;

    [SerializeField]
    private bool showMeshVertices = false;
    [SerializeField]
    private bool showMeshNormals = false;
    [SerializeField]
    private bool showRaycastDirections = false;
    [SerializeField]
    private bool showMirrorHits = false;
    [SerializeField]
    private bool showMirrorNormals = false;
    [SerializeField]
    private bool showReflections = false;
    [SerializeField]
    private bool showMappedVertices = false;
    [SerializeField]
    private bool showMappedNormals = false;
    [SerializeField]
    private bool showAdditionalReflectionDistance = false;

    public float showMin = 0;
    public float showMax = 0;
    public float ShowMinLimit { get; private set; } = 0;
    public float ShowMaxLimit { get; private set; } = 0;

    public void MapObject() {
        Debug.Log("Calculating anamorphic object mapping.");
        // Transform all vertices of target mesh to global space.
        Transform anamorphTransform = anamorphObject.transform;
        Mesh anamorphMesh = anamorphObject.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = anamorphMesh.vertices;
        globalMeshVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) {
            globalMeshVertices[i] = anamorphTransform.TransformPoint(vertices[i]);
        }
        // Do the raycasting
        raycastDirections = new Vector3[vertices.Length];
        numReflections = new int[vertices.Length];
        mirrorHits = new Vector3[vertices.Length, maxReflections];
        mirrorNormals = new Vector3[vertices.Length, maxReflections];
        reflections = new Vector3[vertices.Length, maxReflections];

        bool allCastsHit = true;
        for (int i = 0; i < vertices.Length; i++) {
            // Initial raycast
            Vector3 origin = viewPosition.position; // Use as null value, as Vector3 is not nullable
            raycastDirections[i] = globalMeshVertices[i] - origin;
            Vector3 direction = raycastDirections[i].normalized;

            RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxRaycastDistance);

            numReflections[i] = 0;
            if (hits.Length == 0) {
                allCastsHit = false;
                continue;
            }

            mirrorHits[i, 0] = hits[0].point;
            mirrorNormals[i, 0] = hits[0].normal;

            float d = Vector3.Distance(globalMeshVertices[i], mirrorHits[i, 0]);
            reflections[i, 0] = Vector3.Reflect(direction, mirrorNormals[i, 0]) * d;
            numReflections[i]++;

            // Reflections
            for (int r = 1; r < maxReflections; r++) {
                origin = mirrorHits[i, r - 1];
                direction = reflections[i, r - 1].normalized;
                hits = Physics.RaycastAll(origin, direction, maxRaycastDistance);

                if (hits.Length == 0) break;

                mirrorHits[i, r] = hits[0].point;
                mirrorNormals[i, r] = hits[0].normal;
                reflections[i, r] = Vector3.Reflect(direction, mirrorNormals[i, r]) * d;
                reflections[i, r - 1] = reflections[i, r - 1].normalized * Vector3.Distance(origin, mirrorHits[i, r]);
                numReflections[i]++;
            }
        }
        if (!allCastsHit) {
            Debug.LogError("Some initial raycasts did not hit the mirror. Reposition the mirror or increase the maximum raycast distance.");
        }

        // Take the minimum distance into account
        for (int i = 0; i < vertices.Length; i++) {
            int lastReflection = numReflections[i] - 1;
            if (lastReflection < 0) continue;
            if (reflections[i, lastReflection].magnitude >= minDistance) continue;
            reflections[i, lastReflection].Normalize();
            reflections[i, lastReflection] *= minDistance;
        }

        // Use the final reflections to create mesh
        Mesh mappedMesh = new Mesh();
        mappedVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) {
            int lastReflection = numReflections[i] - 1;
            if (lastReflection < 0) continue;
            mappedVertices[i] = mirrorHits[i, lastReflection] + reflections[i, lastReflection] * scale;
        }
        int[] mappedTriangles = new int[anamorphMesh.triangles.Length];
        for (int i = 0; i < mappedTriangles.Length; i += 3) {
            mappedTriangles[i] = anamorphMesh.triangles[i + 2];
            mappedTriangles[i + 1] = anamorphMesh.triangles[i + 1];
            mappedTriangles[i + 2] = anamorphMesh.triangles[i];
        }

        mappedMesh.SetVertices(mappedVertices);
        mappedMesh.SetTriangles(mappedTriangles, 0);
        mappedMesh.SetUVs(0, anamorphMesh.uv);
        // Send the original normals to the shader as uv values;
        // Using uv 3, since unity doc says that 1 and 2 can be used for various lightmaps
        mappedMesh.SetUVs(3, anamorphMesh.normals);
        mappedMesh.RecalculateNormals();
        GetComponent<MeshFilter>().sharedMesh = mappedMesh;

        ShowMaxLimit = vertices.Length - 1;
        Status = MappingStatus.Mapped;
    }

    private bool OptimizeXZPlane() {
        Debug.Log("Applying XZ plane optimizer. Limited to simple planes morphed by a convex mirror with a vertical curve.");

        // Get some basic components into variables
        Mesh originalMesh = anamorphObject.GetComponent<MeshFilter>().sharedMesh;
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
        if (minAngle != 0) {
            Debug.LogError("There is no angle of incident in the xz-plane of 0 degrees.");
            return false;
        }
        print(minAngleIndex);

        Debug.Log("Displacing vertices");
        Vector2 centralVertex = new(originalVertices[minAngleIndex].x, originalVertices[minAngleIndex].z);
        Vector2 centralVertexPrime = new(verticesPrime[minAngleIndex].x, verticesPrime[minAngleIndex].z);
        for (int i = 0; i < originalVertices.Length; i++) {
            if (i == minAngleIndex) continue;
            int lastReflection = numReflections[i] - 1;
            if (lastReflection < 0) continue;

            // Determine angle gamma
            Vector2 vertex = new(originalVertices[i].x, originalVertices[i].z);
            float angle = Vector2.SignedAngle(new Vector2(1, 0), vertex - centralVertex); //TODO maybe signed dunno?
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
            float gamma = (intersection.x - xzMirrorHit.x) / xzReflection.x;
            verticesPrime[i] = mirrorHits[i, lastReflection] + reflections[i, lastReflection] * gamma;
        }

        mappedMesh.SetVertices(verticesPrime);
        mappedMesh.RecalculateNormals();
        return true;
    }

    public void Optimize() {
        if (!OptimizeXZPlane()) return;
        Status = MappingStatus.Optimized;
    }

    public void Clear() {
        globalMeshVertices = null;
        raycastDirections = null;
        numReflections = null;
        mirrorHits = null;
        mirrorNormals = null;
        reflections = null;
        mappedVertices = null;
        GetComponent<MeshFilter>().sharedMesh = null;

        Status = MappingStatus.None;
    }

    private void OnDrawGizmosSelected() {
        Vector3 origin = viewPosition.position;

        Gizmos.color = Color.white;
        if (showMeshVertices && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (numReflections[i] == 0) Gizmos.color = Color.red;
                Gizmos.DrawSphere(globalMeshVertices[i], 0.1f);
                if (numReflections[i] == 0) Gizmos.color = Color.white;
            }
        }
        Gizmos.color = Color.green;
        if (showMeshNormals && Status != MappingStatus.None) {
            Vector3[] meshNormals = anamorphObject.GetComponent<MeshFilter>().sharedMesh.normals;
            for (int i = (int) showMin; i <= showMax; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawLine(globalMeshVertices[i], globalMeshVertices[i] + meshNormals[i]);
                }
            }
        }
        Gizmos.color = Color.white;
        if (showRaycastDirections && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (numReflections[i] == 0) Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, origin + raycastDirections[i]);
                if (numReflections[i] == 0) Gizmos.color = Color.white;
            }
        }
        Gizmos.color = Color.white;
        if (showMirrorHits && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawSphere(mirrorHits[i, r], 0.1f);
                }
            }
        }
        Gizmos.color = Color.green;
        if (showMirrorNormals && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawLine(mirrorHits[i, r], mirrorHits[i, r] + mirrorNormals[i, r]);
                }
            }
        }
        Gizmos.color = Color.blue;
        if (showReflections && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawLine(mirrorHits[i, r], mirrorHits[i, r] + reflections[i, r]);
                }
            }
        }
        Gizmos.color = Color.white;
        if (showMappedVertices && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (numReflections[i] == 0) continue;
                Gizmos.DrawSphere(mappedVertices[i], 0.1f);
            }
        }
        Gizmos.color = Color.green;
        if (showMappedNormals && Status != MappingStatus.None) {
            Vector3[] mappedNormals = GetComponent<MeshFilter>().sharedMesh.normals;
            for (int i = (int) showMin; i <= showMax; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawLine(mappedVertices[i], mappedVertices[i] + mappedNormals[i]);
                }
            }
        }
        Gizmos.color = Color.magenta;
        if (showAdditionalReflectionDistance && Status != MappingStatus.None) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (numReflections[i] == 0) continue;
                Gizmos.DrawLine(mappedVertices[i], mappedVertices[i] + reflections[i, numReflections[i] - 1]);
            }
        }
    }

    private void UpdateMaterials() {
        if (anamorphObject == null) return;
        MeshRenderer anamorphMeshRenderer = anamorphObject.GetComponent<MeshRenderer>();
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
        switch (relativePlane) {
            case RelativePlane.XY:
                morphedNormalsMaterial.SetInteger(NORMAL_SHADER_RELATIVE_PLANE_PROP_NAME, 1);
                break;
            case RelativePlane.YZ:
                morphedNormalsMaterial.SetInteger(NORMAL_SHADER_RELATIVE_PLANE_PROP_NAME, 2);
                break;
            case RelativePlane.XZ:
                morphedNormalsMaterial.SetInteger(NORMAL_SHADER_RELATIVE_PLANE_PROP_NAME, 3);
                break;
        }
    }

    private void OnValidate() {
        UpdateShaderRelativePlane();
        UpdateMaterials();
    }
}
