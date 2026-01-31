using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using System.Collections.Generic;
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

        // ================= CACHED VALUES ================= //

        private bool showOutline;
        private bool showPlatformOutline;
        private bool showTriggerText;
        private bool showAreaText;

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
            showOutline = Toggle("Show Outline", showOutline);
            showPlatformOutline = Toggle("Show Platform Outline", showPlatformOutline);
            showTriggerText = Toggle("Show Trigger Text", showTriggerText);
            showAreaText = Toggle("Show Area Text", showAreaText);

            if (EditorGUI.EndChangeCheck())
            {
                SavePrefs();
            }
        }

        private void LoadPrefs()
        {
            showOutline = EditorPrefs.GetBool(KEY_SHOW_OUTLINE, true);
            showPlatformOutline = EditorPrefs.GetBool(KEY_SHOW_PLATFORM_OUTLINE, false);
            showTriggerText = EditorPrefs.GetBool(KEY_SHOW_TRIGGER_TEXT, true);
            showAreaText = EditorPrefs.GetBool(KEY_SHOW_AREA_TEXT, false);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetBool(KEY_SHOW_OUTLINE, showOutline);
            EditorPrefs.SetBool(KEY_SHOW_PLATFORM_OUTLINE, showPlatformOutline);
            EditorPrefs.SetBool(KEY_SHOW_TRIGGER_TEXT, showTriggerText);
            EditorPrefs.SetBool(KEY_SHOW_AREA_TEXT, showAreaText);
        }
    }
}