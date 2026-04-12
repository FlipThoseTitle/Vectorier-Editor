using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
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

        private bool _isActive;
        private Vector3 _cameraStartPosition;
        private float _currentZoom = 1f;

        private Renderer[] _selfRenderers;
        private bool[] _selfRenderersEnabled;

        private Vector3 _lastSceneCamPos;
        private Quaternion _lastSceneCamRot;

        private bool IsUnderTaggedParent(Transform transform, string tag)
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (current.CompareTag(tag))
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static bool Approximately1(float v) => Mathf.Abs(v - 1f) <= 0.0001f;

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

        // Stop parallax if scripts reload
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
            if (_isActive)
                StopParallax();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!_isActive || !AttachSceneCamera) return;

            if (Event.current.type != EventType.Repaint) return;

            var sceneCam = sceneView.camera;
            var cam = GetComponent<Camera>();
            if (!sceneCam || !cam) return;
            var pos = sceneCam.transform.position;
            var rot = sceneCam.transform.rotation;

            if (pos == _lastSceneCamPos && rot == _lastSceneCamRot)
                return;

            _lastSceneCamPos = pos;
            _lastSceneCamRot = rot;

            pos.z = cam.transform.position.z;
            cam.transform.SetPositionAndRotation(pos, cam.transform.rotation);

            UpdateParallax();
        }

        //---------------------------------------------------------

        [MenuItem("Vectorier/Tools/Toggle Parallax", false, 35)]
        private static void ToggleParallaxFromMenu()
        {
            var candidates = GameObject.FindGameObjectsWithTag("Camera")
                .Select(go => go.GetComponent<Parallax>())
                .Where(p => p != null)
                .ToList();

            // No camera found
            if (candidates.Count == 0)
            {
                EditorUtility.DisplayDialog("Parallax", "There are no camera in the scene!", "OK");
                return;
            }

            Parallax targetParallax = null;

            // Multiple cameras found
            if (candidates.Count > 1)
            {
                var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Parallax>() : null;

                if (selected == null || !candidates.Contains(selected))
                {
                    EditorUtility.DisplayDialog("Parallax", "There are multiple cameras in the scene! Select the camera to proceed", "OK");
                    return;
                }

                targetParallax = selected;
            }
            else
            {
                targetParallax = candidates[0];
            }

            targetParallax.ToggleParallax();
            EditorUtility.SetDirty(targetParallax);
            EditorApplication.DirtyHierarchyWindowSorting();
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public void ToggleParallax()
        {
            if (_isActive)
                StopParallax();
            else
                StartParallax();
        }

        private void StartParallax()
        {
            var camera = GetComponent<Camera>();
            if (camera == null) return;

            _isActive = true;
            HideSelf();
            _cameraStartPosition = camera.transform.position;
            _currentZoom = zoomValue;

            _targets.Clear();
            _groups.Clear();

            var tags = targetTags.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            foreach (var gameObject in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (!gameObject.activeInHierarchy) continue;
                if (tags.Count > 0 && !tags.Contains(gameObject.tag)) continue;

                if (IsUnderTaggedParent(gameObject.transform, "Object"))
                    continue;

                string layerName = LayerMask.LayerToName(gameObject.layer);
                if (!float.TryParse(layerName, NumberStyles.Float, CultureInfo.InvariantCulture, out float factor))
                    factor = 1f;

                if (!_groups.ContainsKey(factor))
                    _groups[factor] = new ParallaxGroup();

                _targets.Add(new ParallaxTarget
                {
                    transform = gameObject.transform,
                    factor = factor,
                    originalPosition = gameObject.transform.position,
                    originalScale = gameObject.transform.localScale
                });
            }

            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += EditorUpdate;
        }

        private void StopParallax()
        {
            _isActive = false;
            UnhideSelf();
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= EditorUpdate;

            foreach (var target in _targets)
            {
                if (target.transform == null) continue;
                target.transform.position = target.originalPosition;
                target.transform.localScale = target.originalScale;
            }

            _targets.Clear();
            _groups.Clear();

            var camera = GetComponent<Camera>();
            if (camera != null)
                camera.transform.position = _cameraStartPosition;
        }

        private void EditorUpdate()
        {
            if (!_isActive)
                return;

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

                float scale;
                if (effectiveZoom <= 0f)
                    scale = 1f;
                else
                {
                    float denominator = ((1f / effectiveZoom - 1f) * factor + 1f);
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

                if (skipFactor1 && Approximately1(target.factor))
                    continue;

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

            for (int i = 0; i < _targets.Count; i++)
            {
                var t = _targets[i];
                if (t.transform == null) continue;

                if (Approximately1(t.factor))
                {
                    t.originalPosition = t.transform.position;
                    t.originalScale = t.transform.localScale;
                }
            }
        }

        // hide
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
            var go = gameObject;

            if (_selfRenderers != null && _selfRenderersEnabled != null)
            {
                for (int i = 0; i < _selfRenderers.Length; i++)
                {
                    if (_selfRenderers[i] == null) continue;
                    _selfRenderers[i].enabled = _selfRenderersEnabled[i];
                }
            }

            _selfRenderers = null;
            _selfRenderersEnabled = null;

            SceneVisibilityManager.instance.Show(go, true);
        }

        // cleanup
        private void OnDisable()
        {
            if (_isActive)
                StopParallax();
        }

        private void OnDestroy()
        {
            if (_isActive)
                StopParallax();
        }

        private void OnApplicationQuit()
        {
            if (_isActive)
                StopParallax();
        }
    }
}