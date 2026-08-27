using UnityEditor;
using UnityEngine;

namespace Vectorier.EditorScript
{
    [InitializeOnLoad]
    public static class NoParentheses
    {
        static NoParentheses()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private static void OnHierarchyChanged()
        {
            // Don't rename duplicate objects if the option is disabled
            if (!ShouldRenameDuplicate())
                return;

            // Cache selection to avoid multiple native calls
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0) return;

            foreach (GameObject go in selectedObjects)
            {
                string name = go.name;

                // Fast rejection: If it doesn't end with ')', skip immediately
                if (!name.EndsWith(")")) continue;

                // Find the index of " ("
                int lastSpaceOpenParen = name.LastIndexOf(" (");
                if (lastSpaceOpenParen == -1) continue;

                // Verify that all characters between " (" and ")" are digits
                bool isDigitOnly = true;
                for (int i = lastSpaceOpenParen + 2; i < name.Length - 1; i++)
                {
                    if (!char.IsDigit(name[i]))
                    {
                        isDigitOnly = false;
                        break;
                    }
                }

                if (isDigitOnly)
                {
                    string newName = name.Substring(0, lastSpaceOpenParen);

                    // Safety check to prevent infinite loops
                    if (go.name != newName)
                    {
                        Undo.RecordObject(go, "Remove Clone Suffix");
                        go.name = newName;
                    }
                }
            }
        }

        private static bool ShouldRenameDuplicate()
        {
            return EditorPrefs.GetBool("Vectorier_RenameDuplicate", true);
        }
    }
}