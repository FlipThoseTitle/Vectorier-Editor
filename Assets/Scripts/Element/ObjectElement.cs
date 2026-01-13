using System.Globalization;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.Rendering;
using Vectorier.XML;
using Vectorier.Handler;
using Vectorier.Component;
using Vectorier.Dynamic;

namespace Vectorier.Element
{
    public static class ObjectElement
    {
        private enum ImportDepthGroup
        {
            Back,
            Middle,
            Front
        }

        // ================= EXPORT ================= //

        public static XmlElement WriteToXML(GameObject sourceObject, XmlUtility xmlUtility, XmlElement parentXmlElement, ExportHandler.ExportMode exportMode)
        {
            if (sourceObject == null || xmlUtility == null || parentXmlElement == null)
                return null;

            XmlElement objectXmlElement = xmlUtility.AddElement(parentXmlElement, "Object");

            WriteName(sourceObject, xmlUtility, objectXmlElement);
            WriteExportSpecificData(sourceObject, xmlUtility, objectXmlElement, exportMode);

            WriteSelection(sourceObject, xmlUtility, objectXmlElement);
            WriteDynamic(sourceObject, xmlUtility, objectXmlElement);

            return objectXmlElement;
        }

        private static void WriteExportSpecificData(GameObject sourceObject, XmlUtility xmlUtility, XmlElement objectXmlElement, ExportHandler.ExportMode exportMode)
        {
            bool hasChildren = sourceObject.transform.childCount > 0;

            switch (exportMode)
            {
                case ExportHandler.ExportMode.Level:
                    Element.WritePosition(xmlUtility, objectXmlElement, sourceObject);

                    if (hasChildren)
                        WriteContent(sourceObject, xmlUtility, objectXmlElement, exportMode);
                    break;

                case ExportHandler.ExportMode.Objects:
                    WriteContent(sourceObject, xmlUtility, objectXmlElement, exportMode);
                    break;

                case ExportHandler.ExportMode.Buildings:
                    WriteInOut(sourceObject, xmlUtility, objectXmlElement);
                    WriteBounds(sourceObject, xmlUtility, objectXmlElement);
                    WriteContent(sourceObject, xmlUtility, objectXmlElement, exportMode);
                    break;
            }
        }

        private static void WriteContent(GameObject sourceObject, XmlUtility xmlUtility, XmlElement objectXmlElement, ExportHandler.ExportMode exportMode)
        {
            XmlElement contentElement = xmlUtility.AddElement(objectXmlElement, "Content");
            WriteChildren(sourceObject, xmlUtility, contentElement, exportMode);
        }

        // ================= IMPORT ================= //

        public static GameObject WriteToScene(XmlElement xmlElement, Transform parentTransform, string layerName, bool includeBuildingsMarker, XmlUtility xmlUtility)
        {
            if (xmlElement == null)
                return null;

            GameObject gameObject = CreateObject(xmlElement, parentTransform);
            Element.ApplyLayer(gameObject, layerName);

            ApplySortingGroupLayering(xmlElement, gameObject, layerName);

            CreateInOutMarkers(xmlElement, gameObject, includeBuildingsMarker);
            WriteSceneChildren(xmlElement, gameObject.transform, layerName, includeBuildingsMarker, xmlUtility);
            ApplyDynamic(gameObject, xmlUtility, xmlElement);

            return gameObject;
        }

        private static GameObject CreateObject(XmlElement xmlElement, Transform parentTransform)
        {
            string objectName = xmlElement.HasAttribute("Name") ? xmlElement.GetAttribute("Name") : "Object";

            Vector3 position = GetPosition(xmlElement);

            GameObject gameObject = new GameObject(objectName);
            gameObject.transform.SetParent(parentTransform, false);
            gameObject.transform.localPosition = position;

            return gameObject;
        }

        private static void WriteSceneChildren(XmlElement xmlElement, Transform parentTransform, string layerName, bool includeBuildingsMarker, XmlUtility xmlUtility)
        {
            XmlElement contentElement = xmlElement.SelectSingleNode("Content") as XmlElement;
            if (contentElement == null)
                return;

            foreach (XmlNode childNode in contentElement.ChildNodes)
            {
                if (childNode is XmlElement childElement)
                    ImportHandler.WriteByTag(childElement, parentTransform, layerName, includeBuildingsMarker, xmlUtility);
            }
        }

        private static void ApplySortingGroupLayering(XmlElement xmlElement, GameObject objectRoot, string factor)
        {
            if (objectRoot == null)
                return;

            SortingGroup group = objectRoot.GetComponent<SortingGroup>();
            if (group == null)
                group = objectRoot.AddComponent<SortingGroup>();

            ImportDepthGroup depth = GetDepthGroupFromXML(xmlElement);

            float factorValue = float.Parse(factor, CultureInfo.InvariantCulture);
            int factorBand = Mathf.RoundToInt(factorValue * 10000);

            int sortOffset =
                depth == ImportDepthGroup.Front ? 200 + ImportHandler.GlobalOrder_Front++ :
                depth == ImportDepthGroup.Back ? 0 + ImportHandler.GlobalOrder_Back++ :
                100 + ImportHandler.GlobalOrder_Middle++;

            group.sortingOrder = factorBand + sortOffset;
        }

        private static ImportDepthGroup GetDepthGroupFromXML(XmlElement xmlElement)
        {
            if (xmlElement != null && xmlElement.HasAttribute("Depth") && int.TryParse(xmlElement.GetAttribute("Depth"), out int depthValue))
            {
                if (depthValue == 0) return ImportDepthGroup.Front;
                if (depthValue == 1) return ImportDepthGroup.Back;
            }

            return ImportDepthGroup.Middle;
        }

        // ================= POSITION ================= //

        private static Vector3 GetPosition(XmlElement xmlElement)
        {
            float positionX = 0f;
            float positionY = 0f;

            if (xmlElement.HasAttribute("X") && xmlElement.HasAttribute("Y"))
            {
                positionX = Element.ParseFloat(xmlElement.GetAttribute("X"));
                positionY = -Element.ParseFloat(xmlElement.GetAttribute("Y"));
            }
            else if (xmlElement.HasAttribute("InX") && xmlElement.HasAttribute("InY"))
            {
                positionX = Element.ParseFloat(xmlElement.GetAttribute("InX"));
                positionY = -Element.ParseFloat(xmlElement.GetAttribute("InY"));
            }

            return new Vector3(positionX, positionY, 0f);
        }

        // ================= CHILDREN EXPORT ================= //

        private static void WriteChildren(GameObject parentObject, XmlUtility xmlUtility, XmlElement parentXmlElement, ExportHandler.ExportMode exportMode)
        {
            List<GameObject> exportables = new List<GameObject>();

            foreach (Transform childTransform in parentObject.transform)
            {
                GameObject childObject = childTransform.gameObject;

                if (!childObject.activeInHierarchy)
                    continue;

                if (string.IsNullOrEmpty(childObject.tag) || childObject.CompareTag("Untagged"))
                    continue;

                // Only things that affect ordering here r Image + Object.
                // Everything else can be exported after.
                exportables.Add(childObject);
            }

            // 1) Ordered pass: Images + Objects (based on Depth + SortingOrder / SortingGroup)
            var ordered = exportables
                .FindAll(o => o.CompareTag("Image") || o.CompareTag("Object"));

            ordered.Sort((a, b) =>
            {
                int aDepth = GetDepthGroup(a);
                int bDepth = GetDepthGroup(b);
                if (aDepth != bDepth) return aDepth.CompareTo(bDepth);

                int aOrder = GetEffectiveOrder(a);
                int bOrder = GetEffectiveOrder(b);
                if (aOrder != bOrder) return aOrder.CompareTo(bOrder);

                return 0;
            });

            foreach (var go in ordered)
            {
                if (go.CompareTag("Object"))
                    WriteToXML(go, xmlUtility, parentXmlElement, exportMode);
                else
                    ExportHandler.WriteByTag(go, xmlUtility, parentXmlElement);
            }

            // 2) Unordered pass: everything else (not important for layering per request)
            foreach (var go in exportables)
            {
                if (go.CompareTag("Image") || go.CompareTag("Object"))
                    continue;

                ExportHandler.WriteByTag(go, xmlUtility, parentXmlElement);
            }
        }

        private static int GetDepthGroup(GameObject gameObject)
        {
            if (!gameObject.CompareTag("Image"))
                return 1;

            int? depth = TryGetDepthValue(gameObject);
            if (depth == 1) return 0;   // back
            if (depth == 0) return 2;   // front
            return 1;                   // middle (no depth)
        }

        private static int GetEffectiveOrder(GameObject gameObject)
        {
            if (gameObject.CompareTag("Object"))
            {
                var group = gameObject.GetComponent<SortingGroup>();
                return group != null ? group.sortingOrder : 0;
            }

            var renderer = gameObject.GetComponent<SpriteRenderer>();
            return renderer != null ? renderer.sortingOrder : 0;
        }

        private static int? TryGetDepthValue(GameObject imageObject)
        {
            var components = imageObject.GetComponents<ImageComponent>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null) continue;

                var transform = component.GetType();

                var prop = transform.GetProperty("Depth");
                if (prop != null && prop.PropertyType == typeof(int))
                    return (int)prop.GetValue(component);

                var field = transform.GetField("Depth");
                if (field != null && field.FieldType == typeof(int))
                    return (int)field.GetValue(component);
            }

            return null;
        }

        // ================= BUILDINGS ================= //

        private static void CreateInOutMarkers(XmlElement xmlElement, GameObject parentObject, bool includeBuildingsMarker)
        {
            if (!includeBuildingsMarker)
                return;

            Sprite markerSprite = Resources.Load<Sprite>("Images/Editor/Misc/mark");

            CreateMarker(xmlElement, parentObject, "In", "InX", "InY", markerSprite);
            CreateMarker(xmlElement, parentObject, "Out", "OutX", "OutY", markerSprite);
        }

        private static void CreateMarker(XmlElement xmlElement, GameObject parentObject, string markerName, string attributeX, string attributeY, Sprite markerSprite)
        {
            if (!xmlElement.HasAttribute(attributeX) || !xmlElement.HasAttribute(attributeY))
                return;

            float positionX = Element.ParseFloat(xmlElement.GetAttribute(attributeX));
            float positionY = -Element.ParseFloat(xmlElement.GetAttribute(attributeY));

            GameObject markerObject = new GameObject(markerName);
            markerObject.transform.SetParent(parentObject.transform, false);
            markerObject.transform.localPosition = new Vector3(positionX, positionY, 0f);

            SpriteRenderer renderer = markerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.sortingLayerName = "OnTop";
            renderer.color = Color.green;

            markerObject.tag = "EditorOnly";
        }

        private static void WriteInOut(GameObject sourceObject, XmlUtility xmlUtility, XmlElement objectXmlElement)
        {
            Transform outTransform = sourceObject.transform.Find("Out");

            Vector3 inPosition = sourceObject.transform.position;
            Vector3 outPosition = outTransform != null ? outTransform.position : Vector3.zero;

            if (outTransform == null)
                Debug.LogWarning("[ObjectElement] 'Out' child is null for object: " + sourceObject.name + ". Defaulting to 0");

            xmlUtility.SetAttribute(objectXmlElement, "InX", inPosition.x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(objectXmlElement, "InY", (-inPosition.y).ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(objectXmlElement, "OutX", outPosition.x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(objectXmlElement, "OutY", (-outPosition.y).ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteBounds(GameObject sourceObject, XmlUtility xmlUtility, XmlElement objectXmlElement)
        {
            Bounds? combinedBounds = null;

            foreach (Transform childTransform in sourceObject.transform)
            {
                Renderer renderer = childTransform.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                combinedBounds = combinedBounds == null ? renderer.bounds : Encapsulate(combinedBounds.Value, renderer.bounds);
            }

            if (combinedBounds == null)
                return;

            Bounds bounds = combinedBounds.Value;

            Vector3 topLeft = new Vector3(bounds.min.x, bounds.max.y);
            Vector3 topRight = new Vector3(bounds.max.x, bounds.max.y);
            Vector3 bottomLeft = new Vector3(bounds.min.x, bounds.min.y);

            xmlUtility.SetAttribute(objectXmlElement, "BoxX", topLeft.x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(objectXmlElement, "BoxY", (-topLeft.y).ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(objectXmlElement, "BoxWidth", (topRight.x - topLeft.x).ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(objectXmlElement, "BoxHeight", (topLeft.y - bottomLeft.y).ToString(CultureInfo.InvariantCulture));
        }

        private static Bounds Encapsulate(Bounds baseBounds, Bounds additionalBounds)
        {
            baseBounds.Encapsulate(additionalBounds);
            return baseBounds;
        }

        // ================= COMPONENTS ================= //

        private static void WriteSelection( GameObject sourceObject, XmlUtility xmlUtility, XmlElement parentXmlElement)
        {
            SelectionComponent selectionComponent = sourceObject.GetComponent<SelectionComponent>();
            if (selectionComponent == null)
                return;

            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(parentXmlElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            XmlElement selectionElement = xmlUtility.AddElement(staticElement, "Selection");
            xmlUtility.SetAttribute(selectionElement, "Choice", "AITriggers");
            xmlUtility.SetAttribute(selectionElement, "Variant", selectionComponent.Variant.ToString());
        }

        private static void WriteDynamic(GameObject sourceObject, XmlUtility xmlUtility, XmlElement parentXmlElement)
        {
            DynamicTransform dynamicTransform = sourceObject.GetComponent<DynamicTransform>();
            if (dynamicTransform == null)
                return;

            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(parentXmlElement, "Properties");
            dynamicTransform.WriteToXML(xmlUtility, propertiesElement);
        }

        private static void ApplyDynamic(GameObject sourceObject, XmlUtility xmlUtility, XmlElement parentXmlElement)
        {
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(parentXmlElement, "Properties");

            if (propertiesElement == null || sourceObject == null)
                return;

            foreach (XmlNode node in propertiesElement.ChildNodes)
            {
                if (node is not XmlElement dynamicElement || dynamicElement.Name != "Dynamic")
                    continue;

                foreach (XmlNode child in dynamicElement.ChildNodes)
                {
                    if (child is XmlElement transformationElement && transformationElement.Name == "Transformation")
                        DynamicTransform.WriteToScene(transformationElement, sourceObject);
                }
            }
        }

        // ================= NAME ================= //

        private static void WriteName(GameObject sourceObject, XmlUtility xmlUtility, XmlElement objectXmlElement)
        {
            string cleanName = CleanName(sourceObject.name);
            if (!string.IsNullOrEmpty(cleanName))
                xmlUtility.SetAttribute(objectXmlElement, "Name", cleanName);
        }

        private static string CleanName(string objectName)
        {
            return System.Text.RegularExpressions.Regex.Replace(objectName, @"\s*\(\d+\)$", "");
        }
    }
}
