using System;
using UnityEditor;
using UnityEngine;

namespace Vectorier.EditorScript.Tools
{
    public class Geometrize : EditorWindow
    {
        // ================= UI STATE ================= //
        private Vector2 scroll;
        private string jsonText = string.Empty;

        // ================= CONSTANTS ================= //
        private const string ParentName = "GeometryImage";
        private const string ChildName = "v_white";
        private const string ChildTag = "Image";
        private const string SpriteResourcePath = "Images/Editor/Misc/rect";

        // ================= MENU ================= //
        [MenuItem("Vectorier/Tools/Geometrize", false, 35)]
        public static void OpenWindow()
        {
            GetWindow<Geometrize>("Geometrize");
        }

        // ================= UI ================= //
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Geometrize JSON to Sprites", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Paste Geometrize JSON");
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(240));
            jsonText = EditorGUILayout.TextArea(jsonText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(jsonText)))
            {
                if (GUILayout.Button("Convert"))
                {
                    ConvertJsonToSprites();
                }
            }
        }

        // ================= CONVERSION ================= //
        private void ConvertJsonToSprites()
        {
            // Load sprite
            Sprite rectSprite = Resources.Load<Sprite>(SpriteResourcePath);
            if (rectSprite == null)
            {
                Debug.LogError($"[Geometrize] Could not load sprite from Resources at: '{SpriteResourcePath}'.");
                return;
            }

            // Parse JSON
            GeometrizeRoot root;
            try
            {
                root = JsonUtility.FromJson<GeometrizeRoot>(jsonText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Geometrize] JSON parse error: {ex.Message}");
                return;
            }

            if (root == null || root.shapes == null || root.shapes.Length == 0)
            {
                Debug.LogError("[Geometrize] No shapes found. Ensure the JSON has a 'shapes' array.");
                return;
            }

            GameObject parent = new GameObject(ParentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create GeometryImage");
            parent.transform.position = Vector3.zero;

            Vector2 spriteSize = rectSprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                Debug.LogError("[Geometrize] Sprite bounds size is invalid.");
                return;
            }

            // Convert shapes in order
            int created = 0;
            for (int i = 0; i < root.shapes.Length; i++)
            {
                GeometrizeShape shape = root.shapes[i];
                if (shape == null)
                    continue;

                // data=[x1,y1,x2,y2], color=[r,g,b,a]
                if (shape.type != 1 || shape.data == null || shape.data.Length < 4 || shape.color == null || shape.color.Length < 4)
                    continue;

                int x1 = shape.data[0];
                int y1 = shape.data[1];
                int x2 = shape.data[2];
                int y2 = shape.data[3];

                int left = Mathf.Min(x1, x2);
                int top = Mathf.Min(y1, y2);
                int right = Mathf.Max(x1, x2);
                int bottom = Mathf.Max(y1, y2);

                int widthPx = Mathf.Max(0, right - left);
                int heightPx = Mathf.Max(0, bottom - top);

                if (widthPx == 0 || heightPx == 0)
                    continue;

                Color color = new Color(
                    Mathf.Clamp01(shape.color[0] / 255f),
                    Mathf.Clamp01(shape.color[1] / 255f),
                    Mathf.Clamp01(shape.color[2] / 255f),
                    Mathf.Clamp01(shape.color[3] / 255f)
                );

                GameObject gameObject = new GameObject(ChildName);
                Undo.RegisterCreatedObjectUndo(gameObject, "Create Geometrize Rect");
                gameObject.transform.SetParent(parent.transform, false);
                gameObject.transform.localPosition = new Vector3(left, -top, 0f);
                gameObject.transform.localScale = new Vector3(widthPx / spriteSize.x, heightPx / spriteSize.y, 1f);

                SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = rectSprite;
                spriteRenderer.color = color;

                // keep draw order matching JSON order
                spriteRenderer.sortingOrder = i;

                // Tag
                TrySetTag(gameObject, ChildTag);

                created++;
            }

            if (created == 0)
                Debug.LogWarning("[Geometrize] No valid rectangle shapes were created. Check that type==1 entries exist.");
        }

        private static void TrySetTag(GameObject gameObject, string tagName)
        {
            try
            {
                gameObject.tag = tagName;
            }
            catch
            {
                Debug.LogWarning($"[Geometrize] Tag '{tagName}' does not exist. Create it in Tag Manager if you want tagging.");
            }
        }

        // ================= JSON MODELS ================= //
        [Serializable]
        private class GeometrizeRoot
        {
            public GeometrizeShape[] shapes;
        }

        [Serializable]
        private class GeometrizeShape
        {
            public int type;
            public int[] data;     // [x1,y1,x2,y2]
            public int[] color;    // [r,g,b,a]
            public float score;    // not used for rendering
        }
    }
}
