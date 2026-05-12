using UnityEditor;
using UnityEngine;
using System;

namespace Vectorier.Model
{
    public sealed class ModelDebug : IDisposable
    {
        // ================= SETTINGS ================= //

        public bool RenderEdges { get; set; } = false;
        public bool RenderDetector { get; set; } = false;
        public bool RenderTrajectory { get; set; } = true;
        public bool FollowCamera { get; set; } = false;

        public float NodeSphereSize { get; set; } = 2f;
        public float CameraSphereSize { get; set; } = 12f;
        public string CameraNodeName { get; set; } = "Camera";

        public int DeltaDetectorH
        {
            get => deltaDetectorH;
            set
            {
                int next = Mathf.Max(0, value);
                if (deltaDetectorH == next)
                    return;

                deltaDetectorH = next;
                UpdateDetectorLines(lastNodeWorldPositions);
            }
        }

        public int DeltaDetectorV
        {
            get => deltaDetectorV;
            set
            {
                int next = Mathf.Max(0, value);
                if (deltaDetectorV == next)
                    return;

                deltaDetectorV = next;
                UpdateDetectorLines(lastNodeWorldPositions);
            }
        }

        // ================= INTERNAL STATE ================= //

        static Material unlitMaterial;

        ModelData model;
        GameObject modelRootObject;
        Vector3[] lastNodeWorldPositions;

        int deltaDetectorH;
        int deltaDetectorV;

        int detectorHIndex = -1;
        int detectorVIndex = -1;

        GameObject detectorHLineObject;
        GameObject detectorVLineObject;

        LineRenderer detectorHLine;
        LineRenderer detectorVLine;

        GameObject detectorHStartDotObject;
        GameObject detectorHEndDotObject;
        GameObject detectorVStartDotObject;
        GameObject detectorVEndDotObject;

        Transform detectorHStartDot;
        Transform detectorHEndDot;
        Transform detectorVStartDot;
        Transform detectorVEndDot;

        Material detectorLineMaterial;
        Material detectorDotMaterial;

        const float DetectorLineWidth = 2.5f;
        const float DetectorDotDiameter = 8f;
        const int DetectorSortingOrder = 5000;
        const string DetectorSortingLayer = "OnTop";
        const float DetectorOverlayZOffset = -0.01f;

        // ================= PUBLIC API ================= //

        public void AttachToModel(ModelData modelData, GameObject rootObject)
        {
            model = modelData;
            modelRootObject = rootObject;

            DestroyDetectorObjects();

            detectorHIndex = -1;
            detectorVIndex = -1;

            if (model == null || modelRootObject == null)
                return;

            model.TryGetNodeIndex("DetectorH", out detectorHIndex);
            model.TryGetNodeIndex("DetectorV", out detectorVIndex);

            CreateDetectorObjects();
            UpdateDetectorLines(lastNodeWorldPositions);
        }

        public void UpdateNodeWorldPositions(Vector3[] nodePositions, Transform parentTransform)
        {
            if (nodePositions == null)
            {
                lastNodeWorldPositions = null;
                UpdateDetectorLines(null);
                return;
            }

            if (lastNodeWorldPositions == null || lastNodeWorldPositions.Length != nodePositions.Length)
                lastNodeWorldPositions = new Vector3[nodePositions.Length];

            for (int i = 0; i < nodePositions.Length; i++)
            {
                Vector3 localPushedPos = nodePositions[i] + ModelAnimation.AnimZPushVector;

                lastNodeWorldPositions[i] = parentTransform != null
                    ? parentTransform.TransformPoint(localPushedPos)
                    : localPushedPos;
            }

            UpdateDetectorLines(lastNodeWorldPositions);
        }

        public void DrawScene(ModelAnimation animation, Transform parentTransform, bool renderModel)
        {
            if (animation == null)
                return;

            DrawPreview(animation, parentTransform);

            if (RenderEdges)
            {
                DrawAnimation(animation, parentTransform);
                DrawConnections(animation, parentTransform, renderModel);
            }

            if (RenderTrajectory)
                DrawCenterOfMassPath(animation, parentTransform);
        }

        public void FollowSceneViewCameraToNodeXY(ModelAnimation animation)
        {
            if (!FollowCamera || animation == null)
                return;

            Vector3? targetOpt = animation.GetAnimationNodeWorldPosition(CameraNodeName);
            if (!targetOpt.HasValue)
                return;

            SceneView sv = SceneView.lastActiveSceneView;
            if (sv == null)
                return;

            Vector3 pivot = sv.pivot;
            Vector3 target = targetOpt.Value;

            pivot.x = target.x;
            pivot.y = target.y;
            sv.pivot = pivot;
            sv.Repaint();
        }

        public void Destroy()
        {
            DestroyDetectorObjects();
            lastNodeWorldPositions = null;
            model = null;
            modelRootObject = null;

            detectorHIndex = -1;
            detectorVIndex = -1;

            if (detectorLineMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(detectorLineMaterial);
                detectorLineMaterial = null;
            }

            if (detectorDotMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(detectorDotMaterial);
                detectorDotMaterial = null;
            }
        }

        public void Dispose()
        {
            Destroy();
        }

        // ================= SCENE DRAWING ================= //

        void DrawPreview(ModelAnimation animation, Transform parentTransform)
        {
            if (!animation.IsPreviewActive || animation.PreviewNodes == null)
                return;

            DrawNodeSet(
                animation,
                animation.PreviewNodes,
                parentTransform,
                new Color(1f, 0f, 0f, 0.4f),
                new Color(1f, 1f, 0f, 0.6f)
            );
        }

        void DrawAnimation(ModelAnimation animation, Transform parentTransform)
        {
            if (animation.AnimationNodes == null)
                return;

            DrawNodeSet(animation, animation.AnimationNodes, parentTransform, Color.red, Color.yellow);
        }

        void DrawNodeSet(ModelAnimation animation, Vector3[] nodeSet, Transform parentTransform, Color normalColor, Color cameraColor)
        {
            if (nodeSet == null)
                return;

            EnsureUnlitMaterial();

            int cameraIndex = -1;
            if (animation.Model != null)
                animation.Model.TryGetNodeIndex(CameraNodeName, out cameraIndex);

            Mesh sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            if (sphereMesh == null)
                return;

            for (int i = 0; i < nodeSet.Length; i++)
            {
                bool isCamera = i == cameraIndex;

                Vector3 pushedNodePosition = nodeSet[i] + ModelAnimation.AnimZPushVector;

                Vector3 worldPos = parentTransform != null
                    ? parentTransform.TransformPoint(pushedNodePosition)
                    : pushedNodePosition;

                unlitMaterial.SetColor("_Color", isCamera ? cameraColor : normalColor);
                unlitMaterial.SetPass(0);

                Graphics.DrawMeshNow(
                    sphereMesh,
                    Matrix4x4.TRS(
                        worldPos,
                        Quaternion.identity,
                        Vector3.one * (isCamera ? CameraSphereSize : NodeSphereSize)
                    )
                );
            }
        }

        void DrawCenterOfMassPath(ModelAnimation animation, Transform parentTransform)
        {
            if (animation.CenterOfMassPath == null || animation.CenterOfMassPath.Count < 2)
                return;

            Vector3[] points = new Vector3[animation.CenterOfMassPath.Count];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 pushedPoint = animation.CenterOfMassPath[i] + ModelAnimation.AnimZPushVector;
                points[i] = parentTransform != null ? parentTransform.TransformPoint(pushedPoint) : pushedPoint;
            }

            Handles.color = Color.red;
            Handles.DrawAAPolyLine(3f, points);
        }

        void DrawConnections(ModelAnimation animation, Transform parentTransform, bool renderModel)
        {
            if (animation.AnimationNodes == null || animation.Model == null)
                return;

            if (animation.IsPreviewActive && animation.PreviewNodes != null)
            {
                Handles.color = new Color(0f, 1f, 0f, 0.35f);
                DrawConnectionSet(animation.Model, animation.PreviewNodes, parentTransform);
            }

            Handles.color = renderModel ? Color.yellow : Color.green;
            DrawConnectionSet(animation.Model, animation.AnimationNodes, parentTransform);
        }

        void DrawConnectionSet(ModelData modelData, Vector3[] nodeSet, Transform parentTransform)
        {
            if (modelData == null || nodeSet == null)
                return;

            for (int i = 0; i < modelData.Connections.Count; i++)
            {
                var e = modelData.Connections[i];

                if (string.Equals(e.Type, "Muscle", StringComparison.OrdinalIgnoreCase))
                    continue;

                if ((uint)e.A >= (uint)nodeSet.Length || (uint)e.B >= (uint)nodeSet.Length)
                    continue;

                Vector3 a = parentTransform != null
                    ? parentTransform.TransformPoint(nodeSet[e.A] + ModelAnimation.AnimZPushVector)
                    : nodeSet[e.A] + ModelAnimation.AnimZPushVector;

                Vector3 b = parentTransform != null
                    ? parentTransform.TransformPoint(nodeSet[e.B] + ModelAnimation.AnimZPushVector)
                    : nodeSet[e.B] + ModelAnimation.AnimZPushVector;

                Handles.DrawLine(a, b);
            }
        }

        static void EnsureUnlitMaterial()
        {
            if (unlitMaterial != null)
                return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return;

            unlitMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            unlitMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            unlitMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            unlitMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            unlitMaterial.SetInt("_ZWrite", 0);
            unlitMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            unlitMaterial.renderQueue = 5000;
        }

        // ================= DETECTOR DEBUG ================= //

        void CreateDetectorObjects()
        {
            if (modelRootObject == null)
                return;

            CreateDetectorMaterials();

            if (detectorHIndex >= 0)
            {
                detectorHLineObject = new GameObject("DetectorH_Line")
                {
                    hideFlags = HideFlags.DontSave
                };
                detectorHLineObject.transform.SetParent(modelRootObject.transform, false);

                detectorHLine = detectorHLineObject.AddComponent<LineRenderer>();
                ConfigureDetectorLine(detectorHLine);

                detectorHStartDotObject = CreateDetectorDot("DetectorH_StartDot", out detectorHStartDot);
                detectorHEndDotObject = CreateDetectorDot("DetectorH_EndDot", out detectorHEndDot);
            }

            if (detectorVIndex >= 0)
            {
                detectorVLineObject = new GameObject("DetectorV_Line")
                {
                    hideFlags = HideFlags.DontSave
                };
                detectorVLineObject.transform.SetParent(modelRootObject.transform, false);

                detectorVLine = detectorVLineObject.AddComponent<LineRenderer>();
                ConfigureDetectorLine(detectorVLine);

                detectorVStartDotObject = CreateDetectorDot("DetectorV_StartDot", out detectorVStartDot);
                detectorVEndDotObject = CreateDetectorDot("DetectorV_EndDot", out detectorVEndDot);
            }
        }

        void DestroyDetectorObjects()
        {
            detectorHLine = null;
            detectorVLine = null;

            detectorHStartDot = null;
            detectorHEndDot = null;
            detectorVStartDot = null;
            detectorVEndDot = null;

            if (detectorHLineObject != null)
                UnityEngine.Object.DestroyImmediate(detectorHLineObject);
            if (detectorVLineObject != null)
                UnityEngine.Object.DestroyImmediate(detectorVLineObject);
            if (detectorHStartDotObject != null)
                UnityEngine.Object.DestroyImmediate(detectorHStartDotObject);
            if (detectorHEndDotObject != null)
                UnityEngine.Object.DestroyImmediate(detectorHEndDotObject);
            if (detectorVStartDotObject != null)
                UnityEngine.Object.DestroyImmediate(detectorVStartDotObject);
            if (detectorVEndDotObject != null)
                UnityEngine.Object.DestroyImmediate(detectorVEndDotObject);

            detectorHLineObject = null;
            detectorVLineObject = null;
            detectorHStartDotObject = null;
            detectorHEndDotObject = null;
            detectorVStartDotObject = null;
            detectorVEndDotObject = null;
        }

        void CreateDetectorMaterials()
        {
            if (detectorLineMaterial == null)
            {
                Shader lineShader = Shader.Find("Sprites/Default");
                if (lineShader != null)
                {
                    detectorLineMaterial = new Material(lineShader)
                    {
                        hideFlags = HideFlags.DontSave
                    };
                    detectorLineMaterial.renderQueue = 5000;
                }
            }

            if (detectorDotMaterial == null)
            {
                Shader dotShader = Shader.Find("Unlit/Color");
                if (dotShader == null)
                    dotShader = Shader.Find("Sprites/Default");

                if (dotShader != null)
                {
                    detectorDotMaterial = new Material(dotShader)
                    {
                        hideFlags = HideFlags.DontSave,
                        color = Color.green
                    };
                    detectorDotMaterial.renderQueue = 5000;
                }
            }
        }

        void ConfigureDetectorLine(LineRenderer line)
        {
            if (line == null)
                return;

            line.hideFlags = HideFlags.DontSave;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.loop = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.startWidth = DetectorLineWidth;
            line.endWidth = DetectorLineWidth;
            line.startColor = Color.blue;
            line.endColor = Color.blue;

            if (detectorLineMaterial != null)
                line.sharedMaterial = detectorLineMaterial;

            line.sortingOrder = DetectorSortingOrder;
            line.sortingLayerName = DetectorSortingLayer;
        }

        GameObject CreateDetectorDot(string objectName, out Transform dotTransform)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = objectName;
            dot.hideFlags = HideFlags.DontSave;
            dot.transform.SetParent(modelRootObject.transform, false);

            UnityEngine.Object.DestroyImmediate(dot.GetComponent<Collider>());

            dotTransform = dot.transform;
            dotTransform.localScale = Vector3.one * DetectorDotDiameter;

            var renderer = dot.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (detectorDotMaterial != null)
                    renderer.sharedMaterial = detectorDotMaterial;

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = DetectorSortingOrder;
                renderer.sortingLayerName = DetectorSortingLayer;
            }

            return dot;
        }

        void UpdateDetectorLines(Vector3[] nodeWorldPositions)
        {
            if (modelRootObject == null || nodeWorldPositions == null)
                return;

            if (!RenderDetector)
            {
                if (detectorHLine != null)
                    detectorHLine.enabled = false;
                if (detectorVLine != null)
                    detectorVLine.enabled = false;

                SetDetectorDotActive(detectorHStartDot, false);
                SetDetectorDotActive(detectorHEndDot, false);
                SetDetectorDotActive(detectorVStartDot, false);
                SetDetectorDotActive(detectorVEndDot, false);
                return;
            }

            UpdateHorizontalDetectorLine(nodeWorldPositions);
            UpdateVerticalDetectorLine(nodeWorldPositions);
        }

        void UpdateHorizontalDetectorLine(Vector3[] nodeWorldPositions)
        {
            if (detectorHLine == null || detectorHIndex < 0 || detectorHIndex >= nodeWorldPositions.Length)
                return;

            if (deltaDetectorH <= 0)
            {
                detectorHLine.enabled = false;
                SetDetectorDotActive(detectorHStartDot, false);
                SetDetectorDotActive(detectorHEndDot, false);
                return;
            }

            Vector3 center = nodeWorldPositions[detectorHIndex];
            float totalLength = deltaDetectorH;
            float halfLength = totalLength * 0.5f;

            Vector3 start = GetDetectorOverlayPosition(center + Vector3.left * halfLength);
            Vector3 end = GetDetectorOverlayPosition(center + Vector3.right * halfLength);

            detectorHLine.enabled = true;
            detectorHLine.SetPosition(0, start);
            detectorHLine.SetPosition(1, end);

            if (detectorHStartDot != null)
            {
                detectorHStartDot.position = start;
                detectorHStartDot.localScale = Vector3.one * DetectorDotDiameter;
                SetDetectorDotActive(detectorHStartDot, true);
            }

            if (detectorHEndDot != null)
            {
                detectorHEndDot.position = end;
                detectorHEndDot.localScale = Vector3.one * DetectorDotDiameter;
                SetDetectorDotActive(detectorHEndDot, true);
            }
        }

        void UpdateVerticalDetectorLine(Vector3[] nodeWorldPositions)
        {
            if (detectorVLine == null || detectorVIndex < 0 || detectorVIndex >= nodeWorldPositions.Length)
                return;

            if (deltaDetectorV <= 0)
            {
                detectorVLine.enabled = false;
                SetDetectorDotActive(detectorVStartDot, false);
                SetDetectorDotActive(detectorVEndDot, false);
                return;
            }

            Vector3 center = nodeWorldPositions[detectorVIndex];
            float totalLength = deltaDetectorV;
            float halfLength = totalLength * 0.5f;

            Vector3 start = GetDetectorOverlayPosition(center + Vector3.down * halfLength);
            Vector3 end = GetDetectorOverlayPosition(center + Vector3.up * halfLength);

            detectorVLine.enabled = true;
            detectorVLine.SetPosition(0, start);
            detectorVLine.SetPosition(1, end);

            if (detectorVStartDot != null)
            {
                detectorVStartDot.position = start;
                detectorVStartDot.localScale = Vector3.one * DetectorDotDiameter;
                SetDetectorDotActive(detectorVStartDot, true);
            }

            if (detectorVEndDot != null)
            {
                detectorVEndDot.position = end;
                detectorVEndDot.localScale = Vector3.one * DetectorDotDiameter;
                SetDetectorDotActive(detectorVEndDot, true);
            }
        }

        static void SetDetectorDotActive(Transform dot, bool active)
        {
            if (dot != null)
                dot.gameObject.SetActive(active);
        }

        static Vector3 GetDetectorOverlayPosition(Vector3 position)
        {
            return position + new Vector3(0f, 0f, DetectorOverlayZOffset);
        }
    }
}