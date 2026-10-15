using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Globalization;

namespace Vectorier.Parallax
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Vectorier/Parallax/Parallax Component")]
    public class Parallax : MonoBehaviour
    {
        [Header("Parallax Settings")]
        public bool AttachSceneCamera = true;

        public float baseOrthoSize = 400f;
        public float baseZoom = 0.5f;
        public float frameScaleMultiplier = 2f;

        public string targetTags = "Object,Image,Trigger,Area,Platform,Trapezoid,Spawn,Model,Item,Animation,Particle";

        [Header("Zoom")]
        public float zoomValue = 1f;

        // Anything closer than this to what we wrote last frame is still ours, not a user edit.
        private const float ChangeTolerance = 0.0001f;
        // Property events fire every frame while dragging, so don't rescan on each one.
        private const double PropertyRescanInterval = 0.1;

        // [NonSerialized] keeps this state out of undo
        [NonSerialized] private bool _isActive;
        [NonSerialized] private Vector3 _cameraStartPosition;
        [NonSerialized] private float _currentZoom = 1f;
        [NonSerialized] private Renderer[] _selfRenderers;
        [NonSerialized] private bool[] _selfRenderersEnabled;
        [NonSerialized] private Vector3 _lastSceneCamPos;
        [NonSerialized] private Quaternion _lastSceneCamRot;
        [NonSerialized] private bool _wasActiveBeforeSave;

        // Real (unparallaxed) position/scale of every target. This is what gets restored on stop.
        [NonSerialized] private ParallaxLayerStore _layerStore;
        [NonSerialized] private bool _structureDirty;
        [NonSerialized] private bool _propertiesDirty;
        [NonSerialized] private double _nextPropertyRescanTime;
        [NonSerialized] private bool _zoomTransitionPending;

        public bool IsActive => _isActive;

        // Each layer acts like an invisible parent at 0,0,0 (moved by offset, scaled by frameScale):
        //   on screen = offset + layerPosition * frameScale
        // The applied* fields remember our last write, so a mismatch next frame means the user moved it.
        private class ParallaxTarget
        {
            public Transform transform;
            public Transform parent;
            public int layer;
            public string tag;
            public float factor;
            public bool retired;          // stopped tracking, but the index stays so the store doesn't shift
            public bool needsReproject;   // new or re-layered, work out its layer position from the screen

            public Vector3 knownLayerPosition; // last value we put in the store, used to spot undo/redo
            public Vector3 knownLayerScale;

            public bool hasApplied;
            public Vector3 appliedPosition;
            public Vector3 appliedScale;
            public Vector3 appliedOffset;
            public float appliedFrameScale;
        }

        private class ParallaxGroup
        {
            public float factor;
            public Vector3 offset;
            public float frameScale;
        }

        private readonly List<ParallaxTarget> _targets = new List<ParallaxTarget>();
        private readonly Dictionary<Transform, ParallaxTarget> _targetLookup = new Dictionary<Transform, ParallaxTarget>();
        private readonly Dictionary<float, ParallaxGroup> _groups = new Dictionary<float, ParallaxGroup>();
        private readonly Dictionary<int, float> _layerFactorCache = new Dictionary<int, float>();

        private void OnEnable()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private void OnDisable()
        {
            if (_isActive) StopParallax(fullFinalPass: false);

            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
        }

        // Never save parallaxed positions, stop before the save, start again after.
        private void OnSceneSaving(Scene scene, string path)
        {
            if (_isActive)
            {
                _wasActiveBeforeSave = true;
                StopParallax(fullFinalPass: true);
            }
        }

        private void OnSceneSaved(Scene scene)
        {
            if (_wasActiveBeforeSave)
            {
                _wasActiveBeforeSave = false;
                StartParallax();
            }
        }

        private void OnDestroy() { if (_isActive) StopParallax(fullFinalPass: false); }
        private void OnApplicationQuit() { if (_isActive) StopParallax(fullFinalPass: false); }

        // ================= UTILITY ================= //

        private static bool Approximately1(float v) => Mathf.Abs(v - 1f) <= 0.0001f;

        private static bool Same(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= ChangeTolerance * ChangeTolerance;

        // Script recompiles wipe our non-serialized state, so restore everything before that happens.
        [InitializeOnLoadMethod]
        private static void EnsureEditorCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                foreach (var p in FindObjectsByType<Parallax>(FindObjectsSortMode.None))
                    p.SafeStopOnReload();
            };
        }

        private void SafeStopOnReload()
        {
            if (_isActive) StopParallax(fullFinalPass: false);
        }

        // ================= SCENE GUI ================= //

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_isActive || !AttachSceneCamera || Event.current.type != EventType.Repaint) return;

            var sceneCam = sceneView.camera;
            var cam = GetComponent<Camera>();
            if (!sceneCam || !cam) return;

            var pos = sceneCam.transform.position;
            var rot = sceneCam.transform.rotation;

            if (pos == _lastSceneCamPos && rot == _lastSceneCamRot) return;

            _lastSceneCamPos = pos;
            _lastSceneCamRot = rot;

            pos.z = cam.transform.position.z;
            cam.transform.SetPositionAndRotation(pos, cam.transform.rotation);

            UpdateParallax();
        }

        // ================= CHANGE EVENTS ================= //

        private void OnObjectChangesPublished(ref ObjectChangeEventStream stream)
        {
            if (!_isActive) return;

            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    // Might be a tag/layer change on something we don't track yet.
                    // Also fires on every transform drag, hence the throttled rescan.
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                        _propertiesDirty = true;
                        break;

                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                    case ObjectChangeKind.ChangeGameObjectParent:
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.UpdatePrefabInstances:
                    case ObjectChangeKind.ChangeScene:
                        _structureDirty = true;
                        break;
                }
            }
        }

        // Undo can bring back deleted objects or revert reparenting.
        private void OnUndoRedoPerformed()
        {
            if (_isActive) _structureDirty = true;
        }

        // ================= PARALLAX ================= //

        [MenuItem("Vectorier/Tools/◐  Toggle Parallax", false, 24)]
        private static void ToggleParallaxFromMenu()
        {
            if (!TryGetParallaxCamera(out var targetParallax)) return;

            targetParallax.ToggleParallax();

            // Some windows don't refresh on their own after toggling from the menu.
            EditorApplication.DirtyHierarchyWindowSorting();
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public void ToggleParallax()
        {
            // The running instance can always stop itself, even if its Camera tag was removed meanwhile.
            if (_isActive)
            {
                StopParallax(fullFinalPass: true);
                return;
            }

            if (!TryGetParallaxCamera(out var targetParallax)) return;
            targetParallax.StartParallax();
        }

        private static bool TryGetParallaxCamera(out Parallax targetParallax)
        {
            targetParallax = null;

            var candidates = FindObjectsByType<Parallax>(FindObjectsSortMode.None);
            var taggedCameraObjects = GameObject.FindGameObjectsWithTag("Camera");

            // The Camera tag decides. Two tagged objects is ambiguous, even if only one has Parallax.
            if (taggedCameraObjects.Length > 1)
            {
                EditorUtility.DisplayDialog(
                    "Parallax",
                    "There are multiple GameObjects tagged 'Camera' in the scene. " +
                    "Tag exactly one camera before starting Parallax.",
                    "OK");
                Debug.LogError("Parallax could not start because multiple GameObjects are tagged 'Camera'.");
                return false;
            }

            if (taggedCameraObjects.Length == 1)
            {
                targetParallax = taggedCameraObjects[0].GetComponent<Parallax>();
                if (targetParallax != null) return true;
            }

            // Older scenes may not tag the camera. Fine, as long as there's only one Parallax.
            if (candidates.Length == 1)
            {
                targetParallax = candidates[0];
                return true;
            }

            if (candidates.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Parallax",
                    "There are no cameras with a Parallax component in the scene!",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Parallax",
                    "Multiple Parallax components were found, but none is assigned to the 'Camera' tag. " +
                    "Tag exactly one camera to choose the Parallax camera.",
                    "OK");
                Debug.LogError("Parallax could not start because multiple Parallax components exist without a unique 'Camera' tag.");
            }

            return false;
        }

        private void StartParallax()
        {
            var camera = GetComponent<Camera>();
            if (camera == null) return;

            // Inspector buttons can start a second instance directly; two at once would fight over transforms.
            foreach (var other in FindObjectsByType<Parallax>(FindObjectsSortMode.None))
            {
                if (other != this && other._isActive)
                    other.StopParallax(fullFinalPass: true);
            }

            _isActive = true;
            HideSelf();
            _cameraStartPosition = camera.transform.position;
            _currentZoom = zoomValue;

            _targets.Clear();
            _targetLookup.Clear();
            _groups.Clear();
            _layerFactorCache.Clear();

            _layerStore = ScriptableObject.CreateInstance<ParallaxLayerStore>();
            _layerStore.hideFlags = HideFlags.HideAndDontSave;

            // Nothing is parallaxed yet, so current transforms are the real positions.
            foreach (var t in CollectTargetTransforms())
                AddTarget(t, reprojectFromScreen: false);

            var transformsToRecord = _targets.Select(t => t.transform).Where(t => t != null).ToList();
            transformsToRecord.Add(camera.transform);
            Undo.RecordObjects(transformsToRecord.ToArray(), "Toggle Parallax");

            _structureDirty = false;
            _propertiesDirty = false;

            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += EditorUpdate;
            ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void StopParallax(bool fullFinalPass)
        {
            if (_layerStore != null)
            {
                if (fullFinalPass)
                {
                    // One last update so an edit made just before stopping isn't lost
                    // (drag, then Ctrl+S straight away).
                    _structureDirty = true;
                    UpdateParallax();
                }
                else
                {
                    // Closing/reloading/quitting: the scene may be half torn down, so no rescan and no undo.
                    SyncLayerSpace(recordUndo: false);
                    CaptureUnscaledTargets();
                }
            }

            _isActive = false;
            UnhideSelf();
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= EditorUpdate;
            ObjectChangeEvents.changesPublished -= OnObjectChangesPublished;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;

            var camera = GetComponent<Camera>();

            var transformsToRecord = _targets
                .Where(t => !t.retired && t.transform != null)
                .Select(t => t.transform)
                .ToList();
            if (camera != null) transformsToRecord.Add(camera.transform);
            if (transformsToRecord.Count > 0)
            {
                Undo.RecordObjects(transformsToRecord.ToArray(), "Toggle Parallax");
            }

            if (_layerStore != null)
            {
                PadLayerStore();

                for (int i = 0; i < _targets.Count; i++)
                {
                    var target = _targets[i];
                    if (target.retired || target.transform == null) continue;
                    target.transform.position = _layerStore.positions[i];
                    target.transform.localScale = _layerStore.scales[i];
                }

                DestroyImmediate(_layerStore);
                _layerStore = null;
            }

            _targets.Clear();
            _targetLookup.Clear();
            _groups.Clear();

            if (camera != null) camera.transform.position = _cameraStartPosition;
        }

        private void EditorUpdate()
        {
            if (!_isActive) return;

            if (AttachSceneCamera && SceneView.lastActiveSceneView != null)
            {
                var sceneCamera = SceneView.lastActiveSceneView.camera;
                var camera = GetComponent<Camera>();
                if (sceneCamera && camera)
                {
                    Vector3 pos = sceneCamera.transform.position;
                    pos.z = camera.transform.position.z;
                    camera.transform.position = pos;
                }
            }

            UpdateParallax();
        }

        public void ApplyZoomValue()
        {
            bool wasZoom1 = Approximately1(_currentZoom);
            _currentZoom = zoomValue;

            // Default / "1" objects are only tracked away from zoom 1, so crossing it needs a rescan.
            if (_isActive && wasZoom1 != Approximately1(_currentZoom))
            {
                _zoomTransitionPending = true;
                _structureDirty = true;
            }

            UpdateParallax();
        }

        // Order matters: find out what exists, then read the user's edits,
        // and only then overwrite transforms with this frame's parallax.
        private void UpdateParallax()
        {
            var camera = GetComponent<Camera>();
            if (camera == null || !_isActive || _layerStore == null) return;

            DetectStructureChanges();
            if (_structureDirty ||
                (_propertiesDirty && EditorApplication.timeSinceStartup >= _nextPropertyRescanTime))
            {
                RefreshTargets(_zoomTransitionPending);
                _zoomTransitionPending = false;
            }

            SyncLayerSpace(recordUndo: true);
            CaptureUnscaledTargets();

            Vector3 cameraPosition = camera.transform.position;
            cameraPosition.z = 0f;
            ComputeGroups(cameraPosition);
            ResolvePendingReprojections();
            ApplyTargets();
        }

        // ================= TARGETS ================= //

        // Untagged objects (folders) are looked through. The first tagged object in a branch
        // is the target and its children just follow it, whatever their layer. At zoom 1,
        // tagged Default / "1" objects wouldn't move anyway, so they're skipped with everything under them.
        // Inactive objects count too, otherwise enabling one midparallax would make it jump.
        private List<Transform> CollectTargetTransforms()
        {
            var tags = new HashSet<string>(
                targetTags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)));
            bool skipFactor1 = Approximately1(_currentZoom);

            var result = new List<Transform>();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;

                foreach (var root in scene.GetRootGameObjects())
                    CollectRecursive(root.transform, tags, skipFactor1, result);
            }
            return result;
        }

        private void CollectRecursive(Transform t, HashSet<string> tags, bool skipFactor1, List<Transform> result)
        {
            var go = t.gameObject;
            if (tags.Contains(go.tag))
            {
                if (!(skipFactor1 && Approximately1(GetLayerFactor(go.layer))))
                    result.Add(t);
                return;
            }

            for (int i = 0; i < t.childCount; i++)
                CollectRecursive(t.GetChild(i), tags, skipFactor1, result);
        }

        // Layer names are the factor 0.5. Anything that doesn't parse, like Default, counts as 1.
        private float GetLayerFactor(int layer)
        {
            if (!_layerFactorCache.TryGetValue(layer, out float factor))
            {
                if (!float.TryParse(LayerMask.LayerToName(layer), NumberStyles.Float, CultureInfo.InvariantCulture, out factor))
                    factor = 1f;
                _layerFactorCache[layer] = factor;
            }
            return factor;
        }

        private void EnsureGroup(float factor)
        {
            if (!_groups.ContainsKey(factor)) _groups[factor] = new ParallaxGroup { factor = factor, frameScale = 1f };
        }

        private void AddTarget(Transform t, bool reprojectFromScreen)
        {
            PadLayerStore();

            float factor = GetLayerFactor(t.gameObject.layer);
            EnsureGroup(factor);

            var target = new ParallaxTarget
            {
                transform = t,
                parent = t.parent,
                layer = t.gameObject.layer,
                tag = t.gameObject.tag,
                factor = factor,
                needsReproject = reprojectFromScreen,
                knownLayerPosition = t.position,
                knownLayerScale = t.localScale
            };

            _targets.Add(target);
            _targetLookup[t] = target;
            _layerStore.positions.Add(t.position);
            _layerStore.scales.Add(t.localScale);
        }

        // Undo can restore the store to a point before some targets were added, leaving it shorter than _targets.
        private void PadLayerStore()
        {
            for (int i = _layerStore.positions.Count; i < _targets.Count; i++)
                _layerStore.positions.Add(_targets[i].knownLayerPosition);
            for (int i = _layerStore.scales.Count; i < _targets.Count; i++)
                _layerStore.scales.Add(_targets[i].knownLayerScale);
        }

        private void SetLayerSpace(int index, Vector3 position, Vector3 scale)
        {
            _layerStore.positions[index] = position;
            _layerStore.scales[index] = scale;
            _targets[index].knownLayerPosition = position;
            _targets[index].knownLayerScale = scale;
        }

        // Cheap enough to run every frame, so changes to tracked objects dont wait for the throttled rescan
        // Reparenting must be caught before SyncLayerSpace, or Unity localScale fixup looks like a user scale
        private void DetectStructureChanges()
        {
            foreach (var target in _targets)
            {
                if (target.retired) continue;
                var t = target.transform;
                if (t == null ||
                    t.parent != target.parent ||
                    t.gameObject.layer != target.layer ||
                    !t.gameObject.CompareTag(target.tag))
                {
                    _structureDirty = true;
                    return;
                }
            }
        }

        private void RefreshTargets(bool zoomTransition)
        {
            _structureDirty = false;
            _propertiesDirty = false;
            _nextPropertyRescanTime = EditorApplication.timeSinceStartup + PropertyRescanInterval;

            PadLayerStore();
            var wanted = new HashSet<Transform>(CollectTargetTransforms());

            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                if (target.retired) continue;

                if (target.transform == null || !wanted.Contains(target.transform))
                {
                    // Dropped because zoom went back to 1, it has to go back to its real size.
                    // Any other reason (deleted, untagged, moved under another target) it stays
                    // where it is on screen, which is correct when a parent now carries it.
                    if (zoomTransition && target.transform != null && target.hasApplied)
                    {
                        target.transform.position = _layerStore.positions[i];
                        target.transform.localScale = _layerStore.scales[i];
                    }

                    target.retired = true;
                    target.hasApplied = false;
                    target.needsReproject = false;
                    _targetLookup.Remove(target.transform);
                    continue;
                }

                target.parent = target.transform.parent;
                target.layer = target.transform.gameObject.layer;
                target.tag = target.transform.gameObject.tag;

                // New layer: keep it where it looks, just hand it over to the other layer.
                float factor = GetLayerFactor(target.layer);
                if (factor != target.factor)
                {
                    target.factor = factor;
                    EnsureGroup(factor);
                    target.needsReproject = true;
                }
            }

            // Anything created, duplicated or pulled out of a parent mid-parallax is sitting at a
            // screen position. Exception: objects joining on a zoom change were never moved.
            foreach (var t in wanted)
            {
                if (!_targetLookup.ContainsKey(t))
                    AddTarget(t, reprojectFromScreen: !zoomTransition);
            }
        }

        // Turns the user's moves/scales into real positions, so they survive stopping and saving.
        private void SyncLayerSpace(bool recordUndo)
        {
            PadLayerStore();
            bool recorded = !recordUndo;

            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                if (target.retired || target.transform == null) continue;

                // Store changed without us touching it = undo/redo. Trust the store over the transform.
                Vector3 storedPosition = _layerStore.positions[i];
                Vector3 storedScale = _layerStore.scales[i];
                if (!Same(storedPosition, target.knownLayerPosition) || !Same(storedScale, target.knownLayerScale))
                {
                    target.knownLayerPosition = storedPosition;
                    target.knownLayerScale = storedScale;
                    target.hasApplied = false;
                    target.needsReproject = false;
                    continue;
                }

                if (!target.hasApplied || target.needsReproject) continue;

                Vector3 position = target.transform.position;
                Vector3 scale = target.transform.localScale;
                bool moved = !Same(position, target.appliedPosition);
                bool scaled = !Same(scale, target.appliedScale);
                if (!moved && !scaled) continue;

                float frameScale = target.appliedFrameScale;
                if (Mathf.Abs(frameScale) < 1e-6f) continue;

                // Recorded in the same undo group as the user's drag, so one Ctrl+Z reverts both.
                if (!recorded)
                {
                    Undo.RecordObject(_layerStore, "Move Parallax Object");
                    recorded = true;
                }

                Vector3 layerPosition = storedPosition;
                Vector3 layerScale = storedScale;

                if (moved)
                {
                    layerPosition = (position - target.appliedOffset) / frameScale;
                    layerPosition.z = position.z;
                }
                if (scaled)
                {
                    layerScale = scale / frameScale;
                }

                SetLayerSpace(i, layerPosition, layerScale);
                target.hasApplied = false;
            }
        }

        // Factor 1 at zoom 1 is left alone, so whatever the transform says is already the real position.
        private void CaptureUnscaledTargets()
        {
            if (!Approximately1(_currentZoom)) return;

            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                if (target.retired || target.transform == null) continue;
                if (target.hasApplied || target.needsReproject || !Approximately1(target.factor)) continue;

                SetLayerSpace(i, target.transform.position, target.transform.localScale);
            }
        }

        private void ComputeGroups(Vector3 cameraPosition)
        {
            float effectiveZoom = baseZoom * _currentZoom;

            foreach (var groupPair in _groups)
            {
                var parallaxGroup = groupPair.Value;
                float factor = groupPair.Key;

                float scale = 1f;
                if (effectiveZoom > 0f)
                {
                    float denominator = (1f / effectiveZoom - 1f) * factor + 1f;
                    scale = Mathf.Approximately(denominator, 0f) ? 1f : (1f / denominator);
                }

                scale = (float)Math.Round(scale, 1, MidpointRounding.AwayFromZero);
                parallaxGroup.frameScale = (float)Math.Round(scale * frameScaleMultiplier, 1);
                parallaxGroup.factor = factor;
                parallaxGroup.offset = cameraPosition - (cameraPosition * factor * parallaxGroup.frameScale);
            }
        }

        // Inverse of ApplyTargets: what's on screen now -> real position, using this frame's groups.
        private void ResolvePendingReprojections()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                if (target.retired || target.transform == null || !target.needsReproject) continue;

                target.needsReproject = false;
                target.hasApplied = false;

                Vector3 position = target.transform.position;
                Vector3 scale = target.transform.localScale;

                if (!_groups.TryGetValue(target.factor, out var group) || Mathf.Abs(group.frameScale) < 1e-6f)
                {
                    SetLayerSpace(i, position, scale);
                    continue;
                }

                Vector3 layerPosition = (position - group.offset) / group.frameScale;
                layerPosition.z = position.z;
                SetLayerSpace(i, layerPosition, scale / group.frameScale);
            }
        }

        private void ApplyTargets()
        {
            bool skipFactor1 = Approximately1(_currentZoom);

            for (int i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                if (target.retired || target.transform == null) continue;

                Vector3 layerPosition = _layerStore.positions[i];
                Vector3 layerScale = _layerStore.scales[i];

                if (skipFactor1 && Approximately1(target.factor))
                {
                    // Still scaled from the previous zoom, put it back.
                    if (target.hasApplied)
                    {
                        target.transform.position = layerPosition;
                        target.transform.localScale = layerScale;
                        target.hasApplied = false;
                    }
                    continue;
                }

                if (!_groups.TryGetValue(target.factor, out var group)) continue;

                target.transform.localScale = layerScale * group.frameScale;
                Vector3 newPos = group.offset + layerPosition * group.frameScale;
                newPos.z = layerPosition.z;
                target.transform.position = newPos;

                // Read back instead of keeping newPos: for children, Unity's world/local round trip
                // changes the float slightly, and that would look like a user edit next frame.
                target.appliedPosition = target.transform.position;
                target.appliedScale = target.transform.localScale;
                target.appliedOffset = group.offset;
                target.appliedFrameScale = group.frameScale;
                target.hasApplied = true;
            }
        }

        // ================= SELF VISIBILITY ================= //

        // The camera's own gizmo/sprite would get in the way while parallax drives it around.
        private void HideSelf()
        {
            var go = gameObject;
            _selfRenderers = go.GetComponentsInChildren<Renderer>(true);
            _selfRenderersEnabled = new bool[_selfRenderers.Length];

            for (int i = 0; i < _selfRenderers.Length; i++)
            {
                _selfRenderersEnabled[i] = _selfRenderers[i].enabled;
                _selfRenderers[i].enabled = false;
            }

            SceneVisibilityManager.instance.Hide(go, true);
        }

        private void UnhideSelf()
        {
            if (_selfRenderers != null && _selfRenderersEnabled != null)
            {
                for (int i = 0; i < _selfRenderers.Length; i++)
                {
                    if (_selfRenderers[i] != null)
                        _selfRenderers[i].enabled = _selfRenderersEnabled[i];
                }
            }

            _selfRenderers = null;
            _selfRenderersEnabled = null;

            SceneVisibilityManager.instance.Show(gameObject, true);
        }
    }
}