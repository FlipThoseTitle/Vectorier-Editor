using UnityEditor;
using UnityEngine;
using Vectorier.Component;
using Vectorier.Element;

namespace Vectorier.EditorScript
{
    public static class ContextMenu
    {
        private const string MenuRoot = "GameObject/Vectorier/";
        private const int PriorityRoot = -50;

        private static void CreateAndSelect(System.Func<Transform, GameObject> factory, string undoName)
        {
            Transform parent = Selection.activeTransform;
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

                if (parent != null)
                    go.transform.localPosition = parent.InverseTransformPoint(finalPos);
                else
                    go.transform.position = finalPos;
            }

            Selection.activeGameObject = go;
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
        [MenuItem(MenuRoot + "Platform", false, PriorityRoot + 80)]
        private static void AddPlatform()
            => CreateAndSelect(PlatformElement.Create, "Vectorier Add Platform");

        [MenuItem(MenuRoot + "Platform", true)]
        private static bool AddPlatform_Validate() => IsValid();

        [MenuItem(MenuRoot + "Trapezoid/Type1", false, PriorityRoot + 81)]
        private static void AddTrapezoidType1()
            => CreateAndSelect(p => TrapezoidElement.Create(TrapezoidComponent.TrapezoidType.Type1, p), "Vectorier Add Trapezoid Type 1");

        [MenuItem(MenuRoot + "Trapezoid/Type1", true)]
        private static bool AddTrapezoidType1_Validate() => IsValid();

        [MenuItem(MenuRoot + "Trapezoid/Type2", false, PriorityRoot + 82)]
        private static void AddTrapezoidType2()
            => CreateAndSelect(p => TrapezoidElement.Create(TrapezoidComponent.TrapezoidType.Type2, p), "Vectorier Add Trapezoid Type 2");

        [MenuItem(MenuRoot + "Trapezoid/Type2", true)]
        private static bool AddTrapezoidType2_Validate() => IsValid();

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