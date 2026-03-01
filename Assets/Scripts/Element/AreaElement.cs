using System;
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

            if (!gameObject.TryGetComponent<SpriteRenderer>(out var spriteRenderer))
                return null;

            // <Area>
            XmlElement areaElement = xmlUtility.AddElement(parentElement, "Area");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(areaElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Write
            Element.WriteName(gameObject, xmlUtility, areaElement);
            Element.WritePosition(xmlUtility, areaElement, gameObject);
            Element.WriteSize(xmlUtility, areaElement, gameObject);

            // AreaComponent
            gameObject.TryGetComponent<AreaComponent>(out var areaComponent);

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
                }
            }

            // Selection
            Element.WriteSelectionComponent(xmlUtility, staticElement, gameObject);

            return areaElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Properties
            XmlElement propertiesElement = element.SelectSingleNode("Properties") as XmlElement;
            XmlElement staticElement = propertiesElement?.SelectSingleNode("Static") as XmlElement;

            // Create object
            GameObject areaObject = Element.CreateObject("Area", parent, element);

            // Sprite
            SpriteRenderer spriteRenderer = areaObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>("Images/Editor/Trigger/trigger");
            spriteRenderer.color = new Color(1f, 0f, 0f, 1f);

            // Apply
            Element.ApplyPosition(areaObject, element);
            Element.ApplySize(areaObject, spriteRenderer.sprite, element);
            Element.ApplyLayer(areaObject, factor);
            Element.ApplySelectionComponent(staticElement, areaObject);
            Element.ApplyDynamic(propertiesElement, areaObject);

            // Read Type
            string typeStr = element.GetAttribute("Type");

            if (!Enum.TryParse(typeStr, out AreaComponent.AreaType typeEnum))
                typeEnum = AreaComponent.AreaType.Animation;

            // Only add AreaComponent for non-Animation types
            AreaComponent areaComponent;

            if (typeEnum != AreaComponent.AreaType.Animation)
            {
                areaComponent = areaObject.AddComponent<AreaComponent>();
                areaComponent.Type = typeEnum;

                switch (typeEnum)
                {
                    case AreaComponent.AreaType.Catch:
                        int.TryParse(element.GetAttribute("Distance"), out areaComponent.Distance);
                        break;

                    case AreaComponent.AreaType.Trick:
                        areaComponent.ItemName = element.GetAttribute("ItemName");
                        int.TryParse(element.GetAttribute("Score"), out areaComponent.Score);
                        break;

                    case AreaComponent.AreaType.Help:
                        areaComponent.Key = element.GetAttribute("Key");
                        areaComponent.Description = element.GetAttribute("Description");
                        break;
                }
            }

            // Trick visuals
            if (typeEnum == AreaComponent.AreaType.Trick)
            {
                CreateTrickVisuals(areaObject, spriteRenderer, element.GetAttribute("ItemName"));
            }

            // Tag
            areaObject.tag = "Area";
            spriteRenderer.sortingLayerName = "OnTop";
            spriteRenderer.sortingOrder = 1;

            return areaObject;
        }

        public static GameObject Create(Transform parent = null)
        {
            GameObject areaObject = new GameObject("Area");

            // Parent
            if (parent != null)
            {
                areaObject.transform.SetParent(parent, false);
                areaObject.transform.localPosition = Vector3.zero;
            }

            // Component
            AreaComponent areaComponent = areaObject.AddComponent<AreaComponent>();

            // Sprite
            SpriteRenderer spriteRenderer = areaObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>("Images/Editor/Trigger/trigger");
            spriteRenderer.color = new Color(1f, 0f, 0f, 1f);

            // Tag + sorting
            areaObject.tag = "Area";
            spriteRenderer.sortingLayerName = "OnTop";
            spriteRenderer.sortingOrder = 1;

            return areaObject;
        }

        private static void CreateTrickVisuals(GameObject areaObject, SpriteRenderer areaSpriteRenderer, string itemName)
        {
            if (areaObject == null || areaSpriteRenderer == null)
                return;

            Vector3 centerWorld = areaSpriteRenderer.bounds.center;
            Vector3 parentLossy = areaObject.transform.lossyScale;
            Vector3 centerLocal = areaObject.transform.InverseTransformPoint(centerWorld);

            GameObject idleObj = new GameObject("trick_idle_up");
            idleObj.tag = "EditorOnly";
            idleObj.transform.SetParent(areaObject.transform, false);
            idleObj.transform.localPosition = centerLocal;
            idleObj.transform.localRotation = Quaternion.identity;

            const float desiredIdleWorldScaleX = 1f;
            const float desiredIdleWorldScaleY = 1f;

            float idleLocalScaleX = parentLossy.x != 0f ? (desiredIdleWorldScaleX / parentLossy.x) : desiredIdleWorldScaleX;
            float idleLocalScaleY = parentLossy.y != 0f ? (desiredIdleWorldScaleY / parentLossy.y) : desiredIdleWorldScaleY;

            idleObj.transform.localScale = new Vector3(idleLocalScaleX, idleLocalScaleY, 1f);

            SpriteRenderer idleSR = idleObj.AddComponent<SpriteRenderer>();
            idleSR.sprite = Resources.Load<Sprite>("Images/Editor/Tricks/trick_idle_up");
            idleSR.sortingLayerName = "OnTop";
            idleSR.sortingOrder = 5;

            if (idleSR.sprite == null)
                Debug.LogWarning("[AreaElement] Missing trick_idle_up sprite at Resources path 'Images/Editor/Tricks/trick_idle_up'.");

            // ----- track sprite lookup -----
            if (string.IsNullOrEmpty(itemName))
            {
                Debug.LogWarning("[AreaElement] Trick Area has empty ItemName; skipping trick image.");
                return;
            }

            string expectedSpriteName = "TRACK_" + itemName;

            Sprite match = null;
            Sprite[] sprites = Resources.LoadAll<Sprite>("Images/Editor/Tricks");
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && string.Equals(sprites[i].name, expectedSpriteName, StringComparison.Ordinal))
                {
                    match = sprites[i];
                    break;
                }
            }

            if (match == null)
            {
                Debug.LogWarning($"[AreaElement] No matching trick sprite found for ItemName='{itemName}'. " + $"Expected sprite name '{expectedSpriteName}' in Resources/Images/Editor/Tricks. " + "Skipping trick image; keeping only trick_idle_up.");
                return;
            }

            GameObject trickGO = new GameObject(expectedSpriteName);
            trickGO.tag = "EditorOnly";
            trickGO.transform.SetParent(areaObject.transform, false);
            trickGO.transform.localRotation = Quaternion.identity;

            SpriteRenderer trickSR = trickGO.AddComponent<SpriteRenderer>();
            trickSR.sprite = match;
            trickSR.sortingLayerName = "OnTop";
            trickSR.sortingOrder = 5;

            const float desiredWorldYOffset = -23.5259f;
            Vector3 trackWorld = centerWorld + new Vector3(0f, desiredWorldYOffset, 0f);
            Vector3 trackLocal = areaObject.transform.InverseTransformPoint(trackWorld);
            trickGO.transform.localPosition = trackLocal;

            const float desiredWorldScaleX = 0.9059364f;
            const float desiredWorldScaleY = 0.9138624f;

            float localScaleX = parentLossy.x != 0f ? (desiredWorldScaleX / parentLossy.x) : desiredWorldScaleX;
            float localScaleY = parentLossy.y != 0f ? (desiredWorldScaleY / parentLossy.y) : desiredWorldScaleY;

            trickGO.transform.localScale = new Vector3(localScaleX, localScaleY, 1f);
        }
    }
}