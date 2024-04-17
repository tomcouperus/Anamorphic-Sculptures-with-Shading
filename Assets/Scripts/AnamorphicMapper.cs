using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    private Vector3[] globalMeshVertices;
    [SerializeField]
    private Vector3[] raycastDirections;
    [SerializeField]
    private Vector3[] raycastHits;

    [SerializeField]
    private bool showGlobalMeshVertices = false;
    [SerializeField]
    private bool showRaycastDirections = false;
    [SerializeField]
    private bool showRaycastHits = false;

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
        raycastHits = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) {
            RaycastHit[] hits = Physics.RaycastAll(origin, raycastDirections[i].normalized, 20f);
            if (hits.Length > 0) {
                raycastHits[i] = hits[0].point;
            } else {
                Debug.Log("No hit with mirror");
            }
        }
        ShowMaxLimit = vertices.Length - 1;
    }

    public void Clear() {
        globalMeshVertices = null;
        raycastDirections = null;
        raycastHits = null;
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.gray;
        if (showGlobalMeshVertices && globalMeshVertices != null) {
            for (int i = (int) showMin; i <= showMax; i++) {
                Gizmos.DrawSphere(globalMeshVertices[i], 0.1f);
            }
        }
        Gizmos.color = Color.blue;
        if (showRaycastDirections && raycastDirections != null && viewer != null) {
            for (int i = (int) showMin; i <= showMax; i++) {
                Gizmos.DrawLine(viewer.position, viewer.position + raycastDirections[i]);
            }
        }
        Gizmos.color = Color.white;
        if (showRaycastHits && raycastHits != null) {
            for (int i = (int) showMin; i <= showMax; i++) {
                Gizmos.DrawSphere(raycastHits[i], 0.1f);
            }
        }
    }
}
