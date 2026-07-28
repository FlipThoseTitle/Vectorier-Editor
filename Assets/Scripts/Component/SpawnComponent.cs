using UnityEditor;
using UnityEngine;
using Vectorier.EditorScript.Tools;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Spawn Component")]
    public class SpawnComponent : MonoBehaviour
    {
        [Tooltip("First Parameter - Moves name\nSecond Parameter - Starting Frame\nEx. JumpOff|18")]
        public string Animation = "JumpOff|18";
    }

    [CustomEditor(typeof(SpawnComponent))]
    public class SpawnComponentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(10);

            if (GUILayout.Button("Preview Animation", GUILayout.Height(32)))
            {
                SpawnComponent spawnComponent = (SpawnComponent)target;
                MoveVisualizer.OpenAndPreviewSpawnComponent(spawnComponent);
            }
        }
    }
}