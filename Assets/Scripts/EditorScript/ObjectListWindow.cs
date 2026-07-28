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

        // Tracks the last toggled index for Shift-Click functionality
        private int lastToggledIndex = -1;

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
            lastToggledIndex = -1; // Reset on parse

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
            
            // Reset the last toggled index if the user alters the search bar
            EditorGUI.BeginChangeCheck();
            search = EditorGUILayout.TextField("Search", search);
            if (EditorGUI.EndChangeCheck())
            {
                lastToggledIndex = -1; 
            }

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

            // Build a list of currently visible names based on the search
            List<string> visibleNames = new List<string>();
            foreach (string name in objectNames)
            {
                if (!string.IsNullOrEmpty(search) &&
                    !name.ToLower().Contains(search.ToLower()))
                    continue;
                
                visibleNames.Add(name);
            }

            // Render the toggles and handle the shift click
            bool isShift = Event.current.shift;

            for (int i = 0; i < visibleNames.Count; i++)
            {
                string name = visibleNames[i];

                EditorGUI.BeginChangeCheck();
                bool newState = EditorGUILayout.ToggleLeft(name, selection[name]);
                
                if (EditorGUI.EndChangeCheck())
                {
                    if (isShift && lastToggledIndex >= 0)
                    {
                        // Toggle everything between the last clicked item and this item
                        int start = Mathf.Min(lastToggledIndex, i);
                        int end = Mathf.Max(lastToggledIndex, i);
                        
                        for (int j = start; j <= end; j++)
                        {
                            selection[visibleNames[j]] = newState;
                        }
                    }
                    else
                    {
                        // Standard single click
                        selection[name] = newState;
                    }
                    
                    lastToggledIndex = i;
                }
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