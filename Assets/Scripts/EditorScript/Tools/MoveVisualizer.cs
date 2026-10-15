using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using Vectorier.Model;
using Vectorier.Component;
using Vectorier.ProjectManager;

namespace Vectorier.EditorScript.Tools
{
    public class MoveVisualizer : EditorWindow
    {
        Vector2 mainScrollPosition;

        // ================= ENUMS ================= //

        enum PlacementMode { Movement, Tricks, Custom }

        public enum MovementType
        {
            Jump,
            JumpOff,
            JumpOffFly,
            Run,
            RunFast,
            RunFastJump,
            RunFastJumpOff,
            RunFastLandingFall,
            Slide,
            SlideOff,
            FastSlide,
            FastSlideOff,
            DivingKongToFly,
            SpeedVaultToFly,
            CollisionToFly,
            FlyCollision
        }

        public enum TrickType { Wall_Hop_360 }

        // ================= CONSTANTS ================= //

        const string MOVEMENT_BASE_PATH = "Assets/Editor/Tools/MoveVisualizer/Movement";
        const string MODEL_XML_PATH = "Assets/Editor/Tools/MoveVisualizer/runner.xml";
        const string BLACK_MAT_PATH = "Assets/Editor/Material/Black.mat";

        // Paths relative to the active project folder (./Projects/{ActiveProject.Name}/)
        const string MOVES_XML_RELATIVE_PATH = "animations/moves.xml";
        const string BINS_RELATIVE_FOLDER = "animations/data";

        static readonly Dictionary<MovementType, string> MovementBins = new()
        {
            { MovementType.Jump, "fly.bin" },
            { MovementType.JumpOff, "jump_off.bin" },
            { MovementType.JumpOffFly, "jump_off_fly.bin" },
            { MovementType.Run, "run.bin" },
            { MovementType.RunFast, "run_fast_from_run.bin" },
            { MovementType.RunFastJump, "run_fast_fly.bin" },
            { MovementType.RunFastJumpOff, "run_fast_jump_off.bin" },
            { MovementType.RunFastLandingFall, "run_fast_landing_fall.bin" },
            { MovementType.Slide, "slide_simple.bin" },
            { MovementType.SlideOff, "slide_simple_and_fall.bin" },
            { MovementType.FastSlide, "fast_slide_simple.bin" },
            { MovementType.FastSlideOff, "fast_slide_simple_fall.bin" },
            { MovementType.DivingKongToFly, "diving_kong_to_fly.bin" },
            { MovementType.SpeedVaultToFly, "speed_vault_fly.bin" },
            { MovementType.CollisionToFly, "collision_to_fly.bin" },
            { MovementType.FlyCollision, "fly_collision.bin" }
        };

        static readonly string[] PivotNodePresets = { "NPivot", "DetectorH", "DetectorV" };

        // ================= UI STATE ================= //
        public static MoveVisualizer Instance { get; private set; }
        public static bool IsPlacementActive => Instance != null && Instance.placementEnabled;

        string pivotNodeName = "NPivot";

        PlacementMode currentPlacementMode = PlacementMode.Movement;
        MovementType currentMovementType;
        string currentTrickIdentifier;

        string binFolderPath;
        string binFileName;
        string customBinPath;

        bool placementEnabled;
        bool platformSnapHeld;

        bool renderModel = true;
        bool renderBlack = true;
        bool stayInPlace;
        bool reverse = false;
        bool isPlaced = false;
        Vector3 lastPlacedWorldPosition;

        AreaComponent selectedAreaComponent;

        // ================= RUNTIME DATA ================= //

        static Dictionary<string, string> discoveredTrickBins;

        ModelAnimation animation;
        ModelRenderer modelRenderer;
        ModelDebug modelDebug;
        Transform placementHostTransform;

        TrickMovePreviewData ResolveTrickMovePreviewData(AreaComponent areaComponent)
        {
            XDocument document = XDocument.Load(ResolveMovesXmlPath());
            XElement root = document.Root ?? throw new Exception("moves.xml has no root element.");

            XElement trickGroup = root
                .Element("ReactionGroups")?
                .Elements("ReactionGroup")
                .FirstOrDefault(group =>
                    string.Equals((string)group.Attribute("Name"), "TrickGroup", StringComparison.OrdinalIgnoreCase));

            if (trickGroup == null)
                throw new Exception("Could not find <ReactionGroup Name=\"TrickGroup\"> in moves.xml.");

            string[] candidateAreaNames = GetAreaNameCandidates(areaComponent);

            XElement trickReaction = trickGroup
                .Elements()
                .FirstOrDefault(element =>
                    AttributeMatchesAnyCandidate(element, "TriggerName", candidateAreaNames) ||
                    AttributeMatchesAnyCandidate(element, "AreaName", candidateAreaNames));

            if (trickReaction == null)
            {
                throw new Exception(
                    $"Could not find a TrickGroup entry with TriggerName or AreaName matching: {string.Join(", ", candidateAreaNames)}"
                );
            }

            string moveName = trickReaction.Name.LocalName;

            XElement moveElement = root
                .Element("Moves")?
                .Elements()
                .FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, moveName, StringComparison.OrdinalIgnoreCase));

            if (moveElement == null)
                throw new Exception($"Could not find <Moves><{moveName} ... /> in moves.xml.");

            string fileName = (string)moveElement.Attribute("FileName");

            if (string.IsNullOrWhiteSpace(fileName))
                throw new Exception($"Move '{moveName}' does not have a FileName attribute.");

            string pivotNode = (string)moveElement.Attribute("PivotNode");

            if (string.IsNullOrWhiteSpace(pivotNode))
                pivotNode = "NPivot";

            int firstFrame = 0;
            string firstFrameText = (string)trickReaction.Attribute("FirstFrame");

            if (!string.IsNullOrWhiteSpace(firstFrameText))
                int.TryParse(firstFrameText, out firstFrame);

            string binFullPath = ResolveProjectBinPath(fileName);

            return new TrickMovePreviewData
            {
                FirstFrame = firstFrame,
                FileName = fileName,
                PivotNode = pivotNode,
                BinFullPath = binFullPath,
                WorldPosition = GetAreaPreviewPosePlacementPosition(areaComponent, pivotNode)
            };
        }

        static bool AttributeMatchesAnyCandidate(XElement element, string attributeName, string[] candidates)
        {
            string value = (string)element.Attribute(attributeName);

            if (string.IsNullOrWhiteSpace(value))
                return false;

            return candidates.Any(candidate =>
                string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
        }

        static string[] GetAreaNameCandidates(AreaComponent areaComponent)
        {
            List<string> candidates = new();

            if (areaComponent == null)
                return candidates.ToArray();

            if (areaComponent.gameObject != null)
                candidates.Add(areaComponent.gameObject.name);

            if (areaComponent.transform.parent != null)
                candidates.Add(areaComponent.transform.parent.gameObject.name);

            return candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        Vector3 GetAreaPreviewPosePlacementPosition(AreaComponent areaComponent, string pivotNode)
        {
            SpriteRenderer spriteRenderer = areaComponent.GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
                spriteRenderer = areaComponent.GetComponentInChildren<SpriteRenderer>();

            if (spriteRenderer == null)
                spriteRenderer = areaComponent.GetComponentInParent<SpriteRenderer>();

            if (spriteRenderer == null)
                throw new Exception($"No SpriteRenderer found for AreaComponent '{areaComponent.name}'.");

            animation.EnsureModelLoadedForPreview(ResolveXmlPath());

            if (animation.Model == null)
                throw new Exception($"Could not load model XML: {ResolveXmlPath()}");

            if (animation.State.PreviewPose == null || animation.State.PreviewPose.Length == 0)
                throw new Exception("Model preview pose is empty.");

            if (!animation.Model.TryGetNodeIndex(pivotNode, out int pivotIndex))
                throw new Exception($"PivotNode '{pivotNode}' was not found in the model XML.");

            if (!animation.Model.TryGetNodeIndex("COM", out int comIndex))
                throw new Exception("Node 'COM' was not found in the model XML.");

            Bounds bounds = spriteRenderer.bounds;

            Vector3 pivotPosePosition = animation.State.PreviewPose[pivotIndex];
            Vector3 comPosePosition = animation.State.PreviewPose[comIndex];

            const float finalXOffset = 20f;

            float comToPivotX = comPosePosition.x - pivotPosePosition.x;

            float targetPivotX = reverse
                ? bounds.min.x - finalXOffset - comToPivotX
                : bounds.max.x + finalXOffset - comToPivotX;

            float targetPivotY = bounds.min.y;

            return new Vector3(
                targetPivotX,
                targetPivotY,
                bounds.center.z
            );
        }

        // ./Projects/{active}/animations/moves.xml (falls back to the Vector project if missing)
        static string ResolveMovesXmlPath()
        {
            string path = ActiveProject.ResolveFile(MOVES_XML_RELATIVE_PATH);

            if (path == null)
                throw new FileNotFoundException($"moves.xml not found: {ActiveProject.GetPath(MOVES_XML_RELATIVE_PATH)}");

            return Path.GetFullPath(path);
        }

        // ./Projects/{active}/animations/data/{fileName} (falls back to the Vector project if missing)
        static string ResolveProjectBinPath(string fileName)
        {
            string relativePath = $"{BINS_RELATIVE_FOLDER}/{fileName}";
            string path = ActiveProject.ResolveFile(relativePath);

            if (path == null)
                throw new FileNotFoundException($"Animation bin not found: {ActiveProject.GetPath(relativePath)}");

            return Path.GetFullPath(path);
        }

        public static List<Vector3> GetDetectorSnapPoints()
        {
            List<Vector3> points = new List<Vector3>();

            // Check if the window is open, an animation is placed, and Render Edges is enabled
            if (Instance == null || !Instance.isPlaced || Instance.modelDebug == null || !Instance.modelDebug.RenderEdges || Instance.animation == null)
                return points;

            Vector3? detH = Instance.animation.GetAnimationNodeWorldPosition("DetectorH");
            if (detH.HasValue)
                points.Add(detH.Value);

            Vector3? detV = Instance.animation.GetAnimationNodeWorldPosition("DetectorV");
            if (detV.HasValue)
                points.Add(detV.Value);

            return points;
        }

        // ================= WINDOW ================= //

        [MenuItem("Vectorier/Tools/▶  Move Visualizer", false, 25)]
        public static MoveVisualizer OpenWindow()
        {
            var window = GetWindow<MoveVisualizer>("Move Visualizer");
            window.Show();
            return window;
        }

        public static void OpenAndPreviewAreaComponent(AreaComponent areaComponent)
        {
            MoveVisualizer window = OpenWindow();
            window.PreviewAreaComponent(areaComponent);
        }

        public static void OpenAndPreviewSpawnComponent(SpawnComponent spawnComponent)
        {
            MoveVisualizer window = OpenWindow();
            window.PreviewSpawnComponent(spawnComponent);
        }

        public void PreviewSpawnComponent(SpawnComponent spawnComponent)
        {
            if (spawnComponent == null)
            {
                Debug.LogWarning("Preview Animation failed: SpawnComponent is null.");
                return;
            }

            try
            {
                animation.Reverse = reverse;
                SpawnMovePreviewData previewData = ResolveSpawnMovePreviewData(spawnComponent);

                lastPlacedWorldPosition = previewData.WorldPosition;
                isPlaced = true;

                modelDebug?.Destroy();
                modelRenderer?.Destroy();

                placementEnabled = false;

                placementHostTransform = spawnComponent.transform.parent != null
                    ? spawnComponent.transform.parent
                    : spawnComponent.transform;

                animation.StartFrame = previewData.FirstFrame;
                animation.PlaceAt(
                    previewData.WorldPosition,
                    placementHostTransform,
                    ResolveXmlPath(),
                    previewData.BinFullPath,
                    previewData.PivotNode,
                    false
                );

                pivotNodeName = previewData.PivotNode;
                stayInPlace = false;

                if (renderModel && animation.Model != null)
                {
                    modelRenderer?.Create(animation.Model, animation.AnimationNodes, placementHostTransform, renderBlack);
                    modelDebug?.AttachToModel(animation.Model, modelRenderer?.RootObject);
                }
                else
                {
                    modelDebug?.AttachToModel(animation.Model, null);
                }

                SyncAnimationSpaceTransform();
                modelDebug?.UpdateNodeWorldPositions(animation.AnimationNodes, GetPlaybackParentTransform());
                modelDebug?.FollowSceneViewCameraToNodeXY(animation);

                Repaint();
                SceneView.RepaintAll();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Preview Animation failed for '{spawnComponent.name}': {exception.Message}");
            }
        }

        SpawnMovePreviewData ResolveSpawnMovePreviewData(SpawnComponent spawnComponent)
        {
            if (string.IsNullOrWhiteSpace(spawnComponent.Animation))
                throw new Exception("Animation string is empty.");

            // Parse "AnimationName|StartFrame"
            string[] parts = spawnComponent.Animation.Split('|');
            string animationName = parts[0];
            int startFrame = 0;

            if (parts.Length > 1)
                int.TryParse(parts[1], out startFrame);

            string binFullPath;
            string pivotNode = "NPivot";
            string fileName = "";

            // Handle specific overrides (these still come from the Editor Movement folder)
            if (animationName.Equals("JumpOff", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "jump_off_fly.bin";
                binFullPath = ModelAnimation.ResolveFullPath($"{MOVEMENT_BASE_PATH}/jump_off_fly.bin");
                startFrame = 0;
            }
            else if (animationName.Equals("Run", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "run.bin";
                binFullPath = ModelAnimation.ResolveFullPath($"{MOVEMENT_BASE_PATH}/run.bin");
                startFrame = 0;
            }
            else
            {
                // General Case: Read from the active project's moves.xml
                XDocument document = XDocument.Load(ResolveMovesXmlPath());
                XElement root = document.Root ?? throw new Exception("moves.xml has no root element.");

                XElement moveElement = root
                    .Element("Moves")?
                    .Elements()
                    .FirstOrDefault(element =>
                        string.Equals(element.Name.LocalName, animationName, StringComparison.OrdinalIgnoreCase));

                if (moveElement == null)
                    throw new Exception($"Could not find <Moves><{animationName} ... /> in moves.xml.");

                fileName = (string)moveElement.Attribute("FileName");

                if (string.IsNullOrWhiteSpace(fileName))
                    throw new Exception($"Move '{animationName}' does not have a FileName attribute.");

                string attrPivot = (string)moveElement.Attribute("PivotNode");
                if (!string.IsNullOrWhiteSpace(attrPivot))
                    pivotNode = attrPivot;

                // Load from the active project's animations/data folder
                binFullPath = ResolveProjectBinPath(fileName);
            }

            if (!File.Exists(binFullPath))
                throw new FileNotFoundException($"Animation bin not found: {binFullPath}");

            return new SpawnMovePreviewData
            {
                FirstFrame = startFrame,
                FileName = fileName,
                PivotNode = pivotNode,
                BinFullPath = binFullPath,
                WorldPosition = spawnComponent.transform.position // Place right at the spawn component's location
            };
        }

        Transform GetPreviewParentTransform()
        {
            return null;
        }

        Transform GetPlaybackParentTransform()
        {
            return placementHostTransform;
        }

        void SyncAnimationSpaceTransform()
        {
            animation?.SetAnimationSpaceTransform(GetPlaybackParentTransform());
        }

        void OnEnable()
        {
            Instance = this;

            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += UpdatePlayback;
            ActiveProject.Changed += OnActiveProjectChanged;

            // Re-scan tricks in case the files changed while the window was closed
            discoveredTrickBins = null;

            animation ??= new ModelAnimation();
            modelRenderer ??= new ModelRenderer(BLACK_MAT_PATH);
            modelDebug ??= new ModelDebug();

            ResetAll();
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;

            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= UpdatePlayback;
            ActiveProject.Changed -= OnActiveProjectChanged;

            ResetAll();

            modelDebug?.Dispose();
            modelDebug = null;

            modelRenderer?.Dispose();
            modelRenderer = null;
        }

        void OnInspectorUpdate() => Repaint();

        void OnActiveProjectChanged(string _)
        {
            // Trick list comes from the active project, so rebuild it on switch
            discoveredTrickBins = null;
            Repaint();
        }

        // ================= UI ================= //

        void OnGUI()
        {
            RefreshSelectedAreaComponent();

            bool hasSelectedTrickArea = selectedAreaComponent != null &&
                                        selectedAreaComponent.Type == AreaComponent.AreaType.Trick;

            mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);

            GUILayout.Space(5);
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            renderModel = EditorGUILayout.Toggle("Render Model", renderModel);

            if (renderModel)
            {
                EditorGUI.indentLevel++;
                renderBlack = EditorGUILayout.Toggle("Render Black", renderBlack);
                EditorGUI.indentLevel--;
            }

            if (modelRenderer != null && modelRenderer.RootObject != null)
                modelRenderer.ApplyRenderSettings(renderBlack);

            modelDebug.RenderEdges = EditorGUILayout.Toggle("Render Edges", modelDebug.RenderEdges);
            modelDebug.RenderTrajectory = EditorGUILayout.Toggle("Render Trajectory", modelDebug.RenderTrajectory);
            modelDebug.RenderDetector = EditorGUILayout.Toggle("Render Detector", modelDebug.RenderDetector);
            if (modelDebug.RenderDetector)
            {
                EditorGUI.indentLevel++;
                modelDebug.DeltaDetectorH = EditorGUILayout.IntField("Detector H", modelDebug.DeltaDetectorH);
                modelDebug.DeltaDetectorV = EditorGUILayout.IntField("Detector V", modelDebug.DeltaDetectorV);
                EditorGUI.indentLevel--;
            }
            modelDebug.FollowCamera = EditorGUILayout.Toggle("Follow Camera", modelDebug.FollowCamera);
            bool prevReverse = reverse;
            reverse = EditorGUILayout.Toggle("Reverse", reverse);
            if (prevReverse != reverse)
            {
                animation.Reverse = reverse;
                if (isPlaced)
                {
                    // Instantly re-place the animation at the tracked location to reflect the reversed axis
                    if (hasSelectedTrickArea)
                        PreviewAreaComponent(selectedAreaComponent);
                    else
                        PlaceAt(lastPlacedWorldPosition);
                }
            }

            GUILayout.Space(10);

            if (!hasSelectedTrickArea)
            {
                currentPlacementMode = (PlacementMode)EditorGUILayout.EnumPopup("Placement Mode", currentPlacementMode);

                DrawPlacementModeUI();

                DrawPivotNodeField();
                stayInPlace = EditorGUILayout.Toggle("Stay In Place", stayInPlace);
            }
            else
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Selected AreaComponent", selectedAreaComponent.name);
                    EditorGUILayout.LabelField("Preview Source", "moves.xml > ReactionGroups > TrickGroup");
                }
            }

            GUILayout.Space(12);

            string mainButtonName = hasSelectedTrickArea
                ? "Preview Animation"
                : (placementEnabled ? "Stop Placement" : "Start Placement");

            if (GUILayout.Button(mainButtonName, GUILayout.Height(60)))
            {
                if (hasSelectedTrickArea)
                {
                    PreviewAreaComponent(selectedAreaComponent);
                }
                else
                {
                    placementEnabled = !placementEnabled;
                    platformSnapHeld = false;

                    if (!placementEnabled)
                    {
                        animation.ClearPreview();
                        SceneView.RepaintAll();
                    }
                }
            }

            if (GUILayout.Button("Clear", GUILayout.Height(40)))
            {
                ResetAll();
                modelDebug?.Destroy();
                modelRenderer?.Destroy();
            }

            GUILayout.Space(10);

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(animation.IsPlaying ? "Pause" : "Play", GUILayout.Height(30)))
                    animation.IsPlaying = !animation.IsPlaying;

                if (GUILayout.Button("Restart", GUILayout.Height(30)))
                {
                    animation.CurrentFrameIndex = animation.StartFrame;
                    animation.ApplyFrame(animation.StartFrame);

                    if (renderModel)
                        modelRenderer?.UpdateCapsules(animation.AnimationNodes);

                    SyncAnimationSpaceTransform();
                    modelDebug?.UpdateNodeWorldPositions(animation.AnimationNodes, GetPlaybackParentTransform());
                    modelDebug?.FollowSceneViewCameraToNodeXY(animation);
                }
            }

            GUI.enabled = animation.AnimationFrames.Count > 0;

            int maxFrame = Mathf.Max(0, animation.AnimationFrames.Count - 1);

            animation.StartFrame = Mathf.Clamp(animation.StartFrame, 0, maxFrame);

            if (animation.EndFrame > maxFrame || animation.EndFrame == int.MaxValue)
                animation.EndFrame = maxFrame;

            if (animation.EndFrame < animation.StartFrame)
                animation.EndFrame = animation.StartFrame;

            int newFrame = EditorGUILayout.IntSlider(
                "Frame",
                animation.CurrentFrameIndex,
                animation.StartFrame,
                animation.EndFrame
            );

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                animation.StartFrame = EditorGUILayout.IntField("Start", animation.StartFrame);
                animation.EndFrame = EditorGUILayout.IntField("End", animation.EndFrame);
            }

            if (newFrame != animation.CurrentFrameIndex)
            {
                animation.IsPlaying = false;
                animation.CurrentFrameIndex = newFrame;
                animation.ApplyFrame(animation.CurrentFrameIndex);

                if (renderModel)
                    modelRenderer?.UpdateCapsules(animation.AnimationNodes);

                SyncAnimationSpaceTransform();
                modelDebug?.UpdateNodeWorldPositions(animation.AnimationNodes, GetPlaybackParentTransform());
                modelDebug?.FollowSceneViewCameraToNodeXY(animation);
            }

            GUI.enabled = true;

            EditorGUILayout.EndScrollView();

            if (!renderModel && modelRenderer != null && modelRenderer.RootObject != null)
            {
                modelDebug?.Destroy();
                modelRenderer.Destroy();
            }
        }

        void DrawPlacementModeUI()
        {
            switch (currentPlacementMode)
            {
                case PlacementMode.Custom:
                    using (new GUILayout.HorizontalScope())
                    {
                        customBinPath = EditorGUILayout.TextField("Bin Path", customBinPath);

                        if (GUILayout.Button("...", GUILayout.Width(30)))
                        {
                            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                            string selected = EditorUtility.OpenFilePanel("Select animation bin", projectRoot, "bin,bytes");
                            if (!string.IsNullOrEmpty(selected))
                                customBinPath = selected;
                        }
                    }

                    binFolderPath = null;
                    binFileName = null;
                    break;

                case PlacementMode.Movement:
                    currentMovementType = (MovementType)EditorGUILayout.EnumPopup("Movement Type", currentMovementType);
                    binFolderPath = MOVEMENT_BASE_PATH;
                    binFileName = MovementBins[currentMovementType];
                    break;

                case PlacementMode.Tricks:
                    DrawTrickSelectionUI();
                    break;
            }
        }

        void DrawPivotNodeField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Pivot Node");

                Rect dropdownRect = GUILayoutUtility.GetRect(20, 18, GUILayout.Width(20));
                if (EditorGUI.DropdownButton(dropdownRect, GUIContent.none, FocusType.Passive))
                {
                    GenericMenu menu = new GenericMenu();

                    foreach (string preset in PivotNodePresets)
                    {
                        string value = preset;
                        menu.AddItem(new GUIContent(value), pivotNodeName == value, () =>
                        {
                            pivotNodeName = value;
                            GUI.FocusControl(null); // drop text field focus so the new value shows
                            Repaint();
                        });
                    }

                    menu.DropDown(dropdownRect);
                }

                pivotNodeName = EditorGUILayout.TextField(pivotNodeName);
            }
        }

        void DrawTrickSelectionUI()
        {
            // The actual folder is resolved per file in PlaceAt (active project or Vector fallback)
            binFolderPath = null;

            if (discoveredTrickBins == null)
                DiscoverTricks();

            if (discoveredTrickBins == null || discoveredTrickBins.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No trick .bin files found. Tricks are read from the TrickGroup in moves.xml and loaded from '{BINS_RELATIVE_FOLDER}'.",
                    MessageType.Warning);

                if (GUILayout.Button("Refresh"))
                    discoveredTrickBins = null;

                return;
            }

            if (string.IsNullOrEmpty(currentTrickIdentifier) || !discoveredTrickBins.ContainsKey(currentTrickIdentifier))
                currentTrickIdentifier = discoveredTrickBins.Keys.First();

            EditorGUILayout.LabelField("Trick", currentTrickIdentifier);

            if (GUILayout.Button("Select Trick...", GUILayout.Height(30)))
            {
                TrickSelectionWindow.Open(
                    discoveredTrickBins,
                    currentTrickIdentifier,
                    identifier =>
                    {
                        currentTrickIdentifier = identifier;
                        binFileName = discoveredTrickBins[identifier];
                    }
                );
            }

            binFileName = discoveredTrickBins[currentTrickIdentifier];
        }

        void RefreshSelectedAreaComponent()
        {
            selectedAreaComponent = null;

            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject == null)
                return;

            selectedAreaComponent = selectedObject.GetComponent<AreaComponent>();

            if (selectedAreaComponent == null)
            {
                AreaComponent[] childComponents = selectedObject.GetComponentsInChildren<AreaComponent>(true);

                foreach (var comp in childComponents)
                {
                    GameObject childObj = comp.gameObject;

                    if (childObj != selectedObject &&
                        childObj.name == selectedObject.name &&
                        childObj.CompareTag("Area"))
                    {
                        selectedAreaComponent = comp;
                        break;
                    }
                }
            }
        }

        public void PreviewAreaComponent(AreaComponent areaComponent)
        {
            if (areaComponent == null)
            {
                Debug.LogWarning("Preview Animation failed: AreaComponent is null.");
                return;
            }

            if (areaComponent.Type != AreaComponent.AreaType.Trick)
            {
                Debug.LogWarning("Preview Animation is only available when AreaComponent Type is Trick.");
                return;
            }

            try
            {
                animation.Reverse = reverse;
                TrickMovePreviewData previewData = ResolveTrickMovePreviewData(areaComponent);

                lastPlacedWorldPosition = previewData.WorldPosition;
                isPlaced = true;

                modelDebug?.Destroy();
                modelRenderer?.Destroy();

                placementEnabled = false;

                placementHostTransform = areaComponent.transform.parent != null
                    ? areaComponent.transform.parent
                    : areaComponent.transform;

                animation.StartFrame = previewData.FirstFrame;
                animation.PlaceAt(
                    previewData.WorldPosition,
                    placementHostTransform,
                    ResolveXmlPath(),
                    previewData.BinFullPath,
                    previewData.PivotNode,
                    false
                );

                pivotNodeName = previewData.PivotNode;
                stayInPlace = false;

                if (renderModel && animation.Model != null)
                {
                    modelRenderer?.Create(animation.Model, animation.AnimationNodes, placementHostTransform, renderBlack);
                    modelDebug?.AttachToModel(animation.Model, modelRenderer?.RootObject);
                }
                else
                {
                    modelDebug?.AttachToModel(animation.Model, null);
                }

                SyncAnimationSpaceTransform();
                modelDebug?.UpdateNodeWorldPositions(animation.AnimationNodes, GetPlaybackParentTransform());
                modelDebug?.FollowSceneViewCameraToNodeXY(animation);

                Repaint();
                SceneView.RepaintAll();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Preview Animation failed for '{areaComponent.name}': {exception.Message}");
            }
        }

        // ================= SCENE ================= //

        void OnSceneGUI(SceneView sceneView)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            if (placementEnabled)
                HandlePlacementInput();

            modelDebug?.DrawScene(animation, GetPlaybackParentTransform(), renderModel);
        }

        void HandlePlacementInput()
        {
            Event e = Event.current;

            // Hold C to stick the pivot to platforms
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.C)
            {
                platformSnapHeld = true;
                e.Use();
            }
            else if (e.type == EventType.KeyUp && e.keyCode == KeyCode.C)
            {
                platformSnapHeld = false;
                e.Use();
            }

            animation.EnsureModelLoadedForPreview(ResolveXmlPath());

            Transform parentTransform = GetPreviewParentTransform();
            Vector3 cursorWorldPosition = GetCursorWorldPositionOnZPlane();
            Vector3 targetPosition = cursorWorldPosition;

            if (platformSnapHeld &&
                TransformSnap.TryGetPlatformSnapPoint(cursorWorldPosition, out Vector3 snapped, out bool isCorner))
            {
                targetPosition = new Vector3(snapped.x, snapped.y, cursorWorldPosition.z);

                Color prev = Handles.color;
                Handles.color = isCorner ? Color.yellow : Color.cyan;
                Handles.DrawLine(cursorWorldPosition, targetPosition, 2f);
                Handles.DrawSolidDisc(targetPosition, Vector3.forward, HandleUtility.GetHandleSize(targetPosition) * 0.06f);
                Handles.color = prev;
            }

            animation.UpdatePreview(targetPosition, parentTransform, pivotNodeName);

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlaceAt(targetPosition);
                animation.ClearPreview();
                e.Use();
            }

            SceneView.RepaintAll();
        }

        static Vector3 GetCursorWorldPositionOnZPlane()
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            Vector3 world = ray.origin + ray.direction * 10f;
            world.z = 0f;
            return world;
        }

        // ================= CORE ================= //

        void PlaceAt(Vector3 worldPosition)
        {
            lastPlacedWorldPosition = worldPosition;
            isPlaced = true;

            modelDebug?.Destroy();
            modelRenderer?.Destroy();

            string xmlPath = ResolveXmlPath();

            // Trick bins live in the active project's animations/data folder
            if (currentPlacementMode == PlacementMode.Tricks && !string.IsNullOrEmpty(binFileName))
                binFolderPath = Path.GetDirectoryName(ResolveProjectBinPath(binFileName));

            string binPath = ModelAnimation.ResolveBinFullPathOrThrow(
                currentPlacementMode == PlacementMode.Custom,
                customBinPath,
                binFolderPath,
                binFileName);

            placementHostTransform = null;

            animation.PlaceAt(worldPosition, null, xmlPath, binPath, pivotNodeName, stayInPlace);

            if (renderModel && animation.Model != null)
            {
                modelRenderer?.Create(animation.Model, animation.AnimationNodes, null, renderBlack);
                modelDebug?.AttachToModel(animation.Model, modelRenderer?.RootObject);
            }
            else
            {
                modelDebug?.AttachToModel(animation.Model, null);
            }

            SyncAnimationSpaceTransform();
            modelDebug?.UpdateNodeWorldPositions(animation.AnimationNodes, null);
            modelDebug?.FollowSceneViewCameraToNodeXY(animation);
        }

        void UpdatePlayback()
        {
            if (!animation.TryAdvancePlayback())
                return;

            if (renderModel)
            {
                if (modelRenderer != null && modelRenderer.RootObject == null && animation.Model != null)
                {
                    modelRenderer.Create(animation.Model, animation.AnimationNodes, placementHostTransform, renderBlack);
                    modelDebug?.AttachToModel(animation.Model, modelRenderer.RootObject);
                }
                else
                {
                    modelRenderer?.UpdateCapsules(animation.AnimationNodes);
                }
            }

            SyncAnimationSpaceTransform();
            modelDebug?.UpdateNodeWorldPositions(animation.AnimationNodes, GetPlaybackParentTransform());
            modelDebug?.FollowSceneViewCameraToNodeXY(animation);

            Repaint();
            SceneView.RepaintAll();
        }

        void ResetAll()
        {
            isPlaced = false;
            placementHostTransform = null;
            animation?.ResetAll();
            if (animation != null)
                animation.Reverse = reverse;
        }

        static string ResolveXmlPath()
        {
            return ModelAnimation.ResolveFullPath(MODEL_XML_PATH);
        }

        // ================= TRICKS ================= //

        // animations/data holds every move's bin, so the trick list comes from the
        // TrickGroup in moves.xml, mapped to each move's FileName.
        static void DiscoverTricks()
        {
            discoveredTrickBins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            XElement root;

            try
            {
                root = XDocument.Load(ResolveMovesXmlPath()).Root;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not read tricks from moves.xml: {e.Message}");
                return;
            }

            XElement trickGroup = root?
                .Element("ReactionGroups")?
                .Elements("ReactionGroup")
                .FirstOrDefault(group =>
                    string.Equals((string)group.Attribute("Name"), "TrickGroup", StringComparison.OrdinalIgnoreCase));

            XElement moves = root?.Element("Moves");

            if (trickGroup == null || moves == null)
                return;

            foreach (XElement reaction in trickGroup.Elements())
            {
                string moveName = reaction.Name.LocalName;

                XElement moveElement = moves
                    .Elements()
                    .FirstOrDefault(element =>
                        string.Equals(element.Name.LocalName, moveName, StringComparison.OrdinalIgnoreCase));

                string fileName = (string)moveElement?.Attribute("FileName");

                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                // Skip tricks whose bin doesn't exist in the active project or Vector
                if (ActiveProject.ResolveFile($"{BINS_RELATIVE_FOLDER}/{fileName}") == null)
                    continue;

                string identifier =
                    fileName.Equals("360_wall_hop.bin", StringComparison.OrdinalIgnoreCase)
                        ? nameof(TrickType.Wall_Hop_360)
                        : Path.GetFileNameWithoutExtension(fileName);

                discoveredTrickBins[identifier] = fileName;
            }
        }
    }

    sealed class TrickMovePreviewData
    {
        public int FirstFrame;
        public string FileName;
        public string PivotNode;
        public string BinFullPath;
        public Vector3 WorldPosition;

    }

    sealed class SpawnMovePreviewData
    {
        public int FirstFrame;
        public string FileName;
        public string PivotNode;
        public string BinFullPath;
        public Vector3 WorldPosition;
    }

    // ================= TRICK SELECTION WINDOW ================= //

    class TrickSelectionWindow : EditorWindow
    {
        // Relative to the active project folder (./Projects/{ActiveProject.Name}/)
        const string TRICK_ICONS_RELATIVE_FOLDER = "icons/tricks";

        Action<string> onSelected;
        Dictionary<string, string> availableTricks;

        // Images live outside Assets/, so they are loaded from disk and cached here
        readonly Dictionary<string, Texture2D> imageCache = new(StringComparer.OrdinalIgnoreCase);

        static readonly Vector2 WINDOW_SIZE = new(400, 300);
        Vector2 scrollPosition;

        public static void Open(Dictionary<string, string> tricks, string current, Action<string> onSelected)
        {
            var window = CreateInstance<TrickSelectionWindow>();
            window.availableTricks = tricks;
            window.onSelected = onSelected;
            window.titleContent = new GUIContent("Select Trick");
            window.minSize = WINDOW_SIZE;
            window.ShowUtility();
        }

        void OnDestroy()
        {
            foreach (Texture2D texture in imageCache.Values)
            {
                if (texture != null)
                    DestroyImmediate(texture);
            }

            imageCache.Clear();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Available Tricks", EditorStyles.boldLabel);
            GUILayout.Space(8);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, true, true);

            foreach (var pair in availableTricks)
            {
                string identifier = pair.Key;
                string binFile = pair.Value;
                string displayName = FormatTrickName(binFile);

                string imageName = "TRACK_TRICK_" + Path.GetFileNameWithoutExtension(binFile).Replace("_", string.Empty).ToUpperInvariant();
                Texture2D previewTexture = LoadTrickImage(imageName);

                using (new GUILayout.HorizontalScope(GUILayout.Height(64)))
                {
                    if (previewTexture != null)
                        GUILayout.Label(previewTexture, GUILayout.Width(64), GUILayout.Height(64));
                    else
                        GUILayout.Box("No Image", GUILayout.Width(64), GUILayout.Height(64));

                    if (GUILayout.Button(displayName, GUILayout.Height(64), GUILayout.MinWidth(200)))
                    {
                        onSelected?.Invoke(identifier);
                        Close();
                    }
                }

                GUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();
        }

        // ./Projects/{active}/icons/tricks/{imageName}.png (falls back to the Vector project if missing)
        Texture2D LoadTrickImage(string imageName)
        {
            if (imageCache.TryGetValue(imageName, out Texture2D cached))
                return cached;

            Texture2D texture = null;
            string path = ActiveProject.ResolveFile($"{TRICK_ICONS_RELATIVE_FOLDER}/{imageName}.png");

            if (path != null)
            {
                try
                {
                    texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };

                    if (!texture.LoadImage(File.ReadAllBytes(path)))
                    {
                        DestroyImmediate(texture);
                        texture = null;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load trick image '{path}': {e.Message}");
                    if (texture != null) DestroyImmediate(texture);
                    texture = null;
                }
            }

            // Cache misses too, so missing images aren't re-checked every repaint
            imageCache[imageName] = texture;
            return texture;
        }

        static string FormatTrickName(string binFileName)
        {
            string[] parts = Path.GetFileNameWithoutExtension(binFileName).Split('_', StringSplitOptions.RemoveEmptyEntries);

            for (int index = 0; index < parts.Length; index++)
                parts[index] = char.ToUpperInvariant(parts[index][0]) + parts[index][1..].ToLowerInvariant();

            return string.Join(" ", parts);
        }
    }
}