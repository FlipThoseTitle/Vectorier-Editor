using UnityEngine;
using System.Xml;
using System.Collections.Generic;
using Vectorier.XML;
using Vectorier.Element;

namespace Vectorier.Handler
{
    public static class ImportHandler
    {
        public static void Import(string directoryPath, string xmlFileName, List<string> textureFolders, bool untagChildren)
        {
            string fullPathToFile = System.IO.Path.Combine(directoryPath, xmlFileName);

            if (!System.IO.File.Exists(fullPathToFile))
            {
                Debug.LogError("XML file not found: " + fullPathToFile);
                return;
            }

            // Load XML
            XmlUtility xml = new XmlUtility();
            xml.Load(fullPathToFile);

            // Determine file type
            XmlElement trackElement = xml.RootElement.SelectSingleNode("Track") as XmlElement;
            XmlElement objectsElement = null;

            // Look for <Objects> containing children
            foreach (XmlNode node in xml.RootElement.ChildNodes)
            {
                if (node is XmlElement objElement && objElement.Name == "Objects")
                {
                    if (objElement.SelectSingleNode("Object") != null)
                    {
                        objectsElement = objElement;
                        break;
                    }
                }
            }

            bool fileIsLevel;
            XmlElement mainSection;

            if (trackElement != null)
            {
                fileIsLevel = true;
                mainSection = trackElement;
            }
            else if (objectsElement != null)
            {
                fileIsLevel = false;
                mainSection = objectsElement;
            }
            else
            {
                Debug.LogError("Invalid XML: Expected <Track> or <Objects> under the root element.");
                return;
            }

            // Create a root container
            GameObject importRoot = new GameObject(xmlFileName);

            // Process each <Object> entry
            foreach (XmlElement objectElement in mainSection.SelectNodes("Object"))
            {
                GameObject parentGroup = null;
                string layerFactor = "1";
                XmlElement content;

                if (fileIsLevel)
                {
                    // Level: layers based on parallax Factor
                    string factorText = objectElement.GetAttribute("Factor");
                    content = objectElement.SelectSingleNode("Content") as XmlElement;
                    layerFactor = factorText;
                    parentGroup = new GameObject("Factor_" + layerFactor);
                }
                else
                {
                    // Object
                    content = objectsElement;
                    parentGroup = importRoot;
                }

                parentGroup.transform.SetParent(importRoot.transform, false);

                foreach (XmlNode node in content.ChildNodes)
                {
                    if (node is not XmlElement element)
                        continue;

                    WriteByTag(element, parentGroup.transform, layerFactor);
                }
            }

            Debug.Log($"Import completed: {xmlFileName}");
        }

        public static void WriteByTag(XmlElement element, Transform parent, string factor)
        {
            switch (element.Name)
            {
                case "Object":
                    ObjectElement.WriteToScene(element, parent, factor);
                    break;

                case "Platform":
                    PlatformElement.WriteToScene(element, parent, factor);
                    break;
            }
        }
    }
}
