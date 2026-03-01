using System.Globalization;
using System.Xml;
using UnityEngine;
using Vectorier.Component;
using Vectorier.XML;

namespace Vectorier.Element
{
    public static class TriggerElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            if (!gameObject.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                return null;

            if (!gameObject.TryGetComponent<TriggerComponent>(out var triggerComponent))
                return null;

            // <Trigger> 
            XmlElement triggerElement = xmlUtility.AddElement(parentElement, "Trigger");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(triggerElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Compute attributes
            Element.WriteName(gameObject, xmlUtility, triggerElement);
            Element.WritePosition(xmlUtility, triggerElement, gameObject);
            Element.WriteSize(xmlUtility, triggerElement, gameObject);

            // Add <Content> node
            XmlElement contentElement = xmlUtility.AddElement(triggerElement, "Content");

            // Add Content XML
            contentElement.InnerXml = triggerComponent.contentXml;

            // Selection Variant
            Element.WriteSelectionComponent(xmlUtility, staticElement, gameObject);

            return triggerElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Properties
            XmlElement propertiesElement = element.SelectSingleNode("Properties") as XmlElement;
            XmlElement staticElement = propertiesElement?.SelectSingleNode("Static") as XmlElement;

            // Create object
            GameObject triggerObject = Element.CreateObject("Trigger", parent, element);

            // Sprite
            SpriteRenderer renderer = triggerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Resources.Load<Sprite>("Images/Editor/Trigger/trigger");
            renderer.color = new Color(1f, 1f, 0f, 1f);

            // Apply
            Element.ApplyPosition(triggerObject, element);
            Element.ApplySize(triggerObject, renderer.sprite, element);
            Element.ApplyLayer(triggerObject, factor);
            Element.ApplySelectionComponent(staticElement, triggerObject);

            // Add and configure Trigger component
            TriggerComponent trigger = triggerObject.AddComponent<TriggerComponent>();
            XmlNode contentNode = element.SelectSingleNode("Content");
            if (contentNode != null)
                trigger.contentXml = FormatInnerXml(contentNode.InnerXml);
            else
                trigger.contentXml = string.Empty;

            Element.ApplyDynamic(propertiesElement, triggerObject);

            // Set Tag
            triggerObject.tag = "Trigger";
            renderer.sortingLayerName = "OnTop";
            renderer.sortingOrder = 2;

            return triggerObject;
        }

        public static GameObject Create(Transform parent = null)
        {
            GameObject triggerObject = new GameObject("Trigger");

            // Parent
            if (parent != null)
            {
                triggerObject.transform.SetParent(parent, false);
                triggerObject.transform.localPosition = Vector3.zero;
            }

            // Component
            TriggerComponent triggerComponent = triggerObject.AddComponent<TriggerComponent>();

            // Sprite
            SpriteRenderer spriteRenderer = triggerObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>("Images/Editor/Trigger/trigger");
            spriteRenderer.color = new Color(1f, 1f, 0f, 1f);

            // Tag + sorting
            triggerObject.tag = "Trigger";
            spriteRenderer.sortingLayerName = "OnTop";
            spriteRenderer.sortingOrder = 2;

            return triggerObject;
        }

        private static string FormatInnerXml(string rawXml)
        {
            if (string.IsNullOrWhiteSpace(rawXml))
                return string.Empty;

            XmlDocument tempDoc = new XmlDocument();
            XmlDocumentFragment fragment = tempDoc.CreateDocumentFragment();

            try
            {
                fragment.InnerXml = rawXml;
            }
            catch
            {
                return rawXml;
            }

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment
            };

            using (var sw = new System.IO.StringWriter())
            using (var writer = XmlWriter.Create(sw, settings))
            {
                fragment.WriteTo(writer);
                writer.Flush();
                return sw.ToString();
            }
        }
    }
}
