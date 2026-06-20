using System.Globalization;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.Rendering;
using Vectorier.XML;
using Vectorier.Handler;
using Vectorier.Component;

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

            Element.WriteName(sourceObject, xmlUtility, objectXmlElement);
            WriteExportSpecificData(sourceObject, xmlUtility, objectXmlElement, exportMode);
            WriteSelection(sourceObject, xmlUtility, objectXmlElement);
            Element.WriteDynamic(xmlUtility, sourceObject, GetPropertiesElementIfDynamicExists(sourceObject, xmlUtility, objectXmlElement));

            return objectXmlElement;
        }

        private static void WriteExportSpecificData(GameObject sourceObject, XmlUtility xmlUtility, XmlElement objectXmlElement, ExportHandler.ExportMode exportMode)
        {
            switch (exportMode)
            {
                case ExportHandler.ExportMode.Level:
                    Element.WritePosition(xmlUtility, objectXmlElement, sourceObject);

                    if (sourceObject.transform.childCount > 0)
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

            GameObject gameObject = Element.CreateObject("Object", parentTransform, xmlElement);

            ApplyObjectPosition(gameObject, xmlElement);
            Element.ApplyLayer(gameObject, layerName);
            ApplySortingGroupLayering(xmlElement, gameObject, layerName);

            CreateInOutMarkers(xmlElement, gameObject, includeBuildingsMarker);

            ImportHandler.LayerOrderStack.Push(0);

            WriteSceneChildren(xmlElement, gameObject.transform, layerName, includeBuildingsMarker, xmlUtility);

            ImportHandler.LayerOrderStack.Pop();

            XmlElement propertiesElement = xmlElement.SelectSingleNode("Properties") as XmlElement;
            XmlElement staticElement = propertiesElement?.SelectSingleNode("Static") as XmlElement;

            Element.ApplySelectionComponent(staticElement, gameObject);
            Element.ApplyDynamic(propertiesElement, gameObject);

            return gameObject;
        }

        private static void ApplyObjectPosition(GameObject gameObject, XmlElement xmlElement)
        {
            if (xmlElement.HasAttribute("X") && xmlElement.HasAttribute("Y"))
            {
                Element.ApplyPosition(gameObject, xmlElement);
                return;
            }

            if (!xmlElement.HasAttribute("InX") || !xmlElement.HasAttribute("InY"))
                return;

            float x = Element.ParseFloat(xmlElement.GetAttribute("InX"));
            float y = -Element.ParseFloat(xmlElement.GetAttribute("InY"));

            Element.ApplyPosition(gameObject, xmlElement, x, y);
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

        private static void ApplySortingGroupLayering(XmlElement xmlElement, GameObject gameObject, string layerName)
        {
            SortingGroup group = gameObject.AddComponent<SortingGroup>();
            group.sortingLayerName = layerName;

            ImportDepthGroup depthGroup = GetDepthGroupFromXML(xmlElement);

            int depthValue = 2; // Middle Default
            if (depthGroup == ImportDepthGroup.Front) depthValue = 0;
            else if (depthGroup == ImportDepthGroup.Back) depthValue = 1;

            group.sortingOrder = ImportHandler.GetNextLayerOrder(depthValue);
        }

        private static ImportDepthGroup GetDepthGroupFromXML(XmlElement xmlElement)
        {
            if (xmlElement == null || !xmlElement.HasAttribute("Depth"))
                return ImportDepthGroup.Middle;

            int depthValue;
            if (!int.TryParse(xmlElement.GetAttribute("Depth"), out depthValue))
                return ImportDepthGroup.Middle;

            if (depthValue == 0)
                return ImportDepthGroup.Front;

            if (depthValue == 1)
                return ImportDepthGroup.Back;

            return ImportDepthGroup.Middle;
        }

        // ================= CHILDREN EXPORT ================= //

        private static void WriteChildren(GameObject parentObject, XmlUtility xmlUtility, XmlElement parentXmlElement, ExportHandler.ExportMode exportMode)
        {
            List<GameObject> orderedObjects = new List<GameObject>();
            List<GameObject> unorderedObjects = new List<GameObject>();

            foreach (Transform childTransform in parentObject.transform)
            {
                GameObject childObject = childTransform.gameObject;

                if (!childObject.activeInHierarchy)
                    continue;

                if (string.IsNullOrEmpty(childObject.tag) || childObject.CompareTag("Untagged"))
                    continue;

                if (childObject.CompareTag("Image") || childObject.CompareTag("Object"))
                    orderedObjects.Add(childObject);
                else
                    unorderedObjects.Add(childObject);
            }

            orderedObjects.Sort(CompareExportOrder);

            foreach (GameObject childObject in orderedObjects)
                WriteChild(childObject, xmlUtility, parentXmlElement, exportMode);

            foreach (GameObject childObject in unorderedObjects)
                ExportHandler.WriteByTag(childObject, xmlUtility, parentXmlElement);
        }

        private static void WriteChild(GameObject childObject, XmlUtility xmlUtility, XmlElement parentXmlElement, ExportHandler.ExportMode exportMode)
        {
            if (childObject.CompareTag("Object"))
                WriteToXML(childObject, xmlUtility, parentXmlElement, exportMode);
            else
                ExportHandler.WriteByTag(childObject, xmlUtility, parentXmlElement);
        }

        private static int CompareExportOrder(GameObject a, GameObject b)
        {
            int depthCompare = GetExportDepthGroup(a).CompareTo(GetExportDepthGroup(b));
            if (depthCompare != 0)
                return depthCompare;

            return GetEffectiveOrder(a).CompareTo(GetEffectiveOrder(b));
        }

        private static int GetExportDepthGroup(GameObject gameObject)
        {
            if (!gameObject.CompareTag("Image"))
                return 1;

            int? depth = TryGetDepthValue(gameObject);

            if (depth == 1)
                return 0;

            if (depth == 0)
                return 2;

            return 1;
        }

        private static int GetEffectiveOrder(GameObject gameObject)
        {
            if (gameObject.CompareTag("Object"))
            {
                SortingGroup group = gameObject.GetComponent<SortingGroup>();
                return group != null ? group.sortingOrder : 0;
            }

            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
            return renderer != null ? renderer.sortingOrder : 0;
        }

        private static int? TryGetDepthValue(GameObject imageObject)
        {
            ImageComponent[] components = imageObject.GetComponents<ImageComponent>();

            for (int i = 0; i < components.Length; i++)
            {
                ImageComponent component = components[i];
                if (component == null)
                    continue;

                System.Type componentType = component.GetType();

                System.Reflection.PropertyInfo depthProperty = componentType.GetProperty("Depth");
                if (depthProperty != null && depthProperty.PropertyType == typeof(int))
                    return (int)depthProperty.GetValue(component);

                System.Reflection.FieldInfo depthField = componentType.GetField("Depth");
                if (depthField != null && depthField.FieldType == typeof(int))
                    return (int)depthField.GetValue(component);
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
            markerObject.tag = "EditorOnly";

            SpriteRenderer renderer = markerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = markerSprite;
            renderer.sortingLayerName = "OnTop";
            renderer.color = Color.green;
        }

        private static void WriteInOut(GameObject sourceObject, XmlUtility xmlUtility, XmlElement objectXmlElement)
        {
            Transform outTransform = sourceObject.transform.Find("Out");

            Vector3 inPosition = sourceObject.transform.position;
            Vector3 outPosition = outTransform != null ? outTransform.position : Vector3.zero;

            if (outTransform == null)
                Debug.LogWarning("[ObjectElement] 'Out' child is null for object: " + sourceObject.name + ". Defaulting to 0.");

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

                if (combinedBounds == null)
                {
                    combinedBounds = renderer.bounds;
                }
                else
                {
                    Bounds bounds = combinedBounds.Value;
                    bounds.Encapsulate(renderer.bounds);
                    combinedBounds = bounds;
                }
            }

            if (combinedBounds == null)
                return;

            Bounds finalBounds = combinedBounds.Value;

            float boxX = finalBounds.min.x;
            float boxY = -finalBounds.max.y;
            float boxWidth = finalBounds.max.x - finalBounds.min.x;
            float boxHeight = finalBounds.max.y - finalBounds.min.y;

            xmlUtility.SetAttribute(objectXmlElement, "BoxX", boxX.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(objectXmlElement, "BoxY", boxY.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(objectXmlElement, "BoxWidth", boxWidth.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(objectXmlElement, "BoxHeight", boxHeight.ToString(CultureInfo.InvariantCulture));
        }

        // ================= COMPONENTS ================= //

        private static void WriteSelection(GameObject sourceObject, XmlUtility xmlUtility, XmlElement parentXmlElement)
        {
            if (!sourceObject.TryGetComponent<SelectionComponent>(out _))
                return;

            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(parentXmlElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            Element.WriteSelectionComponent(xmlUtility, staticElement, sourceObject);
        }

        private static XmlElement GetPropertiesElementIfDynamicExists(GameObject sourceObject, XmlUtility xmlUtility, XmlElement parentXmlElement)
        {
            if (sourceObject == null || sourceObject.GetComponents<Vectorier.Dynamic.DynamicTransform>().Length == 0)
                return null;

            return xmlUtility.GetOrCreateElement(parentXmlElement, "Properties");
        }
    }
}