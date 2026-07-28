using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Vectorier.Component;

namespace Vectorier.EditorScript
{
    [InitializeOnLoad]
    public static class TextSceneDrawer
    {
        // ================= CACHED DATA ================= //
        private class CachedObject
        {
            public GameObject GameObject;
            public SpriteRenderer SpriteRenderer;
            public string CleanName;
        }

        private static readonly List<CachedObject> CachedObjects = new();
        private static readonly GUIStyle SharedTextStyle = new();
        private static bool NeedsCacheRefresh = true;

        private static bool showOutline;
        private static bool showPlatformOutline;
        private static bool showTriggerText;
        private static bool showAreaText;

        private static readonly Color PlatformOutlineColor = new Color(0f, 0f, 1f, 1f);
        private static readonly Color TriggerOutlineColor = new Color(1f, 0.647f, 0f, 1f);

        // -------- OUTLINE BUFFER --------
        private static readonly Vector3[] OutlinePoints = new Vector3[5];

        // ================= CHECK ================= //
        private const string TriggerAreaSpritePath = "Assets/Resources/Images/Editor/Trigger/trigger.png";
        private const string PlatformSpritePath = "Assets/Resources/Images/Editor/Collision/platform.png";
        private static readonly Color TriggerAreaColor = new Color(1f, 0f, 0f, 1f); // red


        // ================= INIT ================= //

        static TextSceneDrawer()
        {
            EditorApplication.hierarchyChanged += MarkCacheDirty;

            SharedTextStyle.fontSize = 10;
            SharedTextStyle.wordWrap = true;
            SharedTextStyle.alignment = TextAnchor.UpperLeft;
            SharedTextStyle.normal = new GUIStyleState();

            SceneView.duringSceneGui += DrawInSceneView;
        }

        private static void MarkCacheDirty() => NeedsCacheRefresh = true;

        // ================= CACHE SYSTEM ================= //
        private static void RefreshCacheIfNeeded()
        {
            if (!NeedsCacheRefresh) return;

            CachedObjects.Clear();

            // Prevent duplicates
            var seen = new HashSet<int>();

            void CacheUnique(GameObject go)
            {
                if (go == null) return;
                int id = go.GetInstanceID();
                if (!seen.Add(id)) return;
                CacheSceneObject(go);
            }

            foreach (var trigger in UnityEngine.Object.FindObjectsByType<TriggerComponent>(FindObjectsSortMode.None))
                CacheUnique(trigger.gameObject);

            foreach (var area in GameObject.FindGameObjectsWithTag("Area"))
                CacheUnique(area);

            foreach (var platform in GameObject.FindGameObjectsWithTag("Platform"))
                CacheUnique(platform);

            foreach (var comment in GameObject.FindGameObjectsWithTag("Comment"))
                CacheUnique(comment);

            foreach (var sr in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr == null) continue;
                if (IsTriggerSpriteRedArea(sr) || IsPlatformBySprite(sr))
                    CacheUnique(sr.gameObject);
            }

            NeedsCacheRefresh = false;
        }

        private static void CacheSceneObject(GameObject obj)
        {
            var sr = obj.GetComponent<SpriteRenderer>();

            CachedObjects.Add(new CachedObject
            {
                GameObject = obj,
                SpriteRenderer = sr,
                CleanName = CleanObjectName(obj.name)
            });
        }

        private static string CleanObjectName(string name)
        {
            name = name.Replace("(Clone)", "");
            return Regex.Replace(name, @" \(\d+\)$", "");
        }

        // ================= HELPERS ================= //
        private static bool HasSpriteAtPath(SpriteRenderer sr, string assetPath)
        {
            if (sr == null || sr.sprite == null) return false;
            string path = AssetDatabase.GetAssetPath(sr.sprite);
            return string.Equals(path, assetPath, StringComparison.Ordinal);
        }

        private static bool ColorEquals(Color a, Color b, float eps = 0.0001f)
        {
            return Mathf.Abs(a.r - b.r) < eps &&
                   Mathf.Abs(a.g - b.g) < eps &&
                   Mathf.Abs(a.b - b.b) < eps &&
                   Mathf.Abs(a.a - b.a) < eps;
        }

        private static bool IsTriggerSpriteRedArea(SpriteRenderer sr)
        {
            return HasSpriteAtPath(sr, TriggerAreaSpritePath) && ColorEquals(sr.color, TriggerAreaColor);
        }

        private static bool IsPlatformBySprite(SpriteRenderer sr)
        {
            return HasSpriteAtPath(sr, PlatformSpritePath);
        }

        // ================= FADE ================= //
        private static float ComputeFade(SceneView sceneView, Vector3 position)
        {
            var camera = sceneView.camera;
            if (camera == null) return 1f;

            const float baseScale = 100f;

            if (camera.orthographic)
            {
                float size = camera.orthographicSize;
                return Mathf.Clamp01(1f - (size - 5f * baseScale) / (10f * baseScale));
            }

            float distance = Vector3.Distance(camera.transform.position, position);
            return Mathf.Clamp01(1f - (distance - 10f * baseScale) / (30f * baseScale));
        }

        // ================= SCENEVIEW RENDERING ================= //
        private struct LabelDrawData
        {
            public CachedObject Entry;
            public Vector3 WorldPos;
            public float Fade;
        }

        private static readonly List<LabelDrawData> labelsToDrawBuffer = new List<LabelDrawData>(256);

        private static void DrawInSceneView(SceneView sceneView)
        {
            if (Event.current.type == EventType.Layout)
            {
                LoadPrefs();
                RefreshCacheIfNeeded();
            }

            if (Event.current.type != EventType.Repaint)
                return;

            Camera camera = sceneView.camera;
            if (camera == null) return;

            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(camera);
            
            labelsToDrawBuffer.Clear();

            foreach (var entry in CachedObjects)
            {
                if (entry.GameObject == null)
                    continue;

                if (!entry.GameObject.activeInHierarchy || 
                (entry.GameObject.hideFlags & HideFlags.HideInHierarchy) != 0 || 
                SceneVisibilityManager.instance.IsHidden(entry.GameObject))
                    continue;

                Vector3 worldPos = entry.GameObject.transform.position;

                if (entry.SpriteRenderer != null &&
                    !GeometryUtility.TestPlanesAABB(frustum, entry.SpriteRenderer.bounds))
                    continue;

                float fade = ComputeFade(sceneView, worldPos);
                if (fade < 0.01f) continue;

                bool isPlatform = entry.GameObject.CompareTag("Platform") || IsPlatformBySprite(entry.SpriteRenderer);
                bool isTrigger = entry.GameObject.GetComponent<TriggerComponent>() != null;

                // Draw World Space Handles
                if (showOutline && entry.SpriteRenderer != null)
                    DrawSpriteOutline(entry.SpriteRenderer, fade, isPlatform, isTrigger);

                // Buffer the GUI data to draw in a single batch later
                labelsToDrawBuffer.Add(new LabelDrawData { Entry = entry, WorldPos = worldPos, Fade = fade });
            }

            if (labelsToDrawBuffer.Count > 0)
            {
                Handles.BeginGUI();
                foreach (var labelData in labelsToDrawBuffer)
                {
                    DrawLabel(labelData.Entry, labelData.WorldPos, labelData.Fade);
                }
                Handles.EndGUI();
            }
        }
        
        // ================= LABEL ================= //
        private static void DrawLabel(CachedObject entry, Vector3 worldPos, float fade)
        {
            SharedTextStyle.normal.textColor = new Color(0, 0, 0, fade);

            bool isTrigger = entry.GameObject.GetComponent<TriggerComponent>() != null;
            bool isArea = entry.GameObject.CompareTag("Area") || (entry.SpriteRenderer != null && IsTriggerSpriteRedArea(entry.SpriteRenderer));
            bool isPlatform = entry.GameObject.CompareTag("Platform") || IsPlatformBySprite(entry.SpriteRenderer);

            if ((isTrigger && !showTriggerText) || (isArea && !showAreaText) || isPlatform)
                return;

            if (entry.SpriteRenderer != null)
                DrawLabelWithinSprite(entry);
            else
                DrawLabelForWorldPosition(entry, worldPos);
        }

        private static void DrawLabelWithinSprite(CachedObject entry)
        {
            Bounds bound = entry.SpriteRenderer.bounds;

            Vector3 center = bound.center;
            Vector3 extent = bound.extents;

            Vector2 topLeft = HandleUtility.WorldToGUIPoint(new Vector3(center.x - extent.x, center.y + extent.y));
            Vector2 bottomRight = HandleUtility.WorldToGUIPoint(new Vector3(center.x + extent.x, center.y - extent.y));

            float x = topLeft.x;
            float y = topLeft.y;
            float width = bottomRight.x - topLeft.x;
            float height = Mathf.Abs(bottomRight.y - topLeft.y);

            GUI.BeginGroup(new Rect(x, y, width, height));
            GUI.Label(new Rect(0, 0, width, height), entry.CleanName, SharedTextStyle);
            GUI.EndGroup();
        }

        private static void DrawLabelForWorldPosition(CachedObject entry, Vector3 pos)
        {
            Vector2 guiPos = HandleUtility.WorldToGUIPoint(pos + Vector3.up * 0.5f);
            GUI.Label(new Rect(guiPos.x, guiPos.y, 200f, 40f), entry.CleanName, SharedTextStyle);
        }

        // ================= OUTLINE ================= //
        private static void DrawSpriteOutline(SpriteRenderer sr, float fade, bool isPlatform, bool isTrigger)
        {
            if (isPlatform && !showPlatformOutline)
                return;

            Bounds bound = sr.bounds;
            Vector3 center = bound.center;
            Vector3 extent = bound.extents;

            OutlinePoints[0] = new Vector3(center.x - extent.x, center.y + extent.y);
            OutlinePoints[1] = new Vector3(center.x + extent.x, center.y + extent.y);
            OutlinePoints[2] = new Vector3(center.x + extent.x, center.y - extent.y);
            OutlinePoints[3] = new Vector3(center.x - extent.x, center.y - extent.y);
            OutlinePoints[4] = OutlinePoints[0];

            Color outlineColor;

            if (isPlatform) outlineColor = PlatformOutlineColor;
            else if (isTrigger && sr.color == new Color(1f, 1f, 0f, 1f)) outlineColor = TriggerOutlineColor;
            else outlineColor = sr.color;

            outlineColor.a = fade;

            Handles.color = outlineColor;
            Handles.DrawAAPolyLine(4f, OutlinePoints);
        }

        private static void LoadPrefs()
        {
            showOutline = EditorPrefs.GetBool("Vectorier_ShowOutline", true);
            showPlatformOutline = EditorPrefs.GetBool("Vectorier_ShowPlatformOutline", false);
            showTriggerText = EditorPrefs.GetBool("Vectorier_ShowTriggerText", true);
            showAreaText = EditorPrefs.GetBool("Vectorier_ShowAreaText", false);
            TextAnchor anchor = (TextAnchor)EditorPrefs.GetInt("Vectorier_TextAnchor", (int)TextAnchor.UpperLeft);
            SharedTextStyle.alignment = anchor; 
        }
    }
}
