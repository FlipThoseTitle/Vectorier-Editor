using UnityEngine;
using UnityEditor;

namespace Vectorier.EditorScript
{
    [InitializeOnLoad]
    public static class PlatformSnap
    {
        static PlatformSnap()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (Selection.transforms.Length == 0) return;

            foreach (Transform t in Selection.transforms)
            {
                if (t.gameObject.CompareTag("Platform"))
                {
                    if (t.hasChanged)
                    {
                        Vector3 currentPos = t.position;
                        
                        Vector3 roundedPos = new Vector3(
                            Mathf.Round(currentPos.x),
                            Mathf.Round(currentPos.y),
                            Mathf.Round(currentPos.z)
                        );

                        if (currentPos != roundedPos)
                        {
                            Undo.RecordObject(t, "Snap Platform Position");
                            t.position = roundedPos;
                        }
                        
                        t.hasChanged = false; 
                    }
                }
            }
        }
    }
}