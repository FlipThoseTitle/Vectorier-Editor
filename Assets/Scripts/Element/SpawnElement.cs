using System.Globalization;
using System.Xml;
using UnityEngine;
using Vectorier.Component;
using Vectorier.XML;

namespace Vectorier.Element
{
    public static class SpawnElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            // <Spawn>
            XmlElement spawnElement = xmlUtility.AddElement(parentElement, "Spawn");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(spawnElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Compute attributes
            Element.WritePosition(xmlUtility, spawnElement, gameObject);
            Element.WriteName(gameObject, xmlUtility, spawnElement);

            // Spawn Component
            if (!gameObject.TryGetComponent<SpawnComponent>(out var spawnComponent))
            {
                Debug.LogWarning(
                    $"[SpawnElement] GameObject '{gameObject.name}' has no SpawnComponent. Using default animation.",
                    gameObject
                );

                xmlUtility.SetAttribute(spawnElement, "Animation", "JumpOff|18");
            }
            else if (string.IsNullOrWhiteSpace(spawnComponent.Animation))
            {
                Debug.LogWarning(
                    $"[SpawnElement] GameObject '{gameObject.name}' has a SpawnComponent but no Animation set. Using default animation.",
                    gameObject
                );

                xmlUtility.SetAttribute(spawnElement, "Animation", "JumpOff|18");
            }
            else
            {
                xmlUtility.SetAttribute(spawnElement, "Animation", spawnComponent.Animation);
            }

            // Selection Component
            Element.WriteSelectionComponent(xmlUtility, staticElement, gameObject);

            return spawnElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Properties
            XmlElement propertiesElement = element.SelectSingleNode("Properties") as XmlElement;
            XmlElement staticElement = propertiesElement?.SelectSingleNode("Static") as XmlElement;

            // Create object
            GameObject spawnObject = Element.CreateObject("Spawn", parent, element);

            // Apply
            Element.ApplyPosition(spawnObject, element);
            Element.ApplyLayer(spawnObject, factor);
            Element.ApplySelectionComponent(staticElement, spawnObject);

            // SpriteRenderer
            SpriteRenderer spriteRenderer = spawnObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = Resources.Load<Sprite>("Images/Editor/Misc/mark");
            spriteRenderer.color = Color.red;

            // Add and configure Spawn component
            SpawnComponent spawn = spawnObject.AddComponent<SpawnComponent>();
            string animation = element.GetAttribute("Animation");
            spawn.Animation = string.IsNullOrWhiteSpace(animation) ? "JumpOff|18" : animation;

            Element.ApplyDynamic(propertiesElement, spawnObject);

            // Set Tag
            spawnObject.tag = "Spawn";
            spriteRenderer.sortingLayerName = "OnTop";
            spriteRenderer.sortingOrder = 0;

            return spawnObject;
        }
    }
}