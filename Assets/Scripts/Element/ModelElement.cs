using System.Globalization;
using UnityEngine;
using System.Xml;
using Vectorier.XML;
using Vectorier.Component;
using TMPro;

namespace Vectorier.Element
{
    public static class ModelElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            ModelComponent modelComponent = gameObject.GetComponent<ModelComponent>();
            if (modelComponent == null) return null;

            // Create <Model> node
            XmlElement modelElement = xmlUtility.AddElement(parentElement, "Model");

            // Compute attributes
            Vector3 position = gameObject.transform.position;
            float x = position.x * 100f;
            float y = position.y * -100f; // Vector -Y is up.

            // Write attributes
            xmlUtility.SetAttribute(modelElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(modelElement, "Y", y.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(modelElement, "Type", ((int)modelComponent.Type).ToString(CultureInfo.InvariantCulture));
            string cleanName = System.Text.RegularExpressions.Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            xmlUtility.SetAttribute(modelElement, "ClassName", cleanName);
            xmlUtility.SetAttribute(modelElement, "LifeTime", modelComponent.LifeTime.ToString(CultureInfo.InvariantCulture));

            return modelElement;
        }
    }
}