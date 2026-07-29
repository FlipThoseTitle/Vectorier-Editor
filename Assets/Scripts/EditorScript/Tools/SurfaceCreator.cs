using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Vectorier.EditorScript.Tools
{
    [Serializable]
    public class SpriteGroup
    {
        public List<Sprite> sprites = new();
        private int currentSequenceIndex = 0;

        public virtual Sprite LoadSprites() => LoadRandom();

        protected Sprite LoadRandom()
        {
            if (sprites.Count == 0) return null;
            return sprites[UnityEngine.Random.Range(0, sprites.Count)];
        }

        protected Sprite LoadSequence()
        {
            if (sprites.Count == 0)
            {
                ResetSequence();
                return null;
            }

            Sprite output = sprites[currentSequenceIndex];
            if (currentSequenceIndex < sprites.Count - 1)
                currentSequenceIndex++;
            else
                ResetSequence();

            return output;
        }

        public void ResetSequence() => currentSequenceIndex = 0;

        private void ApplyOffsets(SpriteRenderer sprite, Vector3 position)
        {
            if (this is SideGroup side)
            {
                switch (side.type)
                {
                    case SideGroup.Type.Bottom: position.y += sprite.bounds.size.y; break;
                    case SideGroup.Type.Right: position.x -= sprite.bounds.size.x; position.y += sprite.bounds.size.y; break;
                    case SideGroup.Type.Left: position.y += sprite.bounds.size.y; break;
                }
            }
            else if (this is CornerGroup corner)
            {
                switch (corner.type)
                {
                    case CornerGroup.Type.TopRight: position.x -= sprite.bounds.size.x; break;
                    case CornerGroup.Type.BottomRight: position.x -= sprite.bounds.size.x; position.y += sprite.bounds.size.y; break;
                    case CornerGroup.Type.BottomLeft: position.y += sprite.bounds.size.y; break;
                }
            }
            sprite.transform.position = position;
        }

        public GameObject CreateGameObject(Vector3 position, Transform parent, float? sizeX = null, float? sizeY = null)
        {
            Sprite sprite = LoadSprites();
            return CreateGameObject(sprite, position, parent, sizeX, sizeY);
        }

        public GameObject CreateGameObject(Sprite sprite, Vector3 position, Transform parent, float? sizeX = null, float? sizeY = null)
        {
            if (!sprite) return null;

            float newSizeX = sizeX ?? sprite.bounds.size.x;
            float newSizeY = sizeY ?? sprite.bounds.size.y;

            GameObject gameObject = new(sprite.name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Surface Part");
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;
            
            Vector3 scale = gameObject.transform.localScale;
            gameObject.transform.localScale = new Vector3(newSizeX / sprite.bounds.size.x, newSizeY / sprite.bounds.size.y, scale.z);
            
            SpriteRenderer sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            ApplyOffsets(sr, position);
            return gameObject;
        }
    }

    [Serializable]
    public class CornerGroup : SpriteGroup
    {
        public enum Type { TopRight, TopLeft, BottomRight, BottomLeft }
        public Type type;
        protected Sprite loadedSprite;

        public CornerGroup(Type type) => this.type = type;

        public override Sprite LoadSprites()
        {
            loadedSprite = LoadRandom();
            return loadedSprite;
        }

        public Sprite GetLoadedSprite() => loadedSprite;
    }

    [Serializable]
    public class SideGroup : SpriteGroup
    {
        public enum Type { Top, Right, Left, Bottom }
        public enum Choice { RandomArray, SequenceArray, RandomFill }

        public Choice mode = Choice.RandomArray;
        public Type type;
        
        private CornerGroup adjacentCorner1;
        private CornerGroup adjacentCorner2;

        public SideGroup(Type type, CornerGroup adj1, CornerGroup adj2)
        {
            this.type = type;
            adjacentCorner1 = adj1;
            adjacentCorner2 = adj2;
        }

        public override Sprite LoadSprites()
        {
            return mode switch
            {
                Choice.SequenceArray => LoadSequence(),
                Choice.RandomFill => sprites.Count > 0 ? sprites[0] : null,
                _ => LoadRandom(),
            };
        }

        public float GetMaxWidth()
        {
            Sprite adj1 = adjacentCorner1.GetLoadedSprite();
            Sprite adj2 = adjacentCorner2.GetLoadedSprite();
            return (!adj1 || !adj2) ? 0 : Mathf.Max(adj1.bounds.size.x, adj2.bounds.size.x);
        }

        public float GetMaxHeight()
        {
            Sprite adj1 = adjacentCorner1.GetLoadedSprite();
            Sprite adj2 = adjacentCorner2.GetLoadedSprite();
            return (!adj1 || !adj2) ? 0 : Mathf.Max(adj1.bounds.size.y, adj2.bounds.size.y);
        }
    }

    [Serializable]
    public class SurfaceBox
    {
        public enum Corners { TopLeft, TopRight, BottomRight, BottomLeft }
        public enum Sides { Top, Right, Left, Bottom }

        private SpriteRenderer templateSprite;
        private Vector3 topLeft, topRight, bottomLeft, bottomRight;

        // Sprite Groups
        public CornerGroup TLSprites = new(CornerGroup.Type.TopLeft);
        public CornerGroup TRSprites = new(CornerGroup.Type.TopRight);
        public CornerGroup BRSprites = new(CornerGroup.Type.BottomRight);
        public CornerGroup BLSprites = new(CornerGroup.Type.BottomLeft);
        public SideGroup TSprites;
        public SideGroup BSprites;
        public SideGroup LSprites;
        public SideGroup RSprites;
        public SpriteGroup FillSprites = new();

        public SurfaceBox()
        {
            TSprites = new(SideGroup.Type.Top, TLSprites, TRSprites);
            BSprites = new(SideGroup.Type.Bottom, BLSprites, BRSprites);
            LSprites = new(SideGroup.Type.Left, TLSprites, BLSprites);
            RSprites = new(SideGroup.Type.Right, TRSprites, BRSprites);
        }

        public void UpdateTemplateSprite(SpriteRenderer spriteRenderer) => templateSprite = spriteRenderer;

        public void StartProcess(Bounds bounds)
        {
            if (templateSprite == null) return;

            Sprite TLSprite = TLSprites.LoadSprites();
            Sprite TRSprite = TRSprites.LoadSprites();
            Sprite BRSprite = BRSprites.LoadSprites();
            Sprite BLSprite = BLSprites.LoadSprites();

            UpdateCornerPositions();

            GameObject root = new("Corners");
            Undo.RegisterCreatedObjectUndo(root, "Create Surface Part");

            TLSprites.CreateGameObject(topLeft, root.transform);
            TRSprites.CreateGameObject(topRight, root.transform);
            BLSprites.CreateGameObject(bottomLeft, root.transform);
            BRSprites.CreateGameObject(bottomRight, root.transform);

            float TL_H = TLSprite ? TLSprite.bounds.size.x : 0f;
            float TL_V = TLSprite ? TLSprite.bounds.size.y : 0f;
            float TR_H = TRSprite ? TRSprite.bounds.size.x : 0f;
            float TR_V = TRSprite ? TRSprite.bounds.size.y : 0f;
            float BL_H = BLSprite ? BLSprite.bounds.size.x : 0f;
            float BL_V = BLSprite ? BLSprite.bounds.size.y : 0f;
            float BR_H = BRSprite ? BRSprite.bounds.size.x : 0f;
            float BR_V = BRSprite ? BRSprite.bounds.size.y : 0f;

            if (TL_H + TR_H < bounds.size.x)
                CreateSideSprites(TSprites, new Vector3(topLeft.x + TL_H, topLeft.y, 0f), new Vector3(topRight.x - TR_H, topLeft.y, 0f));
            if (BL_H + BR_H < bounds.size.x)
                CreateSideSprites(BSprites, new Vector3(bottomLeft.x + BL_H, bottomLeft.y, 0f), new Vector3(bottomRight.x - BR_H, bottomLeft.y, 0f));
            if (TR_V + BR_V < bounds.size.y)
                CreateSideSprites(RSprites, new Vector3(topRight.x, bottomRight.y + BR_V, 0f), new Vector3(topRight.x, topRight.y - TR_V, 0f));
            if (TL_V + BL_V < bounds.size.y)
                CreateSideSprites(LSprites, new Vector3(topLeft.x, bottomLeft.y + BL_V, 0f), new Vector3(topLeft.x, topLeft.y - TL_V, 0f));

            Vector3 fillStart = new(topLeft.x + Mathf.Min(TL_H, BL_H), topLeft.y - Mathf.Min(TL_V, TR_V), 0f);
            float sizeX = bounds.size.x - Mathf.Min(TL_H, BL_H) - Mathf.Min(TR_H, BR_H);
            
            // We no longer subtract the bottom wall heights so the fill extends fully downwards
            float sizeY = bounds.size.y - Mathf.Min(TL_V, TR_V);

            GameObject fill = new("Fill");
            Undo.RegisterCreatedObjectUndo(fill, "Create Surface Part");
            FillSprites.CreateGameObject(fillStart, fill.transform, sizeX, sizeY);
        }

        private void UpdateCornerPositions()
        {
            if (!templateSprite) return;
            Bounds bounds = templateSprite.bounds;
            topLeft = new(bounds.min.x, bounds.max.y, 0f);
            topRight = new(bounds.max.x, bounds.max.y, 0f);
            bottomLeft = new(bounds.min.x, bounds.min.y, 0f);
            bottomRight = new(bounds.max.x, bounds.min.y, 0f);
        }

        private void CreateSideSprites(SideGroup group, Vector3 start, Vector3 end)
        {
            if (group.sprites.Count == 0) return;

            GameObject root = new("Array");
            Undo.RegisterCreatedObjectUndo(root, "Create Surface Part");

            bool horizontal = Mathf.Approximately(start.y, end.y);
            
            // A small tolerance to prevent missing tiles due to Unity's floating point inaccuracies
            const float EPSILON = 0.01f;

            if (group.mode == SideGroup.Choice.RandomFill)
            {
                float sizeX = horizontal ? end.x - start.x : group.GetMaxWidth();
                float sizeY = horizontal ? group.GetMaxHeight() : end.y - start.y;
                group.CreateGameObject(start, root.transform, sizeX, sizeY);
            }
            else
            {
                Vector3 position = start;
                while (true)
                {
                    float current = horizontal ? position.x : position.y;
                    float target = horizontal ? end.x : end.y;
                    float remaining = target - current;

                    // Stop if we have reached the target
                    if (remaining <= EPSILON) break;

                    Sprite sprite = group.LoadSprites();
                    if (!sprite) break;

                    float spriteSize = horizontal ? sprite.bounds.size.x : sprite.bounds.size.y;

                    // If this tile covers the rest of the distance, align it exactly backward to the end and finish
                    if (spriteSize >= remaining - EPSILON)
                    {
                        if (horizontal) position.x = target - spriteSize;
                        else position.y = target - spriteSize;
                        
                        group.CreateGameObject(sprite, position, root.transform);
                        break;
                    }
                    else
                    {
                        group.CreateGameObject(sprite, position, root.transform);
                        
                        if (horizontal) position.x += spriteSize;
                        else position.y += spriteSize;
                    }
                }
                group.ResetSequence();
            }
        }
    }

    public class SurfaceCreator : EditorWindow
    {
        private SpriteRenderer spriteRenderer;
        private SurfaceBox box = new();
        private Vector2 scrollPosition;
        private bool foldTextureParams = true;
        private Dictionary<List<Sprite>, ReorderableList> lists = new();

        // Default Constants from QuickSurface
        private const string CORNER_UP_L = "Images/Vector/Black/Block/Floor/v_CornerUp_L_01";
        private const string CORNER_UP_R = "Images/Vector/Black/Block/Floor/v_CornerUp_R_01";
        private const string CORNER_DOWN_L = "Images/Vector/Black/Block/Wall/v_CornerDown_L_01";
        private const string CORNER_DOWN_R = "Images/Vector/Black/Block/Wall/v_CornerDown_R_01";
        private const string FLOOR = "Images/Vector/Black/Block/Floor/v_Floor_01";
        private const string WALL_L = "Images/Vector/Black/Block/Wall/v_Wall_L_01";
        private const string WALL_R = "Images/Vector/Black/Block/Wall/v_Wall_R_01";
        private const string BLACK_FILL = "Images/Vector/Black/Block/v_black";

        [MenuItem("Vectorier/Tools/Quick Actions/Build Surface/Custom Surface...", false, 40)]
        public static void Open()
        {
            var window = GetWindow<SurfaceCreator>("Custom Surface");
            window.InitializeDefaultsIfEmpty();
        }

        private void FetchSelection()
        {
            var go = Selection.activeGameObject;
            spriteRenderer = go ? go.GetComponent<SpriteRenderer>() : null;
            box.UpdateTemplateSprite(spriteRenderer);
        }

        private void OnEnable() => FetchSelection();

        private void OnSelectionChange()
        {
            FetchSelection();
            Repaint(); // Forces real-time UI update when selecting a new object
        }

        private void InitializeDefaultsIfEmpty()
        {
            if (box.TLSprites.sprites.Count == 0 && box.TSprites.sprites.Count == 0)
                SetDefaultTextures();
        }

        private void SetDefaultTextures()
        {
            box.TLSprites.sprites = LoadVariants(CORNER_UP_L);
            box.TRSprites.sprites = LoadVariants(CORNER_UP_R);
            box.BLSprites.sprites = LoadVariants(CORNER_DOWN_L);
            box.BRSprites.sprites = LoadVariants(CORNER_DOWN_R);
            
            box.TSprites.sprites = LoadVariants(FLOOR);
            box.LSprites.sprites = LoadVariants(WALL_L);
            box.RSprites.sprites = LoadVariants(WALL_R);
            
            // Bottom wall usually doesn't have a specific default in QuickSurface, but we leave it empty or map it if needed
            box.BSprites.sprites = new List<Sprite>(); 

            Sprite fill = Resources.Load<Sprite>(BLACK_FILL);
            box.FillSprites.sprites = fill ? new List<Sprite> { fill } : new List<Sprite>();
        }

        private List<Sprite> LoadVariants(string basePath, int maxVariants = 3)
        {
            List<Sprite> candidates = new();
            for (int i = 1; i <= maxVariants; i++)
            {
                string path = basePath.Replace("_01", $"_{i:00}");
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite) candidates.Add(sprite);
            }
            return candidates;
        }

        private void OnGUI()
        {
            if (!spriteRenderer)
            {
                EditorGUILayout.HelpBox("SpriteRenderer Not Found. Select an Object with a SpriteRenderer in the scene.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label($"Target: {spriteRenderer.gameObject.name}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Begin the scroll view
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.BeginHorizontal();
            foldTextureParams = EditorGUILayout.Foldout(foldTextureParams, "Texture Parameters", true, EditorStyles.foldoutHeader);
            if (GUILayout.Button("Reset to Defaults", GUILayout.Width(130)))
            {
                SetDefaultTextures();
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            if (foldTextureParams)
            {
                EditorGUIUtility.labelWidth = 80f;

                EditorGUILayout.LabelField("Corners", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical("box"); DisplaySpriteList(box.TLSprites, "Top Left"); GUILayout.EndVertical();
                GUILayout.BeginVertical("box"); DisplaySpriteList(box.TRSprites, "Top Right"); GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical("box"); DisplaySpriteList(box.BLSprites, "Bottom Left"); GUILayout.EndVertical();
                GUILayout.BeginVertical("box"); DisplaySpriteList(box.BRSprites, "Bottom Right"); GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Sides", EditorStyles.boldLabel);
                
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical("box"); DisplaySpriteList(box.TSprites, "Top"); GUILayout.EndVertical();
                GUILayout.BeginVertical("box"); DisplaySpriteList(box.BSprites, "Bottom"); GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical("box"); DisplaySpriteList(box.LSprites, "Left"); GUILayout.EndVertical();
                GUILayout.BeginVertical("box"); DisplaySpriteList(box.RSprites, "Right"); GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                EditorGUILayout.Space();
                GUILayout.BeginVertical("box");
                DisplaySpriteList(box.FillSprites, "Fill");
                GUILayout.EndVertical();
            }

            // End the scroll view
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // The Create Surface button remains outside the scroll view so it is always accessible at the bottom
            if (GUILayout.Button("Create Surface", GUILayout.Height(35)))
            {
                box.StartProcess(spriteRenderer.bounds);
                Selection.activeGameObject = GameObject.Find("Corners")?.transform.parent?.gameObject; // Try to select created block if applicable
            }
            GUI.backgroundColor = Color.white;
        }

        private void DisplaySpriteList(SpriteGroup group, string headerName)
        {
            ReorderableList spriteList = GetList(group.sprites);

            spriteList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, headerName);
            spriteList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                group.sprites[index] = (Sprite)EditorGUI.ObjectField(rect, GUIContent.none, group.sprites[index], typeof(Sprite), false);
            };

            spriteList.drawFooterCallback = (Rect rect) =>
            {
                if (GUI.Button(new Rect(rect.x, rect.y, 30f, EditorGUIUtility.singleLineHeight), "+"))
                    group.sprites.Add(null);
                
                if (GUI.Button(new Rect(rect.x + 30f, rect.y, 30f, EditorGUIUtility.singleLineHeight), "-") && spriteList.index >= 0 && spriteList.index < group.sprites.Count)
                    group.sprites.RemoveAt(spriteList.index);

                if (rect.width >= 220 && group is SideGroup side)
                    side.mode = (SideGroup.Choice)EditorGUI.EnumPopup(new Rect(rect.x + rect.width - 160f, rect.y, 110f, EditorGUIUtility.singleLineHeight), side.mode);

                if (rect.width >= 110 && GUI.Button(new Rect(rect.x + rect.width - 50f, rect.y, 50f, EditorGUIUtility.singleLineHeight), "Clear"))
                    group.sprites.Clear();
            };

            spriteList.DoLayoutList();
        }

        private ReorderableList GetList(List<Sprite> sprites)
        {
            if (!lists.TryGetValue(sprites, out var list))
            {
                list = new ReorderableList(sprites, typeof(Sprite), true, false, false, false);
                lists.Add(sprites, list);
            }
            return list;
        }
    }
}