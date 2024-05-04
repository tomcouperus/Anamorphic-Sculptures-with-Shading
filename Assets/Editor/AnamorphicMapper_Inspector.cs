using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AnamorphicMapper))]
public class AnamorphicMapper_Inspector : Editor {

    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        AnamorphicMapper anamorphicMapper = (AnamorphicMapper) target;

        EditorGUILayout.MinMaxSlider(ref anamorphicMapper.showMin, ref anamorphicMapper.showMax, anamorphicMapper.ShowMinLimit, anamorphicMapper.ShowMaxLimit);

        if (GUILayout.Button("Map")) {
            anamorphicMapper.MapObject();
        }
        if (anamorphicMapper.Status == AnamorphicMapper.MappingStatus.Mapped) {
            if (GUILayout.Button("Optimize")) {
                anamorphicMapper.Optimize();
            }
        }
        if (anamorphicMapper.Status != AnamorphicMapper.MappingStatus.None) {
            if (GUILayout.Button("Destroy")) {
                anamorphicMapper.Clear();
            }
        }
    }
}