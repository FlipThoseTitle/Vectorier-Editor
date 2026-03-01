using UnityEngine;
using System.Xml;
using Vectorier.XML;

namespace Vectorier.Element
{
    public static class PlatformElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            if (!gameObject.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                return null;

            // <Platform>
            XmlElement platformElement = xmlUtility.AddElement(parentElement, "Platform");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(platformElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Compute attributes
            Element.WritePosition(xmlUtility, platformElement, gameObject);
            Element.WriteSize(xmlUtility, platformElement, gameObject);

            // Components
            Element.WriteSelectionComponent(xmlUtility, staticElement, gameObject);

            return platformElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Properties
            XmlElement propertiesElement = element.SelectSingleNode("Properties") as XmlElement;
            XmlElement staticElement = propertiesElement?.SelectSingleNode("Static") as XmlElement;

            // Create object
            GameObject platformObject = Element.CreateObject("Platform", parent, element);

            // SpriteRenderer
            SpriteRenderer spriteRenderer = platformObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>("Images/Editor/Collision/platform");

            // Apply
            Element.ApplyPosition(platformObject, element);
            Element.ApplyLayer(platformObject, factor);
            Element.ApplySize(platformObject, spriteRenderer.sprite, element);
            Element.ApplySelectionComponent(staticElement, platformObject);
            Element.ApplyDynamic(propertiesElement, platformObject);

            // Set Tag
            platformObject.tag = "Platform";
            spriteRenderer.sortingLayerName = "OnTop";
            spriteRenderer.sortingOrder = 0;

            return platformObject;
        }

        public static GameObject Create(Transform parent = null)
        {
            GameObject platformObject = new GameObject("Platform");

            // Parent
            if (parent != null)
            {
                platformObject.transform.SetParent(parent, false);
                platformObject.transform.localPosition = Vector3.zero;
            }

            // Sprite
            SpriteRenderer spriteRenderer = platformObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>("Images/Editor/Collision/platform");

            // Tag + sorting
            platformObject.tag = "Platform";
            spriteRenderer.sortingLayerName = "OnTop";
            spriteRenderer.sortingOrder = 0;

            return platformObject;
        }
    }
}