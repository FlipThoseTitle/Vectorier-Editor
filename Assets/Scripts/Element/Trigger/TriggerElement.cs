using System.Globalization;
using System.Xml;
using UnityEngine;
using UnityEngine.UIElements;
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

            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return null;

            // Get component
            TriggerComponent triggerComponent = gameObject.GetComponent<TriggerComponent>();
            if (triggerComponent == null || string.IsNullOrWhiteSpace(triggerComponent.contentXml))
                return null;

            // Create <Trigger> node
            XmlElement triggerElement = xmlUtility.AddElement(parentElement, "Trigger");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(triggerElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Compute attributes
            Vector3 position = gameObject.transform.localPosition;
            Vector3 scale = gameObject.transform.lossyScale;
            float x = position.x * 100f;
            float y = position.y * -100f; // Vector -Y is up.
            float width = 0f;
            float height = 0f;

            if (spriteRenderer.sprite != null)
            {
                Texture2D texture = spriteRenderer.sprite.texture;
                if (texture != null)
                {
                    width = texture.width * scale.x;
                    height = texture.height * scale.y;
                }
            }

            string cleanName = System.Text.RegularExpressions.Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            xmlUtility.SetAttribute(triggerElement, "Name", cleanName);
            xmlUtility.SetAttribute(triggerElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(triggerElement, "Y", y.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(triggerElement, "Width", width.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(triggerElement, "Height", height.ToString(CultureInfo.InvariantCulture));

            // Add <Content> node
            XmlElement contentElement = xmlUtility.AddElement(triggerElement, "Content");

            // Add Content XML
            contentElement.InnerXml = triggerComponent.contentXml;

            // Selection Variant (if SelectionComponent exists)
            SelectionComponent selectionComponent = gameObject.GetComponent<SelectionComponent>();
            if (selectionComponent != null)
            {
                string variantString = selectionComponent.Variant.ToString();
                XmlElement selectionElement = xmlUtility.AddElement(staticElement, "Selection");
                xmlUtility.SetAttribute(selectionElement, "Choice", "AITriggers");
                xmlUtility.SetAttribute(selectionElement, "Variant", variantString);
            }

            return triggerElement;
        }
    }
}
