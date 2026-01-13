using System.Globalization;
using UnityEngine;
using System.Xml;
using Vectorier.XML;
using Vectorier.Component;
using Vectorier.Handler;

namespace Vectorier.Element
{
    public static class ImageElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            if (!gameObject.TryGetComponent<SpriteRenderer>(out var renderer))
                return null;

            // <Image>
            XmlElement imageElement = xmlUtility.AddElement(parentElement, "Image");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(imageElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Write
            Element.WritePosition(xmlUtility, imageElement, gameObject);
            Element.WriteClassName(gameObject, xmlUtility, imageElement);
            Element.WriteSize(xmlUtility, imageElement, gameObject);
            Element.WriteNativeSize(xmlUtility, imageElement, gameObject);
            Element.WriteColor(xmlUtility, staticElement, gameObject);
            Element.WriteMatrix(xmlUtility, staticElement, imageElement, gameObject, renderer);
            Element.WriteDynamic(xmlUtility, gameObject, propertiesElement);
            Element.WriteSelectionComponent(xmlUtility, staticElement, gameObject);

            // Image Component
            if (gameObject.TryGetComponent<ImageComponent>(out var imageComponent))
            {
                if (imageComponent.Type != ImageComponent.ImageType.None)
                    xmlUtility.SetAttribute(imageElement, "Type", ((int)imageComponent.Type).ToString());

                if (imageComponent.depth == ImageComponent.ImageDepth.Front)
                    xmlUtility.SetAttribute(imageElement, "Depth", "0");
                else if (imageComponent.depth == ImageComponent.ImageDepth.Back)
                    xmlUtility.SetAttribute(imageElement, "Depth", "1");
            }

            return imageElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Basic Info
            Element.GetPositionXML(element, out float positionX, out float positionY);
            Element.GetSizeXML(element, out float xmlWidth, out float xmlHeight);

            // Create GameObject
            GameObject imageObject = new GameObject("Image");
            Element.ApplyName(imageObject, element, "Image");

            // Layer
            Element.ApplyLayer(imageObject, factor);

            // SpriteRenderer
            SpriteRenderer renderer = imageObject.AddComponent<SpriteRenderer>();

            // Component
            ImageComponent imageComponent = imageObject.AddComponent<ImageComponent>();

            // LAYERING SYSTEM

            if (element.HasAttribute("Depth") && int.TryParse(element.GetAttribute("Depth"), out int depthValue))
            {
                imageComponent.depth =
                    depthValue == 0 ? ImageComponent.ImageDepth.Front :
                    depthValue == 1 ? ImageComponent.ImageDepth.Middle :
                    depthValue == 2 ? ImageComponent.ImageDepth.Back :
                    ImageComponent.ImageDepth.Middle;
            }

            float factorValue = float.Parse(factor, CultureInfo.InvariantCulture);
            int factorBand = Mathf.RoundToInt(factorValue * 10000);

            int sortOffset =
                imageComponent.depth == ImageComponent.ImageDepth.Front ? 200 + ImportHandler.GlobalOrder_Front++ :
                imageComponent.depth == ImageComponent.ImageDepth.Back ? 0 + ImportHandler.GlobalOrder_Back++ :
                100 + ImportHandler.GlobalOrder_Middle++;

            renderer.sortingOrder = factorBand + sortOffset;

            // Build cache if needed
            if (!ImportHandler.SpriteCacheBuilt)
                ImportHandler.BuildSpriteCache();

            string className = element.GetAttribute("ClassName");
            ImportHandler.SpriteCache.TryGetValue(className, out Sprite loadedSprite);

            bool isMissingSprite = (loadedSprite == null);
            if (isMissingSprite)
            {
                Debug.LogWarning($"[ImageElement] Sprite '{className}' not found in any texture folder.");

                loadedSprite = Resources.Load<Sprite>("Images/Editor/Misc/red");
                imageObject.name = "[MISSING] " + className;
                imageObject.tag = "EditorOnly";

                if (loadedSprite == null)
                    Debug.LogWarning("[ImageElement] Fallback sprite 'Images/Editor/Misc/red' could not be loaded.");
            }

            // Native Resolution
            bool hasNativeX = int.TryParse(element.GetAttribute("NativeX"), NumberStyles.Any, CultureInfo.InvariantCulture, out int nativeX);
            bool hasNativeY = int.TryParse(element.GetAttribute("NativeY"), NumberStyles.Any, CultureInfo.InvariantCulture, out int nativeY);

            // Fallback 1 - Use sprite texture resolution
            if ((!hasNativeX || !hasNativeY) && loadedSprite != null && loadedSprite.texture != null)
            {
                nativeX = loadedSprite.texture.width;
                nativeY = loadedSprite.texture.height;
            }

            // Fallback 2 - Use XML width/height
            if (!hasNativeX || !hasNativeY)
            {
                if (nativeX == 0) nativeX = Mathf.RoundToInt(xmlWidth);
                if (nativeY == 0) nativeY = Mathf.RoundToInt(xmlHeight);
            }

            // Assign sprite + scale
            if (loadedSprite != null)
            {
                renderer.sprite = loadedSprite;

                Texture2D texture = loadedSprite.texture;
                if (texture != null)
                {
                    float scaleX = xmlWidth / texture.width;
                    float scaleY = xmlHeight / texture.height;
                    imageObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                }
            }


            // ------------ COMPONENT -----------------


            // Image Type
            if (element.HasAttribute("Type") && int.TryParse(element.GetAttribute("Type"), out int typeValue))
            {
                imageComponent.Type =
                    typeValue == 0 ? ImageComponent.ImageType.None :
                    typeValue == 1 ? ImageComponent.ImageType.Static :
                    typeValue == 2 ? ImageComponent.ImageType.Vanishing :
                    typeValue == 3 ? ImageComponent.ImageType.Dynamic :
                    ImageComponent.ImageType.None;
            }

            // Properties
            XmlElement propertiesElement = element.SelectSingleNode("Properties") as XmlElement;
            XmlElement staticElement = propertiesElement?.SelectSingleNode("Static") as XmlElement;

            Element.ApplyColor(staticElement, imageObject);
            Element.ApplySelectionComponent(staticElement, imageObject);
            (float finalX, float finalY) = Element.ApplyMatrix(staticElement, imageObject, renderer, positionX, positionY, xmlWidth, xmlHeight, nativeX, nativeY);
            Element.ApplyDynamic(propertiesElement, imageObject);

            imageObject.transform.SetParent(parent, false);
            imageObject.transform.localPosition = new Vector3(finalX, finalY, 0f);

            // Tag
            if (!isMissingSprite)
                imageObject.tag = "Image";

            if (className == "v_black")
            {
                renderer.sortingOrder = -1;
                renderer.sortingLayerName = "OnTop";
                if (imageObject.TryGetComponent<ImageComponent>(out var vblackdepth))
                    vblackdepth.depth = ImageComponent.ImageDepth.Front;
            }
                
            return imageObject;
        }
    }
}