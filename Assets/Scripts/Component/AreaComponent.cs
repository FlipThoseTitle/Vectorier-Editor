using UnityEditor;
using UnityEngine;
using Vectorier.EditorScript.Tools;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Area Component")]
    public class AreaComponent : MonoBehaviour
    {
        public enum AreaType
        {
            Animation,
            Catch,
            Trick,
            Help
        }
        [Tooltip("Animation - Used to define move area, Default type even if component isn't added.\n" +
            "Catch - Use Distance, If hunter is X distance away while leading, will perform a catch upon entering area.\n" +
            "Trick - Use ItemName and Score, Ex. TRICK_FOLDFLIP\n" +
            "Help - Use Key and Description, area to display tutorial prompt.")]
        public AreaType Type = AreaType.Animation;

        [Tooltip("The minimum distance required for the hunter to catch.\nTriggerCatchFast has distance set to 0\nDefault: 300")]
        public int Distance = 300;

        [Tooltip("The trick that will be activated\nEx. TRICK_FOLDFLIP")]
        public string ItemName = "TRICK_";

        [Tooltip("Score earned from performing trick.\nDefault: 100")]
        public int Score = 100;

        [Tooltip("Key used for the tutorial prompt\nKey: Up, Down, Left, Right")]
        public string Key = "Up";

        [Tooltip("The Description")]
        public string Description;
    }

    [CustomEditor(typeof(AreaComponent))]
    public class AreaComponentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty type = serializedObject.FindProperty("Type");
            SerializedProperty distance = serializedObject.FindProperty("Distance");
            SerializedProperty itemName = serializedObject.FindProperty("ItemName");
            SerializedProperty score = serializedObject.FindProperty("Score");
            SerializedProperty key = serializedObject.FindProperty("Key");
            SerializedProperty description = serializedObject.FindProperty("Description");

            EditorGUILayout.PropertyField(type);

            switch ((AreaComponent.AreaType)type.enumValueIndex)
            {
                case AreaComponent.AreaType.Catch:
                    EditorGUILayout.PropertyField(distance);
                    break;

                case AreaComponent.AreaType.Trick:
                    EditorGUILayout.PropertyField(itemName);
                    EditorGUILayout.PropertyField(score);

                    GUILayout.Space(10);

                    if (GUILayout.Button("Preview Animation", GUILayout.Height(32)))
                    {
                        serializedObject.ApplyModifiedProperties();

                        AreaComponent areaComponent = (AreaComponent)target;
                        MoveVisualizer.OpenAndPreviewAreaComponent(areaComponent);

                        return;
                    }

                    break;

                case AreaComponent.AreaType.Help:
                    EditorGUILayout.PropertyField(key);
                    EditorGUILayout.PropertyField(description);
                    break;

                case AreaComponent.AreaType.Animation:
                default:
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}