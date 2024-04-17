using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProceduralMirror : Mirror {

    protected override void Awake() {
        base.Awake();
        CreateMirror();
    }

    public void CreateMirror() {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.sharedMesh = GenerateMeshData().CreateMesh();
    }

    protected abstract MeshData GenerateMeshData();
}
