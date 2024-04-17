using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshCollider))]
public abstract class ProceduralMirror : Mirror {

    protected override void Awake() {
        base.Awake();
        CreateMirror();
    }

    public void CreateMirror() {
        // TODO memory leak with undestroyed meshes?
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = GenerateMeshData().CreateMesh();
        meshFilter.sharedMesh = mesh;

        MeshCollider meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
    }

    protected abstract MeshData GenerateMeshData();
}
