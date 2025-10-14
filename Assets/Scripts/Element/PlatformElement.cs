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
            Vector3 position = gameObject.transform.position;
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
    }
}