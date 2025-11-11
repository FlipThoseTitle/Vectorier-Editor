using System.Globalization;
using System.Xml;
using TMPro;
using UnityEngine;
using Vectorier.Component;
using Vectorier.XML;

namespace Vectorier.Element
{
    public static class ParticleElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return null;

            // Create <Particle> node
            XmlElement particleElement = xmlUtility.AddElement(parentElement, "Particle");

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

            // Check component
            ParticleComponent particleComponent = gameObject.GetComponent<ParticleComponent>();
            if (particleComponent == null)
                return null;

            // Write base attributes
            xmlUtility.SetAttribute(particleElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(particleElement, "Y", y.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(particleElement, "Width", width.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(particleElement, "Height", height.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(particleElement, "Frame", particleComponent.Frame.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(particleElement, "Type", ((int)particleComponent.Type).ToString(CultureInfo.InvariantCulture));
            string cleanName = System.Text.RegularExpressions.Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            xmlUtility.SetAttribute(particleElement, "ClassName", cleanName);


            return particleElement;
        }
    }
}