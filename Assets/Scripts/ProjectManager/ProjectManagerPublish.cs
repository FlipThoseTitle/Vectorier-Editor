using UnityEditor;
using UnityEngine;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerPublish : EditorWindow
    {
        // State variables
        private string activeProjectName = "";
        private string publishLocation = "";
        
        // Dropdown options
        private readonly string[] gameVersions = new string[] { "Steam", "Unity", "Steam & Unity" };
        private int selectedVersionIndex = 0;

        // Static method to initialize and open the window while passing the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerPublish window = GetWindow<ProjectManagerPublish>("Publish Project");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(450, 200);
            window.Show();
        }

        private void OnGUI()
        {
            // If the user clicks on an empty space, remove focus from text fields
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
                Repaint();
            }

            GUILayout.Space(15);

            // --- Header ---
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Publishing: {activeProjectName}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // --- Location Directory ---
            DrawDirectoryFieldWithBrowse("Location", ref publishLocation);

            GUILayout.Space(10);

            // --- Game Version Dropdown ---
            GUILayout.BeginHorizontal();
            GUILayout.Label("Game Version:", GUILayout.Width(100)); // Fixed width to match standard layout
            
            // Render the dropdown using our string array
            selectedVersionIndex = EditorGUILayout.Popup(selectedVersionIndex, gameVersions);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // --- Publish Button ---
            if (GUILayout.Button("Publish", GUILayout.Height(60)))
            {
                // This is where the actual publishing logic will go later
                Debug.Log($"Publishing '{activeProjectName}' to '{publishLocation}' for {gameVersions[selectedVersionIndex]}");
            }
        }

        // --- Helper Methods ---

        private void DrawDirectoryFieldWithBrowse(string label, ref string directoryPath)
        {
            EditorGUILayout.BeginHorizontal();

            // The standard Unity text field natively clips text that gets too long
            directoryPath = EditorGUILayout.TextField(label, directoryPath);

            // Browse button ("...")
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string startPath = string.IsNullOrEmpty(directoryPath) ? Application.dataPath : directoryPath;
                string picked = EditorUtility.OpenFolderPanel($"Select {label}", startPath, "");

                if (!string.IsNullOrEmpty(picked))
                {
                    directoryPath = picked;
                    
                    // Unselect the text field to force the UI to visually update
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}