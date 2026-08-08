using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Globalization;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerLocations : EditorWindow
    {
        public enum SubjectType
        {
            None,
            Track,
            Location
        }

        private class ModeData
        {
            public string rawPriceInput = "0";
            public int unlockPrice = 0;
            public string rawStarsInput = "0";
            public int starsRequired = 0;
            
            public SubjectType subject = SubjectType.None;
            public string subjectName = "";
        }

        private class LocationItem
        {
            public string locationName = "";
            public string thumbnailPath = ""; // Added to track file path
            public Texture2D thumbnail;
            
            // Holds the separate settings for each mode
            public ModeData classic = new ModeData();
            public ModeData hunter = new ModeData();
        }

        private string activeProjectName = "";
        private List<LocationItem> locationList = new List<LocationItem>();
        private int selectedIndex = -1;
        private Vector2 scrollPosition;
        private bool isSwitchingToLevels = false;
        private List<string> availableTracks = new List<string>();
        private List<string> availableLocations = new List<string>();

        // Opens this window and accepts the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerLocations window = GetWindow<ProjectManagerLocations>("Locations");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(700, 700); 
            window.Show();
            window.Init();
        }

        private void Init()
        {
            LoadLocationsFromXml();
            ParseAvailableSubjects();
        }

        private void OnGUI()
        {
            // Catch background clicks to clear focus and trigger delayed inputs
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
                Repaint();
            }

            GUILayout.Space(10);

            // --- Header & Action Buttons ---
            GUILayout.BeginHorizontal();
            GUILayout.Label("Locations", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(30)))
            {
                GUI.FocusControl(null);
                AddLocation();
            }
            
            GUI.enabled = selectedIndex >= 0 && selectedIndex < locationList.Count;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(30)))
            {
                GUI.FocusControl(null);
                RemoveLocation();
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // --- Location List ---
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            if (locationList.Count == 0)
            {
                GUILayout.Label("No locations added yet. Click '+' to create one.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < locationList.Count; i++)
                {
                    DrawLocationItem(i);
                }
            }

            GUILayout.EndScrollView();
        }

        private void OnDestroy()
        {
            // Reopen the selection window with the current project name
            if (!isSwitchingToLevels && !string.IsNullOrEmpty(activeProjectName))
            {
                ProjectManagerSelection.ShowWindow(activeProjectName);
            }
        }

        private void DrawLocationItem(int index)
        {
            LocationItem item = locationList[index];
            bool isSelected = (selectedIndex == index);

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            if (isSelected) boxStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.5f, 0.9f, 0.5f));

            GUILayout.BeginVertical(boxStyle);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Location {index + 1}", EditorStyles.toolbarButton))
            {
                selectedIndex = index;
                GUI.FocusControl(null);
            }

            // --- Move Up/Down Buttons ---
            GUILayout.FlexibleSpace();
            
            GUI.enabled = index > 0;
            if (GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                MoveLocation(index, -1);
            }
            
            GUI.enabled = index < locationList.Count - 1;
            if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                MoveLocation(index, 1);
            }
            GUI.enabled = true;
            // ----------------------------
            
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Thumbnail Image Preview
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            float imgWidth = 665f;
            float imgHeight = 256f;
            float availableWidth = position.width - 40; 
            
            if (availableWidth < imgWidth)
            {
                float scale = availableWidth / imgWidth;
                imgWidth *= scale;
                imgHeight *= scale;
            }

            Rect imageRect = GUILayoutUtility.GetRect(imgWidth, imgHeight, GUILayout.Width(imgWidth), GUILayout.Height(imgHeight));
            
            // --- THUMBNAIL LOGIC ---
            if (item.thumbnail != null)
            {
                // Draw actual image scaled to fit correctly
                GUI.DrawTexture(imageRect, item.thumbnail, ScaleMode.ScaleToFit);
            }
            else
            {
                // Draw white texture stretched to fill the entire expected bounds
                GUI.DrawTexture(imageRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill);

                GUIStyle centeredTextStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.black } // Dark text so it shows up well on the white texture
                };
                GUI.Label(imageRect, "Click Here to Change Thumbnail", centeredTextStyle);
            }

            // Invisible button covering the entire image area
            if (GUI.Button(imageRect, new GUIContent("", "Click to assign thumbnail"), GUIStyle.none))
            {
                SelectAndImportImage(item);
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Location Name
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.DelayedTextField("Location Name", item.locationName);
            if (EditorGUI.EndChangeCheck())
            {
                string formatted = FormatLocationName(newName);
                if (formatted != item.locationName)
                {
                    bool isDuplicate = locationList.Any(l => l.locationName == formatted);
                    if (isDuplicate)
                    {
                        Debug.LogWarning($"A location named '{formatted}' already exists. Reverting name change.");
                        GUI.FocusControl(null); 
                    }
                    else
                    {
                        RenameLocationImage(item, formatted); 
                        item.locationName = formatted;
                        SaveToXml();
                    }
                }
            }

            // --- Modes Section ---
            GUILayout.Space(5);
            DrawModeSettings("Classic Mode", item.classic);
            GUILayout.Space(5);
            DrawModeSettings("Hunter Mode", item.hunter);

            GUILayout.Space(10);

            // Edit Levels Button
            if (GUILayout.Button("Edit Levels", GUILayout.Height(25)))
            {
                // Set the flag so OnDestroy knows NOT to open the selection window
                isSwitchingToLevels = true;
                
                // Close location editor and swap to level editor
                ProjectManagerLevels.ShowWindow(activeProjectName, item.locationName, index + 1);
                Close();
            }

            GUILayout.Space(5);
            GUILayout.EndVertical();
        }

        private void DrawModeSettings(string title, ModeData data)
        {
            GUILayout.Space(5);
            GUILayout.Label(title, EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            data.rawPriceInput = EditorGUILayout.DelayedTextField("Unlock Price", data.rawPriceInput);
            if (EditorGUI.EndChangeCheck())
            {
                data.unlockPrice = ParseInt(data.rawPriceInput);
                data.rawPriceInput = data.unlockPrice.ToString();
                SaveToXml();
            }

            EditorGUI.BeginChangeCheck();
            data.rawStarsInput = EditorGUILayout.DelayedTextField("Stars Required", data.rawStarsInput);
            if (EditorGUI.EndChangeCheck())
            {
                data.starsRequired = ParseInt(data.rawStarsInput);
                
                if (data.subject == SubjectType.Track && data.starsRequired > 3)
                {
                    data.starsRequired = 3;
                }
                
                data.rawStarsInput = data.starsRequired.ToString();
                SaveToXml();
            }

            if (data.starsRequired > 0)
            {
                EditorGUI.BeginChangeCheck();
                data.subject = (SubjectType)EditorGUILayout.EnumPopup("Subject", data.subject);
                if (EditorGUI.EndChangeCheck())
                {
                    if (data.subject == SubjectType.Track && data.starsRequired > 3)
                    {
                        data.starsRequired = 3;
                        data.rawStarsInput = data.starsRequired.ToString();
                    }
                    
                    SaveToXml();
                }

                if (data.subject != SubjectType.None)
                {
                    EditorGUI.BeginChangeCheck();

                    if (data.subject == SubjectType.Track)
                    {
                        if (availableTracks.Count > 0)
                        {
                            int currentIndex = Mathf.Max(0, availableTracks.IndexOf(data.subjectName));
                            int newIndex = EditorGUILayout.Popup("Subject Name", currentIndex, availableTracks.ToArray());
                            data.subjectName = availableTracks[newIndex];
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Subject Name", "No tracks found.");
                            data.subjectName = "";
                        }
                    }
                    else if (data.subject == SubjectType.Location)
                    {
                        if (availableLocations.Count > 0)
                        {
                            int currentIndex = Mathf.Max(0, availableLocations.IndexOf(data.subjectName));
                            int newIndex = EditorGUILayout.Popup("Subject Name", currentIndex, availableLocations.ToArray());
                            data.subjectName = availableLocations[newIndex];
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Subject Name", "No locations found.");
                            data.subjectName = "";
                        }
                    }

                    if (EditorGUI.EndChangeCheck()) SaveToXml();
                }
            }
        }

        private void AddLocation()
        {
            LocationItem newItem = new LocationItem();
            
            // Determine unique default name
            string baseName = "NEW_LOCATION";
            string assignedName = baseName;
            int counter = 1;
            
            while (locationList.Any(l => l.locationName == assignedName))
            {
                assignedName = $"{baseName}_{counter}";
                counter++;
            }

            newItem.locationName = assignedName;
            // Note: ModeData properties initialize to 0/"0"/None automatically by their class definition

            locationList.Add(newItem);
            selectedIndex = locationList.Count - 1;
            SaveToXml();
        }

        private void RemoveLocation()
        {
            if (selectedIndex < 0 || selectedIndex >= locationList.Count) return;

            LocationItem item = locationList[selectedIndex];

            // Delete Location Image
            if (!string.IsNullOrEmpty(item.thumbnailPath) && File.Exists(item.thumbnailPath))
            {
                File.Delete(item.thumbnailPath);
            }

            // Delete all related level folders and their contents (XMLs and Icons)
            string levelsXmlFolder = $"./Projects/{activeProjectName}/xmlroot/levels/{item.locationName}";
            if (Directory.Exists(levelsXmlFolder))
            {
                Directory.Delete(levelsXmlFolder, true);
            }

            string levelsIconsFolder = $"./Projects/{activeProjectName}/icons/levels/{item.locationName}";
            if (Directory.Exists(levelsIconsFolder))
            {
                Directory.Delete(levelsIconsFolder, true);
            }

            // Remove the location from the local list
            locationList.RemoveAt(selectedIndex);
            selectedIndex = -1;
            
            // Save changes (this inherently drops the location and its nested levels from List_Payed.xml)
            SaveToXml();
        }

        private void MoveLocation(int index, int direction)
        {
            if (index < 0 || index >= locationList.Count) return;
            
            int targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= locationList.Count) return;

            // Swap the items
            LocationItem temp = locationList[index];
            locationList[index] = locationList[targetIndex];
            locationList[targetIndex] = temp;

            // Keep the selected index aligned with the item that moved
            if (selectedIndex == index) selectedIndex = targetIndex;
            else if (selectedIndex == targetIndex) selectedIndex = index;

            GUI.FocusControl(null); // Clear any active text fields
            SaveToXml();
        }

        private void SelectAndImportImage(LocationItem item)
        {
            string sourcePath = EditorUtility.OpenFilePanelWithFilters("Select Location Image (1329x512)", "", new string[] { "Image Files", "png,jpg,jpeg", "All files", "*" });
            
            if (!string.IsNullOrEmpty(sourcePath))
            {
                EnsureDirectories();
                
                string targetDir = $"./Projects/{activeProjectName}/icons/locations";
                string newImageName = item.locationName;
                
                string extension = Path.GetExtension(sourcePath).ToLower();
                string targetPath = $"{targetDir}/{newImageName}{extension}";

                // Delete old image if it exists
                if (!string.IsNullOrEmpty(item.thumbnailPath) && File.Exists(item.thumbnailPath))
                {
                    File.Delete(item.thumbnailPath);
                }

                File.Copy(sourcePath, targetPath, true);
                
                // Manually load the image into a Texture2D to preview in Editor
                item.thumbnailPath = targetPath;
                item.thumbnail = new Texture2D(2, 2);
                item.thumbnail.LoadImage(File.ReadAllBytes(targetPath));

                SaveToXml();
            }
        }

        private void RenameLocationImage(LocationItem item, string newName)
        {
            if (!string.IsNullOrEmpty(item.thumbnailPath) && File.Exists(item.thumbnailPath))
            {
                string dir = Path.GetDirectoryName(item.thumbnailPath);
                string ext = Path.GetExtension(item.thumbnailPath);
                string newPath = Path.Combine(dir, newName + ext).Replace("\\", "/");
                
                if (item.thumbnailPath != newPath)
                {
                    File.Move(item.thumbnailPath, newPath);
                    item.thumbnailPath = newPath;
                }
            }
        }

        // --- Formatting & Parsing ---

        private string FormatLocationName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "NEW_LOCATION";
            return input.ToUpper().Trim().Replace(" ", "_");
        }

        private int ParseInt(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            string clean = input.Replace(",", "");
            if (float.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedVal))
            {
                return Mathf.RoundToInt(parsedVal);
            }
            return 0;
        }

        // --- File & XML Management ---

        private void EnsureDirectories()
        {
            CreateFolderRecursive($"./Projects/{activeProjectName}/icons/locations");
            CreateFolderRecursive($"./Projects/{activeProjectName}/commons");
        }

        private XElement CreateLocationNode(string name, ModeData data)
        {
            XElement locElement = new XElement("Location", new XAttribute("Name", name));

            if (data.starsRequired == 0)
            {
                if (data.unlockPrice > 0)
                {
                    XElement condNode = new XElement("Conditions");
                    condNode.Add(new XElement("Payment", new XAttribute("Required", data.unlockPrice.ToString())));
                    locElement.Add(condNode);
                }
            }
            else
            {
                if (data.unlockPrice > 0)
                {
                    locElement.Add(new XAttribute("UnlockPrice", data.unlockPrice.ToString()));
                }

                XElement condNode = new XElement("Conditions");
                XElement starsNode = new XElement("Stars", new XAttribute("Required", data.starsRequired.ToString()));

                if (data.subject != SubjectType.None)
                {
                    starsNode.Add(new XAttribute("Subject", data.subject.ToString()));
                    if (!string.IsNullOrEmpty(data.subjectName))
                    {
                        starsNode.Add(new XAttribute("Name", data.subjectName));
                    }
                }

                condNode.Add(starsNode);
                locElement.Add(condNode);
            }

            return locElement;
        }

        private void CreateFolderRecursive(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private void SaveToXml()
        {
            EnsureDirectories();
            string relativePath = $"./Projects/{activeProjectName}/commons/List_Payed.xml";
            
            XDocument doc;
            if (File.Exists(relativePath)) doc = XDocument.Load(relativePath);
            else doc = new XDocument(new XElement("LocationList"));

            XElement root = doc.Root;
            if (root == null)
            {
                root = new XElement("LocationList");
                doc.Add(root);
            }

            XElement locationsNode = root.Element("Locations");
            if (locationsNode == null)
            {
                locationsNode = new XElement("Locations");
                root.Add(locationsNode);
            }

            // Cache existing content inside Locations (like Groups) so they aren't overwritten
            Dictionary<string, List<XElement>> existingContent = new Dictionary<string, List<XElement>>();
            foreach (XElement loc in locationsNode.Elements("Location"))
            {
                string locName = (string)loc.Attribute("Name");
                if (!string.IsNullOrEmpty(locName))
                {
                    // Keep all child nodes EXCEPT "Conditions", since CreateLocationNode regenerates Conditions
                    existingContent[locName] = loc.Elements().Where(e => e.Name != "Conditions").ToList();
                }
            }

            locationsNode.RemoveNodes();

            // Loop and add ALL Classic Nodes first
            foreach (LocationItem item in locationList)
            {
                XElement newLocNode = CreateLocationNode(item.locationName, item.classic);
                if (existingContent.TryGetValue(item.locationName, out var savedElements))
                {
                    newLocNode.Add(savedElements);
                }
                locationsNode.Add(newLocNode);
            }

            // Loop and add ALL Hunter Nodes at the bottom
            foreach (LocationItem item in locationList)
            {
                string hunterName = item.locationName + "_HUNTER";
                XElement newLocNode = CreateLocationNode(hunterName, item.hunter);
                if (existingContent.TryGetValue(hunterName, out var savedElements))
                {
                    newLocNode.Add(savedElements);
                }
                locationsNode.Add(newLocNode);
            }

            doc.Save(relativePath);
        }

        private void LoadLocationsFromXml()
        {
            locationList.Clear();
            string relativePath = $"./Projects/{activeProjectName}/commons/List_Payed.xml";

            if (!File.Exists(relativePath)) return;

            XDocument doc = XDocument.Load(relativePath);
            XElement locationsNode = doc.Root?.Element("Locations");
            if (locationsNode == null) return;

            // Use dictionary for linking data, and a list to enforce the order
            Dictionary<string, LocationItem> locationDict = new Dictionary<string, LocationItem>();
            List<LocationItem> orderedList = new List<LocationItem>();

            foreach (XElement node in locationsNode.Elements("Location"))
            {
                string rawName = (string)node.Attribute("Name") ?? "";
                if (string.IsNullOrEmpty(rawName)) continue;

                bool isHunter = rawName.EndsWith("_HUNTER");
                string baseName = isHunter ? rawName.Substring(0, rawName.Length - 7) : rawName;

                if (!locationDict.TryGetValue(baseName, out LocationItem item))
                {
                    item = new LocationItem { locationName = baseName };
                    locationDict[baseName] = item;
                    orderedList.Add(item);
                    
                    // Link Image using the base name
                    string locDir = $"./Projects/{activeProjectName}/icons/locations";
                    if (Directory.Exists(locDir))
                    {
                        string[] possibleExts = { ".png", ".jpg", ".jpeg" };
                        foreach (string ext in possibleExts)
                        {
                            string imgPath = Path.Combine(locDir, baseName + ext).Replace("\\", "/");
                            if (File.Exists(imgPath))
                            {
                                item.thumbnailPath = imgPath;
                                item.thumbnail = new Texture2D(2, 2);
                                item.thumbnail.LoadImage(File.ReadAllBytes(imgPath));
                                break;
                            }
                        }
                    }
                }

                ModeData targetData = isHunter ? item.hunter : item.classic;

                XAttribute locUnlockPriceAttr = node.Attribute("UnlockPrice");
                if (locUnlockPriceAttr != null)
                {
                    targetData.unlockPrice = ParseInt((string)locUnlockPriceAttr);
                    targetData.rawPriceInput = targetData.unlockPrice.ToString();
                }

                XElement conditions = node.Element("Conditions");
                if (conditions != null)
                {
                    XElement payment = conditions.Element("Payment");
                    if (payment != null)
                    {
                        targetData.unlockPrice = ParseInt((string)payment.Attribute("Required"));
                        targetData.rawPriceInput = targetData.unlockPrice.ToString();
                    }

                    XElement stars = conditions.Element("Stars");
                    if (stars != null)
                    {
                        targetData.starsRequired = ParseInt((string)stars.Attribute("Required"));
                        targetData.rawStarsInput = targetData.starsRequired.ToString();

                        string subjectStr = (string)stars.Attribute("Subject");
                        if (!string.IsNullOrEmpty(subjectStr) && System.Enum.TryParse(subjectStr, out SubjectType parsedSubject))
                        {
                            targetData.subject = parsedSubject;
                        }

                        targetData.subjectName = (string)stars.Attribute("Name") ?? "";
                    }
                }
            }

            // Assign from our rigorously ordered list to preserve sorting
            locationList = orderedList;
        }

        private void ParseAvailableSubjects()
        {
            availableTracks.Clear();
            availableLocations.Clear();

            string path = $"./Projects/{activeProjectName}/commons/List_Payed.xml";
            if (!File.Exists(path)) return;

            XDocument doc = XDocument.Load(path);
            XElement locationsNode = doc.Root?.Element("Locations");
            if (locationsNode == null) return;

            foreach (XElement locNode in locationsNode.Elements("Location"))
            {
                string locName = (string)locNode.Attribute("Name") ?? "";
                if (string.IsNullOrEmpty(locName) || locName.EndsWith("_HUNTER")) continue;
                
                if (!availableLocations.Contains(locName))
                {
                    availableLocations.Add(locName);
                }

                XElement groupsNode = locNode.Element("Groups");
                if (groupsNode != null)
                {
                    foreach (XElement groupNode in groupsNode.Elements("Group"))
                    {
                        foreach (XElement trackNode in groupNode.Elements("Track"))
                        {
                            string trackName = (string)trackNode.Attribute("Name") ?? "";
                            if (!string.IsNullOrEmpty(trackName) && !trackName.EndsWith("_HUNTER"))
                            {
                                if (!availableTracks.Contains(trackName))
                                {
                                    availableTracks.Add(trackName);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Helper to generate solid color texture for selection highlighting
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}