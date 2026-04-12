using System.Globalization;
using System.Xml;
using UnityEngine;
using Vectorier.XML;
using Vectorier.Component;
using System;
using Vectorier.Matrix;
using Vectorier.Dynamic;

namespace Vectorier.Element
{
    public static class Element
    {
        public static GameObject CreateObject(string fallback, Transform parent, XmlElement element)
        {
            GameObject gameObject = new GameObject("gameObject");
            gameObject.transform.SetParent(parent, false);
            ApplyName(gameObject, element, fallback);
            return gameObject;
        }

        public static SpriteRenderer GetSpriteRenderer(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            if (!gameObject.TryGetComponent<SpriteRenderer>(out var renderer))
                return null;

            return renderer;
        }

        public static int ParseInt(string element)
        {
            return int.TryParse(element, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        public static float ParseFloat(string element)
        {
            return float.TryParse(element, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        }

        public static string ParseString(string element, string fallback)
        {
            if (string.IsNullOrEmpty(element))
                return fallback;

            return element;
        }

        public static void GetPositionScene(GameObject gameObject, out float x, out float y)
        {
            Vector3 accumulated = Vector3.zero;
            Transform current = gameObject.transform;

            while (current != null)
            {
                if (current != gameObject.transform && current.CompareTag("Object"))
                    break;

                accumulated += current.localPosition;
                current = current.parent;
            }

            x = accumulated.x;
            y = -accumulated.y;
        }

        public static void GetSizeScene(SpriteRenderer renderer, out float width, out float height)
        {
            width = height = 0f;

            if (renderer == null || renderer.sprite == null)
                return;

            Vector3 accumulatedScale = Vector3.one;
            Transform current = renderer.transform;

            while (current != null)
            {
                accumulatedScale = Vector3.Scale(accumulatedScale, current.localScale);

                if (current.CompareTag("Object"))
                    break;

                current = current.parent;
            }

            Texture2D texture = renderer.sprite.texture;

            width = texture.width * accumulatedScale.x;
            height = texture.height * accumulatedScale.y;
        }

        public static void GetNativeSizeScene(SpriteRenderer renderer, out int nativeX, out int nativeY)
        {
            nativeX = nativeY = 0;

            if (renderer.sprite == null || renderer.sprite.texture == null)
                return;

            Texture2D texture = renderer.sprite.texture;
            nativeX = texture.width;
            nativeY = texture.height;
        }

        public static void GetPositionXML(XmlElement element, out float x, out float y)
        {
            x = ParseFloat(element.GetAttribute("X"));
            y = -ParseFloat(element.GetAttribute("Y"));
        }

        public static void GetSizeXML(XmlElement element, out float width, out float height)
        {
            width = ParseFloat(element.GetAttribute("Width"));
            height = ParseFloat(element.GetAttribute("Height"));
        }

        public static void GetNativeSizeXML(XmlElement element, out int nativeX, out int nativeY)
        {
            nativeX = ParseInt(element.GetAttribute("NativeX"));
            nativeY = ParseInt(element.GetAttribute("NativeY"));
        }

        public static void WritePosition(XmlUtility xml, XmlElement element, GameObject gameObject, float? customX = null, float? customY = null)
        {
            if (customX == null || customY == null)
            {
                GetPositionScene(gameObject, out float x, out float y);
                xml.SetAttribute(element, "X", x.ToString(CultureInfo.InvariantCulture));
                xml.SetAttribute(element, "Y", y.ToString(CultureInfo.InvariantCulture));
            }    
            else
            {
                float x = customX.Value;
                float y = customY.Value;

                xml.SetAttribute(element, "X", x.ToString(CultureInfo.InvariantCulture));
                xml.SetAttribute(element, "Y", y.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static void WriteSize(XmlUtility xml, XmlElement element, GameObject gameObject, float? customWidth = null, float? customHeight = null)
        {
            if (customWidth == null || customHeight == null)
            {
                GetSizeScene(gameObject.GetComponent<SpriteRenderer>(), out float width, out float height);
                xml.SetAttribute(element, "Width", width.ToString(CultureInfo.InvariantCulture));
                xml.SetAttribute(element, "Height", height.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                float width = customWidth.Value;
                float height = customHeight.Value;

                xml.SetAttribute(element, "Width", width.ToString(CultureInfo.InvariantCulture));
                xml.SetAttribute(element, "Height", height.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static void WriteNativeSize(XmlUtility xml, XmlElement element, GameObject gameObject)
        {
            GetNativeSizeScene(gameObject.GetComponent<SpriteRenderer>(), out int nativeX, out int nativeY);

            xml.SetAttribute(element, "NativeX", nativeX.ToString(CultureInfo.InvariantCulture));
            xml.SetAttribute(element, "NativeY", nativeY.ToString(CultureInfo.InvariantCulture));
        }

        public static void WriteName(GameObject gameObject, XmlUtility xml, XmlElement element)
        {
            string cleanName = System.Text.RegularExpressions.Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            xml.SetAttribute(element, "Name", cleanName);
        }

        public static void WriteClassName(GameObject gameObject, XmlUtility xml, XmlElement element)
        {
            string cleanName = System.Text.RegularExpressions.Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            xml.SetAttribute(element, "ClassName", cleanName);
        }

        public static void ApplyPosition(GameObject gameObject, XmlElement element, float? customX = null, float? customY = null)
        {
            if (customX == null || customY == null)
            {
                GetPositionXML(element, out float x, out float y);
                gameObject.transform.localPosition = new Vector3(x, y, 0f);
            }
            else
            {
                float x = customX.Value;
                float y = customY.Value;
                gameObject.transform.localPosition = new Vector3(x, y, 0f);
            }
        }

        public static void ApplySize(GameObject gameObject, Sprite sprite, XmlElement element, float? customWidth = null, float? customHeight = null)
        {
            if (sprite == null || sprite.texture == null)
                return;

            if (customWidth == null || customHeight == null)
            {
                GetSizeXML(element, out float width, out float height);
                float scaleX = width / sprite.texture.width;
                float scaleY = height / sprite.texture.height;
                gameObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
            else
            {
                float width = customWidth.Value;
                float height = customHeight.Value;
                float scaleX = width / sprite.texture.width;
                float scaleY = height / sprite.texture.height;
                gameObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
        }

        public static void ApplyName(GameObject gameObject, XmlElement element, string fallback)
        {
            string raw = element.GetAttribute("Name");

            if (string.IsNullOrEmpty(raw))
                raw = element.GetAttribute("ClassName");

            string name = ParseString(raw, fallback);
            gameObject.name = name;
        }

        public static void ApplyLayer(GameObject gameObject, string factor)
        {
            int layer = LayerMask.NameToLayer(factor);
            if (layer != -1)
                gameObject.layer = layer;
            else
                Debug.LogWarning($"Layer '{factor}' does not exist. Platform '{gameObject.name}' assigned to Default layer.");
        }

        // Miscellaneous
        public static void WriteSelectionComponent(XmlUtility xml, XmlElement staticElement, GameObject gameObject)
        {
            if (staticElement == null || gameObject == null)
                return;

            if (!gameObject.TryGetComponent<SelectionComponent>(out var selectionComponent))
                return;

            XmlElement element = xml.AddElement(staticElement, "Selection");
            xml.SetAttribute(element, "Choice", "AITriggers");
            xml.SetAttribute(element, "Variant", selectionComponent.Variant.ToString());
        }

        public static void WriteColor(XmlUtility xml, XmlElement staticElement, GameObject gameObject)
        {
            if (staticElement == null || gameObject == null)
                return;

            if (!gameObject.TryGetComponent<SpriteRenderer>(out var sprite))
                return;

            if (sprite.color == Color.white)
                return;

            XmlElement colorElement = xml.AddElement(staticElement, "StartColor");
            xml.SetAttribute(colorElement, "Color", "#" + ColorUtility.ToHtmlStringRGBA(sprite.color));
        }

        public static void WriteMatrix(XmlUtility xml, XmlElement staticElement, XmlElement element, GameObject gameObject, SpriteRenderer renderer)
        {
            if (xml == null || staticElement == null || element == null || gameObject == null)
                return;

            if (!AffineTransformation.Compute(gameObject, renderer, out var matrix))
                return;

            Vector3 topLeftWorld = new Vector3(matrix.TopLeftX, -matrix.TopLeftY, 0f);
            Vector3 writePos;
            Transform parent = gameObject.transform.parent;
            if (parent != null)
            {
                Vector3 local = parent.InverseTransformPoint(topLeftWorld);
                writePos = new Vector3(local.x, -local.y, 0f);
            }
            else
            {
                writePos = new Vector3(topLeftWorld.x, -topLeftWorld.y, 0f);
            }

            // Bounding + native dimensions
            xml.SetAttribute(element, "X", writePos.x.ToString(CultureInfo.InvariantCulture));
            xml.SetAttribute(element, "Y", writePos.y.ToString(CultureInfo.InvariantCulture));
            xml.SetAttribute(element, "Width", matrix.BoundingWidth.ToString(CultureInfo.InvariantCulture));
            xml.SetAttribute(element, "Height", matrix.BoundingHeight.ToString(CultureInfo.InvariantCulture));
            xml.SetAttribute(element, "NativeX", matrix.NativeWidth.ToString(CultureInfo.InvariantCulture));
            xml.SetAttribute(element, "NativeY", matrix.NativeHeight.ToString(CultureInfo.InvariantCulture));

            // <Matrix> node
            XmlElement matrixNode = xml.AddElement(staticElement, "Matrix");
            xml.SetAttribute(matrixNode, "A", matrix.A.ToString("F6", CultureInfo.InvariantCulture));
            xml.SetAttribute(matrixNode, "B", matrix.B.ToString("F6", CultureInfo.InvariantCulture));
            xml.SetAttribute(matrixNode, "C", matrix.C.ToString("F6", CultureInfo.InvariantCulture));
            xml.SetAttribute(matrixNode, "D", matrix.D.ToString("F6", CultureInfo.InvariantCulture));
            xml.SetAttribute(matrixNode, "Tx", matrix.Tx.ToString("F6", CultureInfo.InvariantCulture));
            xml.SetAttribute(matrixNode, "Ty", matrix.Ty.ToString("F6", CultureInfo.InvariantCulture));
        }

        public static void WriteDynamic(XmlUtility xml, GameObject gameObject, XmlElement propertiesElement)
        {
            if (xml == null || propertiesElement == null || gameObject == null)
                return;

            if (!gameObject.TryGetComponent<DynamicTransform>(out var dynamic))
                return;

            dynamic.WriteToXML(xml, propertiesElement);
        }

        public static void ApplySelectionComponent(XmlElement staticElement, GameObject gameObject)
        {
            if (staticElement == null || gameObject == null)
                return;

            // Find <Selection> element
            XmlElement selectionElement = null;

            foreach (XmlNode node in staticElement.ChildNodes)
            {
                if (node is XmlElement element && element.Name == "Selection")
                {
                    selectionElement = element;
                    break;
                }
            }

            if (selectionElement == null)
                return;

            // Read Variant attribute
            string variantString = selectionElement.GetAttribute("Variant");
            if (string.IsNullOrEmpty(variantString))
                variantString = "CommonMode";

            if (!Enum.TryParse(variantString, out SelectionComponent.SelectionVariant parsedVariant))
                parsedVariant = SelectionComponent.SelectionVariant.CommonMode;

            if (!gameObject.TryGetComponent<SelectionComponent>(out var component))
                component = gameObject.AddComponent<SelectionComponent>();

            component.Variant = parsedVariant;
        }

        public static void ApplyColor(XmlElement staticElement, GameObject gameObject)
        {
            if (staticElement == null || gameObject == null)
                return;

            XmlElement colorElement = null;

            foreach (XmlNode node in staticElement.ChildNodes)
            {
                if (node is XmlElement element && element.Name == "StartColor")
                {
                    colorElement = element;
                    break;
                }
            }

            if (colorElement == null)
                return;

            string colorHex = colorElement.GetAttribute("Color");

            if (string.IsNullOrEmpty(colorHex))
                return;

            if (!ColorUtility.TryParseHtmlString(colorHex, out Color parsedColor))
                return;

            // Apply to SpriteRenderer
            if (!gameObject.TryGetComponent<SpriteRenderer>(out var sprite))
                sprite = gameObject.AddComponent<SpriteRenderer>();

            sprite.color = parsedColor;
        }

        public static (float x, float y) ApplyMatrix(XmlElement staticElement, GameObject gameObject, SpriteRenderer renderer, float x, float y, float width, float height, int nativeX, int nativeY)
        {
            if (staticElement == null || gameObject == null)
                return (x, y);

            XmlElement matrixElement = null;

            foreach (XmlNode node in staticElement.ChildNodes)
            {
                if (node is XmlElement element && element.Name == "Matrix")
                {
                    matrixElement = element;
                    break;
                }
            }

            if (matrixElement != null)
            {
                AffineMatrixData matrix = new AffineMatrixData
                {
                    A = float.Parse(matrixElement.GetAttribute("A"), CultureInfo.InvariantCulture),
                    B = float.Parse(matrixElement.GetAttribute("B"), CultureInfo.InvariantCulture),
                    C = float.Parse(matrixElement.GetAttribute("C"), CultureInfo.InvariantCulture),
                    D = float.Parse(matrixElement.GetAttribute("D"), CultureInfo.InvariantCulture),
                    Tx = float.Parse(matrixElement.GetAttribute("Tx"), CultureInfo.InvariantCulture),
                    Ty = float.Parse(matrixElement.GetAttribute("Ty"), CultureInfo.InvariantCulture),
                    TopLeftX = x,
                    TopLeftY = y,
                    BoundingWidth = width,
                    BoundingHeight = height,
                    NativeWidth = nativeX,
                    NativeHeight = nativeY
                };

                AffineTransformation.ApplyToObject(gameObject, renderer, matrix);

                // Position correction
                x += matrix.Tx;
                y += -matrix.Ty;
            }

            return (x, y);
        }

        public static void ApplyDynamic(XmlElement propertiesElement, GameObject gameObject)
        {
            if (propertiesElement == null || gameObject == null)
                return;

            foreach (XmlNode node in propertiesElement.ChildNodes)
            {
                if (node is not XmlElement dynamicElement || dynamicElement.Name != "Dynamic")
                    continue;

                foreach (XmlNode child in dynamicElement.ChildNodes)
                {
                    if (child is XmlElement transformationElement && transformationElement.Name == "Transformation")
                        DynamicTransform.WriteToScene(transformationElement, gameObject);
                }
            }
        }
    }
}
