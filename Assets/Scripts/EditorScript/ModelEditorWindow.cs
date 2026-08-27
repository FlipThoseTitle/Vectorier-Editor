using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Xml;
using UnityEditor;
using UnityEngine;
using Vectorier.Core;

namespace Vectorier.EditorScript
{
    public class ModelEditorWindow : EditorWindow
    {
        private ExportConfig config;
        private bool editCommonMode;
        private Vector2 scrollPosition;
        private int selectedIndex = -1;
        
        // Cache for scene spawns
        private string[] sceneSpawnNames;

        private List<ExportConfig.ModelDefinition> Models => editCommonMode ? config.commonModeModelDefinitions : config.hunterModeModelDefinitions;
        private string DefaultText => editCommonMode ? ExportConfig.DefaultCommonModeModels : ExportConfig.DefaultHunterModeModels;
        
        private string SourceText
        {
            get => editCommonMode ? config.commonModeModels : config.hunterModeModels;
            set { if (editCommonMode) config.commonModeModels = value; else config.hunterModeModels = value; }
        }

        public static void Open(ExportConfig exportConfig, bool commonMode)
        {
            ModelEditorWindow window = GetWindow<ModelEditorWindow>(true, commonMode ? "Edit Common Mode Models" : "Edit Hunter Mode Models");
            window.minSize = new Vector2(450, 700);
            window.config = exportConfig;
            window.editCommonMode = commonMode;
            window.ReloadData(false);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSceneSpawns();
        }

        private void RefreshSceneSpawns()
        {
            try
            {
                var spawnObjects = GameObject.FindGameObjectsWithTag("Spawn");
                sceneSpawnNames = spawnObjects
                    .Select(g => 
                    {
                        string cleanName = g.name.Trim();
                        cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"\s*\(\d+\)$", "");
                        return cleanName.Trim();
                    })
                    .Where(name => !string.IsNullOrEmpty(name)) // Ignore completely empty names
                    .Distinct()
                    .ToArray();
            }
            catch
            {
                sceneSpawnNames = new string[0];
            }
        }

        private void ReloadData(bool useDefault)
        {
            if (config == null || Models == null) return;

            string text = useDefault ? DefaultText : SourceText;
            if (useDefault) SourceText = text;

            Models.Clear();
            var parsed = ParseModelsFromXml(text);
            
            if (parsed.Count > 0) Models.AddRange(parsed);
            else Models.Add(new ExportConfig.ModelDefinition());

            selectedIndex = -1;

            if (useDefault) EditorUtility.SetDirty(config);
        }

        private void OnGUI()
        {
            if (config == null || Models == null)
            {
                EditorGUILayout.HelpBox("ExportConfig or Model list is missing.", MessageType.Error);
                return;
            }

            // Catch background clicks to clear focus
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
                Repaint();
            }

            GUILayout.Space(10);

            // --- Header & Action Buttons ---
            GUILayout.BeginHorizontal();
            string modeTitle = editCommonMode ? "Common Mode Models" : "Hunter Mode Models";
            GUILayout.Label(modeTitle, EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(30))) 
            {
                GUI.FocusControl(null);
                Models.Add(new ExportConfig.ModelDefinition());
                selectedIndex = Models.Count - 1;
            }
            
            GUI.enabled = selectedIndex >= 0 && selectedIndex < Models.Count;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(30))) 
            {
                GUI.FocusControl(null);
                Models.RemoveAt(selectedIndex);
                selectedIndex = -1;
            }
            GUI.enabled = true;
            
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(30))) 
            {
                if (EditorUtility.DisplayDialog("Clear Models", $"Are you sure you want to clear all {modeTitle}?", "Clear All", "Cancel"))
                {
                    Models.Clear();
                    selectedIndex = -1;
                    GUI.FocusControl(null);
                }
            }
            
            if (GUILayout.Button("R", GUILayout.Width(30), GUILayout.Height(30))) 
            {
                if (EditorUtility.DisplayDialog("Revert Changes", $"Revert {modeTitle} to default? Unsaved changes will be lost.", "Revert", "Cancel"))
                {
                    ReloadData(true);
                    GUI.FocusControl(null);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // --- Model List ---
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            if (Models.Count == 0)
            {
                GUILayout.Label("No models added yet. Click '+' to create one.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < Models.Count; i++)
                {
                    DrawModelItem(i);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.Space(10);

            // --- Bottom Apply Button ---
            if (GUILayout.Button("Apply", GUILayout.Height(30)))
            {
                if (ValidateModels(Models))
                {
                    SourceText = BuildModelsText(Models);
                    EditorUtility.SetDirty(config);
                    Close();
                }
            }
        }

        private void DrawModelItem(int index)
        {
            var m = Models[index];
            bool isSelected = (selectedIndex == index);

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            if (isSelected) boxStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.5f, 0.9f, 0.5f));

            GUILayout.BeginVertical(boxStyle);
            
            // Header Row
            GUILayout.BeginHorizontal();
            string displayName = string.IsNullOrWhiteSpace(m.name) ? "New Model" : m.name;
            if (GUILayout.Button($"Model {index + 1}: {displayName}", EditorStyles.toolbarButton))
            {
                selectedIndex = index;
                GUI.FocusControl(null);
            }

            // --- Move Up/Down Buttons ---
            GUILayout.FlexibleSpace();
            
            GUI.enabled = index > 0;
            if (GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                MoveModel(index, -1);
            }
            
            GUI.enabled = index < Models.Count - 1;
            if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                MoveModel(index, 1);
            }
            GUI.enabled = true;
            // ----------------------------
            
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // --- REQUIRED FIELDS ---
            m.name = EditorGUILayout.TextField(new GUIContent("Model Name *", "The name of the Model."), m.name);
            m.type = EditorGUILayout.Popup(new GUIContent("Type *", "Determines if this model is controlled by a Player or AI."), m.type, new[] { "AI", "Player" });
            m.color = ColorToHex(EditorGUILayout.ColorField(new GUIContent("Model Color *", "The color of the Model."), HexToColor(m.color)));
            
            DrawSpawnDropdown(ref m.birthSpawn, "Spawn *", "The spawn point in the scene for this model.");
            
            m.ai = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("AI Number *", "The number of the AI\nUsed to specify the AI Index for the Trigger.\nExample: Making it so that this trigger is only activated by AI number 2."), m.ai));
            m.time = Mathf.Max(0, EditorGUILayout.FloatField(new GUIContent("Time Until Spawn *", "Delay in seconds before this model spawns."), m.time));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Optional", EditorStyles.boldLabel);

            // --- OPTIONAL FIELDS ---
            DrawStringList(ref m.respawns, "Respawns", "Respawns");

            if (m.type == 1) // Player
            {
                // Passed 'm.name' and 'true' for aiOnly
                DrawModelDropdownList(ref m.forceBlasts, "Force Blast Who?", "AI Models that this Player can Force Blast.", m.name, true, "Add Model");
            }

            m.trick = EditorGUILayout.Toggle(new GUIContent("Perform Trick?", "Can this model perform tricks?\nIf true, the model will perform tricks upon pressing up in the trick's area."), m.trick);
            m.victory = EditorGUILayout.Toggle(new GUIContent("Can Win?", "Can this model trigger a victory?\nIf true, the model will be able to trigger win condition.\nThe player will then go to the level result menu."), m.victory);
            m.lose = EditorGUILayout.Toggle(new GUIContent("Can Lose?", "Can this model trigger a loss condition?\nIf true, will restart the level upon loss."), m.lose);
            m.item = EditorGUILayout.Toggle(new GUIContent("Can Collect Item?", "Can this model pick up bonuses and coins?"), m.item);

            if (m.type == 0) // AI
            {
                m.allowedSpawns = EditorGUILayout.TextField(new GUIContent("Allowed Spawns", "Allowed Spawns"), m.allowedSpawns);
                m.icon = EditorGUILayout.Toggle(new GUIContent("Show Icon?", "Should a hunter icon be shown for this AI?"), m.icon);
            }

            DrawStringList(ref m.skins, "Skins", "Skins for this model.\nUses the .xml file name.\nExample: hat.xml is just 'hat' in the list.");
            
            // Passed 'm.name' and 'false' for aiOnly so they can murder/arrest any other model
            DrawModelDropdownList(ref m.murders, "Murder Who?", "Models this model is allowed to murder.", m.name, false, "Add Model");
            DrawModelDropdownList(ref m.arrests, "Arrest Who?", "Models this model is allowed to arrest.", m.name, false, "Add Model");
            
            DrawStringList(ref m.stocks, "Stocks", "The models that will be attached to this model.\nUses the .xml file name\nExample: bike.xml is just 'bike' in the list.");

            m.lifeTime = Mathf.Max(0, EditorGUILayout.IntField( new GUIContent( "LifeTime", "The amount of time this model will remain alive." ), m.lifeTime ));

            GUILayout.Space(5);
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private void MoveModel(int index, int direction)
        {
            if (index < 0 || index >= Models.Count) return;
            
            int targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= Models.Count) return;

            // Swap the items
            var temp = Models[index];
            Models[index] = Models[targetIndex];
            Models[targetIndex] = temp;

            // Keep the selected index aligned with the item that moved
            if (selectedIndex == index) selectedIndex = targetIndex;
            else if (selectedIndex == targetIndex) selectedIndex = index;

            GUI.FocusControl(null); // Clear any active text fields
        }

        // --- Custom UI Helpers ---

        private void DrawSpawnDropdown(ref string currentSpawn, string label, string tooltip)
        {
            List<string> options = new List<string>();
            
            // Only add "None" if there are absolutely no Spawns found in the scene
            if (sceneSpawnNames != null && sceneSpawnNames.Length > 0)
            {
                options.AddRange(sceneSpawnNames);
            }
            else
            {
                options.Add("None");
            }

            if (!string.IsNullOrEmpty(currentSpawn) && !options.Contains(currentSpawn))
                options.Add(currentSpawn); 

            int currentIndex = options.IndexOf(currentSpawn);
            if (currentIndex == -1) currentIndex = 0;

            int newIndex = EditorGUILayout.Popup(new GUIContent(label, tooltip), currentIndex, options.ToArray());
            
            // Assign the exact selection
            currentSpawn = options[newIndex];
        }

        private void DrawStringList(ref string rawData, string label, string tooltip, string buttonText = null)
        {
            List<string> list = string.IsNullOrEmpty(rawData) 
                ? new List<string>() 
                : new List<string>(rawData.Split('|'));
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));
            EditorGUILayout.BeginVertical();
            
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                // Using DelayedTextField prevents the item from disappearing while the user is clearing the field
                list[i] = EditorGUILayout.DelayedTextField(list[i]);
                
                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    list.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            string btnText = string.IsNullOrEmpty(buttonText) ? $"Add {label}" : buttonText;
            if (GUILayout.Button(btnText, GUILayout.Height(20)))
            {
                // Add specific placeholder based on the field context
                string placeholder = label.Contains("Skins") ? "NewSkin" : "NewModel";
                list.Add(placeholder);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            
            rawData = string.Join("|", list);
        }

        private void DrawModelDropdownList(ref string rawData, string label, string tooltip, string currentModelName, bool aiOnly = false, string buttonText = null)
        {
            // Filter available models: match AI type if required, ignore empty names, and exclude the current model
            List<string> availableModels = Models
                .Where(m => (!aiOnly || m.type == 0) && !string.IsNullOrWhiteSpace(m.name) && m.name != currentModelName)
                .Select(m => m.name)
                .ToList();
                
            string fallbackText = aiOnly ? "No AI Models Available" : "No Models Available";
            
            if (availableModels.Count == 0) availableModels.Add(fallbackText);

            List<string> list = string.IsNullOrEmpty(rawData)
                ? new List<string>()
                : new List<string>(rawData.Split('|'));
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));
            EditorGUILayout.BeginVertical();

            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                int currentIndex = availableModels.IndexOf(list[i]);
                if (currentIndex == -1) currentIndex = 0; 
                
                int newIndex = EditorGUILayout.Popup(currentIndex, availableModels.ToArray());
                
                if (availableModels[0] != fallbackText)
                {
                    list[i] = availableModels[newIndex];
                }

                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    list.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            string btnText = string.IsNullOrEmpty(buttonText) ? $"Add {label}" : buttonText;
            if (GUILayout.Button(btnText, GUILayout.Height(20)))
            {
                list.Add(availableModels[0] != fallbackText ? availableModels[0] : "NewModel");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            rawData = string.Join("|", list.Where(s => s != fallbackText && !string.IsNullOrWhiteSpace(s) && s != "NewModel"));
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

        // --- Core Logic ---

        private bool ValidateModels(List<ExportConfig.ModelDefinition> models)
        {
            for (int i = 0; i < models.Count; i++)
            {
                var m = models[i];
                // Added check to block applying if the spawn is left at "None"
                if (string.IsNullOrWhiteSpace(m.name) || string.IsNullOrWhiteSpace(m.birthSpawn) || m.birthSpawn == "None")
                {
                    EditorUtility.DisplayDialog("Invalid Model", $"Model {i + 1}: Name and Spawn are required. Spawn cannot be 'None'.", "OK");
                    return false;
                }
            }
            return true;
        }

        private static string BuildModelsText(List<ExportConfig.ModelDefinition> models)
        {
            var lines = new List<string>();

            foreach (var m in models)
            {
                var attrs = new List<string>();
                
                void Add(string key, string val, bool cond = true) {
                    if (cond && !string.IsNullOrEmpty(val) && val != "None") attrs.Add($"{key}=\"{SecurityElement.Escape(val)}\"");
                }

                Add("Name", m.name);
                Add("Type", m.type.ToString());
                Add("Color", string.IsNullOrWhiteSpace(m.color) ? "0" : m.color);
                Add("BirthSpawn", m.birthSpawn);
                Add("AI", m.ai.ToString());
                Add("Time", m.time.ToString(CultureInfo.InvariantCulture));

                Add("Respawns", m.respawns);
                Add("ForceBlasts", m.forceBlasts, m.type == 1);
                Add("Trick", "1", m.trick);
                Add("Item", "1", m.item);
                Add("Victory", "1", m.victory);
                Add("Lose", "1", m.lose);
                Add("AllowedSpawns", m.allowedSpawns, m.type == 0);
                Add("Skins", m.skins);
                Add("Murders", m.murders);
                Add("Arrests", m.arrests);
                Add("Icon", "1", m.icon && m.type == 0);
                Add("Stocks", m.stocks);
                Add("LifeTime", m.lifeTime.ToString(), m.lifeTime != 0);

                lines.Add("<Model " + string.Join(" ", attrs) + "/>");
            }
            return string.Join("\n", lines);
        }

        private static List<ExportConfig.ModelDefinition> ParseModelsFromXml(string xmlText)
        {
            var list = new List<ExportConfig.ModelDefinition>();
            if (string.IsNullOrWhiteSpace(xmlText)) return list;

            try
            {
                var doc = new XmlDocument();
                doc.LoadXml($"<Root>{xmlText}</Root>");

                foreach (XmlElement e in doc.SelectNodes("//Model"))
                {
                    list.Add(new ExportConfig.ModelDefinition
                    {
                        name = e.GetAttribute("Name"),
                        type = int.TryParse(e.GetAttribute("Type"), out int t) ? t : 0,
                        color = e.GetAttribute("Color"),
                        birthSpawn = e.GetAttribute("BirthSpawn"),
                        ai = int.TryParse(e.GetAttribute("AI"), out int a) ? a : 0,
                        time = float.TryParse(e.GetAttribute("Time"), NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0f,
                        
                        respawns = e.GetAttribute("Respawns"),
                        forceBlasts = e.GetAttribute("ForceBlasts"),
                        trick = e.GetAttribute("Trick") == "1",
                        victory = e.GetAttribute("Victory") == "1",
                        lose = e.GetAttribute("Lose") == "1",
                        item = e.GetAttribute("Item") == "1",
                        allowedSpawns = e.GetAttribute("AllowedSpawns"),
                        skins = e.GetAttribute("Skins"),
                        murders = e.GetAttribute("Murders"),
                        arrests = e.GetAttribute("Arrests"),
                        icon = e.GetAttribute("Icon") == "1",
                        stocks = e.GetAttribute("Stocks"),
                        lifeTime = int.TryParse( e.GetAttribute("LifeTime"), out int lt ) ? lt : 0
                    });
                }
            }
            catch { /* Returns whatever was successfully parsed before failing */ }
            return list;
        }

        private static Color HexToColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex == "0") return Color.black;
            return ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out Color c) ? c : Color.black;
        }

        private static string ColorToHex(Color c)
        {
            return (c.r == 0 && c.g == 0 && c.b == 0) ? "0" : ColorUtility.ToHtmlStringRGB(c);
        }
    }
}