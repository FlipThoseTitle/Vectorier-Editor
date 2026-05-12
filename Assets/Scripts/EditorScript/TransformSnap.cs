using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vectorier.EditorScript
{
    [InitializeOnLoad]
    public static class TransformSnap
    {
        private const KeyCode VSnapKey = KeyCode.V; 
        private const KeyCode CSnapKey = KeyCode.C; 
        private const float VertexSnapRadius = 50f;
        private const float BoundsSearchThreshold = 200f; 

        private enum SnapMode { None, Normal, Platform }
        private static SnapMode currentMode = SnapMode.None;

        private static Transform[] targetTransforms;
        private static Vector3[] grabOffsets;
        private static Transform primaryTransform;
        private static Vector3 activeSourceVertex;
        private static Object[] cachedSelection;
        private static bool isDragging;

        private struct SourceVertex
        {
            public Vector3 position;
            public Transform rootTransform;
        }

        static TransformSnap()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            if (e.type == EventType.KeyUp)
            {
                if ((e.keyCode == VSnapKey && currentMode == SnapMode.Normal) ||
                    (e.keyCode == CSnapKey && currentMode == SnapMode.Platform))
                {
                    currentMode = SnapMode.None;
                    cachedSelection = null;
                    targetTransforms = null;
                    primaryTransform = null;
                    isDragging = false;
                    Undo.FlushUndoRecordObjects();
                    sceneView.Repaint();
                    return;
                }
            }

            if (e.type == EventType.KeyDown && currentMode == SnapMode.None)
            {
                if (e.keyCode == VSnapKey || e.keyCode == CSnapKey)
                {
                    Transform[] roots = GetTopLevelSelection();
                    if (roots.Length > 0)
                    {
                        bool isPlatform = (e.keyCode == CSnapKey);
                        Vector3 mouseRayPos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
                        mouseRayPos.z = roots[0].position.z;

                        List<SourceVertex> vertices = GatherSourceVertices(roots, isPlatform);
                        if (TryGetClosestSourceVertex(vertices, mouseRayPos, out SourceVertex closest))
                        {
                            currentMode = isPlatform ? SnapMode.Platform : SnapMode.Normal;
                            cachedSelection = Selection.objects;
                            targetTransforms = roots;
                            primaryTransform = closest.rootTransform;
                            activeSourceVertex = closest.position;
                            isDragging = false;
                            UpdateGrabOffsets();
                        }
                    }
                }
            }

            if (currentMode != SnapMode.None)
            {
                if (HasSelectionChanged())
                {
                    Selection.objects = cachedSelection;
                }

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    isDragging = true;
                    e.Use();
                }

                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    isDragging = false;
                }

                Vector3 mouseWorldPos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
                mouseWorldPos.z = primaryTransform != null ? primaryTransform.position.z : 0f;

                if (primaryTransform != null && targetTransforms != null)
                {
                    int idx = System.Array.IndexOf(targetTransforms, primaryTransform);
                    if (idx >= 0)
                    {
                        activeSourceVertex = primaryTransform.position - grabOffsets[idx];
                    }
                }

                bool onlyPlatforms = (currentMode == SnapMode.Platform);
                Vector3 newVertexPos = CalculateSpriteSnapPosition(targetTransforms, onlyPlatforms, mouseWorldPos, out bool isSnapping);

                if (!isDragging && !isSnapping && targetTransforms != null)
                {
                    List<SourceVertex> vertices = GatherSourceVertices(targetTransforms, onlyPlatforms);
                    if (TryGetClosestSourceVertex(vertices, mouseWorldPos, out SourceVertex closest))
                    {
                        activeSourceVertex = closest.position;
                        primaryTransform = closest.rootTransform;
                        UpdateGrabOffsets();
                    }
                }

                if (e.type == EventType.MouseDrag && e.button == 0 && targetTransforms != null)
                {
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    isDragging = true;
                    Undo.RecordObjects(targetTransforms, currentMode == SnapMode.Platform ? "Platform Snap" : "Sprite Snap");
                    
                    for (int i = 0; i < targetTransforms.Length; i++)
                    {
                        if (targetTransforms[i] != null)
                        {
                            targetTransforms[i].position = newVertexPos + grabOffsets[i];
                        }
                    }
                    e.Use();
                }

                DrawVertexVisualizer(activeSourceVertex, Color.green, true);
                sceneView.Repaint();
            }
        }

        private static void UpdateGrabOffsets()
        {
            if (targetTransforms == null) return;
            grabOffsets = new Vector3[targetTransforms.Length];
            for (int i = 0; i < targetTransforms.Length; i++)
            {
                if (targetTransforms[i] != null)
                {
                    grabOffsets[i] = targetTransforms[i].position - activeSourceVertex;
                }
            }
        }

        private static Transform[] GetTopLevelSelection()
        {
            Transform[] selected = Selection.transforms;
            List<Transform> topLevel = new List<Transform>();

            foreach (var t in selected)
            {
                if (t == null) continue;
                bool hasSelectedParent = false;
                Transform curr = t.parent;
                while (curr != null)
                {
                    if (System.Array.IndexOf(selected, curr) >= 0)
                    {
                        hasSelectedParent = true;
                        break;
                    }
                    curr = curr.parent;
                }
                if (!hasSelectedParent)
                {
                    topLevel.Add(t);
                }
            }
            return topLevel.ToArray();
        }

        private static bool HasSelectionChanged()
        {
            if (cachedSelection == null) return true;
            Object[] current = Selection.objects;
            if (current.Length != cachedSelection.Length) return true;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] != cachedSelection[i]) return true;
            }
            return false;
        }

        private static bool IsValidObject(GameObject obj)
        {
            if (obj == null || !obj.activeInHierarchy) return false;
            if (SceneVisibilityManager.instance.IsHidden(obj)) return false;
            return true;
        }

        private static List<SourceVertex> GatherSourceVertices(Transform[] roots, bool onlyPlatforms)
        {
            List<SourceVertex> list = new List<SourceVertex>();
            foreach (var root in roots)
            {
                if (root == null || !IsValidObject(root.gameObject)) continue;
                SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(false);
                foreach (var r in renderers)
                {
                    if (r.sprite == null || !r.enabled) continue;
                    if (!IsValidObject(r.gameObject)) continue;
                    if (onlyPlatforms && !r.CompareTag("Platform")) continue;

                    Vector3[] corners = GetSpriteCorners(r);
                    foreach (var c in corners)
                    {
                        list.Add(new SourceVertex { position = c, rootTransform = root });
                    }
                }
            }
            return list;
        }

        private static bool TryGetClosestSourceVertex(List<SourceVertex> vertices, Vector3 target, out SourceVertex closest)
        {
            closest = default;
            if (vertices.Count == 0) return false;

            float minDist = float.MaxValue;
            foreach (var v in vertices)
            {
                float dist = Vector2.Distance(target, v.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = v;
                }
            }
            return true;
        }

        private static Vector3 CalculateSpriteSnapPosition(Transform[] rootTransforms, bool onlyPlatforms, Vector3 mousePos, out bool isSnapping)
        {
            SpriteRenderer[] allSprites = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            
            Vector3 bestCorner = Vector3.zero;
            float closestCornerDist = float.MaxValue;
            
            Vector3 bestEdgePoint = Vector3.zero;
            float closestEdgeDist = float.MaxValue;
            SpriteRenderer targetEdgeSprite = null;

            float maxSqrDist = BoundsSearchThreshold * BoundsSearchThreshold;

            foreach (var targetSprite in allSprites)
            {
                if (targetSprite.sprite == null || !targetSprite.enabled) continue;
                if (!IsValidObject(targetSprite.gameObject)) continue;

                bool isChildOfSelection = false;
                foreach (var root in rootTransforms)
                {
                    if (root != null && targetSprite.transform.IsChildOf(root))
                    {
                        isChildOfSelection = true;
                        break;
                    }
                }
                if (isChildOfSelection) continue;

                if (onlyPlatforms && !targetSprite.CompareTag("Platform"))
                    continue;

                if (targetSprite.bounds.SqrDistance(mousePos) > maxSqrDist)
                    continue;

                Vector3[] corners = GetSpriteCorners(targetSprite);

                foreach (var corner in corners)
                {
                    float dist = Vector2.Distance(mousePos, corner);
                    if (dist < closestCornerDist)
                    {
                        closestCornerDist = dist;
                        bestCorner = corner;
                    }
                }

                Vector3[] edgePoints = new Vector3[4];
                edgePoints[0] = GetClosestPointOnSegment(corners[0], corners[1], mousePos);
                edgePoints[1] = GetClosestPointOnSegment(corners[1], corners[3], mousePos);
                edgePoints[2] = GetClosestPointOnSegment(corners[3], corners[2], mousePos);
                edgePoints[3] = GetClosestPointOnSegment(corners[2], corners[0], mousePos);

                foreach (var pt in edgePoints)
                {
                    float dist = Vector2.Distance(mousePos, pt);
                    if (dist < closestEdgeDist)
                    {
                        closestEdgeDist = dist;
                        bestEdgePoint = pt;
                        targetEdgeSprite = targetSprite;
                    }
                }
            }

            isSnapping = false;

            if (closestCornerDist <= VertexSnapRadius && closestCornerDist != float.MaxValue)
            {
                isSnapping = true;
                Handles.color = Color.yellow;
                Handles.DrawLine(activeSourceVertex, bestCorner, 2f);
                
                DrawVertexVisualizer(bestCorner, Color.yellow, false);
                return bestCorner;
            }

            if (targetEdgeSprite != null && closestEdgeDist <= BoundsSearchThreshold)
            {
                Handles.color = Color.cyan;
                Handles.DrawLine(activeSourceVertex, bestEdgePoint, 2f);
                
                Handles.DrawSolidDisc(bestEdgePoint, Vector3.forward, HandleUtility.GetHandleSize(bestEdgePoint) * 0.05f);
                return bestEdgePoint;
            }

            return mousePos;
        }

        private static void DrawVertexVisualizer(Vector3 position, Color color, bool drawCenterDot)
        {
            Color originalColor = Handles.color;
            Handles.color = color;

            float size = HandleUtility.GetHandleSize(position) * 0.08f; 

            Vector3[] rectVerts = new Vector3[]
            {
                position + new Vector3(-size, -size, 0f),
                position + new Vector3( size, -size, 0f),
                position + new Vector3( size,  size, 0f),
                position + new Vector3(-size,  size, 0f)
            };

            Handles.DrawSolidRectangleWithOutline(rectVerts, new Color(color.r, color.g, color.b, 0.15f), color);

            if (drawCenterDot)
            {
                Handles.DrawWireDisc(position, Vector3.forward, size * 0.4f);
            }

            Handles.color = originalColor;
        }

        private static Vector3[] GetSpriteCorners(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null) return new Vector3[4];

            Bounds localBounds = renderer.localBounds;
            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            Transform t = renderer.transform;

            return new Vector3[]
            {
                t.TransformPoint(new Vector3(min.x, min.y, 0)),
                t.TransformPoint(new Vector3(max.x, min.y, 0)),
                t.TransformPoint(new Vector3(min.x, max.y, 0)),
                t.TransformPoint(new Vector3(max.x, max.y, 0))
            };
        }

        private static Vector3 GetClosestPointOnSegment(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
        {
            Vector2 heading = (lineEnd - lineStart);
            float magnitudeMax = heading.magnitude;
            heading.Normalize();

            Vector2 lhs = point - lineStart;
            float dotP = Vector2.Dot(lhs, heading);
            dotP = Mathf.Clamp(dotP, 0f, magnitudeMax);

            return (Vector2)lineStart + heading * dotP;
        }
    }
}
