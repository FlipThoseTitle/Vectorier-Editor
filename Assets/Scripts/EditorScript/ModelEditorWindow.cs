using System.Collections.Generic;
using System.Globalization;
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
            window.minSize = new Vector2(420, 500);
            window.config = exportConfig;
            window.editCommonMode = commonMode;
            window.ReloadData(false);
            window.Show();
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

            if (useDefault) EditorUtility.SetDirty(config);
        }

        private void OnGUI()
        {
            if (config == null || Models == null)
            {
                EditorGUILayout.HelpBox("ExportConfig or Model list is missing.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField(editCommonMode ? "Common Mode Models" : "Hunter Mode Models", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Notes:\n" +
                "- Respawns, ForceBlasts, Skins, Murders, Arrests, Stocks can contain multiple values separated by '|'\n" +
                "- Example: Hunter|Player",
                MessageType.Info
            );
            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < Models.Count; i++)
            {
                var m = Models[i];
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Model {i + 1}", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                {
                    Models.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break; // Instantly breaks the GUI loop to avoid Layout errors after deletion
                }
                EditorGUILayout.EndHorizontal();

                m.name = EditorGUILayout.TextField("Name *", m.name);
                m.type = EditorGUILayout.IntPopup("Type *", m.type, new[] { "Player (1)", "AI (0)" }, new[] { 1, 0 });
                m.color = ColorToHex(EditorGUILayout.ColorField("Color *", HexToColor(m.color)));
                m.birthSpawn = EditorGUILayout.TextField("BirthSpawn *", m.birthSpawn);
                m.ai = EditorGUILayout.IntField("AI *", m.ai);
                m.time = EditorGUILayout.FloatField("Time *", m.time);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Optional", EditorStyles.boldLabel);

                m.respawns = EditorGUILayout.TextField("Respawns", m.respawns);
                if (m.type == 1) m.forceBlasts = EditorGUILayout.TextField("ForceBlasts", m.forceBlasts);
                
                m.trick = EditorGUILayout.Toggle("Trick", m.trick);
                m.victory = EditorGUILayout.Toggle("Victory", m.victory);
                m.lose = EditorGUILayout.Toggle("Lose", m.lose);
                m.item = EditorGUILayout.Toggle("Item", m.item);

                if (m.type == 0)
                {
                    m.allowedSpawns = EditorGUILayout.TextField("AllowedSpawns", m.allowedSpawns);
                    m.icon = EditorGUILayout.Toggle("Icon", m.icon);
                }

                m.skins = EditorGUILayout.TextField("Skins", m.skins);
                m.murders = EditorGUILayout.TextField("Murders", m.murders);
                m.arrests = EditorGUILayout.TextField("Arrests", m.arrests);
                m.stocks = EditorGUILayout.TextField("Stocks", m.stocks);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Model", GUILayout.Height(30))) Models.Add(new ExportConfig.ModelDefinition());

            if (GUILayout.Button("Revert", GUILayout.Height(30)))
            {
                string modeName = editCommonMode ? "Common Mode" : "Hunter Mode";
                if (EditorUtility.DisplayDialog("Revert Changes", $"Revert {modeName} models to default? Unsaved changes will be lost.", "Revert", "Cancel"))
                {
                    ReloadData(true);
                    GUI.FocusControl(null);
                }
            }

            if (GUILayout.Button("Apply", GUILayout.Height(30)))
            {
                if (ValidateModels(Models))
                {
                    SourceText = BuildModelsText(Models);
                    EditorUtility.SetDirty(config);
                    Close();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool ValidateModels(List<ExportConfig.ModelDefinition> models)
        {
            for (int i = 0; i < models.Count; i++)
            {
                var m = models[i];
                if (string.IsNullOrWhiteSpace(m.name) || string.IsNullOrWhiteSpace(m.birthSpawn) || (m.type != 0 && m.type != 1))
                {
                    EditorUtility.DisplayDialog("Invalid Model", $"Model {i + 1}: Name and BirthSpawn are required, and Type must be 0 or 1.", "OK");
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
                    if (cond && !string.IsNullOrEmpty(val)) attrs.Add($"{key}=\"{SecurityElement.Escape(val)}\"");
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

                // GetAttribute returns an empty string if missing
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
                        stocks = e.GetAttribute("Stocks")
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