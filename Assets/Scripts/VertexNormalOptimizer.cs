using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VertexNormalOptimizer : MonoBehaviour {
    [SerializeField]
    private MappableObject originalObject;
    [SerializeField]
    private Transform viewPosition;

    public enum OptimizerStatus { None, Deformed, Optimized };
    public OptimizerStatus Status { get; private set; } = OptimizerStatus.None;

    public void Deform() {
        Debug.Log("Deforming mesh");
        Status = OptimizerStatus.Deformed;
    }

    public void Optimize() {
        Debug.Log("Optimizing vertex normals");
        Status = OptimizerStatus.Optimized;
    }

    public void Reset() {
        Debug.Log("Resetting");
        Status = OptimizerStatus.None;
    }
}
