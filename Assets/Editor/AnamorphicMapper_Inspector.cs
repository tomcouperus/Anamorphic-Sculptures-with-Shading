using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AnamorphicMapper))]
public class AnamorphicMapper_Inspector : Editor {
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        AnamorphicMapper anamorphicMapper = (AnamorphicMapper) target;

        if (GUILayout.Button("Map")) {
            anamorphicMapper.MapObject();
        }
        if (GUILayout.Button("Destroy")) {
            anamorphicMapper.globalMeshVertices = null;
            anamorphicMapper.raycastDirections = null;
        }
    }
}