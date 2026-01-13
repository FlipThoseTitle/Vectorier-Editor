using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System;

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

            // Multiple cameras found
            if (candidates.Count > 1)
            {
                var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Parallax>() : null;

                if (selected == null || !candidates.Contains(selected))
                {
                    EditorUtility.DisplayDialog("Parallax", "There are multiple cameras in the scene! Select the camera to proceed", "OK");
                    return;
                }

                selected.ToggleParallax();
                EditorUtility.SetDirty(selected);
                return;
            }

            candidates[0].ToggleParallax();
            EditorUtility.SetDirty(candidates[0]);
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
                if (!float.TryParse(layerName, out float factor))
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

            UpdateParallax();
            EditorApplication.update += EditorUpdate;
        }

        private void StopParallax()
        {
            _isActive = false;
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
                    camera.transform.position = sceneCamera.transform.position;
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

            float effectiveZoom = baseZoom * _currentZoom;
            Vector3 cameraPosition = camera.transform.position;

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
                parallaxGroup.offset = cameraPosition + -(cameraPosition * factor * parallaxGroup.frameScale);
            }

            foreach (var target in _targets)
            {
                if (target.transform == null) continue;
                if (!_groups.TryGetValue(target.factor, out var group)) continue;

                target.transform.localScale = target.originalScale * group.frameScale;
                target.transform.position = group.offset + target.originalPosition * group.frameScale;
            }
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