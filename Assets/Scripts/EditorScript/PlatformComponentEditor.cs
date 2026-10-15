using UnityEditor;
using UnityEngine;
using Vectorier.Component;
using Vectorier.EditorScript.Tools;

namespace Vectorier.EditorScript
{
    [CustomEditor(typeof(PlatformComponent))]
    [CanEditMultipleObjects]
    public class PlatformComponentEditor : Editor
    {
        private SerializedProperty typeProperty;
        private SerializedProperty floorProperty;
        private SerializedProperty leftWallProperty;
        private SerializedProperty rightWallProperty;
        private SerializedProperty seedProperty;

        private void OnEnable()
        {
            typeProperty = serializedObject.FindProperty(nameof(PlatformComponent.Type));
            floorProperty = serializedObject.FindProperty(nameof(PlatformComponent.Floor));
            leftWallProperty = serializedObject.FindProperty(nameof(PlatformComponent.LeftWall));
            rightWallProperty = serializedObject.FindProperty(nameof(PlatformComponent.RightWall));
            seedProperty = serializedObject.FindProperty(nameof(PlatformComponent.Seed));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (!serializedObject.isEditingMultipleObjects && !PlatformSurfaceBuilder.IsValidPlatform(((UnityEngine.Component)target).gameObject))
            {
                EditorGUILayout.HelpBox($"Needs the \"{PlatformSurfaceBuilder.PLATFORM_TAG}\" tag and a SpriteRenderer with a sprite, otherwise nothing gets generated.", MessageType.Warning);
            }

            EditorGUILayout.PropertyField(typeProperty);

            bool mixedType = typeProperty.hasMultipleDifferentValues;
            PlatformComponent.SurfaceType type = (PlatformComponent.SurfaceType)typeProperty.enumValueIndex;
            bool reroll = false;

            // Block is only v_black, none of this applies to it
            if (mixedType || type != PlatformComponent.SurfaceType.Block)
            {
                // Smooth never makes floor tiles, so grey it out instead of having a toggle that does nothing
                using (new EditorGUI.DisabledScope(!mixedType && type == PlatformComponent.SurfaceType.Smooth))
                    EditorGUILayout.PropertyField(floorProperty);

                EditorGUILayout.PropertyField(leftWallProperty);
                EditorGUILayout.PropertyField(rightWallProperty);

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(seedProperty);
                    reroll = GUILayout.Button("Reroll", GUILayout.Width(60f));
                }
            }

            bool changed = serializedObject.ApplyModifiedProperties();

            if (reroll)
            {
                // separate seed per platform, otherwise multi-select gives them all the same layout
                Undo.RecordObjects(targets, "Reroll Platform Variants");
                foreach (PlatformComponent platform in targets)
                {
                    platform.Seed = Random.Range(1, int.MaxValue);
                    EditorUtility.SetDirty(platform);
                }
                changed = true;
            }

            // watcher would catch these anyway but no reason to wait for it
            if (changed)
            {
                foreach (PlatformComponent platform in targets)
                    PlatformSurfaceWatcher.Request(platform);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Surface"))
            {
                Undo.SetCurrentGroupName("Rebuild Platform Surface");
                int group = Undo.GetCurrentGroup();

                foreach (PlatformComponent platform in targets)
                    PlatformSurfaceBuilder.Build(platform, recordUndo: true);

                Undo.CollapseUndoOperations(group);
            }
        }
    }
}