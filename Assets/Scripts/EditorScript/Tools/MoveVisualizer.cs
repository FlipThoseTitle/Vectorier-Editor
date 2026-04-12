using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Vectorier.Model;

namespace Vectorier.EditorScript.Tools
{
    public class MoveVisualizer : EditorWindow
    {
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
        const string TRICKS_BASE_PATH = "Assets/Editor/Tools/MoveVisualizer/Tricks";
        const string DEFAULT_MODEL_XML_PATH = "Assets/Editor/Tools/MoveVisualizer/runner.xml";
        const string BLACK_MAT_PATH = "Assets/Editor/Material/Black.mat";

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

        // ================= UI STATE ================= //

        string pivotNodeName = "NPivot";

        PlacementMode currentPlacementMode = PlacementMode.Movement;
        MovementType currentMovementType;
        string currentTrickIdentifier;

        string binFolderPath;
        string binFileName;
        string customBinPath;

        bool placementEnabled;

        string modelXmlPath = DEFAULT_MODEL_XML_PATH;
        bool renderModel = true;
        bool renderBlack = true;
        bool stayInPlace;

        // ================= RUNTIME DATA ================= //

        static Dictionary<string, string> discoveredTrickBins;

        ModelAnimation animation;
        ModelRenderer modelRenderer;
        ModelDebug modelDebug;
        Transform placementHostTransform;

        // ================= WINDOW ================= //

        [MenuItem("Vectorier/Tools/Move Visualizer", false, 34)]
        static void OpenWindow() => GetWindow<MoveVisualizer>("Move Visualizer");

        Transform GetPreviewParentTransform()
        {
            return Selection.activeTransform;
        }

        Transform GetPlaybackParentTransform()
        {
            if (modelRenderer != null && modelRenderer.RootObject != null)
                return modelRenderer.RootObject.transform;

            return placementHostTransform;
        }

        void SyncAnimationSpaceTransform()
        {
            animation?.SetAnimationSpaceTransform(GetPlaybackParentTransform());
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += UpdatePlayback;

            animation ??= new ModelAnimation();
            modelRenderer ??= new ModelRenderer(BLACK_MAT_PATH);
            modelDebug ??= new ModelDebug();

            ResetAll();
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= UpdatePlayback;

            ResetAll();

            modelDebug?.Dispose();
            modelDebug = null;

            modelRenderer?.Dispose();
            modelRenderer = null;
        }

        void OnInspectorUpdate() => Repaint();

        // ================= UI ================= //

        void OnGUI()
        {
            GUILayout.Space(5);
            EditorGUILayout.LabelField("Model XML", EditorStyles.boldLabel);
            modelXmlPath = EditorGUILayout.TextField("XML Path", modelXmlPath);
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
            modelDebug.FollowCamera = EditorGUILayout.Toggle("Follow Camera", modelDebug.FollowCamera);

            modelDebug.RenderDetector = EditorGUILayout.Toggle("Render Detector", modelDebug.RenderDetector);

            if (modelDebug.RenderDetector)
            {
                EditorGUI.indentLevel++;
                modelDebug.DeltaDetectorH = EditorGUILayout.IntField("Detector H", modelDebug.DeltaDetectorH);
                modelDebug.DeltaDetectorV = EditorGUILayout.IntField("Detector V", modelDebug.DeltaDetectorV);
                EditorGUI.indentLevel--;
            }
            GUILayout.Space(10);
            currentPlacementMode = (PlacementMode)EditorGUILayout.EnumPopup("Placement Mode", currentPlacementMode);

            DrawPlacementModeUI();

            pivotNodeName = EditorGUILayout.TextField("Pivot Node", pivotNodeName);
            stayInPlace = EditorGUILayout.Toggle("Stay In Place", stayInPlace);

            GUILayout.Space(12);

            if (GUILayout.Button(placementEnabled ? "Stop Placement" : "Start Placement", GUILayout.Height(60)))
                placementEnabled = !placementEnabled;

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

        void DrawTrickSelectionUI()
        {
            binFolderPath = TRICKS_BASE_PATH;

            if (discoveredTrickBins == null)
                DiscoverTricks(binFolderPath);

            if (discoveredTrickBins == null || discoveredTrickBins.Count == 0)
            {
                EditorGUILayout.HelpBox("No trick .bin files found in the Tricks folder.", MessageType.Warning);
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
            animation.EnsureModelLoadedForPreview(ResolveXmlPath());

            Transform parentTransform = GetPreviewParentTransform();
            Vector3 cursorWorldPosition = GetCursorWorldPositionOnZPlane();

            animation.UpdatePreview(cursorWorldPosition, parentTransform, pivotNodeName);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                PlaceAt(cursorWorldPosition);
                animation.ClearPreview();
                Event.current.Use();
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
            modelDebug?.Destroy();
            modelRenderer?.Destroy();

            string xmlPath = ResolveXmlPath();
            string binPath = ModelAnimation.ResolveBinFullPathOrThrow(
                currentPlacementMode == PlacementMode.Custom,
                customBinPath,
                binFolderPath,
                binFileName);

            placementHostTransform = GetPreviewParentTransform();

            animation.PlaceAt(worldPosition, placementHostTransform, xmlPath, binPath, pivotNodeName, stayInPlace);

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
            placementHostTransform = null;
            animation?.ResetAll();
        }

        string ResolveXmlPath()
        {
            string p = string.IsNullOrWhiteSpace(modelXmlPath) ? DEFAULT_MODEL_XML_PATH : modelXmlPath;
            return ModelAnimation.ResolveFullPath(p);
        }

        // ================= TRICKS ================= //

        static void DiscoverTricks(string folderPath)
        {
            discoveredTrickBins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(folderPath))
                return;

            foreach (string filePath in Directory.GetFiles(folderPath, "*.bin"))
            {
                string fileName = Path.GetFileName(filePath);

                string identifier =
                    fileName.Equals("360_wall_hop.bin", StringComparison.OrdinalIgnoreCase)
                        ? nameof(TrickType.Wall_Hop_360)
                        : Path.GetFileNameWithoutExtension(fileName);

                discoveredTrickBins[identifier] = fileName;
            }
        }
    }

    // ================= TRICK SELECTION WINDOW ================= //

    class TrickSelectionWindow : EditorWindow
    {
        Action<string> onSelected;
        Dictionary<string, string> availableTricks;

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
                string imagePath = Path.Combine("Assets/Editor/Tools/MoveVisualizer/Image", imageName + ".png");
                Texture2D previewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);

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

        static string FormatTrickName(string binFileName)
        {
            string[] parts = Path.GetFileNameWithoutExtension(binFileName).Split('_', StringSplitOptions.RemoveEmptyEntries);

            for (int index = 0; index < parts.Length; index++)
                parts[index] = char.ToUpperInvariant(parts[index][0]) + parts[index][1..].ToLowerInvariant();

            return string.Join(" ", parts);
        }
    }
}