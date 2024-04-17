using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AnamorphicMapper : MonoBehaviour {
    [SerializeField]
    private Transform viewer;
    [SerializeField]
    private Mirror mirror;
    [SerializeField]
    private GameObject anamorphObject;

    public Vector3[] globalMeshVertices;
    public Vector3[] raycastDirections;

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
    }

    private void OnDrawGizmosSelected() {
        if (globalMeshVertices != null) {
            foreach (Vector3 v in globalMeshVertices) {
                Gizmos.DrawSphere(v, 0.1f);
            }
        }
        if (raycastDirections != null && viewer != null) {
            foreach (Vector3 d in raycastDirections) {
                Gizmos.DrawLine(viewer.position, viewer.position + d);
            }
        }
    }
}
