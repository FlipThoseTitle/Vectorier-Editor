using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vectorier.Core
{
    public class SnailRunner : EditorWindow
    {
        // ================= OPTIONS ================= //

        private string levelName = "";
        private bool noUI = false;
        private bool hunterMode = false;
        private bool showPlatforms = false;
        private bool showTriggers = false;
        private bool showAreas = false;
        private bool showDetectors = false;

        // ================= MAIN ================= //

        [MenuItem("Vectorier/Play Level", false, 2)]
        public static void ShowWindow()
        {
            GetWindow<SnailRunner>("Play Level");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Snail Runner", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawXmlNameFieldWithBrowse("Level Name", ref levelName);
            noUI = EditorGUILayout.Toggle(new GUIContent("Disable Debug UI", "Disable the debug UI when playing the level."), noUI);
            hunterMode = EditorGUILayout.Toggle(new GUIContent("Hunter Mode", "Play the level in Hunter Mode."), hunterMode);
            showPlatforms = EditorGUILayout.Toggle(new GUIContent("Show Platforms", "Show platforms bound in the level."), showPlatforms);
            showTriggers = EditorGUILayout.Toggle(new GUIContent("Show Triggers", "Show triggers bound in the level."), showTriggers);
            showAreas = EditorGUILayout.Toggle(new GUIContent("Show Areas", "Show areas bound in the level."), showAreas);
            showDetectors = EditorGUILayout.Toggle(new GUIContent("Show Detectors", "Show detectors for the model's character."), showDetectors);

            EditorGUILayout.Space(10);

            if (GUILayout.Button(new GUIContent("Play", "Play the selected level."), GUILayout.Height(60)))
            {
                TryRunLevel();
            }
        }

        private void DrawXmlNameFieldWithBrowse(string label, ref string xmlName)
        {
            EditorGUILayout.BeginHorizontal();

            xmlName = EditorGUILayout.TextField(label, xmlName);

            if (GUILayout.Button(new GUIContent("...", "Browse"), GUILayout.Width(28)))
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string startPath = Path.Combine(projectRoot, "Snail Runner/Vector_Data/StreamingAssets/xmlroot/levels");
                
                if (!Directory.Exists(startPath))
                    startPath = projectRoot;

                string picked = EditorUtility.OpenFilePanel($"Select {label}", startPath, "xml");

                if (!string.IsNullOrEmpty(picked))
                {
                    xmlName = Path.GetFileNameWithoutExtension(picked);
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void TryRunLevel()
        {
            if (string.IsNullOrWhiteSpace(levelName))
            {
                EditorUtility.DisplayDialog("Warning", "Level Name cannot be empty.", "OK");
                return;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string xmlDir = Path.Combine(projectRoot, "Snail Runner/Vector_Data/StreamingAssets/xmlroot/levels");
            string xmlPath = Path.Combine(xmlDir, levelName + ".xml");

            if (!File.Exists(xmlPath))
            {
                EditorUtility.DisplayDialog("Error", $"Level XML not found: {xmlPath}", "OK");
                return;
            }

            KillExistingVectorProcess();
            RunVector(levelName);
        }

        public void SetLevelAndPlay(string newLevelName)
        {
            levelName = newLevelName;
            TryRunLevel();
        }

        private void KillExistingVectorProcess()
        {
            Process[] processes = Process.GetProcessesByName("Vector");
            foreach (Process p in processes)
            {
                try { p.Kill(); }
                catch { /* ignored */ }
            }
        }

        private void RunVector(string level)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string baseDir = Path.Combine(projectRoot, "Snail Runner");
            string exePath = Path.Combine(baseDir, "Vector.exe");

            if (!File.Exists(exePath))
            {
                EditorUtility.DisplayDialog("Error", $"Vector.exe not found at:\n{exePath}", "OK");
                return;
            }

            // Build command arguments
            string args = $"-level {level}";
            if (noUI) args += " -noui";
            if (hunterMode) args += " -huntermode";
            if (showPlatforms) args += " -showplatforms";
            if (showTriggers) args += " -showtriggers";
            if (showAreas) args += " -showareas";
            if (showDetectors) args += " -showdetectors";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                WorkingDirectory = baseDir,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            try
            {
                Process.Start(startInfo);
                UnityEngine.Debug.Log("[SnailRunner] Starting Snail Runner...");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Execution Error", ex.Message, "OK");
            }
        }
    }
}
