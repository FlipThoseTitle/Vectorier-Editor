using System.Globalization;
using UnityEditor;
using UnityEngine;
using System.Xml;
using Vectorier.XML;
using Vectorier.Component;
using UnityEngine.Rendering;

namespace Vectorier.Element
{
    public static class ModelElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            gameObject.TryGetComponent<ModelComponent>(out var modelComponent);

            // <Model>
            XmlElement modelElement = xmlUtility.AddElement(parentElement, "Model");

            // Properties
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(modelElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Compute position
            Element.WritePosition(xmlUtility, modelElement, gameObject);

            // Write attributes
            if (modelComponent != null)
                xmlUtility.SetAttribute(modelElement, "Type", ((int)modelComponent.Type).ToString(CultureInfo.InvariantCulture));
            else
                xmlUtility.SetAttribute(modelElement, "Type", "1");

            Element.WriteClassName(gameObject, xmlUtility, modelElement);
            xmlUtility.SetAttribute(modelElement, "LifeTime", modelComponent.LifeTime.ToString(CultureInfo.InvariantCulture));

            // Selection Component
            Element.WriteSelectionComponent(xmlUtility, staticElement, gameObject);

            return modelElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Properties
            XmlElement propertiesElement = element.SelectSingleNode("Properties") as XmlElement;
            XmlElement staticElement = propertiesElement?.SelectSingleNode("Static") as XmlElement;

            string className = element.GetAttribute("ClassName");
            string typeStr = element.GetAttribute("Type");
            string lifetimeStr = element.GetAttribute("LifeTime");

            GameObject modelObject = null;

            GameObject[] prefabs = Resources.LoadAll<GameObject>("Models");

            foreach (var prefab in prefabs)
            {
                if (prefab.name == className)
                {
                    modelObject = Object.Instantiate(prefab, parent);
                    modelObject.name = prefab.name;
                    break;
                }
            }
            if (modelObject == null)
            {
                modelObject = Element.CreateObject("Model", parent, element);
            }

            // Apply
            Element.ApplyPosition(modelObject, element);
            Element.ApplyLayer(modelObject, factor);

            ModelComponent modelComponent = modelObject.GetComponent<ModelComponent>() ?? modelObject.AddComponent<ModelComponent>();
            if (int.TryParse(typeStr, out int typeValue))
                modelComponent.Type = (ModelComponent.ModelType)typeValue;

            if (int.TryParse(lifetimeStr, out int lifetime))
                modelComponent.LifeTime = lifetime;

            Element.ApplySelectionComponent(staticElement, modelObject);
            Element.ApplyDynamic(propertiesElement, modelObject);

            // Tag
            modelObject.tag = "Model";

            // correction
            modelObject.transform.localPosition += new Vector3(0f, 0f, -300f);

            return modelObject;
        }
    }
}