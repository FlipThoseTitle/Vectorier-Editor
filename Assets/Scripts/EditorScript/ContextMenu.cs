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

        private const string SceneRoot = "CONTEXT/GameObjectToolContext/Vectorier/";
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
        [MenuItem(SceneRoot + "Camera", false, PriorityRoot)]
        private static void AddCamera()
            => CreateAndSelect(CameraElement.Create, "Vectorier Add Camera");

        [MenuItem(MenuRoot + "Camera", true)]
        [MenuItem(SceneRoot + "Camera", true)]
        private static bool AddCamera_Validate() => IsValid();

        // -------- ITEM -------- //
        [MenuItem(MenuRoot + "Item/Bonus", false, PriorityRoot + 20)]
        [MenuItem(SceneRoot + "Item/Bonus", false, PriorityRoot + 20)]
        private static void AddBonus()
            => CreateAndSelect(p => ItemElement.Create(ItemComponent.ItemType.Bonus, p), "Vectorier Add Bonus");

        [MenuItem(MenuRoot + "Item/Bonus", true)]
        [MenuItem(SceneRoot + "Item/Bonus", true)]
        private static bool AddBonus_Validate() => IsValid();

        [MenuItem(MenuRoot + "Item/Coin", false, PriorityRoot + 21)]
        [MenuItem(SceneRoot + "Item/Coin", false, PriorityRoot + 21)]
        private static void AddCoin()
            => CreateAndSelect(p => ItemElement.Create(ItemComponent.ItemType.Coin, p), "Vectorier Add Coin");

        [MenuItem(MenuRoot + "Item/Coin", true)]
        [MenuItem(SceneRoot + "Item/Coin", true)]
        private static bool AddCoin_Validate() => IsValid();

        // -------- SPAWN -------- //
        [MenuItem(MenuRoot + "Spawn", false, PriorityRoot + 40)]
        [MenuItem(SceneRoot + "Spawn", false, PriorityRoot + 40)]
        private static void AddSpawn()
            => CreateAndSelect(SpawnElement.Create, "Vectorier Add Spawn");

        [MenuItem(MenuRoot + "Spawn", true)]
        [MenuItem(SceneRoot + "Spawn", true)]
        private static bool AddSpawn_Validate() => IsValid();

        // -------- TRIGGER -------- //
        [MenuItem(MenuRoot + "Trigger", false, PriorityRoot + 60)]
        [MenuItem(SceneRoot + "Trigger", false, PriorityRoot + 60)]
        private static void AddTrigger()
            => CreateAndSelect(TriggerElement.Create, "Vectorier Add Trigger");

        [MenuItem(MenuRoot + "Trigger", true)]
        [MenuItem(SceneRoot + "Trigger", true)]
        private static bool AddTrigger_Validate() => IsValid();

        // -------- AREA -------- //
        private const string AreaMenu = MenuRoot + "Area/";
        private const string SceneAreaMenu = SceneRoot + "Area/";
        private const int AreaBase = PriorityRoot + 61;
        private const int AreaGroupRun      = AreaBase + 20;
        private const int AreaGroupWall     = AreaBase + 40;
        private const int AreaGroupVault    = AreaBase + 60;
        private const int AreaGroupJump     = AreaBase + 80;
        private const int AreaGroupCrawl    = AreaBase + 100;
        private const int AreaGroupCatch    = AreaBase + 120;

        private static void AddNamedArea(string areaName)
        {
            CreateAndSelect(p =>
            {
                GameObject area = AreaElement.Create(p);
                if (area != null) area.name = areaName;
                return area;
            }, "Vectorier Add " + areaName);
        }

        // Area
        [MenuItem(AreaMenu + "Area", false, AreaBase)]
        [MenuItem(SceneAreaMenu + "Area", false, AreaBase)]
        private static void AddArea() => AddNamedArea("Area");
        [MenuItem(AreaMenu + "Area", true)]
        [MenuItem(SceneAreaMenu + "Area", true)]
        private static bool AddArea_Validate() => IsValid();

        // ---- Run ----
        [MenuItem(AreaMenu + "Run Fast", false, AreaGroupRun)]
        [MenuItem(SceneAreaMenu + "Run Fast", false, AreaGroupRun)]
        private static void AddRunFast() => AddNamedArea("TriggerRunFast");
        [MenuItem(AreaMenu + "Run Fast", true)]
        [MenuItem(SceneAreaMenu + "Run Fast", true)]
        private static bool AddRunFast_Validate() => IsValid();

        [MenuItem(AreaMenu + "Run Inhibition", false, AreaGroupRun + 1)]
        [MenuItem(SceneAreaMenu + "Run Inhibition", false, AreaGroupRun + 1)]
        private static void AddRunInhibition() => AddNamedArea("TriggerRunInhibition");
        [MenuItem(AreaMenu + "Run Inhibition", true)]
        [MenuItem(SceneAreaMenu + "Run Inhibition", true)]
        private static bool AddRunInhibition_Validate() => IsValid();

        [MenuItem(AreaMenu + "Run From Inhibition", false, AreaGroupRun + 2)]
        [MenuItem(SceneAreaMenu + "Run From Inhibition", false, AreaGroupRun + 2)]
        private static void AddRunFromInhibition() => AddNamedArea("TriggerRunFromInhibition");
        [MenuItem(AreaMenu + "Run From Inhibition", true)]
        [MenuItem(SceneAreaMenu + "Run From Inhibition", true)]
        private static bool AddRunFromInhibition_Validate() => IsValid();

        [MenuItem(AreaMenu + "Run Reverse", false, AreaGroupRun + 3)]
        [MenuItem(SceneAreaMenu + "Run Reverse", false, AreaGroupRun + 3)]
        private static void AddRunReverse() => AddNamedArea("TriggerRunReverse");
        [MenuItem(AreaMenu + "Run Reverse", true)]
        [MenuItem(SceneAreaMenu + "Run Reverse", true)]
        private static bool AddRunReverse_Validate() => IsValid();

        [MenuItem(AreaMenu + "Run Reverse From Inhibition", false, AreaGroupRun + 4)]
        [MenuItem(SceneAreaMenu + "Run Reverse From Inhibition", false, AreaGroupRun + 4)]
        private static void AddRunReverseFromInhibition() => AddNamedArea("TriggerRunReverseFromInhibition");
        [MenuItem(AreaMenu + "Run Reverse From Inhibition", true)]
        [MenuItem(SceneAreaMenu + "Run Reverse From Inhibition", true)]
        private static bool AddRunReverseFromInhibition_Validate() => IsValid();

        [MenuItem(AreaMenu + "Run Forward From Fail Reverse", false, AreaGroupRun + 5)]
        [MenuItem(SceneAreaMenu + "Run Forward From Fail Reverse", false, AreaGroupRun + 5)]
        private static void AddRunForwardFromFailReverse() => AddNamedArea("TriggerRunForwardFromFailReverse");
        [MenuItem(AreaMenu + "Run Forward From Fail Reverse", true)]
        [MenuItem(SceneAreaMenu + "Run Forward From Fail Reverse", true)]
        private static bool AddRunForwardFromFailReverse_Validate() => IsValid();

        [MenuItem(AreaMenu + "Landing Stop", false, AreaGroupRun + 6)]
        [MenuItem(SceneAreaMenu + "Landing Stop", false, AreaGroupRun + 6)]
        private static void AddLandingStop() => AddNamedArea("TriggerLandingStop");
        [MenuItem(AreaMenu + "Landing Stop", true)]
        [MenuItem(SceneAreaMenu + "Landing Stop", true)]
        private static bool AddLandingStop_Validate() => IsValid();

        // ---- Wall ----
        [MenuItem(AreaMenu + "Wall Jump", false, AreaGroupWall)]
        [MenuItem(SceneAreaMenu + "Wall Jump", false, AreaGroupWall)]
        private static void AddWallJump() => AddNamedArea("TriggerWallJump");
        [MenuItem(AreaMenu + "Wall Jump", true)]
        [MenuItem(SceneAreaMenu + "Wall Jump", true)]
        private static bool AddWallJump_Validate() => IsValid();

        [MenuItem(AreaMenu + "Wall Run From Fail", false, AreaGroupWall + 1)]
        [MenuItem(SceneAreaMenu + "Wall Run From Fail", false, AreaGroupWall + 1)]
        private static void AddWallRunFromFail() => AddNamedArea("TriggerWallRunFromFail");
        [MenuItem(AreaMenu + "Wall Run From Fail", true)]
        [MenuItem(SceneAreaMenu + "Wall Run From Fail", true)]
        private static bool AddWallRunFromFail_Validate() => IsValid();

        [MenuItem(AreaMenu + "Wall Run From Fail Reverse", false, AreaGroupWall + 2)]
        [MenuItem(SceneAreaMenu + "Wall Run From Fail Reverse", false, AreaGroupWall + 2)]
        private static void AddWallRunFromFailReverse() => AddNamedArea("TriggerWallRunFromFailReverse");
        [MenuItem(AreaMenu + "Wall Run From Fail Reverse", true)]
        [MenuItem(SceneAreaMenu + "Wall Run From Fail Reverse", true)]
        private static bool AddWallRunFromFailReverse_Validate() => IsValid();

        [MenuItem(AreaMenu + "No Wall Run", false, AreaGroupWall + 3)]
        [MenuItem(SceneAreaMenu + "No Wall Run", false, AreaGroupWall + 3)]
        private static void AddNoWallRun() => AddNamedArea("NoWallRun");
        [MenuItem(AreaMenu + "No Wall Run", true)]
        [MenuItem(SceneAreaMenu + "No Wall Run", true)]
        private static bool AddNoWallRun_Validate() => IsValid();

        // ---- Vault ----
        [MenuItem(AreaMenu + "Speed Vault", false, AreaGroupVault)]
        [MenuItem(SceneAreaMenu + "Speed Vault", false, AreaGroupVault)]
        private static void AddSpeedVault() => AddNamedArea("TriggerSpeedVault");
        [MenuItem(AreaMenu + "Speed Vault", true)]
        [MenuItem(SceneAreaMenu + "Speed Vault", true)]
        private static bool AddSpeedVault_Validate() => IsValid();

        [MenuItem(AreaMenu + "Speed Vault Fly", false, AreaGroupVault + 1)]
        [MenuItem(SceneAreaMenu + "Speed Vault Fly", false, AreaGroupVault + 1)]
        private static void AddSpeedVaultFly() => AddNamedArea("TriggerSpeedVaultFly");
        [MenuItem(AreaMenu + "Speed Vault Fly", true)]
        [MenuItem(SceneAreaMenu + "Speed Vault Fly", true)]
        private static bool AddSpeedVaultFly_Validate() => IsValid();

        [MenuItem(AreaMenu + "Dive Kong", false, AreaGroupVault + 2)]
        [MenuItem(SceneAreaMenu + "Dive Kong", false, AreaGroupVault + 2)]
        private static void AddDiveKong() => AddNamedArea("TriggerDivingKong");
        [MenuItem(AreaMenu + "Dive Kong", true)]
        [MenuItem(SceneAreaMenu + "Dive Kong", true)]
        private static bool AddDiveKong_Validate() => IsValid();

        [MenuItem(AreaMenu + "Dive Kong Fly", false, AreaGroupVault + 3)]
        [MenuItem(SceneAreaMenu + "Dive Kong Fly", false, AreaGroupVault + 3)]
        private static void AddDiveKongFly() => AddNamedArea("TriggerDivingKongFly");
        [MenuItem(AreaMenu + "Dive Kong Fly", true)]
        [MenuItem(SceneAreaMenu + "Dive Kong Fly", true)]
        private static bool AddDiveKongFly_Validate() => IsValid();

        [MenuItem(AreaMenu + "Hurdle Jump", false, AreaGroupVault + 4)]
        [MenuItem(SceneAreaMenu + "Hurdle Jump", false, AreaGroupVault + 4)]
        private static void AddHurdleJump() => AddNamedArea("TriggerHurdleJump");
        [MenuItem(AreaMenu + "Hurdle Jump", true)]
        [MenuItem(SceneAreaMenu + "Hurdle Jump", true)]
        private static bool AddHurdleJump_Validate() => IsValid();

        [MenuItem(AreaMenu + "Jump And Roll", false, AreaGroupVault + 5)]
        [MenuItem(SceneAreaMenu + "Jump And Roll", false, AreaGroupVault + 5)]
        private static void AddJumpAndRoll() => AddNamedArea("TriggerJumpAndRoll");
        [MenuItem(AreaMenu + "Jump And Roll", true)]
        [MenuItem(SceneAreaMenu + "Jump And Roll", true)]
        private static bool AddJumpAndRoll_Validate() => IsValid();

        [MenuItem(AreaMenu + "Jump To Edge", false, AreaGroupVault + 6)]
        [MenuItem(SceneAreaMenu + "Jump To Edge", false, AreaGroupVault + 6)]
        private static void AddJumpToEdge() => AddNamedArea("TriggerJumpToEdge");
        [MenuItem(AreaMenu + "Jump To Edge", true)]
        [MenuItem(SceneAreaMenu + "Jump To Edge", true)]
        private static bool AddJumpToEdge_Validate() => IsValid();

        [MenuItem(AreaMenu + "Pop Vault", false, AreaGroupVault + 7)]
        [MenuItem(SceneAreaMenu + "Pop Vault", false, AreaGroupVault + 7)]
        private static void AddPopVault() => AddNamedArea("TriggerPopVault");
        [MenuItem(AreaMenu + "Pop Vault", true)]
        [MenuItem(SceneAreaMenu + "Pop Vault", true)]
        private static bool AddPopVault_Validate() => IsValid();

        [MenuItem(AreaMenu + "Pop Vault Close", false, AreaGroupVault + 8)]
        [MenuItem(SceneAreaMenu + "Pop Vault Close", false, AreaGroupVault + 8)]
        private static void AddPopVaultClose() => AddNamedArea("TriggerPopVaultClose");
        [MenuItem(AreaMenu + "Pop Vault Close", true)]
        [MenuItem(SceneAreaMenu + "Pop Vault Close", true)]
        private static bool AddPopVaultClose_Validate() => IsValid();

        // ---- Jump ----
        [MenuItem(AreaMenu + "High Jump", false, AreaGroupJump)]
        [MenuItem(SceneAreaMenu + "High Jump", false, AreaGroupJump)]
        private static void AddHighJump() => AddNamedArea("TriggerHighJump");
        [MenuItem(AreaMenu + "High Jump", true)]
        [MenuItem(SceneAreaMenu + "High Jump", true)]
        private static bool AddHighJump_Validate() => IsValid();

        [MenuItem(AreaMenu + "Collision To Fly", false, AreaGroupJump + 1)]
        [MenuItem(SceneAreaMenu + "Collision To Fly", false, AreaGroupJump + 1)]
        private static void AddCollisionToFly() => AddNamedArea("TriggerCollisionToFly");
        [MenuItem(AreaMenu + "Collision To Fly", true)]
        [MenuItem(SceneAreaMenu + "Collision To Fly", true)]
        private static bool AddCollisionToFly_Validate() => IsValid();

        // ---- Crawl ----
        [MenuItem(AreaMenu + "Crawling Start", false, AreaGroupCrawl)]
        [MenuItem(SceneAreaMenu + "Crawling Start", false, AreaGroupCrawl)]
        private static void AddCrawlingStart() => AddNamedArea("TriggerCrawlingStart");
        [MenuItem(AreaMenu + "Crawling Start", true)]
        [MenuItem(SceneAreaMenu + "Crawling Start", true)]
        private static bool AddCrawlingStart_Validate() => IsValid();

        [MenuItem(AreaMenu + "Crawling Middle", false, AreaGroupCrawl + 1)]
        [MenuItem(SceneAreaMenu + "Crawling Middle", false, AreaGroupCrawl + 1)]
        private static void AddCrawlingMiddle() => AddNamedArea("TriggerCrawlingMiddle");
        [MenuItem(AreaMenu + "Crawling Middle", true)]
        [MenuItem(SceneAreaMenu + "Crawling Middle", true)]
        private static bool AddCrawlingMiddle_Validate() => IsValid();

        [MenuItem(AreaMenu + "Crawling Finish", false, AreaGroupCrawl + 2)]
        [MenuItem(SceneAreaMenu + "Crawling Finish", false, AreaGroupCrawl + 2)]
        private static void AddCrawlingFinish() => AddNamedArea("TriggerCrawlingFinish");
        [MenuItem(AreaMenu + "Crawling Finish", true)]
        [MenuItem(SceneAreaMenu + "Crawling Finish", true)]
        private static bool AddCrawlingFinish_Validate() => IsValid();

        // ---- Catch ----
        [MenuItem(AreaMenu + "Catch", false, AreaGroupCatch)]
        [MenuItem(SceneAreaMenu + "Catch", false, AreaGroupCatch)]
        private static void AddCatch() => AddNamedArea("TriggerCatch");
        [MenuItem(AreaMenu + "Catch", true)]
        [MenuItem(SceneAreaMenu + "Catch", true)]
        private static bool AddCatch_Validate() => IsValid();

        [MenuItem(AreaMenu + "Catch Fast", false, AreaGroupCatch + 1)]
        [MenuItem(SceneAreaMenu + "Catch Fast", false, AreaGroupCatch + 1)]
        private static void AddCatchFast() => AddNamedArea("TriggerCatchFast");
        [MenuItem(AreaMenu + "Catch Fast", true)]
        [MenuItem(SceneAreaMenu + "Catch Fast", true)]
        private static bool AddCatchFast_Validate() => IsValid();

        [MenuItem(AreaMenu + "Catch Front", false, AreaGroupCatch + 2)]
        [MenuItem(SceneAreaMenu + "Catch Front", false, AreaGroupCatch + 2)]
        private static void AddCatchFront() => AddNamedArea("TriggerCatchFront");
        [MenuItem(AreaMenu + "Catch Front", true)]
        [MenuItem(SceneAreaMenu + "Catch Front", true)]
        private static bool AddCatchFront_Validate() => IsValid();

        // -------- PLATFORM & TRAPEZOID -------- //
        [MenuItem(MenuRoot + "Platform/Rough", false, PriorityRoot + 300)]
        [MenuItem(SceneRoot + "Platform/Rough", false, PriorityRoot + 300)]
        private static void AddPlatformRough()
            => CreateAndSelect(p => CreatePlatform(PlatformComponent.SurfaceType.Rough, p), "Vectorier Add Rough Platform");

        [MenuItem(MenuRoot + "Platform/Rough", true)]
        [MenuItem(SceneRoot + "Platform/Rough", true)]
        private static bool AddPlatformRough_Validate() => IsValid();

        [MenuItem(MenuRoot + "Platform/Smooth", false, PriorityRoot + 301)]
        [MenuItem(SceneRoot + "Platform/Smooth", false, PriorityRoot + 301)]
        private static void AddPlatformSmooth()
            => CreateAndSelect(p => CreatePlatform(PlatformComponent.SurfaceType.Smooth, p), "Vectorier Add Smooth Platform");

        [MenuItem(MenuRoot + "Platform/Smooth", true)]
        [MenuItem(SceneRoot + "Platform/Smooth", true)]
        private static bool AddPlatformSmooth_Validate() => IsValid();

        [MenuItem(MenuRoot + "Platform/Block", false, PriorityRoot + 302)]
        [MenuItem(SceneRoot + "Platform/Block", false, PriorityRoot + 302)]
        private static void AddPlatformBlock()
            => CreateAndSelect(p => CreatePlatform(PlatformComponent.SurfaceType.Block, p), "Vectorier Add Block Platform");

        [MenuItem(MenuRoot + "Platform/Block", true)]
        [MenuItem(SceneRoot + "Platform/Block", true)]
        private static bool AddPlatformBlock_Validate() => IsValid();

        [MenuItem(MenuRoot + "Trapezoid/Type1", false, PriorityRoot + 303)]
        [MenuItem(SceneRoot + "Trapezoid/Type1", false, PriorityRoot + 303)]
        private static void AddTrapezoidType1()
            => CreateAndSelect(p => TrapezoidElement.Create(TrapezoidComponent.TrapezoidType.Type1, p), "Vectorier Add Trapezoid Type 1");

        [MenuItem(MenuRoot + "Trapezoid/Type1", true)]
        [MenuItem(SceneRoot + "Trapezoid/Type1", true)]
        private static bool AddTrapezoidType1_Validate() => IsValid();

        [MenuItem(MenuRoot + "Trapezoid/Type2", false, PriorityRoot + 304)]
        [MenuItem(SceneRoot + "Trapezoid/Type2", false, PriorityRoot + 304)]
        private static void AddTrapezoidType2()
            => CreateAndSelect(p => TrapezoidElement.Create(TrapezoidComponent.TrapezoidType.Type2, p), "Vectorier Add Trapezoid Type 2");

        [MenuItem(MenuRoot + "Trapezoid/Type2", true)]
        [MenuItem(SceneRoot + "Trapezoid/Type2", true)]
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
        [MenuItem(MenuRoot + "Comment", false, PriorityRoot + 320)]
        [MenuItem(SceneRoot + "Comment", false, PriorityRoot + 320)]
        private static void AddComment()
            => CreateAndSelect(CreateCommentGameObject, "Vectorier Add Comment");

        [MenuItem(MenuRoot + "Comment", true)]
        [MenuItem(SceneRoot + "Comment", true)]
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