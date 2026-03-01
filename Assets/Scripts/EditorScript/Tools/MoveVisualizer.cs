using UnityEditor;
using UnityEngine;

using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Globalization;
using System.Collections.Generic;

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

        const float Z_OFFSET = -300f;
        const float SOURCE_FPS = 20f;

        static readonly Vector3 ANIM_Z_PUSH_VEC = new(0f, 0f, Z_OFFSET);

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
        bool isPlaying = true;

        string modelXmlPath = DEFAULT_MODEL_XML_PATH;
        bool renderModel = true;
        bool renderBlack = true;
        bool renderEdges = false;
        bool renderTrajectory = true;
        bool followCamera = false;

        // ================= CACHED ASSETS ================= //

        Material blackMat;
        Material defaultMat;

        // ================= RUNTIME DATA ================= //

        static Dictionary<string, string> discoveredTrickBins;

        ModelData model;          // parsed XML (nodes/edges/capsules/previewPose)
        PlaybackState state;      // animation frames, offsets, current nodes, etc.

        // Model render objects
        GameObject modelRootGO;
        readonly List<CapsuleRuntime> capsuleRuntimes = new();

        // ================= WINDOW ================= //

        [MenuItem("Vectorier/Tools/Move Visualizer", false, 34)]
        static void OpenWindow() => GetWindow<MoveVisualizer>("Move Visualizer");

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += UpdatePlayback;
            ResetAll();
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= UpdatePlayback;
            ResetAll();
            DestroyModelRootImmediate();
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

            if (modelRootGO != null)
                ApplyCapsuleRenderSettings();

            renderEdges = EditorGUILayout.Toggle("Render Edges", renderEdges);
            renderTrajectory = EditorGUILayout.Toggle("Render Trajectory", renderTrajectory);
            followCamera = EditorGUILayout.Toggle("Follow Camera", followCamera);

            GUILayout.Space(10);
            currentPlacementMode = (PlacementMode)EditorGUILayout.EnumPopup("Placement Mode", currentPlacementMode);

            DrawPlacementModeUI();

            pivotNodeName = EditorGUILayout.TextField("Pivot Node", pivotNodeName);

            GUILayout.Space(12);

            if (GUILayout.Button(placementEnabled ? "Stop Placement" : "Start Placement", GUILayout.Height(60)))
                placementEnabled = !placementEnabled;

            if (GUILayout.Button("Clear", GUILayout.Height(40)))
            {
                ResetAll();
                DestroyModelRootImmediate();
            }

            GUILayout.Space(10);

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(isPlaying ? "Pause" : "Play", GUILayout.Height(30)))
                    isPlaying = !isPlaying;

                if (GUILayout.Button("Restart", GUILayout.Height(30)))
                {
                    state.CurrentFrameIndex = 0;
                    ApplyFrame(0);
                }
            }

            GUI.enabled = state.AnimationFrames.Count > 0;

            int maxFrame = Mathf.Max(0, state.AnimationFrames.Count - 1);
            int newFrame = EditorGUILayout.IntSlider("Frame", state.CurrentFrameIndex, 0, maxFrame);

            if (newFrame != state.CurrentFrameIndex)
            {
                isPlaying = false;
                state.CurrentFrameIndex = newFrame;
                ApplyFrame(state.CurrentFrameIndex);

                if (followCamera)
                    FollowSceneViewCameraToNodeXY();
            }

            GUI.enabled = true;

            if (!renderModel && modelRootGO != null)
                DestroyModelRootImmediate();
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

            DrawPreview();

            if (renderEdges)
            {
                DrawAnimation();
                DrawConnections();
            }

            if (renderTrajectory)
                DrawCenterOfMassPath();
        }

        void HandlePlacementInput()
        {
            EnsureModelLoadedForPreview();

            Vector3 cursorWorldPosition = GetCursorWorldPositionOnZPlane();
            UpdatePreview(cursorWorldPosition);
            state.IsPreviewActive = true;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                PlaceAt(cursorWorldPosition);
                state.IsPreviewActive = false;
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
            ResetRuntimeStateOnly();
            DestroyModelRootImmediate();

            model = ModelData.LoadOrThrow(ResolveXmlPath());
            state.AllocateNodeBuffers(model.NodeCount);

            LoadBinaryAnimation(model.NodeCount);

            state.CenterOfMassNodeIndex = model.TryGetNodeIndex("COM", out int comIdx) ? comIdx : -1;

            int pivotIndex = ResolvePivotNodeIndex(pivotNodeName, model);

            Vector3 pivotFrameZero = state.AnimationFrames[0][pivotIndex];
            state.StartOffset = worldPosition - pivotFrameZero;
            state.IsOffsetInitialized = true;

            PrecomputeCenterOfMassPath();

            state.CurrentFrameIndex = 0;
            isPlaying = true;
            ApplyFrame(0);

            if (renderModel)
                CreateModelRenderObjectsIfNeeded();

            state.LastPlaybackTime = EditorApplication.timeSinceStartup;
        }

        int ResolvePivotNodeIndex(string requestedPivot, ModelData m)
        {
            if (!string.IsNullOrWhiteSpace(requestedPivot) && m.TryGetNodeIndex(requestedPivot, out int idx))
                return idx;

            if (m.TryGetNodeIndex("NPivot", out int pivotIdx))
                return pivotIdx;

            throw new Exception("Pivot node not found. Requested pivot missing, and 'NPivot' not found in XML <Nodes>.");
        }

        void LoadBinaryAnimation(int expectedNodeCount)
        {
            state.AnimationFrames.Clear();

            string fullPath = ResolveBinFullPathOrThrow();
            using var reader = new BinaryReader(File.OpenRead(fullPath));

            int frameCount = reader.ReadInt32();

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                reader.ReadByte(); // marker

                int nodeCount = reader.ReadInt32();
                //if (nodeCount != expectedNodeCount)
                    //throw new Exception($"Node count mismatch. Bin={nodeCount}, XML Nodes={expectedNodeCount}. Your bin must match XML <Nodes> order/count.");

                var frame = new Vector3[nodeCount];

                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    frame[nodeIndex] = new Vector3(x, y, -z);
                }

                state.AnimationFrames.Add(frame);
            }
        }

        string ResolveBinFullPathOrThrow()
        {
            string fullPath;

            if (currentPlacementMode == PlacementMode.Custom)
            {
                fullPath = customBinPath;
                if (string.IsNullOrWhiteSpace(fullPath))
                    throw new Exception("Custom Bin Path is empty.");
            }
            else
            {
                fullPath = Path.Combine(binFolderPath ?? string.Empty, binFileName ?? string.Empty);
            }

            if (!Path.IsPathRooted(fullPath))
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                fullPath = Path.GetFullPath(Path.Combine(projectRoot, fullPath));
            }

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Bin file not found: {fullPath}");

            return fullPath;
        }

        void UpdatePlayback()
        {
            if (!state.IsOffsetInitialized || state.AnimationFrames.Count == 0 || !isPlaying)
                return;

            double now = EditorApplication.timeSinceStartup;
            double frameInterval = 1.0 / SOURCE_FPS;

            if (now - state.LastPlaybackTime < frameInterval)
                return;

            state.LastPlaybackTime = now;

            state.CurrentFrameIndex = (state.CurrentFrameIndex + 1) % state.AnimationFrames.Count;
            ApplyFrame(state.CurrentFrameIndex);

            if (followCamera)
                FollowSceneViewCameraToNodeXY();

            Repaint();
            SceneView.RepaintAll();
        }

        void ApplyFrame(int frameIndex)
        {
            if (state.AnimationFrames.Count == 0 || state.AnimationNodes == null)
                return;

            Vector3[] frame = state.AnimationFrames[frameIndex];

            for (int i = 0; i < frame.Length; i++)
                state.AnimationNodes[i] = frame[i] + state.StartOffset + ANIM_Z_PUSH_VEC;

            if (renderModel)
                UpdateCapsules(state.AnimationNodes);

            Repaint();
            SceneView.RepaintAll();
        }

        void UpdatePreview(Vector3 cursorWorldPosition)
        {
            if (model == null || state.PreviewPose == null || state.PreviewNodes == null)
                return;

            int pivotIndex = ResolvePivotNodeIndex(pivotNodeName, model);

            Vector3 pivotLocal = state.PreviewPose[pivotIndex];
            Vector3 offset = cursorWorldPosition - pivotLocal;

            for (int i = 0; i < state.PreviewPose.Length; i++)
                state.PreviewNodes[i] = state.PreviewPose[i] + offset + ANIM_Z_PUSH_VEC;
        }

        void PrecomputeCenterOfMassPath()
        {
            state.CenterOfMassPath.Clear();

            if (state.CenterOfMassNodeIndex < 0)
                return;

            foreach (var frame in state.AnimationFrames)
                state.CenterOfMassPath.Add(frame[state.CenterOfMassNodeIndex] + state.StartOffset + ANIM_Z_PUSH_VEC);
        }

        void ResetAll()
        {
            model = null;
            ResetRuntimeStateOnly();
        }

        void ResetRuntimeStateOnly()
        {
            state.Reset();
        }

        void EnsureModelLoadedForPreview()
        {
            string path = ResolveXmlPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            if (model == null || !string.Equals(model.SourcePath, path, StringComparison.OrdinalIgnoreCase))
            {
                model = ModelData.LoadOrThrow(path);
                state.AllocateNodeBuffers(model.NodeCount);
                state.PreviewPose = model.PreviewPose; // share read-only pose array
            }

            if (state.PreviewPose == null || state.PreviewPose.Length == 0)
                state.PreviewPose = model.PreviewPose;
        }

        void FollowSceneViewCameraToNodeXY()
        {
            if (state.AnimationNodes == null || state.AnimationNodes.Length == 0)
                return;

            if (!isPlaying)
                return;

            if (model == null || !model.TryGetNodeIndex("Camera", out int camIdx))
                return;

            if (camIdx < 0 || camIdx >= state.AnimationNodes.Length)
                return;

            Vector3 target = state.AnimationNodes[camIdx];

            SceneView sv = SceneView.lastActiveSceneView;
            if (sv == null)
                return;

            Vector3 pivot = sv.pivot;
            pivot.x = target.x;
            pivot.y = target.y;
            sv.pivot = pivot;
            sv.Repaint();
        }

        string ResolveXmlPath()
        {
            string p = string.IsNullOrWhiteSpace(modelXmlPath) ? DEFAULT_MODEL_XML_PATH : modelXmlPath;

            if (Path.IsPathRooted(p))
                return p;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, p));
        }

        Material GetBlackMaterial()
        {
            if (blackMat != null)
                return blackMat;

            blackMat = AssetDatabase.LoadAssetAtPath<Material>(BLACK_MAT_PATH);
            if (blackMat == null)
                Debug.LogWarning($"[MoveVisualizer] Black material not found at {BLACK_MAT_PATH}");

            return blackMat;
        }

        // ================= XML MODEL DATA ================= //

        sealed class ModelData
        {
            public string SourcePath { get; private set; }

            public readonly List<string> NodeNamesOrdered = new();
            public readonly Dictionary<string, int> NodeIndexByName = new(StringComparer.OrdinalIgnoreCase);
            public readonly List<EdgeIndex> Connections = new();
            public readonly List<CapsuleDef> Capsules = new();

            public Vector3[] PreviewPose { get; private set; }
            public int NodeCount => NodeNamesOrdered.Count;

            public bool TryGetNodeIndex(string name, out int idx) => NodeIndexByName.TryGetValue(name, out idx);

            public static ModelData LoadOrThrow(string path)
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    throw new FileNotFoundException($"Model XML not found. Resolved: '{path}'");

                // Keep your existing encoding assumption
                byte[] bytes = File.ReadAllBytes(path);
                string xmlText = System.Text.Encoding.GetEncoding("Windows-1251").GetString(bytes);

                var doc = new XmlDocument();
                doc.LoadXml(xmlText);

                XmlNode scene = doc.SelectSingleNode("/Scene") ?? throw new Exception("Invalid XML: missing <Scene> root.");
                XmlNode nodesElem = scene.SelectSingleNode("Nodes") ?? throw new Exception("Invalid XML: missing <Nodes>.");

                var m = new ModelData { SourcePath = path };

                // Nodes
                int idx = 0;
                foreach (XmlNode n in nodesElem.ChildNodes)
                {
                    if (n.NodeType != XmlNodeType.Element)
                        continue;

                    string name = n.Name;
                    m.NodeNamesOrdered.Add(name);
                    m.NodeIndexByName[name] = idx++;
                }

                if (m.NodeCount <= 0)
                    throw new Exception("XML <Nodes> is empty.");

                // Build preview pose from nodes list
                m.PreviewPose = new Vector3[m.NodeCount];
                int i = 0;
                foreach (XmlNode n in nodesElem.ChildNodes)
                {
                    if (n.NodeType != XmlNodeType.Element)
                        continue;

                    float x = GetFloatAttr(n, "X", 0f);
                    float y = GetFloatAttr(n, "Y", 0f);
                    float z = GetFloatAttr(n, "Z", 0f);

                    m.PreviewPose[i++] = new Vector3(x, y, -z);
                    if (i >= m.PreviewPose.Length)
                        break;
                }

                // Edges
                XmlNode edgesElem = scene.SelectSingleNode("Edges");
                if (edgesElem != null)
                {
                    foreach (XmlNode e in edgesElem.ChildNodes)
                    {
                        if (e.NodeType != XmlNodeType.Element)
                            continue;

                        string edgeName = e.Name;
                        string end1 = GetAttr(e, "End1");
                        string end2 = GetAttr(e, "End2");

                        if (!m.NodeIndexByName.TryGetValue(end1, out int a) || !m.NodeIndexByName.TryGetValue(end2, out int b))
                            continue;

                        m.Connections.Add(new EdgeIndex { Name = edgeName, A = a, B = b });
                    }
                }

                // Capsules
                XmlNode figsElem = scene.SelectSingleNode("Figures");
                if (figsElem != null)
                {
                    foreach (XmlNode f in figsElem.ChildNodes)
                    {
                        if (f.NodeType != XmlNodeType.Element)
                            continue;

                        string type = GetAttrOrNull(f, "Type");
                        if (!string.Equals(type, "Capsule", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string capName = f.Name;
                        string edgeName = GetAttr(f, "Edge");

                        float r1 = GetFloatAttr(f, "Radius1", 1f);
                        float r2 = GetFloatAttr(f, "Radius2", r1);
                        float m1 = Mathf.Clamp01(GetFloatAttr(f, "Margin1", 0f));
                        float m2 = Mathf.Clamp01(GetFloatAttr(f, "Margin2", 0f));

                        // Find edge endpoints from connections
                        int edgeIdx = m.Connections.FindIndex(x => string.Equals(x.Name, edgeName, StringComparison.OrdinalIgnoreCase));
                        if (edgeIdx < 0)
                            continue;

                        var edge = m.Connections[edgeIdx];

                        m.Capsules.Add(new CapsuleDef
                        {
                            Name = capName,
                            EdgeName = edgeName,
                            A = edge.A,
                            B = edge.B,
                            Radius1 = r1,
                            Radius2 = r2,
                            Margin1 = m1,
                            Margin2 = m2
                        });
                    }
                }

                return m;
            }
        }

        // ================= STATE ================= //

        struct PlaybackState
        {
            public readonly List<Vector3[]> AnimationFrames;
            public readonly List<Vector3> CenterOfMassPath;

            public Vector3[] AnimationNodes;
            public Vector3[] PreviewNodes;
            public Vector3[] PreviewPose;

            public int CurrentFrameIndex;
            public int CenterOfMassNodeIndex;

            public Vector3 StartOffset;
            public bool IsOffsetInitialized;
            public bool IsPreviewActive;

            public double LastPlaybackTime;

            public PlaybackState(bool init)
            {
                AnimationFrames = new List<Vector3[]>();
                CenterOfMassPath = new List<Vector3>();

                AnimationNodes = null;
                PreviewNodes = null;
                PreviewPose = null;

                CurrentFrameIndex = 0;
                CenterOfMassNodeIndex = -1;

                StartOffset = default;
                IsOffsetInitialized = false;
                IsPreviewActive = false;

                LastPlaybackTime = 0;
            }

            public void Reset()
            {
                AnimationFrames.Clear();
                CenterOfMassPath.Clear();

                CurrentFrameIndex = 0;
                CenterOfMassNodeIndex = -1;

                StartOffset = default;
                IsOffsetInitialized = false;
                IsPreviewActive = false;

                AnimationNodes = null;
                PreviewNodes = null;
            }

            public void AllocateNodeBuffers(int nodeCount)
            {
                if (nodeCount <= 0)
                    return;

                AnimationNodes = new Vector3[nodeCount];
                PreviewNodes = new Vector3[nodeCount];
            }
        }

        MoveVisualizer()
        {
            state = new PlaybackState(init: true);
        }

        // ================= XML HELPERS ================= //

        struct EdgeIndex { public int A; public int B; public string Name; }

        struct CapsuleDef
        {
            public string Name;
            public string EdgeName;
            public int A;
            public int B;
            public float Radius1;
            public float Radius2;
            public float Margin1; // [0..1] start trim
            public float Margin2; // [0..1] end trim
        }

        struct CapsuleRuntime
        {
            public CapsuleDef Def;
            public GameObject Root;
            public Transform Cylinder;
            public Transform SphereA;
            public Transform SphereB;
        }

        static string GetAttr(XmlNode node, string attr)
        {
            var a = node.Attributes?[attr];
            if (a == null || string.IsNullOrWhiteSpace(a.Value))
                throw new Exception($"XML missing attribute '{attr}' on <{node.Name}>");
            return a.Value;
        }

        static string GetAttrOrNull(XmlNode node, string attr) => node.Attributes?[attr]?.Value;

        static float GetFloatAttr(XmlNode node, string attr, float fallback)
        {
            var a = node.Attributes?[attr];
            if (a == null || string.IsNullOrWhiteSpace(a.Value))
                return fallback;

            if (float.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return v;

            if (float.TryParse(a.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;

            return fallback;
        }

        // ================= MODEL RENDER (Capsules) ================= //

        void CreateModelRenderObjectsIfNeeded()
        {
            if (!renderModel || modelRootGO != null || model == null)
                return;

            modelRootGO = new GameObject("MoveVisualizerModel")
            {
                hideFlags = HideFlags.DontSave
            };

            modelRootGO.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            modelRootGO.transform.localScale = Vector3.one;

            capsuleRuntimes.Clear();

            foreach (var c in model.Capsules)
            {
                var capRoot = new GameObject(c.Name) { hideFlags = HideFlags.DontSave };
                capRoot.transform.SetParent(modelRootGO.transform, false);

                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                CacheDefaultMatIfNeeded(cyl);

                cyl.name = "Cylinder";
                cyl.hideFlags = HideFlags.DontSave;
                cyl.transform.SetParent(capRoot.transform, false);
                DestroyImmediate(cyl.GetComponent<Collider>());

                GameObject sA = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sA.name = "Sphere_A";
                sA.hideFlags = HideFlags.DontSave;
                sA.transform.SetParent(capRoot.transform, false);
                DestroyImmediate(sA.GetComponent<Collider>());

                GameObject sB = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sB.name = "Sphere_B";
                sB.hideFlags = HideFlags.DontSave;
                sB.transform.SetParent(capRoot.transform, false);
                DestroyImmediate(sB.GetComponent<Collider>());

                capsuleRuntimes.Add(new CapsuleRuntime
                {
                    Def = c,
                    Root = capRoot,
                    Cylinder = cyl.transform,
                    SphereA = sA.transform,
                    SphereB = sB.transform
                });
            }

            ApplyCapsuleRenderSettings();

            if (state.AnimationNodes != null && state.AnimationNodes.Length > 0)
                UpdateCapsules(state.AnimationNodes);
        }

        void CacheDefaultMatIfNeeded(GameObject primitive)
        {
            if (defaultMat != null || primitive == null)
                return;

            var r = primitive.GetComponent<Renderer>();
            if (r != null)
                defaultMat = r.sharedMaterial;
        }

        void DestroyModelRootImmediate()
        {
            capsuleRuntimes.Clear();

            if (modelRootGO != null)
            {
                DestroyImmediate(modelRootGO);
                modelRootGO = null;
            }
        }

        void UpdateCapsules(Vector3[] nodeWorld)
        {
            if (!renderModel || model == null)
                return;

            if (modelRootGO == null)
                CreateModelRenderObjectsIfNeeded();

            if (modelRootGO == null)
                return;

            for (int i = 0; i < capsuleRuntimes.Count; i++)
            {
                var rt = capsuleRuntimes[i];
                var d = rt.Def;

                if ((uint)d.A >= (uint)nodeWorld.Length || (uint)d.B >= (uint)nodeWorld.Length)
                    continue;

                Vector3 a = nodeWorld[d.A];
                Vector3 b = nodeWorld[d.B];

                float t0 = Mathf.Clamp01(d.Margin1);
                float t1 = Mathf.Clamp01(1f - d.Margin2);
                if (t1 < t0) (t0, t1) = (t1, t0);

                Vector3 p0 = Vector3.Lerp(a, b, t0);
                Vector3 p1 = Vector3.Lerp(a, b, t1);

                Vector3 dir = (p1 - p0);
                float len = dir.magnitude;

                if (len < 1e-4f)
                {
                    SetCapsuleActive(rt, false);
                    capsuleRuntimes[i] = rt;
                    continue;
                }

                SetCapsuleActive(rt, true);

                Vector3 dirN = dir / len;
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, dirN);

                float rCyl = Mathf.Max(0.0001f, (d.Radius1 + d.Radius2) * 0.5f);
                float rA = Mathf.Max(0.0001f, d.Radius1);
                float rB = Mathf.Max(0.0001f, d.Radius2);

                rt.SphereA.position = p0;
                rt.SphereA.rotation = Quaternion.identity;
                rt.SphereA.localScale = Vector3.one * (rA * 2f);

                rt.SphereB.position = p1;
                rt.SphereB.rotation = Quaternion.identity;
                rt.SphereB.localScale = Vector3.one * (rB * 2f);

                float cylHeight = Mathf.Max(0.0001f, len);
                rt.Cylinder.position = (p0 + p1) * 0.5f;
                rt.Cylinder.rotation = rot;
                rt.Cylinder.localScale = new Vector3(rCyl * 2f, cylHeight * 0.5f, rCyl * 2f);

                capsuleRuntimes[i] = rt;
            }
        }

        static void SetCapsuleActive(CapsuleRuntime rt, bool active)
        {
            if (rt.Cylinder != null) rt.Cylinder.gameObject.SetActive(active);
            if (rt.SphereA != null) rt.SphereA.gameObject.SetActive(active);
            if (rt.SphereB != null) rt.SphereB.gameObject.SetActive(active);
        }

        void ApplyCapsuleRenderSettings()
        {
            Material targetMat = null;

            if (renderBlack)
                targetMat = GetBlackMaterial();

            if (targetMat == null)
                targetMat = defaultMat;

            for (int i = 0; i < capsuleRuntimes.Count; i++)
            {
                var rt = capsuleRuntimes[i];
                ApplyRendererSettings(rt.Cylinder, targetMat);
                ApplyRendererSettings(rt.SphereA, targetMat);
                ApplyRendererSettings(rt.SphereB, targetMat);
            }
        }

        static void ApplyRendererSettings(Transform t, Material mat)
        {
            if (t == null)
                return;

            var r = t.GetComponent<Renderer>();
            if (r == null)
                return;

            if (mat != null)
                r.sharedMaterial = mat;
        }

        // ================= DRAWING ================= //

        void DrawPreview()
        {
            if (!state.IsPreviewActive || state.PreviewNodes == null)
                return;

            Handles.color = new Color(1f, 0f, 0f, 0.4f);
            for (int i = 0; i < state.PreviewNodes.Length; i++)
                Handles.DotHandleCap(0, state.PreviewNodes[i], Quaternion.identity, 3f, EventType.Repaint);
        }

        void DrawAnimation()
        {
            if (state.AnimationNodes == null)
                return;

            Handles.color = Color.red;
            for (int i = 0; i < state.AnimationNodes.Length; i++)
                Handles.DotHandleCap(0, state.AnimationNodes[i], Quaternion.identity, 3f, EventType.Repaint);
        }

        void DrawCenterOfMassPath()
        {
            if (state.CenterOfMassPath.Count < 2)
                return;

            Handles.color = Color.red;
            Handles.DrawAAPolyLine(3f, state.CenterOfMassPath.ToArray());
        }

        void DrawConnections()
        {
            if (state.AnimationNodes == null || model == null)
                return;

            if (state.IsPreviewActive && state.PreviewNodes != null)
            {
                Handles.color = new Color(0f, 1f, 0f, 0.35f);
                DrawConnectionSet(state.PreviewNodes);
            }

            Handles.color = Color.green;
            DrawConnectionSet(state.AnimationNodes);
        }

        void DrawConnectionSet(Vector3[] nodeSet)
        {
            for (int i = 0; i < model.Connections.Count; i++)
            {
                var e = model.Connections[i];
                if ((uint)e.A >= (uint)nodeSet.Length || (uint)e.B >= (uint)nodeSet.Length)
                    continue;

                Handles.DrawLine(nodeSet[e.A], nodeSet[e.B]);
            }
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
