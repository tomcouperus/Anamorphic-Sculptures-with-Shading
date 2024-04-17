using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ProceduralMirror : Mirror {

    protected override void Awake() {
        base.Awake();
        meshFilter.sharedMesh = GenerateMeshData().CreateMesh();
    }

    protected abstract MeshData GenerateMeshData();

#if UNITY_EDITOR
    private void OnValidate() {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf.sharedMesh != null) {
            DestroyImmediate(mf.sharedMesh);
        }
        mf.sharedMesh = GenerateMeshData().CreateMesh();
    }
#endif
}
