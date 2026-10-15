using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vectorier.Component;

namespace Vectorier.EditorScript.Tools
{
    // Does the actual texture generation for PlatformComponent.
    // QuickSurface (menu), ContextMenu and PlatformSurfaceWatcher all go through here.
    public static class PlatformSurfaceBuilder
    {
        public const string PLATFORM_TAG = "Platform";

        public const string FLOOR_GROUP = "Floor";
        public const string LEFT_WALL_GROUP = "Left Wall";
        public const string RIGHT_WALL_GROUP = "Right Wall";
        public const string FILL_NAME = "v_black";

        private const string CORNER_UP_L = "Images/Vector/Black/Block/Floor/v_CornerUp_L_01";
        private const string CORNER_UP_R = "Images/Vector/Black/Block/Floor/v_CornerUp_R_01";
        private const string CORNER_DOWN_L = "Images/Vector/Black/Block/Wall/v_CornerDown_L_01";
        private const string CORNER_DOWN_R = "Images/Vector/Black/Block/Wall/v_CornerDown_R_01";

        private const string FLOOR = "Images/Vector/Black/Block/Floor/v_Floor_01";
        private const string FLOOR_LONG = "Images/Vector/Black/Block/Floor/v_LongFloor_01";

        private const string WALL_L = "Images/Vector/Black/Block/Wall/v_Wall_L_01";
        private const string WALL_R = "Images/Vector/Black/Block/Wall/v_Wall_R_01";
        private const string WALL_L_LONG = "Images/Vector/Black/Block/Wall/v_LongCornerUp_L_01";
        private const string WALL_R_LONG = "Images/Vector/Black/Block/Wall/v_LongCornerUp_R_01";

        private const string BLACK_FILL = "Images/Vector/Black/Block/v_black";

        // Smooth: v_black starts at the corner's top-left +4/-2 and ends at the right corner's top-right -4/-2
        private static readonly Vector2 SMOOTH_FILL_INSET = new(4f, 2f);

        // small tolerance so float noise doesn't spawn/skip a tile
        private const float EPSILON = 0.01f;

        // safety net in case a sprite comes back with 0 size or someone makes a 1km platform
        private const int MAX_TILES = 4096;

        // Bump this if the generation logic changes, so old platforms rebuild the next time they're touched
        private const int BUILD_VERSION = 1;

        private static readonly Dictionary<string, List<Sprite>> variantCache = new();

        // ================= PUBLIC =================

        public static bool IsValidPlatform(GameObject gameObject) => TryGetPlatformRenderer(gameObject, out _);

        public static bool TryGetPlatformRenderer(GameObject gameObject, out SpriteRenderer spriteRenderer)
        {
            spriteRenderer = null;
            return gameObject
                && gameObject.CompareTag(PLATFORM_TAG)
                && gameObject.TryGetComponent(out spriteRenderer)
                && spriteRenderer.sprite;
        }

        // Adds the component if needed, sets the type, builds. Used by the Quick Actions menu.
        public static PlatformComponent Setup(GameObject gameObject, PlatformComponent.SurfaceType type, bool recordUndo)
        {
            if (!IsValidPlatform(gameObject))
                return null;

            if (!gameObject.TryGetComponent(out PlatformComponent platform))
                platform = recordUndo ? Undo.AddComponent<PlatformComponent>(gameObject) : gameObject.AddComponent<PlatformComponent>();

            if (recordUndo)
                Undo.RecordObject(platform, "Set Platform Surface");

            platform.Type = type;
            Build(platform, recordUndo);
            return platform;
        }

        public static bool NeedsRebuild(PlatformComponent platform)
        {
            if (!platform || !TryGetPlatformRenderer(platform.gameObject, out SpriteRenderer spriteRenderer))
                return false;

            if (platform.BuiltHash != ComputeHash(platform, spriteRenderer))
                return true;

            // Hash matches but stuff is missing (e.g. children got deleted by hand, or an undo cleared them)
            Transform root = platform.transform;
            if (!root.Find(FILL_NAME))
                return true;
            if (platform.Type != PlatformComponent.SurfaceType.Block && !root.Find(FLOOR_GROUP))
                return true;

            return false;
        }

        // recordUndo = true for user actions (menu, buttons). The real-time rebuilds skip undo on purpose,
        // the textures are derived from the platform so undoing the platform change rebuilds them anyway.
        public static void Build(PlatformComponent platform, bool recordUndo)
        {
            if (!platform || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // prefab asset sitting on disk, not touching that
            if (EditorUtility.IsPersistent(platform))
                return;

            GameObject gameObject = platform.gameObject;
            if (!TryGetPlatformRenderer(gameObject, out SpriteRenderer spriteRenderer))
            {
                // only complain on manual builds, otherwise this spams every frame while dragging
                if (recordUndo)
                    Debug.LogWarning($"[PlatformSurface] '{gameObject.name}' needs the \"{PLATFORM_TAG}\" tag and a SpriteRenderer with a sprite.", gameObject);
                return;
            }

            if (recordUndo)
                Undo.RecordObject(platform, "Build Platform Surface");

            if (platform.Seed == 0)
                platform.Seed = Random.Range(1, int.MaxValue);

            Transform root = gameObject.transform;

            // Always start clean, gets rid of leftovers if the component was removed and readded etc.
            ClearChildren(root, recordUndo);
            int firstCreated = root.childCount;

            Bounds bounds = spriteRenderer.bounds;
            System.Random rng = new(platform.Seed);

            switch (platform.Type)
            {
                case PlatformComponent.SurfaceType.Rough:
                    BuildSurface(platform, root, bounds, rng, smooth: false);
                    break;

                case PlatformComponent.SurfaceType.Smooth:
                    BuildSurface(platform, root, bounds, rng, smooth: true);
                    break;

                case PlatformComponent.SurfaceType.Block:
                    CreateFill(root, bounds.min.x, bounds.max.y, bounds.max.x, bounds.min.y, root.position.z + 1f);
                    break;
            }

            if (recordUndo)
            {
                // registering the top level children covers everything under them
                for (int i = firstCreated; i < root.childCount; i++)
                    Undo.RegisterCreatedObjectUndo(root.GetChild(i).gameObject, "Build Platform Surface");
            }

            platform.BuiltHash = ComputeHash(platform, spriteRenderer);
            EditorUtility.SetDirty(platform);
            MarkSceneDirty(gameObject);
        }

        // Only removes what we generate (by name). Used when the component gets removed.
        public static void ClearGenerated(Transform root)
        {
            if (!root)
                return;

            bool removedAny = false;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                if (!IsGeneratedName(child.name) || IsLockedByPrefab(root, child))
                    continue;

                Object.DestroyImmediate(child);
                removedAny = true;
            }

            if (removedAny)
                MarkSceneDirty(root.gameObject);
        }

        // ================= BUILD =================

        private static void BuildSurface(PlatformComponent platform, Transform root, Bounds bounds, System.Random rng, bool smooth)
        {
            float z = root.position.z;

            Transform floorGroup = CreateGroup(FLOOR_GROUP, root);
            Transform leftGroup = CreateGroup(LEFT_WALL_GROUP, root);
            Transform rightGroup = CreateGroup(RIGHT_WALL_GROUP, root);

            // Top corners live in the Floor group.
            // Smooth always wants both (and always the _01 ones), Rough only needs one if a floor or wall meets there
            bool wantUpLeft = smooth || platform.Floor || platform.LeftWall;
            bool wantUpRight = smooth || platform.Floor || platform.RightWall;

            Sprite upLeft = wantUpLeft ? (smooth ? Load(CORNER_UP_L) : PickVariant(CORNER_UP_L, rng)) : null;
            Sprite upRight = wantUpRight ? (smooth ? Load(CORNER_UP_R) : PickVariant(CORNER_UP_R, rng)) : null;

            if (upLeft)
                CreateSprite(upLeft, new Vector3(bounds.min.x, bounds.max.y, z), floorGroup);

            if (upRight)
                CreateSprite(upRight, new Vector3(bounds.max.x - upRight.bounds.size.x, bounds.max.y, z), floorGroup);

            // -------- FLOOR --------
            // Smooth skips this, v_black covers the top instead
            if (!smooth && platform.Floor && upLeft && upRight)
            {
                float start = bounds.min.x + upLeft.bounds.size.x;
                float end = bounds.max.x - upRight.bounds.size.x;
                TileHorizontal(start, end, bounds.max.y, z, FLOOR, FLOOR_LONG, rng, floorGroup);
            }

            // -------- LEFT WALL --------
            if (platform.LeftWall && upLeft)
            {
                Sprite downLeft = PickVariant(CORNER_DOWN_L, rng);
                if (downLeft)
                {
                    // pivots are top-left, so the bottom corner sits one sprite height above the bottom edge
                    float downTop = bounds.min.y + downLeft.bounds.size.y;
                    CreateSprite(downLeft, new Vector3(bounds.min.x, downTop, z), leftGroup);
                    TileVertical(bounds.max.y - upLeft.bounds.size.y, downTop, bounds.min.x, false, z, WALL_L, WALL_L_LONG, rng, leftGroup);
                }
            }

            // -------- RIGHT WALL --------
            if (platform.RightWall && upRight)
            {
                Sprite downRight = PickVariant(CORNER_DOWN_R, rng);
                if (downRight)
                {
                    float downTop = bounds.min.y + downRight.bounds.size.y;
                    CreateSprite(downRight, new Vector3(bounds.max.x - downRight.bounds.size.x, downTop, z), rightGroup);
                    TileVertical(bounds.max.y - upRight.bounds.size.y, downTop, bounds.max.x, true, z, WALL_R, WALL_R_LONG, rng, rightGroup);
                }
            }

            // -------- FILL --------
            if (smooth)
            {
                CreateFill(root,
                    bounds.min.x + SMOOTH_FILL_INSET.x,
                    bounds.max.y - SMOOTH_FILL_INSET.y,
                    bounds.max.x - SMOOTH_FILL_INSET.x,
                    bounds.min.y,
                    z + 1f);
            }
            else
            {
                // Fill whatever's inside the edges. If an edge is turned off, the fill goes all the way to the boundary there
                float left = platform.LeftWall && upLeft ? bounds.min.x + upLeft.bounds.size.x : bounds.min.x;
                float right = platform.RightWall && upRight ? bounds.max.x - upRight.bounds.size.x : bounds.max.x;
                float top = platform.Floor && upLeft ? bounds.max.y - upLeft.bounds.size.y : bounds.max.y;

                CreateFill(root, left, top, right, bounds.min.y, z + 1f);
            }
        }

        // Same tiling as the old QuickSurface: long tiles while they fit, short ones after,
        // and the last tile gets snapped to the end so there's never a gap
        private static void TileHorizontal(float start, float end, float y, float z, string shortPath, string longPath, System.Random rng, Transform parent)
        {
            List<Sprite> shorts = GetVariants(shortPath);
            List<Sprite> longs = GetVariants(longPath);
            if (shorts == null || longs == null)
                return;

            float longWidth = longs[0].bounds.size.x;
            float x = start;

            for (int i = 0; i < MAX_TILES && end - x > EPSILON; i++)
            {
                float remaining = end - x;
                Sprite chosen = Pick(remaining >= longWidth - EPSILON ? longs : shorts, rng);
                float width = chosen.bounds.size.x;
                if (width <= 0f)
                    break;

                if (width >= remaining - EPSILON)
                {
                    CreateSprite(chosen, new Vector3(end - width, y, z), parent);
                    break;
                }

                CreateSprite(chosen, new Vector3(x, y, z), parent);
                x += width;
            }
        }

        // Goes top -> bottom. edgeX is the left edge, or the right edge if alignRight is set
        private static void TileVertical(float top, float bottom, float edgeX, bool alignRight, float z, string shortPath, string longPath, System.Random rng, Transform parent)
        {
            List<Sprite> shorts = GetVariants(shortPath);
            List<Sprite> longs = GetVariants(longPath);
            if (shorts == null || longs == null)
                return;

            float longHeight = longs[0].bounds.size.y;
            float y = top;

            for (int i = 0; i < MAX_TILES && y - bottom > EPSILON; i++)
            {
                float remaining = y - bottom;
                Sprite chosen = Pick(remaining >= longHeight - EPSILON ? longs : shorts, rng);
                Vector2 size = chosen.bounds.size;
                if (size.y <= 0f)
                    break;

                float x = alignRight ? edgeX - size.x : edgeX;

                if (size.y >= remaining - EPSILON)
                {
                    CreateSprite(chosen, new Vector3(x, bottom + size.y, z), parent);
                    break;
                }

                CreateSprite(chosen, new Vector3(x, y, z), parent);
                y -= size.y;
            }
        }

        private static void CreateFill(Transform root, float left, float top, float right, float bottom, float z)
        {
            float width = right - left;
            float height = top - bottom;

            // platform's too small for a fill, nothing to do
            if (width <= EPSILON || height <= EPSILON)
                return;

            Sprite fill = Load(BLACK_FILL);
            if (!fill)
                return;

            Vector2 size = fill.bounds.size;
            GameObject fillObject = CreateSprite(fill, new Vector3(left, top, z), root, new Vector2(width / size.x, height / size.y));
            fillObject.name = FILL_NAME;
        }

        // ================= HELPERS =================

        private static Transform CreateGroup(string name, Transform root)
        {
            GameObject group = new(name);
            group.transform.SetParent(root, false);
            return group.transform;
        }

        private static GameObject CreateSprite(Sprite sprite, Vector3 worldPosition, Transform parent, Vector2? worldScale = null)
        {
            GameObject gameObject = new(sprite.name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = worldPosition;

            // The platform itself is scaled to its size, so cancel that out or every tile gets stretched with it
            gameObject.transform.localScale = ToLocalScale(parent, worldScale ?? Vector2.one);

            gameObject.AddComponent<SpriteRenderer>().sprite = sprite;
            return gameObject;
        }

        private static Vector3 ToLocalScale(Transform parent, Vector2 worldScale)
        {
            Vector3 parentScale = parent.lossyScale;
            return new Vector3(
                worldScale.x / NonZero(parentScale.x),
                worldScale.y / NonZero(parentScale.y),
                1f / NonZero(parentScale.z));
        }

        private static float NonZero(float value) => Mathf.Abs(value) < 0.00001f ? 1f : value;

        private static void ClearChildren(Transform root, bool recordUndo)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;

                if (IsLockedByPrefab(root, child))
                {
                    if (recordUndo)
                        Debug.LogWarning($"[PlatformSurface] Can't remove '{child.name}', it's part of the platform's prefab. Skipping it.", child);
                    continue;
                }

                if (recordUndo)
                    Undo.DestroyObjectImmediate(child);
                else
                    Object.DestroyImmediate(child);
            }
        }

        // Children baked into the platform's own prefab can't be deleted from the instance
        private static bool IsLockedByPrefab(Transform root, GameObject child)
        {
            GameObject rootPrefab = PrefabUtility.GetOutermostPrefabInstanceRoot(root.gameObject);
            return rootPrefab && PrefabUtility.GetOutermostPrefabInstanceRoot(child) == rootPrefab;
        }

        private static bool IsGeneratedName(string name) =>
            name == FLOOR_GROUP || name == LEFT_WALL_GROUP || name == RIGHT_WALL_GROUP || name == FILL_NAME;

        // Deterministic on purpose (no System.HashCode, that one's randomized per session)
        private static int ComputeHash(PlatformComponent platform, SpriteRenderer spriteRenderer)
        {
            Vector3 scale = spriteRenderer.transform.lossyScale;
            Bounds local = spriteRenderer.localBounds;

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + BUILD_VERSION;
                hash = hash * 31 + (int)platform.Type;
                hash = hash * 31 + (platform.Floor ? 1 : 0);
                hash = hash * 31 + (platform.LeftWall ? 1 : 0);
                hash = hash * 31 + (platform.RightWall ? 1 : 0);
                hash = hash * 31 + platform.Seed;
                hash = hash * 31 + Quantize(scale.x);
                hash = hash * 31 + Quantize(scale.y);
                hash = hash * 31 + Quantize(local.size.x);
                hash = hash * 31 + Quantize(local.size.y);
                hash = hash * 31 + Quantize(local.center.x);
                hash = hash * 31 + Quantize(local.center.y);
                return hash;
            }
            // position isn't in here on purpose, moving the platform just drags the children along
        }

        private static int Quantize(float value) => Mathf.RoundToInt(value * 1000f);

        private static void MarkSceneDirty(GameObject gameObject)
        {
            Scene scene = gameObject.scene;
            if (scene.IsValid() && !EditorSceneManager.IsPreviewScene(scene))
                EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Sprite Load(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (!sprite)
                Debug.LogError($"[PlatformSurface] Missing: Resources/{path}.png");
            return sprite;
        }

        private static Sprite PickVariant(string basePath, System.Random rng)
        {
            List<Sprite> variants = GetVariants(basePath);
            return variants == null ? null : Pick(variants, rng);
        }

        private static Sprite Pick(List<Sprite> sprites, System.Random rng) => sprites[rng.Next(sprites.Count)];

        // Cached since the real-time rebuild hits this a lot while dragging
        private static List<Sprite> GetVariants(string basePath, int maxVariants = 3)
        {
            // drop the cache entry if a sprite died (reimport etc.)
            if (variantCache.TryGetValue(basePath, out List<Sprite> cached) && !cached.Exists(s => !s))
                return cached;

            List<Sprite> variants = new();
            for (int i = 1; i <= maxVariants; i++)
            {
                Sprite sprite = Resources.Load<Sprite>(basePath.Replace("_01", $"_{i:00}"));
                if (sprite)
                    variants.Add(sprite);
            }

            if (variants.Count == 0)
            {
                variantCache.Remove(basePath);
                Debug.LogError($"[PlatformSurface] Missing variants for: Resources/{basePath}.png");
                return null;
            }

            variantCache[basePath] = variants;
            return variants;
        }
    }
}