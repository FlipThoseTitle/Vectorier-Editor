using System.Globalization;
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

            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return null;

            // Create <Platform> node
            XmlElement platformElement = xmlUtility.AddElement(parentElement, "Platform");

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

            // Write attributes
            xmlUtility.SetAttribute(platformElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(platformElement, "Y", y.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(platformElement, "Width", width.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(platformElement, "Height", height.ToString(CultureInfo.InvariantCulture));

            return platformElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Parse basic Info
            float x = float.Parse(element.GetAttribute("X")) / 100f;
            float y = -float.Parse(element.GetAttribute("Y")) / 100f;
            float xmlWidth = float.Parse(element.GetAttribute("Width"), CultureInfo.InvariantCulture);
            float xmlHeight = float.Parse(element.GetAttribute("Height"), CultureInfo.InvariantCulture);

            // Create object
            GameObject platformObject = new GameObject("Platform");
            platformObject.transform.SetParent(parent, false);
            platformObject.transform.localPosition = new Vector3(x, y, 0f);

            // Set layer by factor
            string layerName = factor;
            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex != -1)
                platformObject.layer = layerIndex;
            else
                Debug.LogWarning($"Layer '{layerName}' does not exist. Platform '{platformObject.name}' assigned to Default layer.");

            // Add sprite
            SpriteRenderer spriteRenderer = platformObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>("Images/Editor/Collision/platform");

            if (spriteRenderer.sprite != null)
            {
                Texture2D texture = spriteRenderer.sprite.texture;
                if (texture != null)
                {
                    float scaleX = xmlWidth / texture.width;
                    float scaleY = xmlHeight / texture.height;
                    platformObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                }
            }

            // Set Tag
            platformObject.tag = "Platform";

            return platformObject;
        }
    }
}