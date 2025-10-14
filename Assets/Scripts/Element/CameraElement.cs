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
            Vector3 position = gameObject.transform.position;
            float x = position.x * 100f;
            float y = position.y * -100f; // Vector -Y is up.

            // Write attributes
            xmlUtility.SetAttribute(cameraElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(cameraElement, "Y", y.ToString(CultureInfo.InvariantCulture));

            return cameraElement;
        }
    }
}