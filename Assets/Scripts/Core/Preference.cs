using UnityEngine;
using UnityEditor;
using static UnityEditor.EditorGUILayout;

namespace Vectorier.Core.Preferences
{
    public class Preference : EditorWindow
    {
        // ================= EDITOR_PREFS KEY ================= //

        private const string KEY_SHOW_OUTLINE = "Vectorier_ShowOutline";
        private const string KEY_SHOW_PLATFORM_OUTLINE = "Vectorier_ShowPlatformOutline";
        private const string KEY_SHOW_TRIGGER_TEXT = "Vectorier_ShowTriggerText";
        private const string KEY_SHOW_AREA_TEXT = "Vectorier_ShowAreaText";
        private const string KEY_TEXT_ANCHOR = "Vectorier_TextAnchor";
        private const string KEY_RENAME_DUPLICATE = "Vectorier_RenameDuplicate";

        // ================= CACHED VALUES ================= //

        private bool showOutline;
        private bool showPlatformOutline;
        private bool showTriggerText;
        private bool showAreaText;
        private TextAnchor textAnchor;
        private bool renameDuplicate;

        // ================= MAIN ================= //

        [MenuItem("Vectorier/Preferences...", false, 31)]
        private static void OpenWindow()
        {
            var window = GetWindow<Preference>("Preferences");
            window.minSize = new Vector2(360, 240);
            window.LoadPrefs();
        }

        private void OnEnable()
        {
            LoadPrefs();
        }

        private void OnGUI()
        {
            var subHeaderStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            LabelField("Scene", subHeaderStyle);
            Space(3);

            LabelField("Trigger & Area", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            
            showOutline = Toggle( new GUIContent( "Show Outline", "Displays an outline around Trigger objects in the Scene view." ), showOutline );
            showPlatformOutline = Toggle( new GUIContent( "Show Platform Outline", "Displays an outline around Platform objects in the Scene view." ), showPlatformOutline );
            showTriggerText = Toggle( new GUIContent( "Show Trigger Text", "Displays text labels for Trigger objects in the Scene view." ), showTriggerText );
            showAreaText = Toggle( new GUIContent( "Show Area Text", "Displays text labels for Area objects in the Scene view." ), showAreaText );
            textAnchor = (TextAnchor)EnumPopup( new GUIContent( "Text Anchor", "Determines the anchor point for text labels." ), textAnchor );

            Space(8);

            LabelField("Hierarchy", EditorStyles.boldLabel);

            renameDuplicate = Toggle( new GUIContent( "Rename Duplicate", "Renames duplicate objects in the Hierarchy when the user duplicates them.\nEx: Cube (1) turns into Cube." ), renameDuplicate );

            if (EditorGUI.EndChangeCheck())
            {
                SavePrefs();
                SceneView.RepaintAll();
            }
        }

        private void LoadPrefs()
        {
            showOutline = EditorPrefs.GetBool(KEY_SHOW_OUTLINE, true);
            showPlatformOutline = EditorPrefs.GetBool(KEY_SHOW_PLATFORM_OUTLINE, true);
            showTriggerText = EditorPrefs.GetBool(KEY_SHOW_TRIGGER_TEXT, true);
            showAreaText = EditorPrefs.GetBool(KEY_SHOW_AREA_TEXT, true);
            textAnchor = (TextAnchor)EditorPrefs.GetInt(KEY_TEXT_ANCHOR, (int)TextAnchor.UpperLeft);
            renameDuplicate = EditorPrefs.GetBool(KEY_RENAME_DUPLICATE, true);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetBool(KEY_SHOW_OUTLINE, showOutline);
            EditorPrefs.SetBool(KEY_SHOW_PLATFORM_OUTLINE, showPlatformOutline);
            EditorPrefs.SetBool(KEY_SHOW_TRIGGER_TEXT, showTriggerText);
            EditorPrefs.SetBool(KEY_SHOW_AREA_TEXT, showAreaText);
            EditorPrefs.SetInt(KEY_TEXT_ANCHOR, (int)textAnchor);
            EditorPrefs.SetBool(KEY_RENAME_DUPLICATE, renameDuplicate);
        }
    }
}