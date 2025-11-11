using System.Globalization;
using UnityEngine;
using System.Xml;
using Vectorier.XML;
using Vectorier.Component;
using Vectorier.Matrix;
using Vectorier.Dynamic;

namespace Vectorier.Element
{
    public static class ImageElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            SpriteRenderer spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return null;

            // Create <Image> node
            XmlElement imageElement = xmlUtility.AddElement(parentElement, "Image");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(imageElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Compute attributes
            Vector3 position = gameObject.transform.localPosition;
            Vector3 scale = gameObject.transform.lossyScale;

            float x = position.x * 100f;
            float y = position.y * -100f; // Vector -Y is up.
            float width = 0f;
            float height = 0f;
            int nativeX = 0;
            int nativeY = 0;

            if (spriteRenderer.sprite != null)
            {
                Texture2D texture = spriteRenderer.sprite.texture;
                if (texture != null)
                {
                    nativeX = texture.width;
                    nativeY = texture.height;
                    width = texture.width * scale.x;
                    height = texture.height * scale.y;
                }
            }

            // Write attributes
            xmlUtility.SetAttribute(imageElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(imageElement, "Y", y.ToString(CultureInfo.InvariantCulture));

            string cleanName = System.Text.RegularExpressions.Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            xmlUtility.SetAttribute(imageElement, "ClassName", cleanName);

            xmlUtility.SetAttribute(imageElement, "Width", width.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(imageElement, "Height", height.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(imageElement, "NativeX", nativeX.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(imageElement, "NativeY", nativeY.ToString(CultureInfo.InvariantCulture));

            // Image Type (if ImageComponent exists)
            ImageComponent imageComponent = gameObject.GetComponent<ImageComponent>();
            if (imageComponent != null)
            {
                int typeValue = (int)imageComponent.Type; // None=0, Static=1, Vanishing=2, Dynamic=3
                xmlUtility.SetAttribute(imageElement, "Type", typeValue);
            }

            // --- Affine Transformation (rotation / flipping) ---
            if (AffineTransformation.Compute(gameObject, spriteRenderer, out var matrix))
            {
                // Override base position and size using bounding box values
                xmlUtility.SetAttribute(imageElement, "X", System.Math.Round(matrix.TopLeftX).ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(imageElement, "Y", System.Math.Round(matrix.TopLeftY).ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(imageElement, "Width", matrix.BoundingWidth.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(imageElement, "Height", matrix.BoundingHeight.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(imageElement, "NativeX", matrix.NativeWidth.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(imageElement, "NativeY", matrix.NativeHeight.ToString(CultureInfo.InvariantCulture));

                // Create <Matrix> node inside <Static>
                XmlElement matrixElement = xmlUtility.AddElement(staticElement, "Matrix");
                xmlUtility.SetAttribute(matrixElement, "A", matrix.A.ToString("F6", CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(matrixElement, "B", matrix.B.ToString("F6", CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(matrixElement, "C", matrix.C.ToString("F6", CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(matrixElement, "D", matrix.D.ToString("F6", CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(matrixElement, "Tx", matrix.Tx.ToString("F6", CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(matrixElement, "Ty", matrix.Ty.ToString("F6", CultureInfo.InvariantCulture));
            }

            // Sprite color (only if not white)
            Color spriteColor = spriteRenderer.color;
            if (spriteColor != Color.white)
            {
                string hexColor = ColorUtility.ToHtmlStringRGBA(spriteColor);
                XmlElement startColorElement = xmlUtility.AddElement(staticElement, "StartColor");
                xmlUtility.SetAttribute(startColorElement, "Color", "#" + hexColor);
            }

            // Selection Variant (if SelectionComponent exists)
            SelectionComponent selectionComponent = gameObject.GetComponent<SelectionComponent>();
            if (selectionComponent != null)
            {
                string variantString = selectionComponent.Variant.ToString();
                XmlElement selectionElement = xmlUtility.AddElement(staticElement, "Selection");
                xmlUtility.SetAttribute(selectionElement, "Choice", "AITriggers");
                xmlUtility.SetAttribute(selectionElement, "Variant", variantString);
            }


            DynamicTransform dynamic = gameObject.GetComponent<DynamicTransform>();
            if (dynamic != null)
            {
                dynamic.WriteToXML(xmlUtility, propertiesElement);
            }

            return imageElement;
        }
    }
}