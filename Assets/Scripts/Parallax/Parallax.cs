#if UNITY_EDITOR
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
        [Tooltip("Attach camera to Scene view camera during Parallax.")]
        public bool AttachSceneCamera = true;

        [Tooltip("Orthographic size that represents zoom = 1.0.\nDefault: 4")]
        public float baseOrthoSize = 4f;

        [Tooltip("Base zoom value.\nDefault: 0.5")]
        public float baseZoom = 0.5f;

        [Tooltip("Multiplier applied to each group's scale.\nUse 1.0 to disable scaling adjustments.\nDefault: 2")]
        public float frameScaleMultiplier = 2f;

        [Tooltip("Comma-separated tags for parallax objects\nEx. Image,Object\nDefault = 'Object,Image,Trigger,Area,Platform,Trapezoid,Item,Model")]
        public string targetTags = "Image,Trigger,Area,Platform,Trapezoid,Item,Model";

        [Header("Zoom")]
        [Tooltip("Zoom multiplier to apply\nPreset:\nZoom Minimum = 0.65\nZoom80 = 0.8\nZoom Normal = 1\nZoom Maximum = 1.1")]
        public float zoomValue = 1f;

        private bool _isActive;
        private Vector3 _cameraStartPosition;
        private float _currentZoom = 1f;

        // Check if this transform is a child (at any level) of an object tagged with a specific tag
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

        // Represents each scene object participating in the parallax effect
        private class ParallaxTarget
        {
            public Transform transform;
            public float factor;                // parsed from Layer name
            public Vector3 originalPosition;    // original world-space position
            public Vector3 originalScale;       // original local scale
        }

        // Represents all layers sharing the same parallax factor
        private class ParallaxGroup
        {
            public float factor;                // factor shared by all layers of this group
            public Vector3 offset;              // Camera offset-based translation
            public float frameScale;            // Final applied scale per frame
        }

        private readonly List<ParallaxTarget> _targets = new List<ParallaxTarget>();
        private readonly Dictionary<float, ParallaxGroup> _groups = new Dictionary<float, ParallaxGroup>();

        // Called from the editor to toggle parallax
        public void ToggleParallax()
        {
            if (_isActive)
                StopParallax();
            else
                StartParallax();
        }

        // Begin parallax by finding all target objects and save their base transforms
        private void StartParallax()
        {
            var camera = GetComponent<Camera>();
            if (camera == null) return;

            _isActive = true;
            _cameraStartPosition = camera.transform.position;
            _currentZoom = zoomValue;

            _targets.Clear();
            _groups.Clear();

            // Parse tag filters
            var tags = targetTags.Split(',')
                                 .Select(t => t.Trim())
                                 .Where(t => !string.IsNullOrEmpty(t))
                                 .ToList();

            // Collect all matching GameObjects in the scene
            foreach (var gameObject in FindObjectsOfType<GameObject>())
            {
                if (!gameObject.activeInHierarchy) continue;
                if (tags.Count > 0 && !tags.Contains(gameObject.tag)) continue;

                // Skip if the object (or any parent) is under a GameObject tagged "Object"
                if (IsUnderTaggedParent(gameObject.transform, "Object"))
                    continue;

                // Read layer name as a float factor (Ex. layer named “0.5” --> factor = 0.5)
                string layerName = LayerMask.LayerToName(gameObject.layer);
                if (!float.TryParse(layerName, out float factor))
                    factor = 1f; // Default if no numeric layer name

                // Register group for this factor
                if (!_groups.ContainsKey(factor))
                    _groups[factor] = new ParallaxGroup { factor = factor, offset = Vector3.zero, frameScale = 1f };

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

        // Stop parallax and restore all object transforms
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

        // Keep camera synced with Scene view and refresh parallax every frame
        private void EditorUpdate()
        {
            if (!_isActive) return;

            if (AttachSceneCamera && SceneView.lastActiveSceneView != null)
            {
                var sceneCamera = SceneView.lastActiveSceneView.camera;
                var camera = GetComponent<Camera>();
                if (sceneCamera && camera)
                    camera.transform.position = sceneCamera.transform.position;
            }

            UpdateParallax();
        }

        // Called from eidtor button. Update current zoom value and apply it to scene.
        public void ApplyZoomValue()
        {
            _currentZoom = zoomValue;
            UpdateParallax();
        }

        // Calculate new positions and scales for each parallax group
        private void UpdateParallax()
        {
            var camera = GetComponent<Camera>();
            if (camera == null || !_isActive) return;

            // Effective zoom is the combination of base and user zoom
            float effectiveZoom = baseZoom * _currentZoom;
            Vector3 cameraPosition = camera.transform.position;

            // Compute per-group scale and offset
            foreach (var groupPair in _groups)
            {
                var parallaxGroup = groupPair.Value;
                float factor = parallaxGroup.factor;

                float scale;

                // Same formula as in VisualContainer.cs, inverse interpolation to get layer scale
                // scale = 1 / (((1 / zoom) - 1) * factor + 1)
                if (effectiveZoom <= 0f)
                {
                    scale = 1f;
                }
                else
                {
                    float denominator = ((1f / effectiveZoom - 1f) * factor + 1f);
                    scale = Mathf.Approximately(denominator, 0f) ? 1f : (1f / denominator);
                }

                // Round scale one decimal
                scale = (float)Math.Round(scale, 1, MidpointRounding.AwayFromZero);

                // FrameScale adds multiplier
                parallaxGroup.frameScale = scale * frameScaleMultiplier;
                parallaxGroup.frameScale = (float)Math.Round(parallaxGroup.frameScale, 1, MidpointRounding.AwayFromZero);

                // Offset determines how much each layer moves relative to camera position
                // Layers with smaller factors move less (further in background)
                parallaxGroup.offset = cameraPosition + -(cameraPosition * factor * parallaxGroup.frameScale);
            }

            // Apply calculated transform to each parallax object
            foreach (var target in _targets)
            {
                if (target.transform == null) continue;
                if (!_groups.TryGetValue(target.factor, out var group)) continue;

                target.transform.localScale = target.originalScale * group.frameScale;
                target.transform.position = group.offset + target.originalPosition * group.frameScale;
            }

        }

        private void OnDisable()
        {
            if (_isActive) StopParallax();
        }
    }
}
#endif
