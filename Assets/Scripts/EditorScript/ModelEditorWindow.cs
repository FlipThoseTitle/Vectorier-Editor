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

        public static void Open(ExportConfig exportConfig, bool commonMode)
        {
            ModelEditorWindow window = GetWindow<ModelEditorWindow>(true, commonMode ? "Edit Common Mode Models" : "Edit Hunter Mode Models");
            window.minSize = new Vector2(420, 500);
            window.config = exportConfig;
            window.editCommonMode = commonMode;
            window.ReloadFromSourceText();
            window.Show();
        }

        private List<ExportConfig.ModelDefinition> GetTargetList()
        {
            return editCommonMode ? config.commonModeModelDefinitions : config.hunterModeModelDefinitions;
        }

        private string GetSourceText()
        {
            return editCommonMode ? config.commonModeModels : config.hunterModeModels;
        }

        private string GetDefaultSourceText()
        {
            return editCommonMode
                ? ExportConfig.DefaultCommonModeModels
                : ExportConfig.DefaultHunterModeModels;
        }

        private void ReloadFromDefaultText()
        {
            if (config == null)
                return;

            string defaultText = GetDefaultSourceText();
            SetSourceText(defaultText);

            List<ExportConfig.ModelDefinition> targetList = GetTargetList();
            if (targetList == null)
                return;

            targetList.Clear();

            List<ExportConfig.ModelDefinition> parsed = ParseModelsFromXml(defaultText);

            if (parsed.Count > 0)
                targetList.AddRange(parsed);
            else
                targetList.Add(new ExportConfig.ModelDefinition());

            EditorUtility.SetDirty(config);
        }

        private void SetSourceText(string value)
        {
            if (editCommonMode)
                config.commonModeModels = value;
            else
                config.hunterModeModels = value;
        }

        private void ReloadFromSourceText()
        {
            if (config == null)
                return;

            List<ExportConfig.ModelDefinition> targetList = GetTargetList();
            if (targetList == null)
                return;

            targetList.Clear();

            string sourceText = GetSourceText();
            List<ExportConfig.ModelDefinition> parsed = ParseModelsFromXml(sourceText);

            if (parsed.Count > 0)
                targetList.AddRange(parsed);
            else
                targetList.Add(new ExportConfig.ModelDefinition());
        }

        private void OnGUI()
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("ExportConfig reference is missing.", MessageType.Error);
                return;
            }

            List<ExportConfig.ModelDefinition> models = GetTargetList();
            if (models == null)
            {
                EditorGUILayout.HelpBox("Model list is unavailable.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField(editCommonMode ? "Common Mode Models" : "Hunter Mode Models", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            int removeIndex = -1;

            for (int i = 0; i < models.Count; i++)
            {
                ExportConfig.ModelDefinition model = models[i];

                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Model {i + 1}", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                    removeIndex = i;
                EditorGUILayout.EndHorizontal();

                model.name = EditorGUILayout.TextField("Name *", model.name);
                model.type = EditorGUILayout.IntPopup("Type *", model.type, new[] { "Player (1)", "AI (0)" }, new[] { 1, 0 });

                Color currentColor = HexToColor(model.color);
                Color pickedColor = EditorGUILayout.ColorField("Color *", currentColor);
                model.color = ColorToExportHex(pickedColor);

                model.birthSpawn = EditorGUILayout.TextField("BirthSpawn *", model.birthSpawn);
                model.ai = EditorGUILayout.IntField("AI *", model.ai);
                model.time = EditorGUILayout.FloatField("Time *", model.time);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Optional", EditorStyles.boldLabel);

                model.respawns = EditorGUILayout.TextField("Respawns", model.respawns);

                if (model.type == 1)
                    model.forceBlasts = EditorGUILayout.TextField("ForceBlasts", model.forceBlasts);

                model.trick = EditorGUILayout.Toggle("Trick", model.trick);
                model.victory = EditorGUILayout.Toggle("Victory", model.victory);
                model.lose = EditorGUILayout.Toggle("Lose", model.lose);
                model.item = EditorGUILayout.Toggle("Item", model.item);

                if (model.type == 0)
                {
                    model.allowedSpawns = EditorGUILayout.TextField("AllowedSpawns", model.allowedSpawns);
                    model.icon = EditorGUILayout.Toggle("Icon", model.icon);
                }

                model.skins = EditorGUILayout.TextField("Skins", model.skins);
                model.murders = EditorGUILayout.TextField("Murders", model.murders);
                model.arrests = EditorGUILayout.TextField("Arrests", model.arrests);
                model.stocks = EditorGUILayout.TextField("Stocks", model.stocks);

                EditorGUILayout.HelpBox(
                    "Notes:\n" +
                    "- Respawns, ForceBlasts, Skins, Murders, Arrests, Stocks can contain multiple values separated by '|'\n" +
                    "- Example: Hunter|Player",
                    MessageType.None
                );

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            if (removeIndex >= 0)
                models.RemoveAt(removeIndex);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Model", GUILayout.Height(30)))
            {
                models.Add(new ExportConfig.ModelDefinition());
            }

            if (GUILayout.Button("Revert", GUILayout.Height(30)))
            {
                string modeName = editCommonMode ? "Common Mode" : "Hunter Mode";

                if (EditorUtility.DisplayDialog(
                    "Revert Changes",
                    $"Revert {modeName} models to the default? Unsaved changes in this window will be lost.",
                    "Revert",
                    "Cancel"))
                {
                    ReloadFromDefaultText();
                    GUI.FocusControl(null);
                }
            }

            if (GUILayout.Button("Apply", GUILayout.Height(30)))
            {
                if (!ValidateModels(models))
                    return;

                string xmlText = BuildModelsText(models);
                SetSourceText(xmlText);

                EditorUtility.SetDirty(config);
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool ValidateModels(List<ExportConfig.ModelDefinition> models)
        {
            for (int i = 0; i < models.Count; i++)
            {
                var m = models[i];

                if (string.IsNullOrWhiteSpace(m.name))
                {
                    EditorUtility.DisplayDialog("Invalid Model", $"Model {i + 1}: Name is required.", "OK");
                    return false;
                }

                if (m.type != 0 && m.type != 1)
                {
                    EditorUtility.DisplayDialog("Invalid Model", $"Model {i + 1}: Type must be 0 or 1.", "OK");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(m.birthSpawn))
                {
                    EditorUtility.DisplayDialog("Invalid Model", $"Model {i + 1}: BirthSpawn is required.", "OK");
                    return false;
                }
            }

            return true;
        }

        private static string BuildModelsText(List<ExportConfig.ModelDefinition> models)
        {
            List<string> lines = new List<string>();

            foreach (var m in models)
            {
                List<string> attrs = new List<string>
                {
                    $"Name=\"{EscapeAttr(m.name)}\"",
                    $"Type=\"{m.type}\"",
                    $"Color=\"{EscapeAttr(string.IsNullOrWhiteSpace(m.color) ? "0" : m.color)}\"",
                    $"BirthSpawn=\"{EscapeAttr(m.birthSpawn)}\"",
                    $"AI=\"{m.ai}\"",
                    $"Time=\"{m.time.ToString(CultureInfo.InvariantCulture)}\""
                };

                if (!string.IsNullOrWhiteSpace(m.respawns))
                    attrs.Add($"Respawns=\"{EscapeAttr(m.respawns)}\"");

                if (!string.IsNullOrWhiteSpace(m.forceBlasts) && m.type == 1)
                    attrs.Add($"ForceBlasts=\"{EscapeAttr(m.forceBlasts)}\"");

                if (m.trick)
                    attrs.Add("Trick=\"1\"");

                if (m.item)
                    attrs.Add("Item=\"1\"");

                if (m.victory)
                    attrs.Add("Victory=\"1\"");

                if (m.lose)
                    attrs.Add("Lose=\"1\"");

                if (!string.IsNullOrWhiteSpace(m.allowedSpawns) && m.type == 0)
                    attrs.Add($"AllowedSpawns=\"{EscapeAttr(m.allowedSpawns)}\"");

                if (!string.IsNullOrWhiteSpace(m.skins))
                    attrs.Add($"Skins=\"{EscapeAttr(m.skins)}\"");

                if (!string.IsNullOrWhiteSpace(m.murders))
                    attrs.Add($"Murders=\"{EscapeAttr(m.murders)}\"");

                if (!string.IsNullOrWhiteSpace(m.arrests))
                    attrs.Add($"Arrests=\"{EscapeAttr(m.arrests)}\"");

                if (m.icon && m.type == 0)
                    attrs.Add("Icon=\"1\"");

                if (!string.IsNullOrWhiteSpace(m.stocks))
                    attrs.Add($"Stocks=\"{EscapeAttr(m.stocks)}\"");

                lines.Add("<Model " + string.Join(" ", attrs) + "/>");
            }

            return string.Join("\n", lines);
        }

        private static string EscapeAttr(string value)
        {
            return SecurityElement.Escape(value) ?? "";
        }

        private static List<ExportConfig.ModelDefinition> ParseModelsFromXml(string xmlText)
        {
            List<ExportConfig.ModelDefinition> list = new List<ExportConfig.ModelDefinition>();

            if (string.IsNullOrWhiteSpace(xmlText))
                return list;

            try
            {
                string wrapped = $"<Root>{xmlText}</Root>";
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(wrapped);

                XmlNodeList nodes = doc.SelectNodes("//Model");
                if (nodes == null)
                    return list;

                foreach (XmlNode node in nodes)
                {
                    if (node is not XmlElement element)
                        continue;

                    ExportConfig.ModelDefinition model = new ExportConfig.ModelDefinition();

                    model.name = GetString(element, "Name", model.name);
                    model.type = GetInt(element, "Type", model.type);
                    model.color = NormalizeImportedColor(GetString(element, "Color", model.color));
                    model.birthSpawn = GetString(element, "BirthSpawn", model.birthSpawn);
                    model.ai = GetInt(element, "AI", model.ai);
                    model.time = GetFloat(element, "Time", model.time);

                    model.respawns = GetString(element, "Respawns", "");
                    model.forceBlasts = GetString(element, "ForceBlasts", "");
                    model.trick = GetBool01(element, "Trick");
                    model.victory = GetBool01(element, "Victory");
                    model.lose = GetBool01(element, "Lose");
                    model.item = GetBool01(element, "Item");
                    model.allowedSpawns = GetString(element, "AllowedSpawns", "");
                    model.skins = GetString(element, "Skins", "");
                    model.murders = GetString(element, "Murders", "");
                    model.arrests = GetString(element, "Arrests", "");
                    model.icon = GetBool01(element, "Icon");
                    model.stocks = GetString(element, "Stocks", "");

                    list.Add(model);
                }
            }
            catch
            {
                // return empty
            }

            return list;
        }

        private static string GetString(XmlElement e, string attr, string defaultValue)
        {
            return e.HasAttribute(attr) ? e.GetAttribute(attr) : defaultValue;
        }

        private static int GetInt(XmlElement e, string attr, int defaultValue)
        {
            if (!e.HasAttribute(attr))
                return defaultValue;

            return int.TryParse(e.GetAttribute(attr), out int value) ? value : defaultValue;
        }

        private static float GetFloat(XmlElement e, string attr, float defaultValue)
        {
            if (!e.HasAttribute(attr))
                return defaultValue;

            return float.TryParse(
                e.GetAttribute(attr),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value)
                ? value
                : defaultValue;
        }

        private static bool GetBool01(XmlElement e, string attr)
        {
            return e.HasAttribute(attr) && e.GetAttribute(attr) == "1";
        }

        private static string NormalizeImportedColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "0";

            string cleaned = value.Trim().TrimStart('#').ToUpperInvariant();

            if (cleaned == "000000" || cleaned == "0")
                return "0";

            return cleaned;
        }

        private static Color HexToColor(string value)
        {
            string normalized = NormalizeImportedColor(value);

            if (normalized == "0")
                return Color.black;

            if (ColorUtility.TryParseHtmlString("#" + normalized, out Color color))
                return color;

            return Color.black;
        }

        private static string ColorToExportHex(Color color)
        {
            Color32 c = color;
            bool isBlack = c.r == 0 && c.g == 0 && c.b == 0;

            if (isBlack)
                return "0";

            return $"{c.r:X2}{c.g:X2}{c.b:X2}";
        }
    }
}