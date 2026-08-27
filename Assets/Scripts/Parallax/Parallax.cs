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

        // [NonSerialized] prevents the undo
        [NonSerialized] private bool _isActive;
        [NonSerialized] private Vector3 _cameraStartPosition;
        [NonSerialized] private float _currentZoom = 1f;
        [NonSerialized] private Renderer[] _selfRenderers;
        [NonSerialized] private bool[] _selfRenderersEnabled;
        [NonSerialized] private Vector3 _lastSceneCamPos;
        [NonSerialized] private Quaternion _lastSceneCamRot;
        [NonSerialized] private bool _wasActiveBeforeSave;

        public bool IsActive => _isActive;

        private class ParallaxTarget
        {
            public Transform transform;
            public float factor;
            public Vector3 originalPosition;
            public Vector3 originalScale;
        }

        private class ParallaxGroup
        {
            public float factor;
            public Vector3 offset;
            public float frameScale;
        }

        private readonly List<ParallaxTarget> _targets = new List<ParallaxTarget>();
        private readonly Dictionary<float, ParallaxGroup> _groups = new Dictionary<float, ParallaxGroup>();

        private void OnEnable()
        {
            // Hook into the scene saving events
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        private void OnDisable()
        {
            if (_isActive) StopParallax();
            
            // Unhook to prevent memory leaks or duplicate calls
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
        }

        private void OnSceneSaving(Scene scene, string path)
        {
            // If there is a save while parallax is running, turn it off temporarily.
            if (_isActive)
            {
                _wasActiveBeforeSave = true;
                StopParallax();
            }
        }

        private void OnSceneSaved(Scene scene)
        {
            // turn it back once the save is finished
            if (_wasActiveBeforeSave)
            {
                _wasActiveBeforeSave = false;
                StartParallax();
            }
        }

        private void OnDestroy() { if (_isActive) StopParallax(); }
        private void OnApplicationQuit() { if (_isActive) StopParallax(); }

        // ================= UTILITY ================= //

        private bool IsUnderTaggedParent(Transform transform, string tag)
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (current.CompareTag(tag)) return true;
                current = current.parent;
            }
            return false;
        }

        private static bool Approximately1(float v) => Mathf.Abs(v - 1f) <= 0.0001f;

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
            if (_isActive) StopParallax();
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

        // ================= PARALLAX ================= //

        [MenuItem("Vectorier/Tools/Toggle Parallax", false, 35)]
        private static void ToggleParallaxFromMenu()
        {
            if (!TryGetParallaxCamera(out var targetParallax)) return;

            targetParallax.ToggleParallax();
            
            // Forced UI repaints
            EditorApplication.DirtyHierarchyWindowSorting();
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public void ToggleParallax()
        {
            // Always allow the instance that is currently active to stop itself,
            // even if its tag was changed since it was started.
            if (_isActive)
            {
                StopParallax();
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

            // The tag is authoritative. A duplicate is ambiguous even when only
            // one of the tagged objects currently has a Parallax component.
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

            // Backwards-compatible fallback: when no Camera tag is present,
            // component selection is valid only when it is unambiguous.
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

            // Keep the scene in a single, well-defined parallax state if an
            // inspector button or a script starts another instance directly.
            foreach (var other in FindObjectsByType<Parallax>(FindObjectsSortMode.None))
            {
                if (other != this && other._isActive)
                    other.StopParallax();
            }

            _isActive = true;
            HideSelf();
            _cameraStartPosition = camera.transform.position;
            _currentZoom = zoomValue;

            _targets.Clear();
            _groups.Clear();

            var tags = targetTags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));
            var layerFactorCache = new Dictionary<int, float>();

            foreach (var tag in tags)
            {
                foreach (var gameObject in GameObject.FindGameObjectsWithTag(tag))
                {
                    if (!gameObject.activeInHierarchy || IsUnderTaggedParent(gameObject.transform, "Object")) 
                        continue;

                    int layer = gameObject.layer;
                    
                    // Cache layer to float conversions so we dont string parse thousands of times
                    if (!layerFactorCache.TryGetValue(layer, out float factor))
                    {
                        if (!float.TryParse(LayerMask.LayerToName(layer), NumberStyles.Float, CultureInfo.InvariantCulture, out factor))
                            factor = 1f;
                        layerFactorCache[layer] = factor;
                    }

                    if (!_groups.ContainsKey(factor)) _groups[factor] = new ParallaxGroup();

                    _targets.Add(new ParallaxTarget
                    {
                        transform = gameObject.transform,
                        factor = factor,
                        originalPosition = gameObject.transform.position,
                        originalScale = gameObject.transform.localScale
                    });
                }
            }

            // Record initial transforms in Unity's Undo stack before applying parallax updates
            var transformsToRecord = _targets.Select(t => t.transform).Where(t => t != null).ToList();
            transformsToRecord.Add(camera.transform);
            Undo.RecordObjects(transformsToRecord.ToArray(), "Toggle Parallax");

            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += EditorUpdate;
        }

        private void StopParallax()
        {
            _isActive = false;
            UnhideSelf();
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= EditorUpdate;

            var camera = GetComponent<Camera>();

            // Record modified transforms in Undo stack before resetting back to original state
            var transformsToRecord = _targets.Select(t => t.transform).Where(t => t != null).ToList();
            if (camera != null) transformsToRecord.Add(camera.transform);
            if (transformsToRecord.Count > 0)
            {
                Undo.RecordObjects(transformsToRecord.ToArray(), "Toggle Parallax");
            }

            foreach (var target in _targets)
            {
                if (target.transform == null) continue;
                target.transform.position = target.originalPosition;
                target.transform.localScale = target.originalScale;
            }

            _targets.Clear();
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
            _currentZoom = zoomValue;
            UpdateParallax();
        }

        private void UpdateParallax()
        {
            var camera = GetComponent<Camera>();
            if (camera == null || !_isActive) return;

            UpdateTargetsCaptureForFactor1AtZoom1();

            float effectiveZoom = baseZoom * _currentZoom;
            Vector3 cameraPosition = camera.transform.position;
            cameraPosition.z = 0f;

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

            bool skipFactor1 = Approximately1(_currentZoom);

            foreach (var target in _targets)
            {
                if (target.transform == null) continue;
                if (skipFactor1 && Approximately1(target.factor)) continue;
                if (!_groups.TryGetValue(target.factor, out var group)) continue;

                target.transform.localScale = target.originalScale * group.frameScale;
                Vector3 newPos = group.offset + target.originalPosition * group.frameScale;
                newPos.z = target.originalPosition.z;
                target.transform.position = newPos;
            }
        }

        private void UpdateTargetsCaptureForFactor1AtZoom1()
        {
            if (!Approximately1(_currentZoom)) return;

            foreach (var t in _targets)
            {
                if (t.transform == null || !Approximately1(t.factor)) continue;
                t.originalPosition = t.transform.position;
                t.originalScale = t.transform.localScale;
            }
        }

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
