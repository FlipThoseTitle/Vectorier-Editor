using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Vectorier.EditorScript.Tools
{
    public class PolylineTool : EditorWindow
    {
        // ================= EDITOR STATE ================= //

        private Sprite lineSprite;
        private Sprite fillerSprite;
        private bool isDrawingActive;

        private GameObject polygonRootObject;

        private GameObject previewSegment;
        private SpriteRenderer previewRenderer;

        private float lineThicknessMultiplier = 1f;
        private readonly List<GameObject> segmentObjects = new();
        private readonly List<Vector3> polygonPoints = new();

        // ================= CONSTANTS ================= //

        private const float PolygonCloseDistance = 15f;
        private const float HandleDisplaySize = 4f;
        private const float AngleSnapStep = 45f;

        // ================= MENU ================= //

        [MenuItem("Vectorier/Tools/Polyline Tool", false, 38)]
        public static void OpenWindow()
        {
            GetWindow<PolylineTool>("Polyline Tool");
        }

        // ================= UI ================= //

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Polyline Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawSpriteSelectionFields();

            EditorGUILayout.Space();
            lineThicknessMultiplier = EditorGUILayout.FloatField("Line Thickness", Mathf.Max(0.01f, lineThicknessMultiplier));

            DrawControlButtons();
        }

        private void DrawSpriteSelectionFields()
        {
            lineSprite = (Sprite)EditorGUILayout.ObjectField("Line Sprite", lineSprite, typeof(Sprite), false);
            fillerSprite = (Sprite)EditorGUILayout.ObjectField("Corner Filler Sprite", fillerSprite, typeof(Sprite), false);
        }

        private void DrawControlButtons()
        {
            GUI.enabled = lineSprite != null;

            if (!isDrawingActive)
            {
                if (GUILayout.Button("Start"))
                {
                    BeginDrawing();
                }
            }
            else
            {
                if (GUILayout.Button("Stop"))
                {
                    EndDrawing();
                }
            }

            GUI.enabled = true;
        }

        // ================= DRAWING ================= //

        private void BeginDrawing()
        {
            isDrawingActive = true;

            polygonPoints.Clear();
            segmentObjects.Clear();

            polygonRootObject = new GameObject("LineSegment");
            Undo.RegisterCreatedObjectUndo(polygonRootObject, "Create Polygon Root");

            SceneView.duringSceneGui += OnSceneGUI;
            CreatePreviewSegment();
        }

        private void CreatePreviewSegment()
        {
            previewSegment = new GameObject("PreviewSegment");
            previewSegment.hideFlags = HideFlags.HideAndDontSave;

            previewRenderer = previewSegment.AddComponent<SpriteRenderer>();
            previewRenderer.sprite = lineSprite;
            previewRenderer.color = new Color(1f, 1f, 1f, 0.5f);
        }

        private void EndDrawing()
        {
            isDrawingActive = false;

            polygonPoints.Clear();
            segmentObjects.Clear();
            polygonRootObject = null;

            SceneView.duringSceneGui -= OnSceneGUI;

            CleanupPreview();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            CleanupPreview();
            isDrawingActive = false;
        }

        private void OnDestroy()
        {
            CleanupPreview();
        }

        // ================= SCENE ================= //

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!isDrawingActive)
            {
                return;
            }

            HandleUtility.AddDefaultControl(
                GUIUtility.GetControlID(FocusType.Passive)
            );

            Event currentEvent = Event.current;
            Vector3 mouseWorldPosition = ConvertMouseToWorld(currentEvent.mousePosition);

            DrawPointHandles(mouseWorldPosition);

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && !currentEvent.alt)
            {
                ProcessMouseClick(mouseWorldPosition);
                currentEvent.Use();
            }
            SceneView.RepaintAll();
            UpdatePreviewSegment(mouseWorldPosition);
        }

        private void UpdatePreviewSegment(Vector3 mouseWorldPosition)
        {
            if (previewSegment == null || polygonPoints.Count == 0)
            {
                if (previewSegment != null)
                    previewSegment.SetActive(false);
                return;
            }

            previewSegment.SetActive(true);

            Vector3 start = polygonPoints[^1];
            Vector3 rawDirection = mouseWorldPosition - start;

            float spriteWorldLength = lineSprite.bounds.size.x;

            Vector3 direction = GetFinalDirection(rawDirection, Event.current.shift, Event.current.control, spriteWorldLength);

            float length = direction.magnitude;

            if (length < 0.01f)
            {
                previewSegment.SetActive(false);
                return;
            }

            previewSegment.transform.position = start;
            previewSegment.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);

            Vector2 spriteSize = lineSprite.bounds.size;
            previewSegment.transform.localScale = Event.current.control ? new Vector3(1f, lineThicknessMultiplier, 1f) : new Vector3(length / spriteSize.x, lineThicknessMultiplier, 1f);
        }

        private void ProcessMouseClick(Vector3 clickWorldPosition)
        {
            Vector3 finalPosition = clickWorldPosition;

            if (polygonPoints.Count > 0)
            {
                Vector3 start = polygonPoints[^1];
                Vector3 rawDirection = clickWorldPosition - start;

                float spriteWorldLength = lineSprite.bounds.size.x;

                Vector3 direction = GetFinalDirection(rawDirection, Event.current.shift, Event.current.control, spriteWorldLength);

                finalPosition = start + direction;
            }

            if (ShouldClosePolygon(finalPosition))
            {
                ClosePolygon();
                EndDrawing();
                return;
            }

            AddNewPoint(finalPosition);
        }

        private bool ShouldClosePolygon(Vector3 clickWorldPosition)
        {
            return polygonPoints.Count >= 2 && Vector3.Distance(clickWorldPosition, polygonPoints[0]) <= PolygonCloseDistance;
        }

        private Vector3 GetSnappedDirection(Vector3 rawDirection)
        {
            float angle = Mathf.Atan2(rawDirection.y, rawDirection.x) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / AngleSnapStep) * AngleSnapStep;

            float length = rawDirection.magnitude;
            float rad = snappedAngle * Mathf.Deg2Rad;

            return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * length;
        }

        private Vector3 GetFinalDirection(Vector3 rawDirection, bool snapAngle, bool lockLength, float spriteWorldLength)
        {
            Vector3 dir = rawDirection;

            if (snapAngle)
                dir = GetSnappedDirection(dir);

            if (lockLength)
                dir = dir.normalized * spriteWorldLength;

            return dir;
        }

        // ================= POLYGON CONSTRUCTION ================= //

        private void ClosePolygon()
        {
            GameObject closingSegment = CreateSegment(polygonPoints[^1], polygonPoints[0]);

            if (segmentObjects.Count > 0)
                CreateJoint(segmentObjects[^1].transform, closingSegment.transform);

            CreateJoint(closingSegment.transform, segmentObjects[0].transform);
        }

        private void AddNewPoint(Vector3 worldPosition)
        {
            if (polygonPoints.Count > 0)
            {
                GameObject newSegment = CreateSegment(polygonPoints[^1], worldPosition);

                if (segmentObjects.Count > 0)
                    CreateJoint(segmentObjects[^1].transform, newSegment.transform);

                segmentObjects.Add(newSegment);
            }

            polygonPoints.Add(worldPosition);
        }

        // ================= RENDERING HELPERS ================= //

        private void DrawPointHandles(Vector3 mouseWorldPosition)
        {
            Handles.color = Color.white;

            foreach (Vector3 point in polygonPoints)
                Handles.DrawSolidRectangleWithOutline(GetHandleRectangle(point), Color.white, Color.black);

            if (polygonPoints.Count > 0)
                Handles.DrawLine(polygonPoints[^1], mouseWorldPosition);
        }

        private Rect GetHandleRectangle(Vector3 worldPosition)
        {
            Vector2 handleSize = Vector2.one * HandleDisplaySize;
            return new Rect(worldPosition - (Vector3)(handleSize * 0.5f), handleSize);
        }

        // ================= SEGMENT CREATION ================= //

        private GameObject CreateSegment(Vector3 startPosition, Vector3 endPosition)
        {
            Vector3 direction = endPosition - startPosition;
            float segmentLength = direction.magnitude;

            if (segmentLength < 0.1f)
                return null;

            GameObject segmentObject = new GameObject(lineSprite.name);
            Undo.RegisterCreatedObjectUndo(segmentObject, "Create Segment");

            segmentObject.transform.SetParent(polygonRootObject.transform, false);
            segmentObject.transform.position = startPosition;
            segmentObject.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction);

            SpriteRenderer spriteRenderer = segmentObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = lineSprite;

            Vector2 spriteSize = lineSprite.bounds.size;

            Vector3 localScale = Vector3.one;
            localScale.x = segmentLength / spriteSize.x;
            localScale.y = lineThicknessMultiplier;

            segmentObject.transform.localScale = localScale;

            return segmentObject;
        }

        // ================= JOINT / GAP FILLING ================= //

        private void CreateJoint(Transform previousSegment, Transform currentSegment)
        {
            if (fillerSprite == null)
                return;

            float previousRotation = previousSegment.eulerAngles.z;
            float currentRotation = currentSegment.eulerAngles.z;

            float rotationDifference = Mathf.DeltaAngle(previousRotation, currentRotation);

            if (Mathf.Abs(rotationDifference) < 0.1f)
                return;

            GameObject jointFillerObject = new GameObject(fillerSprite.name);
            Undo.RegisterCreatedObjectUndo(jointFillerObject, "Create Joint Filler");

            jointFillerObject.transform.SetParent(currentSegment.parent, false);

            SpriteRenderer fillerSpriteRenderer = jointFillerObject.AddComponent<SpriteRenderer>();
            fillerSpriteRenderer.sprite = fillerSprite;

            float bisectorRotation = previousRotation + rotationDifference * 0.5f;

            jointFillerObject.transform.rotation = Quaternion.Euler(0f, 0f, bisectorRotation);

            SpriteRenderer previousSpriteRenderer = previousSegment.GetComponent<SpriteRenderer>();
            SpriteRenderer currentSpriteRenderer = currentSegment.GetComponent<SpriteRenderer>();

            float lineThickness = previousSpriteRenderer.sprite.bounds.size.y * previousSegment.localScale.y;
            float previousSegmentWidth = previousSpriteRenderer.sprite.bounds.size.x * previousSegment.localScale.x;
            float previousSegmentHeight = previousSpriteRenderer.sprite.bounds.size.y * previousSegment.localScale.y;

            Vector3 previousBottomRightLocal = new Vector3(previousSegmentWidth, -previousSegmentHeight, 0f);
            Vector3 jointPivotWorld = previousSegment.position + previousSegment.rotation * previousBottomRightLocal;

            float currentSegmentHeight = currentSpriteRenderer.sprite.bounds.size.y * currentSegment.localScale.y;

            Vector3 currentBottomLeftLocal = new Vector3(0f, -currentSegmentHeight, 0f);
            Vector3 currentBottomLeftWorld = currentSegment.position + currentSegment.rotation * currentBottomLeftLocal;
            Vector3 pivotToTarget = currentBottomLeftWorld - jointPivotWorld;
            Vector3 fillerDirection = jointFillerObject.transform.right;

            float fillerWorldWidth = Vector3.Dot(pivotToTarget, fillerDirection);

            if (fillerWorldWidth <= 0f)
            {
                DestroyImmediate(jointFillerObject);
                return;
            }

            float fillerSpriteWidth = fillerSpriteRenderer.sprite.bounds.size.x;

            Vector3 fillerScale = Vector3.one;
            fillerScale.x = fillerWorldWidth / fillerSpriteWidth;
            fillerScale.y = previousSegment.localScale.y;

            jointFillerObject.transform.localScale = fillerScale;

            Vector3 fillerBottomLeftLocal = new Vector3(0f, -lineThickness, 0f);
            Vector3 fillerBottomLeftWorldOffset = jointFillerObject.transform.rotation * fillerBottomLeftLocal;

            jointFillerObject.transform.position = jointPivotWorld - fillerBottomLeftWorldOffset;
        }

        // ================= UTILITY ================= //

        private Vector3 ConvertMouseToWorld(Vector2 mousePosition)
        {
            Ray guiRay = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane drawingPlane = new Plane(Vector3.forward, Vector3.zero);

            drawingPlane.Raycast(guiRay, out float distance);

            return guiRay.GetPoint(distance);
        }

        private void CleanupPreview()
        {
            if (previewSegment != null)
            {
                DestroyImmediate(previewSegment);
                previewSegment = null;
                previewRenderer = null;
            }
        }
    }
}
