using System.Globalization;
using System.Xml;
using UnityEngine;
using Vectorier.Component;
using Vectorier.XML;

namespace Vectorier.Element
{
    public static class TrapezoidElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return null;

            // Create <Trapezoid> node
            XmlElement trapezoidElement = xmlUtility.AddElement(parentElement, "Trapezoid");

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
            TrapezoidComponent trapezoidComponent = gameObject.GetComponent<TrapezoidComponent>();

            bool isType1 = false;
            bool isType2 = false;

            if (trapezoidComponent != null)
            {
                isType1 = trapezoidComponent.Type == TrapezoidComponent.TrapezoidType.Type1;
                isType2 = trapezoidComponent.Type == TrapezoidComponent.TrapezoidType.Type2;
            }
            else
            {
                // Fallback to name check
                string nameLower = gameObject.name.ToLowerInvariant();
                if (nameLower.Contains("trapezoid_type1")) isType1 = true;
                else if (nameLower.Contains("trapezoid_type2")) isType2 = true;
            }

            if (!isType1 && !isType2)
                return null;

            float heightBase = height;
            float height1Value = heightBase * scale.y + 1f;

            // Write attributes
            xmlUtility.SetAttribute(trapezoidElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(trapezoidElement, "Y", y.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(trapezoidElement, "Width", width.ToString(CultureInfo.InvariantCulture));

            if (isType1)
            {
                xmlUtility.SetAttribute(trapezoidElement, "Height", "1");
                xmlUtility.SetAttribute(trapezoidElement, "Height1", height1Value.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(trapezoidElement, "Type", "1");
            }
            else if (isType2)
            {
                xmlUtility.SetAttribute(trapezoidElement, "Height", height1Value.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(trapezoidElement, "Height1", "1");
                xmlUtility.SetAttribute(trapezoidElement, "Type", "2");
            }


            return trapezoidElement;
        }
    }
}


