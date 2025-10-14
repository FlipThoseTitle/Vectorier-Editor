#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Vectorier.Parallax
{
    [CustomEditor(typeof(Parallax))]
    public class ParallaxEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var handler = (Parallax)target;

            GUI.enabled = !Application.isPlaying;
            bool isActive = (bool)handler.GetType()
                .GetField("_isActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(handler);

            string label = isActive ? "Stop Parallax" : "Start Parallax";

            if (GUILayout.Button(label, GUILayout.Height(40)))
                handler.ToggleParallax();
            if (GUILayout.Button("Apply ZoomValue", GUILayout.Height(35)))
                handler.ApplyZoomValue();

            GUI.enabled = true;
        }
    }
}
#endif