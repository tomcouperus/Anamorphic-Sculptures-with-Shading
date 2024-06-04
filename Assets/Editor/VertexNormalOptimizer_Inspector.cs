using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(VertexNormalOptimizer))]
public class VertexNormalOptimizer_Inspector : Editor {
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        VertexNormalOptimizer vertexNormalOptimizer = (VertexNormalOptimizer) target;

        if (GUILayout.Button("Initialize")) {
            vertexNormalOptimizer.Initialize();
        }

        if (vertexNormalOptimizer.Status == VertexNormalOptimizer.OptimizerStatus.Initialized) {
            if (GUILayout.Button("Deform")) {
                vertexNormalOptimizer.Deform();
            }
        }
        if (vertexNormalOptimizer.Status == VertexNormalOptimizer.OptimizerStatus.Deformed || vertexNormalOptimizer.Status == VertexNormalOptimizer.OptimizerStatus.OptimizingManual) {
            if (GUILayout.Button("Optimize")) {
                vertexNormalOptimizer.Optimize();
            }
        }
        if (GUILayout.Button("Reset")) {
            vertexNormalOptimizer.Reset();
        }

    }
}
