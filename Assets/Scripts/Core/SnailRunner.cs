using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vectorier.Core
{
    public class SnailRunner : EditorWindow
    {
        private string levelName = "";
        private bool noUI = false;
        private bool hunterMode = false;
        private bool showPlatforms = false;
        private bool showTriggers = false;
        private bool showAreas = false;
        private bool showDetectors = false;

        [MenuItem("Vectorier/Play Level")]
        public static void ShowWindow()
        {
            GetWindow<SnailRunner>("Play Level");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Snail Runner", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            levelName = EditorGUILayout.TextField("Level Name", levelName);
            noUI = EditorGUILayout.Toggle("Disable Debug UI", noUI);
            hunterMode = EditorGUILayout.Toggle("Hunter Mode", hunterMode);
            showPlatforms = EditorGUILayout.Toggle("Show Platforms", showPlatforms);
            showTriggers = EditorGUILayout.Toggle("Show Triggers", showTriggers);
            showAreas = EditorGUILayout.Toggle("Show Areas", showAreas);
            showDetectors = EditorGUILayout.Toggle("Show Detectors", showDetectors);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Play", GUILayout.Height(60)))
            {
                TryRunLevel();
            }
        }

        private void TryRunLevel()
        {
            if (string.IsNullOrWhiteSpace(levelName))
            {
                EditorUtility.DisplayDialog("Warning", "Level Name cannot be empty.", "OK");
                return;
            }

            string xmlDir = Path.Combine(Application.dataPath, "Snail Runner/Vector_Data/StreamingAssets/xmlroot/levels");
            string xmlPath = Path.Combine(xmlDir, levelName + ".xml");

            if (!File.Exists(xmlPath))
            {
                EditorUtility.DisplayDialog("Error", $"Level XML not found: {xmlPath}", "OK");
                return;
            }

            KillExistingVectorProcess();
            RunVector(levelName);
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
            string baseDir = Path.Combine(Application.dataPath, "Snail Runner");
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
                UnityEngine.Debug.Log("Starting Snail Runner...");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Execution Error", ex.Message, "OK");
            }
        }
    }
}
