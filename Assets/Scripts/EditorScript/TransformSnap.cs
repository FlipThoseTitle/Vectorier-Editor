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

        private static bool isAHeld;
        private static bool isScaleSnapMode;
        private static Vector3 originalScale;
        private static Vector3 originalPosition;
        private static int activeCornerIndex = -1;
        private static Vector3 grabFixedCorner;
        private static Vector3 grabOrigDelta;
        private static Vector3 grabOrigPosDelta;

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

            // Track A key modifier state
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.A)
            {
                isAHeld = true;

                // Transition into scale snap if A is pressed after V/C
                if (currentMode != SnapMode.None && !isScaleSnapMode)
                {
                    if (CanScaleSnap())
                    {
                        isScaleSnapMode = true;
                        InitializeScaleSnapState(activeSourceVertex);
                    }
                }
            }

            if (e.type == EventType.KeyUp && e.keyCode == KeyCode.A)
            {
                isAHeld = false;

                // Exit scale snap if A is released, retaining normal snap behavior
                if (isScaleSnapMode)
                {
                    isScaleSnapMode = false;
                    UpdateGrabOffsets();
                }
            }

            if (e.type == EventType.KeyUp)
            {
                if ((e.keyCode == VSnapKey && currentMode == SnapMode.Normal) ||
                    (e.keyCode == CSnapKey && currentMode == SnapMode.Platform))
                {
                    currentMode = SnapMode.None;
                    isScaleSnapMode = false;
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
                // MoveVisualizer uses C for its own platform snap during placement
                bool cOwnedByPlacement = e.keyCode == CSnapKey && Tools.MoveVisualizer.IsPlacementActive;

                if ((e.keyCode == VSnapKey || e.keyCode == CSnapKey) && !cOwnedByPlacement)
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

                            isScaleSnapMode = false;

                            // Validate Scale Snap Conditions
                            if (isAHeld && CanScaleSnap())
                            {
                                isScaleSnapMode = true;
                                InitializeScaleSnapState(activeSourceVertex);
                            }

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

                // Keep activeSourceVertex synced accurately based on the mode
                if (isScaleSnapMode && isDragging && activeCornerIndex != -1)
                {
                    if (TryGetScaleCorners(primaryTransform, out Vector3[] scaleCorners))
                    {
                        activeSourceVertex = scaleCorners[activeCornerIndex];
                    }
                }
                else if (primaryTransform != null && targetTransforms != null)
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
                    // While scaling, only the object's own outer corners count. A child's corner (like a platform's
                    // floor tile) isn't something you can scale from
                    List<SourceVertex> vertices = isScaleSnapMode
                        ? GatherScaleVertices(targetTransforms[0])
                        : GatherSourceVertices(targetTransforms, onlyPlatforms);

                    if (TryGetClosestSourceVertex(vertices, mouseWorldPos, out SourceVertex closest))
                    {
                        activeSourceVertex = closest.position;
                        primaryTransform = closest.rootTransform;
                        UpdateGrabOffsets();

                        // Re-initialize state if user shifts focus to a different corner before clicking
                        if (isScaleSnapMode)
                        {
                            InitializeScaleSnapState(activeSourceVertex);
                        }
                    }
                }

                if (e.type == EventType.MouseDrag && e.button == 0 && targetTransforms != null)
                {
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    isDragging = true;
                    Undo.RecordObjects(targetTransforms, isScaleSnapMode ? "Scale Snap" : (currentMode == SnapMode.Platform ? "Platform Snap" : "Sprite Snap"));

                    if (isScaleSnapMode && activeCornerIndex != -1)
                    {
                        // Calculate intent to lock into a single axis
                        Vector3 originalGrabbedCorner = grabFixedCorner + grabOrigDelta;
                        Vector3 intentDelta = mouseWorldPos - originalGrabbedCorner;

                        float scaleX = 1f;
                        float scaleY = 1f;

                        // Prevent division by zero if sprite width/height evaluates extremely close to 0
                        if (Mathf.Abs(intentDelta.x) > Mathf.Abs(intentDelta.y))
                        {
                            // Extending horizontally: snap to newVertexPos.x, lock Y to original
                            float newDeltaX = newVertexPos.x - grabFixedCorner.x;
                            scaleX = Mathf.Abs(grabOrigDelta.x) > 0.0001f ? newDeltaX / grabOrigDelta.x : 1f;
                        }
                        else
                        {
                            // Extending vertically: snap to newVertexPos.y, lock X to original
                            float newDeltaY = newVertexPos.y - grabFixedCorner.y;
                            scaleY = Mathf.Abs(grabOrigDelta.y) > 0.0001f ? newDeltaY / grabOrigDelta.y : 1f;
                        }

                        primaryTransform.localScale = new Vector3(originalScale.x * scaleX, originalScale.y * scaleY, originalScale.z);

                        Vector3 newPos = grabFixedCorner + new Vector3(grabOrigPosDelta.x * scaleX, grabOrigPosDelta.y * scaleY, grabOrigPosDelta.z);
                        newPos.z = originalPosition.z;
                        primaryTransform.position = newPos;
                    }
                    else
                    {
                        for (int i = 0; i < targetTransforms.Length; i++)
                        {
                            if (targetTransforms[i] != null)
                            {
                                Vector3 newPos = newVertexPos + grabOffsets[i];
                                newPos.z = targetTransforms[i].position.z;
                                targetTransforms[i].position = newPos;
                            }
                        }
                    }
                    e.Use();
                }

                DrawVertexVisualizer(activeSourceVertex, Color.green, true);
                sceneView.Repaint();
            }
        }

        private static void InitializeScaleSnapState(Vector3 sourceVertex)
        {
            activeCornerIndex = -1;

            if (TryGetScaleCorners(primaryTransform, out Vector3[] corners))
            {
                originalScale = primaryTransform.localScale;
                originalPosition = primaryTransform.position;

                float minDist = float.MaxValue;
                for (int i = 0; i < 4; i++)
                {
                    float dist = Vector3.Distance(sourceVertex, corners[i]);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        activeCornerIndex = i;
                    }
                }

                // 0(BL) opp is 3(TR), 1(BR) opp is 2(TL), 2(TL) opp is 1(BR), 3(TR) opp is 0(BL)
                int oppositeIndex = 3 - activeCornerIndex;
                grabFixedCorner = corners[oppositeIndex];
                grabOrigDelta = corners[activeCornerIndex] - grabFixedCorner;
                grabOrigPosDelta = originalPosition - grabFixedCorner;

                // The grabbed point might've been a child's corner from before A was pressed, jump to the real outer corner
                activeSourceVertex = corners[activeCornerIndex];
            }
        }

        // Scale snap needs one object with something to measure. Children are fine now
        private static bool CanScaleSnap()
        {
            return targetTransforms != null
                && targetTransforms.Length == 1
                && primaryTransform != null
                && TryGetScaleCorners(primaryTransform, out _);
        }

        private static List<SourceVertex> GatherScaleVertices(Transform root)
        {
            List<SourceVertex> list = new List<SourceVertex>();
            if (root != null && TryGetScaleCorners(root, out Vector3[] corners))
            {
                foreach (var c in corners)
                {
                    list.Add(new SourceVertex { position = c, rootTransform = root });
                }
            }
            return list;
        }

        // Corners used for scaling, in BL, BR, TL, TR order (same as GetSpriteCorners).
        // If the object has its own sprite, that's the box (platform -> platform box, its texture children don't matter).
        // If it doesn't (empty parent / group), use the combined box of everything under it instead.
        // Either way the box is measured in the object's own local space, so it scales exactly with localScale.
        private static bool TryGetScaleCorners(Transform t, out Vector3[] corners)
        {
            corners = null;
            if (t == null) return false;

            if (t.TryGetComponent(out SpriteRenderer own) && own.sprite != null && own.enabled)
            {
                corners = GetSpriteCorners(own);
                return true;
            }

            bool found = false;
            Bounds local = default;

            foreach (var r in t.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (r.sprite == null || !r.enabled) continue;

                Bounds lb = r.localBounds;
                Vector3[] points =
                {
                    new Vector3(lb.min.x, lb.min.y, 0f),
                    new Vector3(lb.max.x, lb.min.y, 0f),
                    new Vector3(lb.min.x, lb.max.y, 0f),
                    new Vector3(lb.max.x, lb.max.y, 0f)
                };

                foreach (var p in points)
                {
                    // child local -> world -> root local
                    Vector3 inRoot = t.InverseTransformPoint(r.transform.TransformPoint(p));
                    if (!found)
                    {
                        local = new Bounds(inRoot, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        local.Encapsulate(inRoot);
                    }
                }
            }

            if (!found) return false;

            Vector3 min = local.min;
            Vector3 max = local.max;

            corners = new Vector3[]
            {
                t.TransformPoint(new Vector3(min.x, min.y, 0)),
                t.TransformPoint(new Vector3(max.x, min.y, 0)),
                t.TransformPoint(new Vector3(min.x, max.y, 0)),
                t.TransformPoint(new Vector3(max.x, max.y, 0))
            };
            return true;
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

            var detectorPoints = Tools.MoveVisualizer.GetDetectorSnapPoints();
            foreach (var pt in detectorPoints)
            {
                float dist = Vector2.Distance(mousePos, pt);
                if (dist < closestCornerDist) // Treat detectors like strong vertex corners
                {
                    closestCornerDist = dist;
                    bestCorner = pt;
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

        // Finds a snap point on Platform-tagged sprites near 'point'. Corners first, then edges.
        public static bool TryGetPlatformSnapPoint(Vector3 point, out Vector3 snapped, out bool isCorner)
        {
            snapped = point;
            isCorner = false;

            SpriteRenderer[] allSprites = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            Vector3 bestCorner = default, bestEdge = default;
            float bestCornerDist = float.MaxValue, bestEdgeDist = float.MaxValue;
            float maxSqrDist = BoundsSearchThreshold * BoundsSearchThreshold;

            foreach (var sprite in allSprites)
            {
                if (sprite.sprite == null || !sprite.enabled) continue;
                if (!IsValidObject(sprite.gameObject)) continue;
                if (!sprite.CompareTag("Platform")) continue;

                // Compare on the sprite's own Z so the bounds check is purely 2D
                Vector3 flat = new Vector3(point.x, point.y, sprite.bounds.center.z);
                if (sprite.bounds.SqrDistance(flat) > maxSqrDist) continue;

                Vector3[] c = GetSpriteCorners(sprite);

                foreach (var corner in c)
                {
                    float d = Vector2.Distance(point, corner);
                    if (d < bestCornerDist) { bestCornerDist = d; bestCorner = corner; }
                }

                Vector3[] edges =
                {
            GetClosestPointOnSegment(c[0], c[1], point),
            GetClosestPointOnSegment(c[1], c[3], point),
            GetClosestPointOnSegment(c[3], c[2], point),
            GetClosestPointOnSegment(c[2], c[0], point)
        };

                foreach (var pt in edges)
                {
                    float d = Vector2.Distance(point, pt);
                    if (d < bestEdgeDist) { bestEdgeDist = d; bestEdge = pt; }
                }
            }

            if (bestCornerDist <= VertexSnapRadius)
            {
                snapped = bestCorner;
                isCorner = true;
                return true;
            }

            if (bestEdgeDist <= BoundsSearchThreshold)
            {
                snapped = bestEdge;
                return true;
            }

            return false;
        }
    }
}