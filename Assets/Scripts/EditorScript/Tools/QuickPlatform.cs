using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Vectorier.EditorScript.Tools
{
    public static class QuickPlatform
    {
        private const string PLATFORM_SPRITE_PATH = "Images/Editor/Collision/platform";

        [MenuItem("Vectorier/Tools/Quick Actions/Build Platform from Sprite", false, 55)]
        private static void BuildPlatformFromSprite()
        {
            if (Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Quick Platform", "No GameObject selected.\n\nClick any object in the Scene to build a platform from it.", "OK");
                SceneView.duringSceneGui += WaitForSceneClick;
                return;
            }

            TryBuildFromSelection(Selection.gameObjects);
        }

        private static void WaitForSceneClick(SceneView sceneView)
        {
            Event e = Event.current;

            if (e.type != EventType.MouseDown || e.button != 0)
                return;

            GameObject picked = HandleUtility.PickGameObject(e.mousePosition, false);
            if (picked == null)
                return;

            SceneView.duringSceneGui -= WaitForSceneClick;
            Selection.activeGameObject = picked;

            TryBuildFromSelection(new[] { picked });
            e.Use();
        }

        private static void TryBuildFromSelection(GameObject[] selection)
        {
            List<SpriteRenderer> renderers = new List<SpriteRenderer>();

            foreach (GameObject gameObject in selection)
            {
                SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                    renderers.Add(spriteRenderer);
            }

            if (renderers.Count == 0)
            {
                EditorUtility.DisplayDialog("Quick Platform", "Selected object(s) do not contain valid SpriteRenderers.", "OK");
                return;
            }

            Bounds combinedBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++)
                combinedBounds.Encapsulate(renderers[i].bounds);

            CreatePlatformFromBounds(combinedBounds);
        }

        private static void CreatePlatformFromBounds(Bounds bounds)
        {
            Sprite platformSprite = LoadPlatformSprite();
            if (platformSprite == null)
                return;

            GameObject platform = CreatePlatformObject(platformSprite);

            Vector2 targetSize = bounds.size;
            Vector2 spriteSize = platformSprite.bounds.size;

            platform.transform.localScale = new Vector3(targetSize.x / spriteSize.x, targetSize.y / spriteSize.y, 1f);

            Vector3 topLeftPosition = new Vector3(bounds.min.x, bounds.max.y, bounds.center.z);

            platform.transform.position = topLeftPosition;

            Selection.activeGameObject = platform;
        }

        private static Sprite LoadPlatformSprite()
        {
            Sprite sprite = Resources.Load<Sprite>(PLATFORM_SPRITE_PATH);

            if (sprite == null)
                Debug.LogError($"QuickPlatform: Sprite not found at Resources/{PLATFORM_SPRITE_PATH}.png");

            return sprite;
        }

        private static GameObject CreatePlatformObject(Sprite sprite)
        {
            GameObject platform = new GameObject("Platform");
            Undo.RegisterCreatedObjectUndo(platform, "Create Platform");

            SpriteRenderer spriteRenderer = platform.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 0;
            spriteRenderer.sortingLayerName = "OnTop";

            try
            {
                platform.tag = "Platform";
            }
            catch
            {
                Debug.LogWarning("QuickPlatform: Tag 'Platform' does not exist. Please create it in the Tag Manager.");
            }

            return platform;
        }
    }
}
