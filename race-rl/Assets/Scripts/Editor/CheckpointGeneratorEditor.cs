#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;     // ← DODAJ TO!


[CustomEditor(typeof(CheckpointGenerator))]
public class CheckpointGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate Checkpoints"))
        {
            ((CheckpointGenerator)target).Generate();
        }
        if (GUILayout.Button("Generate Walls"))
        {
            ((CheckpointGenerator)target).GenerateWalls();
        }
    }
}
#endif
