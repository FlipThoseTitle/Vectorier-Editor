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

            // Create <Spawn> node
            XmlElement spawnElement = xmlUtility.AddElement(parentElement, "Spawn");

            // Compute attributes
            Vector3 position = gameObject.transform.position;
            float x = position.x * 100f;
            float y = position.y * -100f; // Vector -Y is up.

            // Write attributes
            xmlUtility.SetAttribute(spawnElement, "X", x.ToString(CultureInfo.InvariantCulture));
            xmlUtility.SetAttribute(spawnElement, "Y", y.ToString(CultureInfo.InvariantCulture));

            string cleanName = System.Text.RegularExpressions.Regex.Replace(gameObject.name, @"\s*\(\d+\)$", "");
            xmlUtility.SetAttribute(spawnElement, "Name", cleanName);

            // Spawn Component
            SpawnComponent spawnComponent = gameObject.GetComponent<SpawnComponent>();
            string animationString = spawnComponent.Animation;
            if (spawnComponent == null || string.IsNullOrWhiteSpace(spawnComponent.Animation))
                xmlUtility.SetAttribute(spawnElement, "Animation", "JumpOff|18");
            else
                xmlUtility.SetAttribute(spawnElement, "Animation", animationString);

            return spawnElement;
        }
    }
}