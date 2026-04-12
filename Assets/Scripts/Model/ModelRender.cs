using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Xml;
using System.Globalization;
using System.Collections.Generic;

namespace Vectorier.Model
{
    [Serializable]
    public struct EdgeIndex
    {
        public int A;
        public int B;
        public string Name;
        public string Type;
    }

    [Serializable]
    public struct CapsuleDef
    {
        public string Name;
        public string EdgeName;
        public int A;
        public int B;
        public float Radius1;
        public float Radius2;
        public float Margin1; // [0..1] start trim
        public float Margin2; // [0..1] end trim
    }

    public struct CapsuleRuntime
    {
        public CapsuleDef Def;
        public GameObject Root;
        public Transform Cylinder;
        public Transform SphereA;
        public Transform SphereB;
    }

    public sealed class ModelData
    {
        public string SourcePath { get; private set; }

        public readonly List<string> NodeNamesOrdered = new();
        public readonly Dictionary<string, int> NodeIndexByName = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<EdgeIndex> Connections = new();
        public readonly List<CapsuleDef> Capsules = new();

        public Vector3[] PreviewPose { get; private set; }
        public int NodeCount => NodeNamesOrdered.Count;

        public bool TryGetNodeIndex(string name, out int idx) => NodeIndexByName.TryGetValue(name, out idx);

        public static ModelData LoadOrThrow(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException($"Model XML not found. Resolved: '{path}'");

            byte[] bytes = File.ReadAllBytes(path);
            string xmlText = System.Text.Encoding.GetEncoding("Windows-1251").GetString(bytes);

            var doc = new XmlDocument();
            doc.LoadXml(xmlText);

            XmlNode scene = doc.SelectSingleNode("/Scene") ?? throw new Exception("Invalid XML: missing <Scene> root.");
            XmlNode nodesElem = scene.SelectSingleNode("Nodes") ?? throw new Exception("Invalid XML: missing <Nodes>.");

            var model = new ModelData
            {
                SourcePath = path
            };

            int idx = 0;
            foreach (XmlNode node in nodesElem.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element)
                    continue;

                string name = node.Name;
                model.NodeNamesOrdered.Add(name);
                model.NodeIndexByName[name] = idx++;
            }

            if (model.NodeCount <= 0)
                throw new Exception("XML <Nodes> is empty.");

            model.PreviewPose = new Vector3[model.NodeCount];

            int previewIndex = 0;
            foreach (XmlNode node in nodesElem.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element)
                    continue;

                float x = XmlUtility.GetFloatAttr(node, "X", 0f);
                float y = XmlUtility.GetFloatAttr(node, "Y", 0f);
                float z = XmlUtility.GetFloatAttr(node, "Z", 0f);

                model.PreviewPose[previewIndex++] = new Vector3(x, y, -z);

                if (previewIndex >= model.PreviewPose.Length)
                    break;
            }

            XmlNode edgesElem = scene.SelectSingleNode("Edges");
            if (edgesElem != null)
            {
                foreach (XmlNode edgeNode in edgesElem.ChildNodes)
                {
                    if (edgeNode.NodeType != XmlNodeType.Element)
                        continue;

                    string edgeName = edgeNode.Name;
                    string type = XmlUtility.GetAttrOrNull(edgeNode, "Type");
                    string end1 = XmlUtility.GetAttr(edgeNode, "End1");
                    string end2 = XmlUtility.GetAttr(edgeNode, "End2");

                    if (!model.NodeIndexByName.TryGetValue(end1, out int a) ||
                        !model.NodeIndexByName.TryGetValue(end2, out int b))
                        continue;

                    model.Connections.Add(new EdgeIndex
                    {
                        Name = edgeName,
                        Type = type,
                        A = a,
                        B = b
                    });
                }
            }

            XmlNode figuresElem = scene.SelectSingleNode("Figures");
            if (figuresElem != null)
            {
                foreach (XmlNode figureNode in figuresElem.ChildNodes)
                {
                    if (figureNode.NodeType != XmlNodeType.Element)
                        continue;

                    string type = XmlUtility.GetAttrOrNull(figureNode, "Type");
                    if (!string.Equals(type, "Capsule", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string capsuleName = figureNode.Name;
                    string edgeName = XmlUtility.GetAttr(figureNode, "Edge");

                    float radius1 = XmlUtility.GetFloatAttr(figureNode, "Radius1", 1f);
                    float radius2 = XmlUtility.GetFloatAttr(figureNode, "Radius2", radius1);
                    float margin1 = Mathf.Clamp01(XmlUtility.GetFloatAttr(figureNode, "Margin1", 0f));
                    float margin2 = Mathf.Clamp01(XmlUtility.GetFloatAttr(figureNode, "Margin2", 0f));

                    int edgeIdx = model.Connections.FindIndex(x =>
                        string.Equals(x.Name, edgeName, StringComparison.OrdinalIgnoreCase));

                    if (edgeIdx < 0)
                        continue;

                    var edge = model.Connections[edgeIdx];

                    model.Capsules.Add(new CapsuleDef
                    {
                        Name = capsuleName,
                        EdgeName = edgeName,
                        A = edge.A,
                        B = edge.B,
                        Radius1 = radius1,
                        Radius2 = radius2,
                        Margin1 = margin1,
                        Margin2 = margin2
                    });
                }
            }

            return model;
        }
    }

    public sealed class ModelRenderer : IDisposable
    {
        readonly string blackMaterialPath;

        GameObject rootObject;
        readonly List<CapsuleRuntime> capsuleRuntimes = new();

        Material blackMaterial;
        Material defaultMaterial;

        public GameObject RootObject => rootObject;
        public IReadOnlyList<CapsuleRuntime> CapsuleRuntimes => capsuleRuntimes;

        public ModelRenderer(string blackMaterialPath)
        {
            this.blackMaterialPath = blackMaterialPath;
        }

        public void Create(ModelData model, Vector3[] nodeLocalPositions, Transform parentTransform, bool renderBlack)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            if (rootObject != null)
                return;

            rootObject = new GameObject("MoveVisualizerModel")
            {
                hideFlags = HideFlags.DontSave
            };

            rootObject.transform.SetParent(parentTransform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;

            capsuleRuntimes.Clear();

            foreach (var capsule in model.Capsules)
            {
                var capsuleRoot = new GameObject(capsule.Name)
                {
                    hideFlags = HideFlags.DontSave
                };
                capsuleRoot.transform.SetParent(rootObject.transform, false);

                GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                CacheDefaultMaterialIfNeeded(cylinder);
                cylinder.name = "Cylinder";
                cylinder.hideFlags = HideFlags.DontSave;
                cylinder.transform.SetParent(capsuleRoot.transform, false);
                UnityEngine.Object.DestroyImmediate(cylinder.GetComponent<Collider>());

                GameObject sphereA = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereA.name = "Sphere_A";
                sphereA.hideFlags = HideFlags.DontSave;
                sphereA.transform.SetParent(capsuleRoot.transform, false);
                UnityEngine.Object.DestroyImmediate(sphereA.GetComponent<Collider>());

                GameObject sphereB = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereB.name = "Sphere_B";
                sphereB.hideFlags = HideFlags.DontSave;
                sphereB.transform.SetParent(capsuleRoot.transform, false);
                UnityEngine.Object.DestroyImmediate(sphereB.GetComponent<Collider>());

                capsuleRuntimes.Add(new CapsuleRuntime
                {
                    Def = capsule,
                    Root = capsuleRoot,
                    Cylinder = cylinder.transform,
                    SphereA = sphereA.transform,
                    SphereB = sphereB.transform
                });
            }

            ApplyRenderSettings(renderBlack);

            if (nodeLocalPositions != null && nodeLocalPositions.Length > 0)
                UpdateCapsules(nodeLocalPositions);
        }

        public void Destroy()
        {
            capsuleRuntimes.Clear();

            if (rootObject != null)
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                rootObject = null;
            }
        }

        public void ApplyRenderSettings(bool renderBlack)
        {
            Material targetMaterial = null;

            if (renderBlack)
                targetMaterial = GetBlackMaterial();

            if (targetMaterial == null)
                targetMaterial = defaultMaterial;

            for (int i = 0; i < capsuleRuntimes.Count; i++)
            {
                var runtime = capsuleRuntimes[i];
                ApplyRendererSettings(runtime.Cylinder, targetMaterial);
                ApplyRendererSettings(runtime.SphereA, targetMaterial);
                ApplyRendererSettings(runtime.SphereB, targetMaterial);
            }
        }

        public void UpdateCapsules(Vector3[] nodeLocal)
        {
            if (nodeLocal == null || rootObject == null)
                return;

            for (int i = 0; i < capsuleRuntimes.Count; i++)
            {
                var runtime = capsuleRuntimes[i];
                var def = runtime.Def;

                if ((uint)def.A >= (uint)nodeLocal.Length || (uint)def.B >= (uint)nodeLocal.Length)
                    continue;

                Vector3 a = nodeLocal[def.A];
                Vector3 b = nodeLocal[def.B];

                float t0 = Mathf.Clamp01(def.Margin1);
                float t1 = Mathf.Clamp01(1f - def.Margin2);
                if (t1 < t0)
                    (t0, t1) = (t1, t0);

                Vector3 p0 = Vector3.Lerp(a, b, t0);
                Vector3 p1 = Vector3.Lerp(a, b, t1);

                Vector3 dir = p1 - p0;
                float len = dir.magnitude;

                if (len < 1e-4f)
                {
                    SetCapsuleActive(runtime, false);
                    capsuleRuntimes[i] = runtime;
                    continue;
                }

                SetCapsuleActive(runtime, true);

                Vector3 dirNormalized = dir / len;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, dirNormalized);

                float cylinderRadius = Mathf.Max(0.0001f, (def.Radius1 + def.Radius2) * 0.5f);
                float radiusA = Mathf.Max(0.0001f, def.Radius1);
                float radiusB = Mathf.Max(0.0001f, def.Radius2);

                runtime.SphereA.localPosition = p0;
                runtime.SphereA.localRotation = Quaternion.identity;
                runtime.SphereA.localScale = Vector3.one * (radiusA * 2f);

                runtime.SphereB.localPosition = p1;
                runtime.SphereB.localRotation = Quaternion.identity;
                runtime.SphereB.localScale = Vector3.one * (radiusB * 2f);

                float cylinderHeight = Mathf.Max(0.0001f, len);
                runtime.Cylinder.localPosition = (p0 + p1) * 0.5f;
                runtime.Cylinder.localRotation = rotation;
                runtime.Cylinder.localScale = new Vector3(
                    cylinderRadius * 2f,
                    cylinderHeight * 0.5f,
                    cylinderRadius * 2f
                );

                capsuleRuntimes[i] = runtime;
            }
        }

        void CacheDefaultMaterialIfNeeded(GameObject primitive)
        {
            if (defaultMaterial != null || primitive == null)
                return;

            var renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
                defaultMaterial = renderer.sharedMaterial;
        }

        Material GetBlackMaterial()
        {
            if (blackMaterial != null)
                return blackMaterial;

            blackMaterial = AssetDatabase.LoadAssetAtPath<Material>(blackMaterialPath);
            if (blackMaterial == null)
                Debug.LogWarning($"[ModelRenderer] Black material not found at {blackMaterialPath}");

            return blackMaterial;
        }

        static void SetCapsuleActive(CapsuleRuntime runtime, bool active)
        {
            if (runtime.Cylinder != null) runtime.Cylinder.gameObject.SetActive(active);
            if (runtime.SphereA != null) runtime.SphereA.gameObject.SetActive(active);
            if (runtime.SphereB != null) runtime.SphereB.gameObject.SetActive(active);
        }

        static void ApplyRendererSettings(Transform target, Material material)
        {
            if (target == null)
                return;

            var renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                return;

            if (material != null)
                renderer.sharedMaterial = material;
        }

        public void Dispose()
        {
            Destroy();
        }
    }

    public static class XmlUtility
    {
        public static string GetAttr(XmlNode node, string attr)
        {
            var a = node.Attributes?[attr];
            if (a == null || string.IsNullOrWhiteSpace(a.Value))
                throw new Exception($"XML missing attribute '{attr}' on <{node.Name}>");

            return a.Value;
        }

        public static string GetAttrOrNull(XmlNode node, string attr)
        {
            return node.Attributes?[attr]?.Value;
        }

        public static float GetFloatAttr(XmlNode node, string attr, float fallback)
        {
            var a = node.Attributes?[attr];
            if (a == null || string.IsNullOrWhiteSpace(a.Value))
                return fallback;

            if (float.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                return value;

            if (float.TryParse(a.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return value;

            return fallback;
        }
    }
}