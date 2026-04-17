using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using Vectorier.Model;

namespace Vectorier.EditorScripts.Tools
{
    public class MoveEditor : EditorWindow
    {
        // --- Configuration Paths ---
        private string movesXmlPath = "";
        private string binFolderPath = "";
        private string modelXmlPath = "";
        private string defaultMaterialPath = "Assets/Materials/Black.mat";

        // --- Data State ---
        private XmlDocument moveDoc;
        private List<MoveDef> moves = new List<MoveDef>();
        private List<string> eventGroups = new List<string>();
        private List<string> reactionGroups = new List<string>();

        // --- Editor State ---
        private Vector2 leftScroll;
        private Vector2 rightScroll;
        private string searchQuery = "";
        private MoveDef selectedMove;
        private IntervalDef selectedInterval;

        // --- Visualization State ---
        private ModelData currentModel;
        private ModelRenderer modelRenderer;
        private ModelAnimation modelAnimation;
        private ModelDebug modelDebug;
        private GameObject previewRoot;
        private double lastUpdateTime;

        [MenuItem("Vectorier/Tools/Move Editor", false, 34)]
        public static void ShowWindow()
        {
            var window = GetWindow<MoveEditor>("Move Editor");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnEnable()
        {
            movesXmlPath = EditorPrefs.GetString("MoveEditor_MovesXmlPath", "");
            binFolderPath = EditorPrefs.GetString("MoveEditor_BinFolderPath", "");
            modelXmlPath = EditorPrefs.GetString("MoveEditor_ModelXmlPath", "");
            
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            EditorPrefs.SetString("MoveEditor_MovesXmlPath", movesXmlPath);
            EditorPrefs.SetString("MoveEditor_BinFolderPath", binFolderPath);
            EditorPrefs.SetString("MoveEditor_ModelXmlPath", modelXmlPath);

            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= OnSceneGUI;

            CleanupVisualization();
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            
            // Left Panel: Move List
            EditorGUILayout.BeginVertical(GUILayout.Width(250), GUILayout.ExpandHeight(true));
            DrawMoveList();
            EditorGUILayout.EndVertical();

            // Right Panel: Move Details
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawMoveDetails();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            EditorGUILayout.BeginHorizontal();
            movesXmlPath = EditorGUILayout.TextField("Moves XML Path", movesXmlPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70))) movesXmlPath = EditorUtility.OpenFilePanel("Select moves.xml", "", "xml");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            binFolderPath = EditorGUILayout.TextField("Bin Folder Path", binFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70))) binFolderPath = EditorUtility.OpenFolderPanel("Select Bin Folder", "", "");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            modelXmlPath = EditorGUILayout.TextField("Model XML Path", modelXmlPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70))) modelXmlPath = EditorUtility.OpenFilePanel("Select Model XML", "", "xml");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import Moves XML", GUILayout.Height(30))) LoadXML();
            if (GUILayout.Button("Save Moves XML", GUILayout.Height(30))) SaveXML();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawMoveList()
        {
            EditorGUILayout.BeginHorizontal();
            searchQuery = EditorGUILayout.TextField("", searchQuery, "SearchTextField");
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(40))) searchQuery = "";
            EditorGUILayout.EndHorizontal();

            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
            
            var filteredMoves = moves.Where(m => string.IsNullOrEmpty(searchQuery) || m.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            foreach (var move in filteredMoves)
            {
                GUI.backgroundColor = (selectedMove == move) ? Color.cyan : Color.white;
                if (GUILayout.Button(move.Name, EditorStyles.helpBox))
                {
                    selectedMove = move;
                    selectedInterval = null;
                    PlayMoveVisualization(move, null);
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add New Move"))
            {
                var newMove = new MoveDef { Name = "NewMove", FileName = "new_move.bin" };
                moves.Add(newMove);
                selectedMove = newMove;
            }
        }

        private void DrawMoveDetails()
        {
            if (selectedMove == null)
            {
                EditorGUILayout.LabelField("Select a move from the list to edit its properties.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

            EditorGUILayout.LabelField("Move Properties", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            selectedMove.Name = EditorGUILayout.TextField("Move Name (Tag)", selectedMove.Name);
            if (GUILayout.Button("Delete Move", GUILayout.Width(100)))
            {
                moves.Remove(selectedMove);
                selectedMove = null;
                CleanupVisualization();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
                return;
            }
            EditorGUILayout.EndHorizontal();

            // Properties
            selectedMove.ID = EditorGUILayout.IntField("ID", selectedMove.ID);
            selectedMove.Type = EditorGUILayout.IntField("Type", selectedMove.Type);
            selectedMove.Loop = EditorGUILayout.Toggle("Loop", selectedMove.Loop);
            selectedMove.Mirror = EditorGUILayout.Toggle("Mirror", selectedMove.Mirror);
            selectedMove.FileName = EditorGUILayout.TextField("FileName", selectedMove.FileName);
            selectedMove.MidFrames = EditorGUILayout.IntField("MidFrames", selectedMove.MidFrames);
            selectedMove.FirstFrame = EditorGUILayout.IntField("FirstFrame", selectedMove.FirstFrame);
            selectedMove.EndFrame = EditorGUILayout.IntField("EndFrame", selectedMove.EndFrame);
            selectedMove.PivotNode = EditorGUILayout.TextField("PivotNode", selectedMove.PivotNode);
            selectedMove.Priority = EditorGUILayout.IntField("Priority", selectedMove.Priority);
            selectedMove.VelocityX = EditorGUILayout.FloatField("VelocityX", selectedMove.VelocityX);
            selectedMove.VelocityY = EditorGUILayout.FloatField("VelocityY", selectedMove.VelocityY);
            selectedMove.Binding = EditorGUILayout.Toggle("Binding", selectedMove.Binding);
            selectedMove.DeltaDetectorH = EditorGUILayout.IntField("DeltaDetectorH", selectedMove.DeltaDetectorH);
            selectedMove.DeltaDetectorV = EditorGUILayout.IntField("DeltaDetectorV", selectedMove.DeltaDetectorV);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Intervals", EditorStyles.boldLabel);

            for (int i = 0; i < selectedMove.Intervals.Count; i++)
            {
                var interval = selectedMove.Intervals[i];
                EditorGUILayout.BeginVertical("box");
                
                EditorGUILayout.BeginHorizontal();
                bool isSelected = (selectedInterval == interval);
                GUI.color = isSelected ? Color.green : Color.white;
                if (GUILayout.Button($"Interval {interval.Start} - {interval.End}", EditorStyles.toolbarButton))
                {
                    selectedInterval = interval;
                    PlayMoveVisualization(selectedMove, interval);
                }
                GUI.color = Color.white;
                
                if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    selectedMove.Intervals.RemoveAt(i);
                    if (selectedInterval == interval) selectedInterval = null;
                    i--;
                    continue;
                }
                EditorGUILayout.EndHorizontal();

                if (selectedInterval == interval)
                {
                    interval.Start = EditorGUILayout.IntField("Start", interval.Start);
                    interval.End = EditorGUILayout.IntField("End", interval.End);
                    interval.Safe = EditorGUILayout.Toggle("Safe", interval.Safe);
                    interval.Groups = EditorGUILayout.TextField("Groups", interval.Groups);
                    interval.Action = EditorGUILayout.TextField("Action", interval.Action);
                    
                    EditorGUILayout.LabelField("Events associated with Interval will go here...", EditorStyles.miniLabel);
                    // Add nested event/reaction UI lists as needed here
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Interval"))
            {
                selectedMove.Intervals.Add(new IntervalDef());
            }

            EditorGUILayout.EndScrollView();
        }

        // --- Visualization & Playback ---

        private void PlayMoveVisualization(MoveDef move, IntervalDef intervalOverride)
        {
            CleanupVisualization();

            if (string.IsNullOrEmpty(modelXmlPath) || !File.Exists(modelXmlPath)) return;
            if (string.IsNullOrEmpty(binFolderPath) || string.IsNullOrEmpty(move.FileName)) return;

            string fullBinPath = Path.Combine(binFolderPath, move.FileName);
            if (!File.Exists(fullBinPath))
            {
                Debug.LogWarning($"[MoveEditor] Bin file not found: {fullBinPath}");
                return;
            }

            previewRoot = new GameObject($"Preview_{move.Name}");
            previewRoot.hideFlags = HideFlags.DontSave;

            try
            {
                currentModel = ModelData.LoadOrThrow(modelXmlPath);
                
                modelRenderer = new ModelRenderer(defaultMaterialPath);
                modelRenderer.Create(currentModel, null, previewRoot.transform, false);

                modelAnimation = new ModelAnimation();
                modelAnimation.LoadModel(modelXmlPath);
                
                modelDebug = new ModelDebug();
                modelDebug.AttachToModel(currentModel, previewRoot);
                modelDebug.DeltaDetectorH = move.DeltaDetectorH;
                modelDebug.DeltaDetectorV = move.DeltaDetectorV;

                int startFrame = intervalOverride != null ? intervalOverride.Start : move.FirstFrame;
                int endFrame = intervalOverride != null ? intervalOverride.End : move.EndFrame;

                modelAnimation.StartFrame = startFrame;
                modelAnimation.EndFrame = endFrame;
                
                string pivot = string.IsNullOrEmpty(move.PivotNode) ? "NPivot" : move.PivotNode;
                modelAnimation.PlaceAt(Vector3.zero, previewRoot.transform, modelXmlPath, fullBinPath, pivot, move.Binding);

                lastUpdateTime = EditorApplication.timeSinceStartup;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MoveEditor] Failed to play animation: {ex.Message}");
                CleanupVisualization();
            }
        }

        private void OnEditorUpdate()
        {
            if (modelAnimation != null && modelAnimation.IsPlaying)
            {
                if (modelAnimation.TryAdvancePlayback())
                {
                    // Handle Velocity Translation
                    if (selectedMove != null && selectedMove.Binding)
                    {
                        if (selectedMove.VelocityX != 0 || selectedMove.VelocityY != 0)
                        {
                            Vector3 velocityOffset = new Vector3(selectedMove.VelocityX, selectedMove.VelocityY, 0f);
                            if (previewRoot != null)
                            {
                                previewRoot.transform.position += velocityOffset;
                            }
                        }
                    }

                    if (modelRenderer != null)
                        modelRenderer.UpdateCapsules(modelAnimation.AnimationNodes);
                        
                    if (modelDebug != null)
                        modelDebug.UpdateNodeWorldPositions(modelAnimation.AnimationNodes, previewRoot.transform);

                    SceneView.RepaintAll();
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (modelDebug != null && modelAnimation != null)
            {
                modelDebug.DrawScene(modelAnimation, previewRoot != null ? previewRoot.transform : null, true);
            }
        }

        private void CleanupVisualization()
        {
            if (modelRenderer != null) { modelRenderer.Dispose(); modelRenderer = null; }
            if (modelDebug != null) { modelDebug.Dispose(); modelDebug = null; }
            if (modelAnimation != null) { modelAnimation.ResetAll(); modelAnimation = null; }
            if (previewRoot != null) { DestroyImmediate(previewRoot); previewRoot = null; }
            currentModel = null;
        }

        // --- XML Serialization ---

        private void LoadXML()
        {
            if (!File.Exists(movesXmlPath)) return;

            moves.Clear();
            moveDoc = new XmlDocument();
            moveDoc.Load(movesXmlPath);

            XmlNode movesNode = moveDoc.SelectSingleNode("/Data/Moves") ?? moveDoc.SelectSingleNode("Moves");
            if (movesNode == null) return;

            foreach (XmlNode child in movesNode.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;

                var move = new MoveDef { Name = child.Name };
                move.ID = GetIntAttr(child, "ID");
                move.Type = GetIntAttr(child, "Type");
                move.Loop = GetBoolAttr(child, "Loop");
                move.Mirror = GetBoolAttr(child, "Mirror");
                move.FileName = GetStrAttr(child, "FileName");
                move.MidFrames = GetIntAttr(child, "MidFrames");
                move.FirstFrame = GetIntAttr(child, "FirstFrame");
                move.EndFrame = GetIntAttr(child, "EndFrame");
                move.PivotNode = GetStrAttr(child, "PivotNode");
                move.Priority = GetIntAttr(child, "Priority");
                move.VelocityX = GetFloatAttr(child, "VelocityX");
                move.VelocityY = GetFloatAttr(child, "VelocityY");
                move.Binding = GetBoolAttr(child, "Binding");
                move.DeltaDetectorH = GetIntAttr(child, "DeltaDetectorH");
                move.DeltaDetectorV = GetIntAttr(child, "DeltaDetectorV");

                foreach (XmlNode intNode in child.ChildNodes)
                {
                    if (intNode.Name == "Interval")
                    {
                        var interval = new IntervalDef();
                        interval.Start = GetIntAttr(intNode, "Start");
                        interval.End = GetIntAttr(intNode, "End");
                        interval.Safe = GetBoolAttr(intNode, "Safe");
                        interval.Groups = GetStrAttr(intNode, "Groups");
                        interval.Action = GetStrAttr(intNode, "Action");
                        move.Intervals.Add(interval);
                        
                        // NOTE: Event/Reaction parsing would be deeply nested here
                    }
                }
                moves.Add(move);
            }
        }

        private void SaveXML()
        {
            if (string.IsNullOrEmpty(movesXmlPath)) return;
            
            // For safety, this rebuilds a basic structure. 
            // A full implementation would merge with the original to preserve unmapped elements.
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("Moves");
            doc.AppendChild(root);

            foreach (var move in moves)
            {
                XmlElement mNode = doc.CreateElement(move.Name);
                mNode.SetAttribute("ID", move.ID.ToString());
                mNode.SetAttribute("Type", move.Type.ToString());
                mNode.SetAttribute("Loop", move.Loop ? "1" : "0");
                if (move.Mirror) mNode.SetAttribute("Mirror", "1");
                mNode.SetAttribute("FileName", move.FileName);
                mNode.SetAttribute("FirstFrame", move.FirstFrame.ToString());
                mNode.SetAttribute("EndFrame", move.EndFrame.ToString());
                if (!string.IsNullOrEmpty(move.PivotNode)) mNode.SetAttribute("PivotNode", move.PivotNode);
                
                if (move.VelocityX != 0) mNode.SetAttribute("VelocityX", move.VelocityX.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (move.VelocityY != 0) mNode.SetAttribute("VelocityY", move.VelocityY.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (move.Binding) mNode.SetAttribute("Binding", "1");
                if (move.DeltaDetectorH != 0) mNode.SetAttribute("DeltaDetectorH", move.DeltaDetectorH.ToString());
                if (move.DeltaDetectorV != 0) mNode.SetAttribute("DeltaDetectorV", move.DeltaDetectorV.ToString());

                foreach (var interval in move.Intervals)
                {
                    XmlElement iNode = doc.CreateElement("Interval");
                    iNode.SetAttribute("Start", interval.Start.ToString());
                    iNode.SetAttribute("End", interval.End.ToString());
                    if (interval.Safe) iNode.SetAttribute("Safe", "1");
                    if (!string.IsNullOrEmpty(interval.Groups)) iNode.SetAttribute("Groups", interval.Groups);
                    if (!string.IsNullOrEmpty(interval.Action)) iNode.SetAttribute("Action", interval.Action);
                    
                    mNode.AppendChild(iNode);
                }

                root.AppendChild(mNode);
            }

            doc.Save(movesXmlPath);
            Debug.Log($"Saved Moves to {movesXmlPath}");
        }

        // --- Utility ---
        private int GetIntAttr(XmlNode node, string attr) { return int.TryParse(node.Attributes?[attr]?.Value, out int v) ? v : 0; }
        private float GetFloatAttr(XmlNode node, string attr) { return float.TryParse(node.Attributes?[attr]?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f; }
        private bool GetBoolAttr(XmlNode node, string attr) { return node.Attributes?[attr]?.Value == "1"; }
        private string GetStrAttr(XmlNode node, string attr) { return node.Attributes?[attr]?.Value ?? ""; }
    }

    // --- Data Definitions ---
    [Serializable]
    public class MoveDef
    {
        public string Name;
        public int ID;
        public int Type;
        public bool Loop;
        public bool Mirror;
        public string FileName;
        public int MidFrames;
        public int FirstFrame;
        public int EndFrame;
        public string PivotNode;
        public int Priority;
        public float VelocityX;
        public float VelocityY;
        public bool Binding;
        public int DeltaDetectorH;
        public int DeltaDetectorV;
        
        public List<IntervalDef> Intervals = new List<IntervalDef>();
    }

    [Serializable]
    public class IntervalDef
    {
        public int Start;
        public int End;
        public bool Safe;
        public string Groups;
        public string Action;
        // Extendable for nested Events/Reactions
    }
}