using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;
using Vectorier.XML;
using Vectorier.Element;
using Vectorier.Debug;
using System.Linq;

namespace Vectorier.Handler
{
    public static class ExportHandler
    {
        public enum ExportMode
        {
            Level,
            Objects
        }

        public static void Export(ExportMode mode, string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                DebugLog.Error("[ExportHandler] Output path is empty.");
                return;
            }

            XmlUtility xmlUtility = new XmlUtility();

            if (File.Exists(outputPath))
            {
                xmlUtility.Load(outputPath);
                DebugLog.Info("[ExportHandler] Loaded existing XML to append content.");
            }
            else
            {
                xmlUtility.Create("Root");
                DebugLog.Info("[ExportHandler] Created new XML document.");
            }

            XmlElement root = xmlUtility.RootElement;

            switch (mode)
            {
                case ExportMode.Level:
                    HandleLevelExport(xmlUtility, root);
                    break;

                case ExportMode.Objects:
                    HandleObjectsExport(xmlUtility, root);
                    break;
            }

            xmlUtility.RemoveEmptyElements(root);
            xmlUtility.Save(outputPath);

            DebugLog.Info($"[ExportHandler] Export completed: {outputPath}");
        }

        private static void HandleLevelExport(XmlUtility xmlUtility, XmlElement root)
        {
            XmlElement trackElement = xmlUtility.AddElement(root, "Track");
            Dictionary<string, XmlElement> layerObjects = new Dictionary<string, XmlElement>();
            Dictionary<string, List<GameObject>> groupedObjects = new Dictionary<string, List<GameObject>>();

            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

            // Group by layer factor
            foreach (GameObject obj in allObjects)
            {
                if (!obj.activeInHierarchy)
                    continue;

                string tag = obj.tag;
                if (string.IsNullOrEmpty(tag) || tag == "Untagged")
                    continue;

                string layerName = LayerToFactor(obj.layer);
                if (!float.TryParse(layerName, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float layerValue))
                    layerValue = 1f;

                string layerFactor = layerValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (!groupedObjects.ContainsKey(layerFactor))
                    groupedObjects[layerFactor] = new List<GameObject>();

                groupedObjects[layerFactor].Add(obj);
            }

            // Process by layer factor order (low to high)
            foreach (var kvp in groupedObjects.OrderBy(k => float.Parse(k.Key, System.Globalization.CultureInfo.InvariantCulture)))
            {
                string layerFactor = kvp.Key;
                List<GameObject> objs = kvp.Value;

                XmlElement objectElement = xmlUtility.AddElement(trackElement, "Object");
                xmlUtility.SetAttribute(objectElement, "Factor", layerFactor);
                XmlElement contentElement = xmlUtility.AddElement(objectElement, "Content");

                // Sort image elements by SpriteRenderer.sortingOrder (ascending)
                var imageObjs = objs.Where(o => o.tag == "Image").OrderBy(o =>
                {
                    var sr = o.GetComponent<SpriteRenderer>();
                    return sr ? sr.sortingOrder : 0;
                });

                // Write sorted images first
                foreach (var img in imageObjs)
                    WriteByTag(img, xmlUtility, contentElement);

                // Write non-image objects as-is
                foreach (var other in objs.Where(o => o.tag != "Image"))
                    WriteByTag(other, xmlUtility, contentElement);
            }
        }

        private static void HandleObjectsExport(XmlUtility xmlUtility, XmlElement root)
        {
            XmlElement objectsElement = xmlUtility.AddElement(root, "Objects");
            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

            foreach (GameObject obj in allObjects)
            {
                if (!obj.activeInHierarchy)
                    continue;

                string tag = obj.tag;
                if (string.IsNullOrEmpty(tag) || tag == "Untagged")
                    continue;

                XmlElement objectElement = xmlUtility.AddElement(objectsElement, "Object");
                xmlUtility.SetAttribute(objectElement, "Name", obj.name);

                XmlElement contentElement = xmlUtility.AddElement(objectElement, "Content");

                WriteByTag(obj, xmlUtility, contentElement);
            }
        }

        private static void WriteByTag(GameObject obj, XmlUtility xmlUtility, XmlElement parent)
        {
            switch (obj.tag)
            {
                case "Image":
                    ImageElement.WriteToXML(obj, xmlUtility, parent);
                    break;

                case "Trigger":
                    TriggerElement.WriteToXML(obj, xmlUtility, parent);
                    break;

                case "Area":
                    AreaElement.WriteToXML(obj, xmlUtility, parent);
                    break;

                case "Platform":
					PlatformElement.WriteToXML(obj, xmlUtility, parent);
					break;

                case "Trapezoid":
                    TrapezoidElement.WriteToXML(obj, xmlUtility, parent);
                    break;

                case "Camera":
                    CameraElement.WriteToXML(obj, xmlUtility, parent);
                    break;

                case "Spawn":
                    SpawnElement.WriteToXML(obj, xmlUtility, parent);
                    break;

                case "Item":
                    ItemElement.WriteToXML(obj, xmlUtility, parent);
                    break;

                case "Model":
                    ModelElement.WriteToXML(obj, xmlUtility, parent);
                    break;

                case "Particle":
                    ModelElement.WriteToXML(obj, xmlUtility, parent);
                    break;
            }
        }

        private static string LayerToFactor(int layer)
        {
            // Use the actual layer name as the Factor, ex: "0.5"
            string name = LayerMask.LayerToName(layer);
            return name;
        }
    }
}
