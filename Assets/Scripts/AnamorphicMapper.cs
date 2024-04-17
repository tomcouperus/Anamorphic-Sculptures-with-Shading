using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class AnamorphicMapper : MonoBehaviour {
    [Header("Settings")]
    [SerializeField]
    private Transform viewer;
    [SerializeField]
    private Mirror mirror;
    [SerializeField]
    private GameObject anamorphObject;

    [Header("Debug")]
    [SerializeField]
    private Vector3[] globalMeshVertices = null;
    [SerializeField]
    private Vector3[] raycastDirections = null;
    [SerializeField]
    private Vector3[] mirrorHits = null;
    [SerializeField]
    private Vector3[] mirrorNormals = null;
    [SerializeField]
    private Vector3[] reflections = null;
    [SerializeField]
    private Vector3[] mappedVertices = null;

    [SerializeField]
    private bool showGlobalMeshVertices = false;
    [SerializeField]
    private bool showRaycastDirections = false;
    [SerializeField]
    private bool showMirrorHits = false;
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
        // Create raycasts
        Vector3 origin = viewer.position;
        raycastDirections = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) {
            raycastDirections[i] = globalMeshVertices[i] - origin;
        }
        // Do the raycasting
        mirrorHits = new Vector3[vertices.Length];
        mirrorNormals = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) {
            RaycastHit[] hits = Physics.RaycastAll(origin, raycastDirections[i].normalized, 20f);
            if (hits.Length > 0) {
                mirrorHits[i] = hits[0].point;
                mirrorNormals[i] = hits[0].normal;
            } else {
                mirrorHits[i] = origin;
                mirrorNormals[i] = origin;
                Debug.Log("No hit with mirror");
            }
        }
        // Reflect the raycast around the normal of the hit
        reflections = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) {
            if (mirrorHits[i] == origin) {
                reflections[i] = origin;
                continue;
            }
            reflections[i] = Vector3.Reflect(raycastDirections[i].normalized, mirrorNormals[i]);
            reflections[i] *= Vector3.Distance(globalMeshVertices[i], mirrorHits[i]);
        }
        // Calculate new mesh
        mappedVertices = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) {
            mappedVertices[i] = mirrorHits[i] + reflections[i];
        }

        ShowMaxLimit = vertices.Length - 1;
    }

    public void Clear() {
        globalMeshVertices = null;
        raycastDirections = null;
        mirrorHits = null;
        mirrorNormals = null;
        reflections = null;
        mappedVertices = null;
    }

    private void OnDrawGizmosSelected() {
        Vector3 origin = viewer.position;

        Gizmos.color = Color.white;
        if (showGlobalMeshVertices && globalMeshVertices != null) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (mirrorHits[i] == origin) Gizmos.color = Color.red;
                Gizmos.DrawSphere(globalMeshVertices[i], 0.1f);
                if (mirrorHits[i] == origin) Gizmos.color = Color.white;
            }
        }
        Gizmos.color = Color.white;
        if (showRaycastDirections && raycastDirections != null && viewer != null) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (mirrorHits[i] == origin) Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, origin + raycastDirections[i]);
                if (mirrorHits[i] == origin) Gizmos.color = Color.white;
            }
        }
        Gizmos.color = Color.white;
        if (showMirrorHits && mirrorHits != null) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (mirrorHits[i] == origin) continue;
                Gizmos.DrawSphere(mirrorHits[i], 0.1f);
            }
        }
        Gizmos.color = Color.blue;
        if (showReflections && reflections != null) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (mirrorHits[i] == origin) continue;
                Gizmos.DrawLine(mirrorHits[i], mirrorHits[i] + reflections[i]);
            }
        }
        Gizmos.color = Color.white;
        if (showMappedVertices && mappedVertices != null) {
            for (int i = (int) showMin; i <= showMax; i++) {
                if (mirrorHits[i] == origin) continue;
                Gizmos.DrawSphere(mappedVertices[i], 0.1f);
            }
        }
    }
}
