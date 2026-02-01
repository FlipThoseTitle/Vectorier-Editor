using System.Globalization;
using System.Xml;
using UnityEngine;
using UnityEngine.UIElements;
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

            if (!gameObject.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                return null;

            // <Trapezoid>
            XmlElement trapezoidElement = xmlUtility.AddElement(parentElement, "Trapezoid");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(trapezoidElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Compute attributes
            Element.GetSizeScene(spriteRenderer, out float width, out float height);
            Element.WritePosition(xmlUtility, trapezoidElement, gameObject);

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

            // Write attributes
            xmlUtility.SetAttribute(trapezoidElement, "Width", width.ToString(CultureInfo.InvariantCulture));

            if (isType1)
            {
                float heightBase = 1;
                float height1 = height + heightBase;

                xmlUtility.SetAttribute(trapezoidElement, "Height", heightBase.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(trapezoidElement, "Height1", height1.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(trapezoidElement, "Type", "1");
            }
            else if (isType2)
            {
                float height1 = 1;
                float heightBase = height + height1;
                
                xmlUtility.SetAttribute(trapezoidElement, "Height", heightBase.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(trapezoidElement, "Height1", height1.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(trapezoidElement, "Type", "2");
            }

            // Selection Variant
            Element.WriteSelectionComponent(xmlUtility, staticElement, gameObject);

            return trapezoidElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Properties
            XmlElement propertiesElement = element.SelectSingleNode("Properties") as XmlElement;

            float xmlWidth = float.Parse(element.GetAttribute("Width"), CultureInfo.InvariantCulture);
            float xmlHeight = float.Parse(element.GetAttribute("Height"), CultureInfo.InvariantCulture);
            float xmlHeight1 = float.Parse(element.GetAttribute("Height1"), CultureInfo.InvariantCulture);

            // Create object
            GameObject trapezoidObject = Element.CreateObject("Trapezoid", parent, element);

            // Apply
            Element.ApplyPosition(trapezoidObject, element);
            Element.ApplyLayer(trapezoidObject, factor);

            // Sprite
            SpriteRenderer spriteRenderer = trapezoidObject.AddComponent<SpriteRenderer>();

            // Component
            TrapezoidComponent trapezoidComponent = trapezoidObject.AddComponent<TrapezoidComponent>();

            if (element.GetAttribute("Type") == "1")
            {
                spriteRenderer.sprite = Resources.Load<Sprite>("Images/Editor/Collision/trapezoid_type1");
                trapezoidComponent.Type = TrapezoidComponent.TrapezoidType.Type1;
                trapezoidObject.name = "trapezoid_type1";
            }
            else if (element.GetAttribute("Type") == "2")
            {
                spriteRenderer.sprite = Resources.Load<Sprite>("Images/Editor/Collision/trapezoid_type2");
                trapezoidComponent.Type = TrapezoidComponent.TrapezoidType.Type2;
                trapezoidObject.name = "trapezoid_type2";
            }
            else
            {
                spriteRenderer.sprite = Resources.Load<Sprite>("Images/Editor/Collision/trapezoid_type1");
                trapezoidComponent.Type = TrapezoidComponent.TrapezoidType.Type1;
                trapezoidObject.name = "trapezoid_type1";
            }

            float triangleHeight = Mathf.Abs(xmlHeight - xmlHeight1);
            if (triangleHeight < 1f)
                triangleHeight = 1f;

            Sprite sprite = spriteRenderer.sprite;
            if (sprite != null && sprite.texture != null)
            {
                float scaleX = xmlWidth / sprite.texture.width;
                float scaleY = triangleHeight / sprite.texture.height;

                trapezoidObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }

            Element.ApplyDynamic(propertiesElement, trapezoidObject);

            // Tag
            trapezoidObject.tag = "Trapezoid";
            spriteRenderer.sortingLayerName = "OnTop";
            spriteRenderer.sortingOrder = 0;

            return trapezoidObject;
        }
    }
}


