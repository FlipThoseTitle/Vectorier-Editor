using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using Vectorier.Core;

namespace Vectorier.EditorScript
{
    public class ObjectListWindow : EditorWindow
    {
        private ImportConfig config;
        private Vector2 scroll;
        private string search = "";

        private List<string> objectNames = new List<string>();
        private Dictionary<string, bool> selection = new Dictionary<string, bool>();

        private const string ScrollYKeyPrefix = "Vectorier_ObjectList_ScrollY_";
        private string scrollKey;

        public static void Open(ImportConfig config)
        {
            string fullPath = Path.Combine(config.filePathDirectory, config.xmlName + ".xml");

            if (!File.Exists(fullPath))
            {
                Debug.LogError("XML file does not exist.");
                return;
            }

            XmlDocument document = new XmlDocument();
            try
            {
                document.Load(fullPath);
            }
            catch
            {
                Debug.LogError("Failed to parse XML.");
                return;
            }

            XmlNode root = document.DocumentElement;
            if (root == null)
            {
                Debug.LogError("Invalid XML: No root element.");
                return;
            }

            XmlNode objectsNode = root.SelectSingleNode("Objects");
            XmlNodeList objectNodes = objectsNode?.SelectNodes("Object");

            if (objectNodes == null || objectNodes.Count == 0)
            {
                Debug.LogError("Invalid XML: Make sure that the XML is an object or a building type.");
                return;
            }

            ObjectListWindow window = CreateInstance<ObjectListWindow>();
            window.config = config;

            window.scrollKey = ScrollYKeyPrefix + config.xmlName;
            float savedScrollY = EditorPrefs.GetFloat(window.scrollKey, 0f);
            window.scroll = new Vector2(0f, savedScrollY);

            window.ParseObjectNames(objectNodes, config.selectedObject);
            window.titleContent = new GUIContent("Object Selector");
            window.ShowUtility();
        }

        private void ParseObjectNames(XmlNodeList nodes, string selected)
        {
            objectNames.Clear();
            selection.Clear();

            HashSet<string> selectedSet = new HashSet<string>();
            if (!string.IsNullOrEmpty(selected))
            {
                foreach (string s in selected.Split(','))
                    selectedSet.Add(s.Trim());
            }

            foreach (XmlNode node in nodes)
            {
                if (node is XmlElement element)
                {
                    string name = element.GetAttribute("Name");
                    if (!string.IsNullOrEmpty(name))
                    {
                        objectNames.Add(name);
                        selection[name] = string.IsNullOrEmpty(selected)
                                         ? true     // empty selectedObject == everything selected
                                         : selectedSet.Contains(name);
                    }
                }
            }
        }

        private void OnGUI()
        {
            string expectedKey = ScrollYKeyPrefix + config.xmlName;
            if (scrollKey != expectedKey)
            {
                scrollKey = expectedKey;
                scroll = Vector2.zero;
            }

            EditorGUILayout.LabelField("Object Name List", EditorStyles.boldLabel);
            search = EditorGUILayout.TextField("Search", search);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All"))
            {
                foreach (var key in objectNames)
                    selection[key] = true;
            }
            if (GUILayout.Button("Unselect All"))
            {
                foreach (var key in objectNames)
                    selection[key] = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (string name in objectNames)
            {
                if (!string.IsNullOrEmpty(search) &&
                    !name.ToLower().Contains(search.ToLower()))
                    continue;

                selection[name] = EditorGUILayout.ToggleLeft(name, selection[name]);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            if (GUILayout.Button("Apply", GUILayout.Height(30)))
            {
                ApplySelection();
                Close();
            }
        }

        private void ApplySelection()
        {
            List<string> selected = new List<string>();

            foreach (var kv in selection)
                if (kv.Value)
                    selected.Add(kv.Key);

            // empty string means all selected
            if (selected.Count == objectNames.Count)
                config.selectedObject = "";
            else
                config.selectedObject = string.Join(",", selected);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(scrollKey))
            {
                EditorPrefs.SetFloat(scrollKey, scroll.y);
            }
        }
    }
}
