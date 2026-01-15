using System.Globalization;
using System.Xml;
using UnityEngine;
using UnityEngine.UIElements;
using Vectorier.Component;
using Vectorier.Handler;
using Vectorier.XML;

namespace Vectorier.Element
{
    public static class AnimationElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            if (!gameObject.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                return null;

            if (!gameObject.TryGetComponent<AnimationComponent>(out var animationComponent))
                return null;

            // <Animation>
            XmlElement animationElement = xmlUtility.AddElement(parentElement, "Animation");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(animationElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Write
            Element.WritePosition(xmlUtility, animationElement, gameObject);
            Element.WriteSize(xmlUtility, animationElement, gameObject);
            Element.WriteClassName(gameObject, xmlUtility, animationElement);
            xmlUtility.SetAttribute(animationElement, "Type", ((int)animationComponent.Type).ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(animationElement, "ScaleX", gameObject.transform.lossyScale.x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(animationElement, "ScaleY", gameObject.transform.lossyScale.y.ToString(CultureInfo.InvariantCulture));

            if (animationComponent.Type == AnimationComponent.AnimationType.Type1)
            {
                xmlUtility.SetAttribute(animationElement, "Direction", animationComponent.Direction);
                xmlUtility.SetAttribute(animationElement, "Acceleration", animationComponent.Acceleration);
                xmlUtility.SetAttribute(animationElement, "Time", animationComponent.Time.ToString(CultureInfo.InvariantCulture));
            }

            // Selection
            Element.WriteSelectionComponent(xmlUtility, staticElement, gameObject);


            return animationElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Properties
            XmlElement propertiesElement = element.SelectSingleNode("Properties") as XmlElement;
            XmlElement staticElement = propertiesElement?.SelectSingleNode("Static") as XmlElement;

            // Create object
            GameObject animationObject = Element.CreateObject("Animation", parent, element);

            // Sprite
            SpriteRenderer renderer = animationObject.AddComponent<SpriteRenderer>();

            if (!ImportHandler.SpriteCacheBuilt)
                ImportHandler.BuildSpriteCache();

            string className = element.GetAttribute("ClassName");
            ImportHandler.SpriteCache.TryGetValue(className, out Sprite loadedSprite);

            bool isMissingSprite = (loadedSprite == null);
            if (isMissingSprite)
            {
                Debug.LogWarning($"[AnimationElement] Sprite '{className}' not found in any texture folder.");

                loadedSprite = Resources.Load<Sprite>("Images/Editor/Misc/rect");
                animationObject.name = className + " [MISSING]";
                animationObject.tag = "EditorOnly";
                renderer.color = Color.red;

                if (loadedSprite == null)
                    Debug.LogWarning("[AnimationElement] Fallback sprite 'Images/Editor/Misc/rect' could not be loaded.");
            }

            // Size
            Element.GetSizeXML(element, out float xmlWidth, out float xmlHeight);

            // Assign sprite + scale
            if (loadedSprite != null)
            {
                renderer.sprite = loadedSprite;

                Texture2D texture = loadedSprite.texture;
                if (texture != null)
                {
                    float scaleX = xmlWidth / texture.width;
                    float scaleY = xmlHeight / texture.height;
                    animationObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                }
            }

            // Apply
            Element.ApplyPosition(animationObject, element);
            Element.ApplyLayer(animationObject, factor);
            Element.ApplySelectionComponent(staticElement, animationObject);
            Element.ApplyDynamic(propertiesElement, animationObject);

            // Component
            var animationComponent = animationObject.AddComponent<AnimationComponent>();
            if (int.TryParse(element.GetAttribute("Type"), out int typeValue))
            {
                animationComponent.Type = (AnimationComponent.AnimationType)typeValue;
            }
            if (animationComponent.Type == AnimationComponent.AnimationType.Type1)
            {
                if (element.HasAttribute("Direction"))
                    animationComponent.Direction = element.GetAttribute("Direction");

                if (element.HasAttribute("Acceleration"))
                    animationComponent.Acceleration = element.GetAttribute("Acceleration");

                if (float.TryParse(element.GetAttribute("Time"), NumberStyles.Float,
                                   CultureInfo.InvariantCulture, out float time))
                {
                    animationComponent.Time = time;
                }
            }

            // Tag
            if (!isMissingSprite)
                animationObject.tag = "Animation";

            renderer.sortingOrder = 3;
            renderer.sortingLayerName = "OnTop";

            return animationObject;
        }
    }
}