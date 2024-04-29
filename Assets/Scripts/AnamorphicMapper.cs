using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class AnamorphicMapper : MonoBehaviour {
    private const string NORMAL_SHADER_MODE_PROP_NAME = "_Mode";
    private const string NORMAL_SHADER_RELATIVE_PLANE_PROP_NAME = "_RelativePlane";

    [Header("Settings")]
    [SerializeField]
    private Transform viewer;
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

    private bool mapped = false;

    private enum RenderMode { Texture, Normals, RelativeNormals };
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
    private RelativePlane relativePlane = RelativePlane.XY;
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
    private bool showGlobalMeshVertices = false;
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

    public float showMin = 0;
    public float showMax = 0;
    public float ShowMinLimit { get; private set; } = 0;
    public float ShowMaxLimit { get; private set; } = 0;

    public void MapObject() {
        // Transform all vertices of target mesh to global space.
        Transform anamorphTransform = anamorphObject.transform;
        Mesh anamorphMesh;
        anamorphMesh = anamorphObject.GetComponent<MeshFilter>().sharedMesh;
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
            Vector3 origin = viewer.position; // Use as null value, as Vector3 is not nullable
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
            Debug.Log("Some initial raycasts did not hit the mirror. Reposition the mirror or increase the maximum raycast distance.");
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
        mappedMesh.SetUVs(3, anamorphMesh.normals); // Using uv 3, since unity doc says that 1 and 2 can be used for various lightmaps
        mappedMesh.RecalculateNormals();
        GetComponent<MeshFilter>().sharedMesh = mappedMesh;

        ShowMaxLimit = vertices.Length - 1;
        mapped = true;
    }

    public void Clear() {
        mapped = false;
        globalMeshVertices = null;
        raycastDirections = null;
        numReflections = null;
        mirrorHits = null;
        mirrorNormals = null;
        reflections = null;
        mappedVertices = null;
        GetComponent<MeshFilter>().sharedMesh = null;
    }

    private void OnDrawGizmosSelected() {
        Vector3 origin = viewer.position;

        Gizmos.color = Color.white;
        if (showGlobalMeshVertices && mapped) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (numReflections[i] == 0) Gizmos.color = Color.red;
                Gizmos.DrawSphere(globalMeshVertices[i], 0.1f);
                if (numReflections[i] == 0) Gizmos.color = Color.white;
            }
        }
        Gizmos.color = Color.white;
        if (showRaycastDirections && mapped) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (numReflections[i] == 0) Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, origin + raycastDirections[i]);
                if (numReflections[i] == 0) Gizmos.color = Color.white;
            }
        }
        Gizmos.color = Color.white;
        if (showMirrorHits && mapped) {
            for (int i = (int) showMin; i <= showMax; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawSphere(mirrorHits[i, r], 0.1f);
                }
            }
        }
        Gizmos.color = Color.green;
        if (showMirrorNormals && mapped) {
            for (int i = (int) showMin; i <= showMax; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawLine(mirrorHits[i, r], mirrorHits[i, r] + mirrorNormals[i, r]);
                }
            }
        }
        Gizmos.color = Color.blue;
        if (showReflections && mapped) {
            for (int i = (int) showMin; i <= showMax; i++) {
                for (int r = 0; r < numReflections[i]; r++) {
                    Gizmos.DrawLine(mirrorHits[i, r], mirrorHits[i, r] + reflections[i, r]);
                }
            }
        }
        Gizmos.color = Color.white;
        if (showMappedVertices && mapped) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (numReflections[i] == 0) continue;
                Gizmos.DrawSphere(mappedVertices[i], 0.1f);
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
            case RenderMode.RelativeNormals:
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
