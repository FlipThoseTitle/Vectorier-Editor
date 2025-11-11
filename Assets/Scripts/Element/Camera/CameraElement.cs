using System.Globalization;
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

            Camera cameraComponent = gameObject.GetComponent<Camera>();
            if (cameraComponent == null)
                return null;

            // Create <Camera> node
            XmlElement cameraElement = xmlUtility.AddElement(parentElement, "Camera");

            // Compute attributes
            Vector3 position = gameObject.transform.localPosition;
            float x = position.x * 100f;
            float y = position.y * -100f; // Vector -Y is up.

            // Write attributes
            xmlUtility.SetAttribute(cameraElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(cameraElement, "Y", y.ToString(CultureInfo.InvariantCulture));

            return cameraElement;
        }

        public static GameObject WriteToScene(XmlElement element, Transform parent, string factor)
        {
            if (element == null)
                return null;

            // Parse basic Info
            float x = float.Parse(element.GetAttribute("X")) / 100f;
            float y = -float.Parse(element.GetAttribute("Y")) / 100f;

            // Create object
            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(x, y, 0f);

            // Add and configure Camera component
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4f;

            // Set layer by factor
            string layerName = factor;
            int layerIndex = LayerMask.NameToLayer(layerName);
            if (layerIndex != -1)
                cameraObject.layer = layerIndex;
            else
                Debug.LogWarning($"Layer '{layerName}' does not exist. Camera '{cameraObject.name}' assigned to Default layer.");

            // Set Tag
            cameraObject.tag = "Camera";

            return cameraObject;
        }
    }
}