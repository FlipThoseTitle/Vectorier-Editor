using System.Globalization;
using System.Xml;
using UnityEngine;
using Vectorier.Component;
using Vectorier.XML;

namespace Vectorier.Element
{
    public static class AreaElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return null;

            // Create <Area> node
            XmlElement areaElement = xmlUtility.AddElement(parentElement, "Area");

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
            string cleanName = System.Text.RegularExpressions.Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            xmlUtility.SetAttribute(areaElement, "Name", cleanName);
            xmlUtility.SetAttribute(areaElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(areaElement, "Y", y.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(areaElement, "Width", width.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(areaElement, "Height", height.ToString(CultureInfo.InvariantCulture));

            // Get AreaComponent
            AreaComponent areaComponent = gameObject.GetComponent<AreaComponent>();

            // Default type = Animation (if no component)
            AreaComponent.AreaType type = AreaComponent.AreaType.Animation;
            if (areaComponent != null)
                type = areaComponent.Type;

            xmlUtility.SetAttribute(areaElement, "Type", type.ToString());

            // Write type-specific attributes
            if (areaComponent != null)
            {
                switch (type)
                {
                    case AreaComponent.AreaType.Catch:
                        xmlUtility.SetAttribute(areaElement, "Distance",
                            areaComponent.Distance.ToString(CultureInfo.InvariantCulture));
                        break;

                    case AreaComponent.AreaType.Trick:
                        xmlUtility.SetAttribute(areaElement, "ItemName", areaComponent.ItemName);
                        xmlUtility.SetAttribute(areaElement, "Score",
                            areaComponent.Score.ToString(CultureInfo.InvariantCulture));
                        break;

                    case AreaComponent.AreaType.Help:
                        xmlUtility.SetAttribute(areaElement, "Key", areaComponent.Key);
                        xmlUtility.SetAttribute(areaElement, "Description", areaComponent.Description ?? string.Empty);
                        break;

                        // Animation type has no extra attributes
                }
            }

            return areaElement;
        }
    }
}