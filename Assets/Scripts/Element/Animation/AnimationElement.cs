using System.Globalization;
using System.Xml;
using TMPro;
using UnityEngine;
using Vectorier.Component;
using Vectorier.XML;

namespace Vectorier.Element
{
    public static class AnimationElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return null;

            AnimationComponent animationComponent = gameObject.GetComponent<AnimationComponent>();
            if (animationComponent == null)
                return null;

            // Create <Animation> node
            XmlElement animationElement = xmlUtility.AddElement(parentElement, "Animation");

            Vector3 pos = gameObject.transform.localPosition;
            Vector3 scale = gameObject.transform.lossyScale;
            float x = pos.x * 100f;
            float y = pos.y * -100f;
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

            // Write base attributes
            xmlUtility.SetAttribute(animationElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(animationElement, "Y", y.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(animationElement, "Width", width.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(animationElement, "Height", height.ToString(CultureInfo.InvariantCulture));
            string cleanName = System.Text.RegularExpressions.Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            xmlUtility.SetAttribute(animationElement, "ClassName", cleanName);
            xmlUtility.SetAttribute(animationElement, "Type", ((int)animationComponent.Type).ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(animationElement, "ScaleX", scale.x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(animationElement, "ScaleY", scale.y.ToString(CultureInfo.InvariantCulture));

            if (animationComponent.Type == AnimationComponent.AnimationType.Type1)
            {
                xmlUtility.SetAttribute(animationElement, "Direction", animationComponent.Direction);
                xmlUtility.SetAttribute(animationElement, "Acceleration", animationComponent.Acceleration);
                xmlUtility.SetAttribute(animationElement, "Time", animationComponent.Time.ToString(CultureInfo.InvariantCulture));
            }

            return animationElement;
        }
    }
}