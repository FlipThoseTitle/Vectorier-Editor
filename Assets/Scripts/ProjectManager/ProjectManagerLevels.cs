using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Globalization;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerLevels : EditorWindow
    {
        public enum SubjectType
        {
            None,
            Track,
            Location
        }

        private enum ViewState
        {
            ModeSelect,
            LevelList,
            TemplateEdit
        }

        private class LevelItem
        {
            public string internalName = "";
            public string displayName = "";
            public string thumbnailPath = ""; // Track external thumbnail path
            public Texture2D thumbnail;
            public string xmlPath = ""; // Track external XML path

            public ModeData story = new ModeData();
            public ModeData hunter = new ModeData();
        }

        private class ModeData
        {
            public string rawPriceInput = "0";
            public int unlockPrice = 0;
            public string rawStarsInput = "0";
            public int starsRequired = 0;

            public SubjectType subject = SubjectType.None;
            public string subjectName = "";

            public List<string> tricks = new List<string>();

            public string starsTemplate = "";
            public string rewardTemplate = "";
        }

        private class StarsTemplate
        {
            public string name = "New_Stars_Template";
            public bool completed = true;
            public bool bonuses = true;
            public bool tricks = true;
        }

        private class RewardTemplate
        {
            public string name = "New_Reward_Template";
            public int coins1 = 100;
            public int coins2 = 150;
            public int coins3 = 250;
        }

        private string activeProjectName = "";
        private string activeLocationName = "";
        private int locationOrder = 1;
        private string activeMode = "";
        private ViewState currentState = ViewState.ModeSelect;

        private List<LevelItem> levelList = new List<LevelItem>();
        private List<string> availableTracks = new List<string>();
        private List<string> availableLocations = new List<string>();
        private List<string> availableTricksFromShop = new List<string>();
        private List<StarsTemplate> starsTemplates = new List<StarsTemplate>();
        private List<RewardTemplate> rewardTemplates = new List<RewardTemplate>();

        private int selectedIndex = -1;
        private Vector2 scrollPosition;
        private Vector2 templateScrollPos;

        private static readonly string[] LOCALIZATION_LANGS =
        {
            "eng", "rus", "ger", "ita", "fre", "spa", "tur", "por", "jap", "kor",
            "chi1", "chi2", "viet", "hin", "arab", "heb", "thai", "pol", "cze",
            "lat", "dut", "nor", "dan", "finn", "swe", "ukr", "gre"
        };

        public static void ShowWindow(string projectName, string locationName, int locOrder)
        {
            ProjectManagerLevels window = GetWindow<ProjectManagerLevels>("Levels");
            window.activeProjectName = projectName;
            window.activeLocationName = locationName;
            window.locationOrder = locOrder;
            window.currentState = ViewState.ModeSelect;
            window.minSize = new Vector2(700, 700);
            window.Show();
            window.Init();
        }

        private void Init()
        {
            LoadTemplatesFromXml();
            LoadAvailableSubjects();
            LoadAvailableTricks();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
                Repaint();
            }

            GUILayout.Space(10);

            switch (currentState)
            {
                case ViewState.ModeSelect:
                    DrawModeSelect();
                    break;
                case ViewState.LevelList:
                    DrawLevelList();
                    break;
                case ViewState.TemplateEdit:
                    DrawTemplateEdit();
                    break;
            }
        }

        // --- VIEWS ---

        private void DrawModeSelect()
        {
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Select Mode for {activeLocationName}", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            if (GUILayout.Button("Story", GUILayout.Width(200), GUILayout.Height(80)))
            {
                SelectMode("STORY");
            }

            GUILayout.Space(20);

            if (GUILayout.Button("Bonus", GUILayout.Width(200), GUILayout.Height(80)))
            {
                SelectMode("BONUS");
            }
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back to Locations", GUILayout.Width(200), GUILayout.Height(30)))
            {
                ProjectManagerLocations.ShowWindow(activeProjectName);
                Close();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        private void DrawLevelList()
        {
            // Header
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back", GUILayout.Width(60), GUILayout.Height(25)))
            {
                SaveToXml();
                currentState = ViewState.ModeSelect;
                GUIUtility.ExitGUI();
            }

            GUILayout.Space(10);

            string properModeName = activeMode == "STORY" ? "Story" : "Bonus";
            GUILayout.Label($"{activeLocationName} - {properModeName}", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();

            GUIStyle countStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label($"Total Levels - {levelList.Count}", countStyle, GUILayout.Height(25));
            GUILayout.Space(10);

            if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(25))) AddLevel();

            // Enabled only if a specific level is selected
            GUI.enabled = selectedIndex >= 0 && selectedIndex < levelList.Count;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(25))) RemoveLevel();

            // Enabled as long as there is at least one level to clear
            GUI.enabled = levelList.Count > 0;
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(25))) ClearAllLevels();

            // Enabled only if a specific level is selected
            GUI.enabled = selectedIndex >= 0 && selectedIndex < levelList.Count;
            if (GUILayout.Button("^", GUILayout.Width(30), GUILayout.Height(25))) ReorderLevel(-1);
            if (GUILayout.Button("v", GUILayout.Width(30), GUILayout.Height(25))) ReorderLevel(1);
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // List
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            if (levelList.Count == 0)
            {
                GUILayout.Label($"No {properModeName} levels added yet. Click '+' to create one.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < levelList.Count; i++)
                {
                    DrawLevelItem(i);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawLevelItem(int index)
        {
            LevelItem item = levelList[index];
            bool isSelected = (selectedIndex == index);

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            if (isSelected) boxStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.5f, 0.9f, 0.5f));

            GUILayout.BeginVertical(boxStyle);

            if (GUILayout.Button(item.internalName, EditorStyles.toolbarButton))
            {
                selectedIndex = index;
                GUI.FocusControl(null);
            }

            GUILayout.Space(10);

            // Thumbnail
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            float imgWidth = 512f;
            float imgHeight = 340f;
            float availableWidth = position.width - 40;

            if (availableWidth < imgWidth)
            {
                float scale = availableWidth / imgWidth;
                imgWidth *= scale;
                imgHeight *= scale;
            }

            Rect imageRect = GUILayoutUtility.GetRect(imgWidth, imgHeight, GUILayout.Width(imgWidth), GUILayout.Height(imgHeight));

            if (item.thumbnail != null)
            {
                GUI.DrawTexture(imageRect, item.thumbnail, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.DrawTexture(imageRect, EditorGUIUtility.whiteTexture, ScaleMode.StretchToFill);

                GUIStyle centeredTextStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.black }
                };
                GUI.Label(imageRect, "Click Here to Change Thumbnail", centeredTextStyle);
            }

            if (GUI.Button(imageRect, new GUIContent("", "Click to assign thumbnail"), GUIStyle.none))
            {
                SelectAndImportImage(item);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.Label("Level XML Path", EditorStyles.boldLabel);
            item.xmlPath = EditorGUILayout.TextField("XML Path", item.xmlPath);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Browse"))
            {
                BrowseAndAssignXML(item);
            }
            if (GUILayout.Button("Export Scene"))
            {
                ExportSceneToXML(item);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Display Name
            EditorGUI.BeginChangeCheck();
            item.displayName = EditorGUILayout.DelayedTextField("Display Name", item.displayName);
            if (EditorGUI.EndChangeCheck())
            {
                SaveLocalization();
            }

            // --- Modes Section ---
            GUILayout.Space(10);
            DrawModeSettings("Story Mode", item.story);
            GUILayout.Space(10);
            DrawModeSettings("Hunter Mode", item.hunter);

            GUILayout.Space(10);
            GUILayout.EndVertical();
        }

        private void DrawModeSettings(string title, ModeData data)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(title, EditorStyles.boldLabel);
            GUILayout.Space(5);

            // Unlock Conditions
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
                        if (availableTracks != null && availableTracks.Count > 0)
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
                        if (availableLocations != null && availableLocations.Count > 0)
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

            GUILayout.Space(5);

            // Tricks
            GUILayout.Label("Tricks", EditorStyles.boldLabel);
            for (int t = 0; t < data.tricks.Count; t++)
            {
                GUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();

                if (availableTricksFromShop != null && availableTricksFromShop.Count > 0)
                {
                    int currentIndex = Mathf.Max(0, availableTricksFromShop.IndexOf(data.tricks[t]));
                    int newIndex = EditorGUILayout.Popup(currentIndex, availableTricksFromShop.ToArray());
                    data.tricks[t] = availableTricksFromShop[newIndex];
                }
                else
                {
                    // Fallback to text field just in case Shop_payed.xml fails to load
                    data.tricks[t] = EditorGUILayout.TextField(data.tricks[t]);
                }

                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    data.tricks.RemoveAt(t);
                    SaveToXml();
                    GUIUtility.ExitGUI();
                }
                if (EditorGUI.EndChangeCheck()) SaveToXml();
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Trick", GUILayout.Width(100)))
            {
                if (availableTricksFromShop != null && availableTricksFromShop.Count > 0)
                {
                    // Assign the first valid trick on the list instead of "TRICK_NEW"
                    data.tricks.Add(availableTricksFromShop[0]);
                }
                else
                {
                    data.tricks.Add("TRICK_NEW");
                }
                SaveToXml();
            }

            GUILayout.Space(10);

            // Templates
            string[] starsNames = starsTemplates.Select(t => t.name).ToArray();
            string[] rewardNames = rewardTemplates.Select(t => t.name).ToArray();

            int sIndex = Mathf.Max(0, System.Array.IndexOf(starsNames, data.starsTemplate));
            int rIndex = Mathf.Max(0, System.Array.IndexOf(rewardNames, data.rewardTemplate));

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            EditorGUI.BeginChangeCheck();
            if (starsNames.Length > 0)
            {
                sIndex = EditorGUILayout.Popup("Stars Template", sIndex, starsNames);
                data.starsTemplate = starsNames[sIndex];
            }
            else EditorGUILayout.LabelField("Stars Template", "None Found");

            if (rewardNames.Length > 0)
            {
                rIndex = EditorGUILayout.Popup("Rewards Template", rIndex, rewardNames);
                data.rewardTemplate = rewardNames[rIndex];
            }
            else EditorGUILayout.LabelField("Rewards Template", "None Found");

            if (EditorGUI.EndChangeCheck()) SaveToXml();

            GUILayout.EndVertical();

            if (GUILayout.Button("Edit\nTemplates", GUILayout.Width(80), GUILayout.Height(40)))
            {
                currentState = ViewState.TemplateEdit;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawTemplateEdit()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back to Levels", GUILayout.Width(120), GUILayout.Height(25)))
            {
                SaveTemplatesToXml();
                currentState = ViewState.LevelList;
                GUIUtility.ExitGUI();
            }
            GUILayout.Label("Templates Editor", EditorStyles.largeLabel);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            templateScrollPos = GUILayout.BeginScrollView(templateScrollPos);

            GUILayout.BeginHorizontal();

            // Stars Templates
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(position.width / 2 - 15));
            GUILayout.Label("Stars Templates", EditorStyles.boldLabel);
            for (int i = 0; i < starsTemplates.Count; i++)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                starsTemplates[i].name = EditorGUILayout.TextField(starsTemplates[i].name);
                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    starsTemplates.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();
                starsTemplates[i].completed = EditorGUILayout.Toggle("Completed", starsTemplates[i].completed);
                starsTemplates[i].bonuses = EditorGUILayout.Toggle("Bonuses", starsTemplates[i].bonuses);
                starsTemplates[i].tricks = EditorGUILayout.Toggle("Tricks", starsTemplates[i].tricks);
                GUILayout.EndVertical();
            }
            if (GUILayout.Button("+ Add Stars Template")) starsTemplates.Add(new StarsTemplate());
            GUILayout.EndVertical();

            // Rewards Templates
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(position.width / 2 - 15));
            GUILayout.Label("Rewards Templates", EditorStyles.boldLabel);
            for (int i = 0; i < rewardTemplates.Count; i++)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                rewardTemplates[i].name = EditorGUILayout.TextField(rewardTemplates[i].name);
                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    rewardTemplates.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();
                rewardTemplates[i].coins1 = EditorGUILayout.IntField("1 Star Coins", rewardTemplates[i].coins1);
                rewardTemplates[i].coins2 = EditorGUILayout.IntField("2 Star Coins", rewardTemplates[i].coins2);
                rewardTemplates[i].coins3 = EditorGUILayout.IntField("3 Star Coins", rewardTemplates[i].coins3);
                GUILayout.EndVertical();
            }
            if (GUILayout.Button("+ Add Reward Template")) rewardTemplates.Add(new RewardTemplate());
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        // --- ACTIONS ---

        private void SelectMode(string mode)
        {
            activeMode = mode;
            currentState = ViewState.LevelList;
            LoadFromXml();
        }

        private void AddLevel()
        {
            LevelItem newItem = new LevelItem();
            levelList.Add(newItem);
            selectedIndex = levelList.Count - 1;

            RefreshLevelNamesAndOrders();
        }

        private void RemoveLevel()
        {
            if (selectedIndex < 0 || selectedIndex >= levelList.Count) return;

            LevelItem item = levelList[selectedIndex];

            if (!string.IsNullOrEmpty(item.thumbnailPath) && File.Exists(item.thumbnailPath))
            {
                File.Delete(item.thumbnailPath);
            }

            if (!string.IsNullOrEmpty(item.xmlPath) && File.Exists(item.xmlPath))
            {
                File.Delete(item.xmlPath);
            }
            else
            {
                string fallbackXmlPath = $"./Projects/{activeProjectName}/xmlroot/levels/{item.internalName}.xml";
                if (File.Exists(fallbackXmlPath)) File.Delete(fallbackXmlPath);
            }

            // Remove from localization
            string locPath = $"./Projects/{activeProjectName}/localization/localization_all.xml";
            if (File.Exists(locPath))
            {
                XDocument doc = XDocument.Load(locPath);
                XElement node = doc.Root?.Element($"item_{item.internalName}");
                if (node != null)
                {
                    node.Remove();
                    doc.Save(locPath);
                }
            }

            levelList.RemoveAt(selectedIndex);
            selectedIndex = -1;
            RefreshLevelNamesAndOrders();
        }

        private void ClearAllLevels()
        {
            if (levelList.Count == 0) return;

            string properModeName = activeMode == "STORY" ? "Story" : "Bonus";
            bool confirm = EditorUtility.DisplayDialog(
                "Clear All Levels",
                $"Are you sure you want to delete ALL {levelList.Count} {properModeName} levels? This action cannot be undone.",
                "Yes, Clear All",
                "Cancel"
            );

            if (!confirm) return;

            string locPath = $"./Projects/{activeProjectName}/localization/localization_all.xml";
            XDocument doc = null;
            if (File.Exists(locPath))
            {
                doc = XDocument.Load(locPath);
            }

            for (int i = levelList.Count - 1; i >= 0; i--)
            {
                LevelItem item = levelList[i];

                if (!string.IsNullOrEmpty(item.thumbnailPath) && File.Exists(item.thumbnailPath))
                {
                    File.Delete(item.thumbnailPath);
                }

                if (!string.IsNullOrEmpty(item.xmlPath) && File.Exists(item.xmlPath))
                {
                    File.Delete(item.xmlPath);
                }
                else
                {
                    string xmlPath = $"./Projects/{activeProjectName}/xmlroot/levels/{item.internalName}.xml";
                    if (File.Exists(xmlPath)) File.Delete(xmlPath);
                }

                if (doc != null)
                {
                    XElement node = doc.Root?.Element($"item_{item.internalName}");
                    if (node != null) node.Remove();
                }
            }

            if (doc != null) doc.Save(locPath);

            levelList.Clear();
            selectedIndex = -1;

            SaveToXml();
            GUIUtility.ExitGUI();
        }

        private void ReorderLevel(int dir)
        {
            if (selectedIndex < 0 || selectedIndex >= levelList.Count) return;
            int newIndex = selectedIndex + dir;
            if (newIndex < 0 || newIndex >= levelList.Count) return;

            LevelItem item = levelList[selectedIndex];
            levelList.RemoveAt(selectedIndex);
            levelList.Insert(newIndex, item);
            selectedIndex = newIndex;

            RefreshLevelNamesAndOrders();
        }

        private void RefreshLevelNamesAndOrders()
        {
            EnsureDirectories();
            string properModeName = activeMode == "STORY" ? "Story" : "Bonus";
            bool xmlChanged = false;

            for (int i = 0; i < levelList.Count; i++)
            {
                LevelItem item = levelList[i];
                string expectedInternalName = $"{activeLocationName}_{activeMode}_{(i + 1):D2}";
                string expectedDisplayName = $"{properModeName} {locationOrder}-{i + 1}";

                if (string.IsNullOrEmpty(item.internalName))
                {
                    item.internalName = expectedInternalName;
                    item.displayName = expectedDisplayName;
                    xmlChanged = true;
                }
                else if (item.internalName != expectedInternalName)
                {
                    string oldName = item.internalName;
                    item.internalName = expectedInternalName;

                    // Rename Image via System.IO
                    if (!string.IsNullOrEmpty(item.thumbnailPath) && File.Exists(item.thumbnailPath))
                    {
                        string dir = Path.GetDirectoryName(item.thumbnailPath);
                        string ext = Path.GetExtension(item.thumbnailPath);
                        string newPath = Path.Combine(dir, expectedInternalName + ext).Replace("\\", "/");
                        if (item.thumbnailPath != newPath)
                        {
                            if (File.Exists(newPath)) File.Delete(newPath);
                            File.Move(item.thumbnailPath, newPath);
                            item.thumbnailPath = newPath;
                        }
                    }

                    // Rename XML via System.IO
                    string oldXmlPath = $"./Projects/{activeProjectName}/xmlroot/levels/{oldName}.xml";
                    string newXmlPath = $"./Projects/{activeProjectName}/xmlroot/levels/{expectedInternalName}.xml";
                    if (File.Exists(oldXmlPath))
                    {
                        if (File.Exists(newXmlPath)) File.Delete(newXmlPath);
                        File.Move(oldXmlPath, newXmlPath);
                        item.xmlPath = newXmlPath;
                    }
                    else if (!string.IsNullOrEmpty(item.xmlPath) && File.Exists(item.xmlPath))
                    {
                        if (File.Exists(newXmlPath)) File.Delete(newXmlPath);
                        File.Move(item.xmlPath, newXmlPath);
                        item.xmlPath = newXmlPath;
                    }

                    RenameLocalizationKey(oldName, expectedInternalName);
                    xmlChanged = true;
                }
            }

            if (xmlChanged)
            {
                SaveToXml();
                SaveLocalization();
            }
        }

        private void SelectAndImportImage(LevelItem item)
        {
            string sourcePath = EditorUtility.OpenFilePanelWithFilters("Select Level Image (512x340)", "", new string[] { "Image Files", "png,jpg,jpeg", "All files", "*" });

            if (!string.IsNullOrEmpty(sourcePath))
            {
                EnsureDirectories();
                string targetDir = $"./Projects/{activeProjectName}/icons/stories";
                string extension = Path.GetExtension(sourcePath).ToLower();
                string targetPath = $"{targetDir}/{item.internalName}{extension}";

                if (!string.IsNullOrEmpty(item.thumbnailPath) && File.Exists(item.thumbnailPath))
                {
                    File.Delete(item.thumbnailPath);
                }

                File.Copy(sourcePath, targetPath, true);

                item.thumbnailPath = targetPath;
                item.thumbnail = new Texture2D(2, 2);
                item.thumbnail.LoadImage(File.ReadAllBytes(targetPath));

                SaveToXml();
            }
        }

        // --- DATA MANAGEMENT ---

        private void EnsureDirectories()
        {
            CreateFolderRecursive($"./Projects/{activeProjectName}/icons/stories");
            CreateFolderRecursive($"./Projects/{activeProjectName}/commons");
            CreateFolderRecursive($"./Projects/{activeProjectName}/localization");
            CreateFolderRecursive($"./Projects/{activeProjectName}/xmlroot/levels");
        }

        private void CreateFolderRecursive(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private XElement CreateTrackNode(string trackName, ModeData data)
        {
            XElement trackNode = new XElement("Track", new XAttribute("Name", trackName));
            if (data.unlockPrice > 0) trackNode.Add(new XAttribute("UnlockPrice", data.unlockPrice));

            // Conditions
            if (data.starsRequired > 0 || data.unlockPrice > 0 && data.starsRequired == 0)
            {
                XElement condNode = new XElement("Conditions");
                if (data.starsRequired > 0)
                {
                    XElement starsNode = new XElement("Stars", new XAttribute("Required", data.starsRequired));
                    if (data.subject != SubjectType.None)
                    {
                        starsNode.Add(new XAttribute("Subject", data.subject.ToString()));
                        if (!string.IsNullOrEmpty(data.subjectName)) starsNode.Add(new XAttribute("Name", data.subjectName));
                    }
                    condNode.Add(starsNode);
                }
                else if (data.unlockPrice > 0)
                {
                    condNode.Add(new XElement("Payment", new XAttribute("Required", data.unlockPrice)));
                }
                trackNode.Add(condNode);
            }

            // Tricks
            if (data.tricks.Count > 0)
            {
                XElement tricksNode = new XElement("Tricks");
                foreach (string t in data.tricks) tricksNode.Add(new XElement("Trick", new XAttribute("Name", t)));
                trackNode.Add(tricksNode);
            }

            // Templates
            if (!string.IsNullOrEmpty(data.starsTemplate)) trackNode.Add(new XElement("Stars", new XAttribute("Template", data.starsTemplate)));
            if (!string.IsNullOrEmpty(data.rewardTemplate)) trackNode.Add(new XElement("Reward", new XAttribute("Template", data.rewardTemplate)));

            return trackNode;
        }

        private void BrowseAndAssignXML(LevelItem item)
{
    string sourcePath = EditorUtility.OpenFilePanel("Select Level XML", "", "xml");
    if (!string.IsNullOrEmpty(sourcePath))
    {
        string targetDir = $"./Projects/{activeProjectName}/xmlroot/levels";
        CreateFolderRecursive(targetDir);
        string targetPath = $"{targetDir}/{item.internalName}.xml";
        
        File.Copy(sourcePath, targetPath, true);
        item.xmlPath = targetPath;
        
        Repaint();
    }
}

        private void ExportSceneToXML(LevelItem item)
{
    string targetDir = $"./Projects/{activeProjectName}/xmlroot/levels";
    CreateFolderRecursive(targetDir);

    GameObject configObj = GameObject.Find("[EDITORONLY]ExportConfigHolder");
    if (configObj == null)
    {
        configObj = new GameObject("[EDITORONLY]ExportConfigHolder");
        configObj.hideFlags = HideFlags.HideInHierarchy;
    }
    
    if (!configObj.TryGetComponent(out Vectorier.Core.ExportConfig config))
    {
        config = configObj.AddComponent<Vectorier.Core.ExportConfig>();
    }

    config.exportType = Vectorier.Core.ExportConfig.ExportType.Level;
    config.filePathDirectory = targetDir;
    config.exportAsXML = true;
    config.fileName = item.internalName;

    EditorUtility.SetDirty(config);

    Vectorier.Core.Export exportWindow = GetWindow<Vectorier.Core.Export>("Export");
    var buildMethod = typeof(Vectorier.Core.Export).GetMethod("BuildLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var parallaxMethod = typeof(Vectorier.Core.Export).GetMethod("ExecuteWithParallaxDisabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    
    if (buildMethod != null && parallaxMethod != null)
    {
        System.Action action = () => buildMethod.Invoke(exportWindow, null);
        parallaxMethod.Invoke(exportWindow, new object[] { action });
    }
    else
    {
        Debug.LogError("[ProjectManager] Could not find Build methods in Export Window.");
        return;
    }

    string targetPath = $"{targetDir}/{item.internalName}.xml";
    item.xmlPath = targetPath;
    
    Repaint();
}

        private void SaveToXml()
        {
            EnsureDirectories();
            string path = $"./Projects/{activeProjectName}/commons/List_Payed.xml";
            
            XDocument doc;
            if (File.Exists(path)) doc = XDocument.Load(path);
            else doc = new XDocument(new XElement("LocationList", new XElement("Locations")));

            XElement root = doc.Root;
            XElement locationsNode = root.Element("Locations");
            if (locationsNode == null)
            {
                locationsNode = new XElement("Locations");
                root.AddFirst(locationsNode);
            }

            XElement baseLocNode = locationsNode.Elements("Location").FirstOrDefault(x => (string)x.Attribute("Name") == activeLocationName);
            if (baseLocNode == null)
            {
                baseLocNode = new XElement("Location", new XAttribute("Name", activeLocationName));
                locationsNode.Add(baseLocNode);
            }

            XElement baseGroupsNode = baseLocNode.Element("Groups");
            if (baseGroupsNode == null)
            {
                baseGroupsNode = new XElement("Groups");
                baseLocNode.Add(baseGroupsNode);
            }

            XElement baseModeGroup = baseGroupsNode.Elements("Group").FirstOrDefault(x => (string)x.Attribute("Name") == activeMode);
            if (baseModeGroup == null)
            {
                baseModeGroup = new XElement("Group", new XAttribute("Name", activeMode));
                baseGroupsNode.Add(baseModeGroup);
            }

            var baseNonTracks = baseModeGroup.Elements().Where(e => e.Name != "Track").ToList();
            baseModeGroup.RemoveNodes();
            baseModeGroup.Add(baseNonTracks);

            string hunterLocationName = activeLocationName + "_HUNTER";
            XElement hunterLocNode = locationsNode.Elements("Location").FirstOrDefault(x => (string)x.Attribute("Name") == hunterLocationName);
            if (hunterLocNode == null)
            {
                hunterLocNode = new XElement("Location", new XAttribute("Name", hunterLocationName));
                locationsNode.Add(hunterLocNode);
            }

            XElement hunterGroupsNode = hunterLocNode.Element("Groups");
            if (hunterGroupsNode == null)
            {
                hunterGroupsNode = new XElement("Groups");
                hunterLocNode.Add(hunterGroupsNode);
            }

            XElement hunterModeGroup = hunterGroupsNode.Elements("Group").FirstOrDefault(x => (string)x.Attribute("Name") == activeMode);
            if (hunterModeGroup == null)
            {
                hunterModeGroup = new XElement("Group", new XAttribute("Name", activeMode));
                hunterGroupsNode.Add(hunterModeGroup);
            }
            
            var hunterNonTracks = hunterModeGroup.Elements().Where(e => e.Name != "Track").ToList();
            hunterModeGroup.RemoveNodes();
            hunterModeGroup.Add(hunterNonTracks);

            foreach (LevelItem item in levelList)
            {
                baseModeGroup.Add(CreateTrackNode(item.internalName, item.story));
                string hunterTrackName = item.internalName + "_HUNTER";
                hunterModeGroup.Add(CreateTrackNode(hunterTrackName, item.hunter));
            }

            doc.Save(path);
        }

        private void LoadFromXml()
        {
            levelList.Clear();
            string path = $"./Projects/{activeProjectName}/commons/List_Payed.xml";
            if (!File.Exists(path)) return;

            XDocument doc = XDocument.Load(path);
            
            XElement baseLocNode = doc.Root?.Element("Locations")?.Elements("Location").FirstOrDefault(x => (string)x.Attribute("Name") == activeLocationName);
            XElement baseModeGroup = baseLocNode?.Element("Groups")?.Elements("Group").FirstOrDefault(x => (string)x.Attribute("Name") == activeMode);

            Dictionary<string, LevelItem> itemDict = new Dictionary<string, LevelItem>();

            if (baseModeGroup != null)
            {
                foreach (XElement track in baseModeGroup.Elements("Track"))
                {
                    string internalName = (string)track.Attribute("Name") ?? "";
                    if (string.IsNullOrEmpty(internalName)) continue;

                    LevelItem item = new LevelItem { internalName = internalName };
                    PopulateModeData(track, item.story);
                    
                    // Load Image via System.IO
                    string storiesDir = $"./Projects/{activeProjectName}/icons/stories";
                    if (Directory.Exists(storiesDir))
                    {
                        string[] possibleExts = { ".png", ".jpg", ".jpeg" };
                        foreach (string ext in possibleExts)
                        {
                            string imgPath = Path.Combine(storiesDir, internalName + ext).Replace("\\", "/");
                            if (File.Exists(imgPath))
                            {
                                item.thumbnailPath = imgPath;
                                item.thumbnail = new Texture2D(2, 2);
                                item.thumbnail.LoadImage(File.ReadAllBytes(imgPath));
                                break;
                            }
                        }
                    }

                    string xmlPath = $"./Projects/{activeProjectName}/xmlroot/levels/{internalName}.xml";
                    if (File.Exists(xmlPath))
                    {
                        item.xmlPath = xmlPath;
                    }

                    levelList.Add(item);
                    itemDict[internalName] = item;
                }
            }

            string hunterLocationName = activeLocationName + "_HUNTER";
            XElement hunterLocNode = doc.Root?.Element("Locations")?.Elements("Location").FirstOrDefault(x => (string)x.Attribute("Name") == hunterLocationName);
            XElement hunterModeGroup = hunterLocNode?.Element("Groups")?.Elements("Group").FirstOrDefault(x => (string)x.Attribute("Name") == activeMode);

            if (hunterModeGroup != null)
            {
                foreach (XElement track in hunterModeGroup.Elements("Track"))
                {
                    string hunterTrackName = (string)track.Attribute("Name") ?? "";
                    if (string.IsNullOrEmpty(hunterTrackName) || !hunterTrackName.EndsWith("_HUNTER")) continue;

                    string baseName = hunterTrackName.Substring(0, hunterTrackName.Length - 7);
                    
                    if (itemDict.TryGetValue(baseName, out LevelItem targetItem))
                    {
                        PopulateModeData(track, targetItem.hunter);
                    }
                }
            }

            LoadLocalization();
        }

        private void SaveTemplatesToXml()
        {
            EnsureDirectories();
            string path = $"./Projects/{activeProjectName}/commons/List_Payed.xml";
            
            XDocument doc;
            if (File.Exists(path)) doc = XDocument.Load(path);
            else doc = new XDocument(new XElement("LocationList", new XElement("Locations")));

            XElement root = doc.Root;
            XElement templatesNode = root.Element("Templates");
            if (templatesNode == null)
            {
                templatesNode = new XElement("Templates");
                root.Add(templatesNode);
            }

            var otherTemplates = templatesNode.Elements().Where(e => e.Name != "Stars" && e.Name != "Reward").ToList();
            templatesNode.RemoveNodes();
            templatesNode.Add(otherTemplates);

            foreach (var st in starsTemplates)
            {
                XElement sNode = new XElement("Stars", new XAttribute("Name", st.name));
                if (st.completed) sNode.Add(new XElement("Completed"));
                if (st.bonuses) sNode.Add(new XElement("Bonuses"));
                if (st.tricks) sNode.Add(new XElement("Tricks"));
                templatesNode.Add(sNode);
            }

            foreach (var rt in rewardTemplates)
            {
                XElement rNode = new XElement("Reward", new XAttribute("Name", rt.name));
                rNode.Add(new XElement("Stars", new XAttribute("Count", 1), new XAttribute("Coins", rt.coins1)));
                rNode.Add(new XElement("Stars", new XAttribute("Count", 2), new XAttribute("Coins", rt.coins2)));
                rNode.Add(new XElement("Stars", new XAttribute("Count", 3), new XAttribute("Coins", rt.coins3)));
                templatesNode.Add(rNode);
            }

            doc.Save(path);
        }

        private void LoadTemplatesFromXml()
        {
            starsTemplates.Clear();
            rewardTemplates.Clear();

            string path = $"./Projects/{activeProjectName}/commons/List_Payed.xml";
            if (!File.Exists(path)) return;

            XDocument doc = XDocument.Load(path);
            XElement templatesNode = doc.Root?.Element("Templates");
            if (templatesNode == null) return;

            foreach (XElement sNode in templatesNode.Elements("Stars"))
            {
                StarsTemplate st = new StarsTemplate { name = (string)sNode.Attribute("Name") ?? "Stars_Unknown" };
                st.completed = sNode.Element("Completed") != null;
                st.bonuses = sNode.Element("Bonuses") != null;
                st.tricks = sNode.Element("Tricks") != null;
                starsTemplates.Add(st);
            }

            foreach (XElement rNode in templatesNode.Elements("Reward"))
            {
                RewardTemplate rt = new RewardTemplate { name = (string)rNode.Attribute("Name") ?? "Reward_Unknown" };
                foreach (XElement sn in rNode.Elements("Stars"))
                {
                    int count = ParseInt((string)sn.Attribute("Count"));
                    int coins = ParseInt((string)sn.Attribute("Coins"));
                    if (count == 1) rt.coins1 = coins;
                    if (count == 2) rt.coins2 = coins;
                    if (count == 3) rt.coins3 = coins;
                }
                rewardTemplates.Add(rt);
            }
        }

        private void SaveLocalization()
        {
            EnsureDirectories();
            string locPath = $"./Projects/{activeProjectName}/localization/localization_all.xml";
            XDocument doc;
            
            if (File.Exists(locPath)) doc = XDocument.Load(locPath);
            else doc = new XDocument(new XElement("log"));

            foreach (LevelItem item in levelList)
            {
                string tag = $"item_{item.internalName}";
                XElement itemNode = doc.Root.Element(tag);
                
                if (itemNode == null)
                {
                    itemNode = new XElement(tag);
                    doc.Root.Add(itemNode);
                }
                
                foreach (string lang in LOCALIZATION_LANGS)
                {
                    itemNode.SetAttributeValue(lang, item.displayName);
                }
            }

            doc.Save(locPath);
        }

        private void LoadLocalization()
        {
            string locPath = $"./Projects/{activeProjectName}/localization/localization_all.xml";
            if (!File.Exists(locPath)) return;

            XDocument doc = XDocument.Load(locPath);
            foreach (LevelItem item in levelList)
            {
                XElement itemNode = doc.Root?.Element($"item_{item.internalName}");
                if (itemNode != null)
                {
                    string loadedName = (string)itemNode.Attribute("eng");
                    if (!string.IsNullOrEmpty(loadedName)) item.displayName = loadedName;
                }
            }
        }

        private void LoadAvailableSubjects()
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
                if (!string.IsNullOrEmpty(locName) && !locName.EndsWith("_HUNTER"))
                {
                    if (!availableLocations.Contains(locName))
                    {
                        availableLocations.Add(locName);
                    }
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

        private void LoadAvailableTricks()
        {
            availableTricksFromShop.Clear();
            string path = $"./Projects/{activeProjectName}/commons/Shop_payed.xml";
            
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ProjectManager] Could not find {path}. Tricks dropdown might be empty.");
                return;
            }

            try
            {
                XDocument doc = XDocument.Load(path);
                
                foreach (XElement item in doc.Descendants("Item"))
                {
                    string trickName = (string)item.Attribute("Name");
                    if (!string.IsNullOrEmpty(trickName) && !availableTricksFromShop.Contains(trickName))
                    {
                        availableTricksFromShop.Add(trickName);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ProjectManager] Error parsing Shop_payed.xml: {e.Message}");
            }
        }

        private void RenameLocalizationKey(string oldName, string newName)
        {
            string locPath = $"./Projects/{activeProjectName}/localization/localization_all.xml";
            if (!File.Exists(locPath)) return;

            XDocument doc = XDocument.Load(locPath);
            XElement oldNode = doc.Root?.Element($"item_{oldName}");
            if (oldNode != null)
            {
                oldNode.Name = $"item_{newName}";
                doc.Save(locPath);
            }
        }

        private void PopulateModeData(XElement track, ModeData data)
        {
            if (track.Attribute("UnlockPrice") != null)
            {
                data.unlockPrice = ParseInt((string)track.Attribute("UnlockPrice"));
                data.rawPriceInput = data.unlockPrice.ToString();
            }

            XElement conditions = track.Element("Conditions");
            if (conditions != null)
            {
                XElement stars = conditions.Element("Stars");
                if (stars != null)
                {
                    data.starsRequired = ParseInt((string)stars.Attribute("Required"));
                    data.rawStarsInput = data.starsRequired.ToString();

                    string subjStr = (string)stars.Attribute("Subject");
                    if (!string.IsNullOrEmpty(subjStr) && System.Enum.TryParse(subjStr, out SubjectType pSubj))
                        data.subject = pSubj;

                    data.subjectName = (string)stars.Attribute("Name") ?? "";
                }
                XElement payment = conditions.Element("Payment");
                if (payment != null && data.starsRequired == 0)
                {
                    data.unlockPrice = ParseInt((string)payment.Attribute("Required"));
                    data.rawPriceInput = data.unlockPrice.ToString();
                }
            }

            XElement tricksNode = track.Element("Tricks");
            if (tricksNode != null)
            {
                foreach (XElement tNode in tricksNode.Elements("Trick"))
                    data.tricks.Add((string)tNode.Attribute("Name"));
            }

            data.starsTemplate = (string)track.Element("Stars")?.Attribute("Template") ?? "";
            data.rewardTemplate = (string)track.Element("Reward")?.Attribute("Template") ?? "";
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

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}