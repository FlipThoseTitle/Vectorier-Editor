using UnityEditor;
using UnityEngine;
using Vectorier.Component;
using Vectorier.Element;
using Vectorier.EditorScript.Tools;

namespace Vectorier.EditorScript
{
    public static class ContextMenu
    {
        private const string MenuRoot = "GameObject/Vectorier/";
        private const int PriorityRoot = -50;

        private static void CreateAndSelect(System.Func<Transform, GameObject> factory, string undoName)
        {
            Transform parent = GetSafeParent(Selection.activeTransform);
            GameObject go = factory(parent);
            if (go == null) return;

            Undo.RegisterCreatedObjectUndo(go, undoName);

            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null && sv.camera != null)
            {
                Camera cam = sv.camera;

                float distance = cam.orthographic ? Mathf.Abs(cam.transform.position.z) : 10f;
                Vector3 worldCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));

                Vector3 finalPos = go.transform.position;
                finalPos.x = worldCenter.x;
                finalPos.y = worldCenter.y;

                // world position works whether it's parented or not
                go.transform.position = finalPos;
            }

            Selection.activeGameObject = go;
        }

        // Anything created inside a generated platform would get wiped on its next rebuild,
        // so bump it up next to the platform instead
        private static Transform GetSafeParent(Transform selected)
        {
            if (selected == null)
                return null;

            PlatformComponent host = selected.GetComponentInParent<PlatformComponent>(true);
            return host != null ? host.transform.parent : selected;
        }

        private static bool IsValid() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // -------- CAMERA -------- //
        [MenuItem(MenuRoot + "Camera", false, PriorityRoot)]
        private static void AddCamera()
            => CreateAndSelect(CameraElement.Create, "Vectorier Add Camera");

        [MenuItem(MenuRoot + "Camera", true)]
        private static bool AddCamera_Validate() => IsValid();

        // -------- ITEM -------- //
        [MenuItem(MenuRoot + "Item/Bonus", false, PriorityRoot + 20)]
        private static void AddBonus()
            => CreateAndSelect(p => ItemElement.Create(ItemComponent.ItemType.Bonus, p), "Vectorier Add Bonus");

        [MenuItem(MenuRoot + "Item/Bonus", true)]
        private static bool AddBonus_Validate() => IsValid();

        [MenuItem(MenuRoot + "Item/Coin", false, PriorityRoot + 21)]
        private static void AddCoin()
            => CreateAndSelect(p => ItemElement.Create(ItemComponent.ItemType.Coin, p), "Vectorier Add Coin");

        [MenuItem(MenuRoot + "Item/Coin", true)]
        private static bool AddCoin_Validate() => IsValid();

        // -------- SPAWN -------- //
        [MenuItem(MenuRoot + "Spawn", false, PriorityRoot + 40)]
        private static void AddSpawn()
            => CreateAndSelect(SpawnElement.Create, "Vectorier Add Spawn");

        [MenuItem(MenuRoot + "Spawn", true)]
        private static bool AddSpawn_Validate() => IsValid();

        // -------- TRIGGER & AREA -------- //
        [MenuItem(MenuRoot + "Trigger", false, PriorityRoot + 60)]
        private static void AddTrigger()
            => CreateAndSelect(TriggerElement.Create, "Vectorier Add Trigger");

        [MenuItem(MenuRoot + "Trigger", true)]
        private static bool AddTrigger_Validate() => IsValid();

        [MenuItem(MenuRoot + "Area", false, PriorityRoot + 61)]
        private static void AddArea()
            => CreateAndSelect(AreaElement.Create, "Vectorier Add Area");

        [MenuItem(MenuRoot + "Area", true)]
        private static bool AddArea_Validate() => IsValid();

        // -------- PLATFORM & TRAPEZOID -------- //
        [MenuItem(MenuRoot + "Platform/Rough", false, PriorityRoot + 80)]
        private static void AddPlatformRough()
            => CreateAndSelect(p => CreatePlatform(PlatformComponent.SurfaceType.Rough, p), "Vectorier Add Rough Platform");

        [MenuItem(MenuRoot + "Platform/Rough", true)]
        private static bool AddPlatformRough_Validate() => IsValid();

        [MenuItem(MenuRoot + "Platform/Smooth", false, PriorityRoot + 81)]
        private static void AddPlatformSmooth()
            => CreateAndSelect(p => CreatePlatform(PlatformComponent.SurfaceType.Smooth, p), "Vectorier Add Smooth Platform");

        [MenuItem(MenuRoot + "Platform/Smooth", true)]
        private static bool AddPlatformSmooth_Validate() => IsValid();

        [MenuItem(MenuRoot + "Platform/Block", false, PriorityRoot + 82)]
        private static void AddPlatformBlock()
            => CreateAndSelect(p => CreatePlatform(PlatformComponent.SurfaceType.Block, p), "Vectorier Add Block Platform");

        [MenuItem(MenuRoot + "Platform/Block", true)]
        private static bool AddPlatformBlock_Validate() => IsValid();

        [MenuItem(MenuRoot + "Trapezoid/Type1", false, PriorityRoot + 83)]
        private static void AddTrapezoidType1()
            => CreateAndSelect(p => TrapezoidElement.Create(TrapezoidComponent.TrapezoidType.Type1, p), "Vectorier Add Trapezoid Type 1");

        [MenuItem(MenuRoot + "Trapezoid/Type1", true)]
        private static bool AddTrapezoidType1_Validate() => IsValid();

        [MenuItem(MenuRoot + "Trapezoid/Type2", false, PriorityRoot + 84)]
        private static void AddTrapezoidType2()
            => CreateAndSelect(p => TrapezoidElement.Create(TrapezoidComponent.TrapezoidType.Type2, p), "Vectorier Add Trapezoid Type 2");

        [MenuItem(MenuRoot + "Trapezoid/Type2", true)]
        private static bool AddTrapezoidType2_Validate() => IsValid();

        private static GameObject CreatePlatform(PlatformComponent.SurfaceType type, Transform parent)
        {
            GameObject platformObject = PlatformElement.Create(parent, type);

            // Build right away so it shows up textured. Children move with the platform,
            // so CreateAndSelect repositioning it afterwards is fine
            PlatformSurfaceBuilder.Build(platformObject.GetComponent<PlatformComponent>(), recordUndo: false);

            return platformObject;
        }

        // -------- COMMENT -------- //
        [MenuItem(MenuRoot + "Comment", false, PriorityRoot + 100)]
        private static void AddComment()
            => CreateAndSelect(CreateCommentGameObject, "Vectorier Add Comment");

        [MenuItem(MenuRoot + "Comment", true)]
        private static bool AddComment_Validate() => IsValid();

        private static GameObject CreateCommentGameObject(Transform parent)
        {
            GameObject go = new GameObject("New Comment");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            go.tag = "Comment";

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.color = Color.green;
            string assetPath = "Assets/Resources/Images/Editor/Trigger/trigger.png";
            Sprite loadedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

            if (loadedSprite != null)
            {
                renderer.sprite = loadedSprite;
            }
            else
            {
                Texture2D loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (loadedTex != null)
                {
                    renderer.sprite = Sprite.Create(loadedTex, new Rect(0, 0, loadedTex.width, loadedTex.height), new Vector2(0.5f, 0.5f), 100f);
                }
                else
                {
                    Debug.LogWarning($"[ContextMenu] Could not find texture asset at path: {assetPath}. Make sure the file exists and the path is exact.");
                }
            }

            return go;
        }
    }
}