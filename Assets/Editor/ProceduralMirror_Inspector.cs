using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Runtime;

[CustomEditor(typeof(ProceduralMirror), true)]
public class ProceduralMirror_Inspector : Editor {
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        ProceduralMirror proceduralMirror = (ProceduralMirror) target;

        if (GUILayout.Button("Create Mirror")) {
            proceduralMirror.CreateMirror();
        }
    }
}