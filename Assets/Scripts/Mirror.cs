using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class Mirror : MonoBehaviour {
    protected MeshFilter meshFilter;
    protected virtual void Awake() {
        meshFilter = GetComponent<MeshFilter>();
    }
}
