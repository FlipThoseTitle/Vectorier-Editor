using System.Xml;
using UnityEngine;
using Vectorier.XML;

namespace Vectorier.Element
{
    public static class CameraElement
    {
        public static XmlElement WriteToXML(GameObject gameObject, XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (gameObject == null || xmlUtility == null || parentElement == null)
                return null;

            // <Camera>
            XmlElement cameraElement = xmlUtility.AddElement(parentElement, "Camera");

            // Try to find <Properties> if it already exists; otherwise create it
            XmlElement propertiesElement = xmlUtility.GetOrCreateElement(cameraElement, "Properties");
            XmlElement staticElement = xmlUtility.GetOrCreateElement(propertiesElement, "Static");

            // Write
            Element.WritePosition(xmlUtility, cameraElement, gameObject);
            Element.WriteSelectionComponent(xmlUtility, staticElement, gameObject);

            return cameraElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Properties
            XmlElement propertiesElement = element.SelectSingleNode("Properties") as XmlElement;
            XmlElement staticElement = propertiesElement?.SelectSingleNode("Static") as XmlElement;

            // Create object
            GameObject cameraObject = Element.CreateObject("Camera", parent, element);

            // Apply
            Element.ApplyPosition(cameraObject, element);
            Element.ApplyLayer(cameraObject, factor);
            Element.ApplySelectionComponent(staticElement, cameraObject);

            // Correction
            cameraObject.transform.localPosition += new Vector3(0f, 0f, -500f);

            // Add and configure Camera component
            Camera cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = 400f;
            cameraComponent.farClipPlane = 100000f;

            cameraObject.AddComponent<Parallax.Parallax>();

            // Set Tag
            cameraObject.tag = "Camera";

            return cameraObject;
        }

        public static GameObject Create(Transform parent = null)
        {
            GameObject cameraObject = new GameObject("Camera");

            // Parent
            if (parent != null)
            {
                cameraObject.transform.SetParent(parent, false);
                cameraObject.transform.localPosition = Vector3.zero;
            }

            cameraObject.transform.localPosition += new Vector3(0f, 0f, -500f);

            // Add and configure Camera component
            Camera cameraComponent = cameraObject.AddComponent<Camera>();
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = 400f;
            cameraComponent.farClipPlane = 100000f;
            cameraObject.AddComponent<Parallax.Parallax>();
            cameraObject.tag = "Camera";

            return cameraObject;
        }
    }
}