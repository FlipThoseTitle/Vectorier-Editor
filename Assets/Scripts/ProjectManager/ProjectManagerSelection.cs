using UnityEditor;
using UnityEngine;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerSelection : EditorWindow
    {
        // The currently targeted project folder/name
        private string activeProjectName = "";

        // Static method to initialize and open the window while passing the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerSelection window = GetWindow<ProjectManagerSelection>("Selection");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(300, 450);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);

            // Display which project we are editing for visual confirmation
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Editing: {activeProjectName}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            // Draw the list of buttons
            DrawCategoryButton("Textures");
            DrawCategoryButton("Animations");
            DrawCategoryButton("Locations");
            DrawCategoryButton("Tricks");
            DrawCategoryButton("Gears");
            DrawCategoryButton("Models");
            DrawCategoryButton("Musics");
            DrawCategoryButton("Sounds");
            DrawCategoryButton("Videos");
            DrawCategoryButton("XML Files");
        }

        private void DrawCategoryButton(string categoryName)
        {
            if (GUILayout.Button(categoryName, GUILayout.Height(35)))
            {
                // Route to the correct window based on the button pressed
                switch (categoryName)
                {
                    case "Textures":
                        ProjectManagerTextures.ShowWindow(activeProjectName);
                        this.Close();
                        break;

                    case "Animations":
                        ProjectManagerAnimations.ShowWindow(activeProjectName);
                        this.Close();
                        break;

                    case "Locations":
                        ProjectManagerLocations.ShowWindow(activeProjectName);
                        this.Close();
                        break;

                    case "Tricks":
                        ProjectManagerTricks.ShowWindow(activeProjectName);
                        this.Close();
                        break;

                    case "Gears":
                        ProjectManagerGears.ShowWindow(activeProjectName);
                        this.Close();
                        break;

                    case "Models":
                        ProjectManagerModels.ShowWindow(activeProjectName);
                        this.Close();
                        break;

                    case "Musics":
                        ProjectManagerMusics.ShowWindow(activeProjectName);
                        this.Close();
                        break;

                    case "Sounds":
                        ProjectManagerSounds.ShowWindow(activeProjectName);
                        this.Close();
                        break;

                    case "Videos":
                        ProjectManagerVideos.ShowWindow(activeProjectName);
                        this.Close();
                        break;

                    case "XML Files":
                        ProjectManagerXML.ShowWindow(activeProjectName);
                        this.Close();
                        break;
                }
            }
            GUILayout.Space(5);
        }
    }
}