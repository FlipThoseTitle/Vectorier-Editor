using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vectorier.EditorScript.Tools
{
    public static class MoveVisualizerData
    {
        // ================= CONSTANT DATA ================= //

        public const string MOVEMENT_BASE_PATH = "Assets/Editor/Tools/MoveVisualizer/Movement";
        public const string TRICKS_BASE_PATH = "Assets/Editor/Tools/MoveVisualizer/Tricks";
        public const string IMAGE_BASE_PATH = "Assets/Editor/Tools/MoveVisualizer/Image";

        public const float SOURCE_FPS = 20f;

        public static readonly Dictionary<MoveVisualizer.MovementType, string> MovementBins = new()
        {
            { MoveVisualizer.MovementType.Jump, "fly.bin" },
            { MoveVisualizer.MovementType.JumpOff, "jump_off.bin" },
            { MoveVisualizer.MovementType.JumpOffFly, "jump_off_fly.bin" },
            { MoveVisualizer.MovementType.Run, "run.bin" },
            { MoveVisualizer.MovementType.RunFast, "run_fast_from_run.bin" },
            { MoveVisualizer.MovementType.RunFastJump, "run_fast_fly.bin" },
            { MoveVisualizer.MovementType.RunFastJumpOff, "run_fast_jump_off.bin" },
            { MoveVisualizer.MovementType.RunFastLandingFall, "run_fast_landing_fall.bin" },
            { MoveVisualizer.MovementType.Slide, "slide_simple.bin" },
            { MoveVisualizer.MovementType.SlideOff, "slide_simple_and_fall.bin" },
            { MoveVisualizer.MovementType.FastSlide, "fast_slide_simple.bin" },
            { MoveVisualizer.MovementType.FastSlideOff, "fast_slide_simple_fall.bin" },
            { MoveVisualizer.MovementType.DivingKongToFly, "diving_kong_to_fly.bin" },
            { MoveVisualizer.MovementType.SpeedVaultToFly, "speed_vault_fly.bin" },
            { MoveVisualizer.MovementType.CollisionToFly, "collision_to_fly.bin" },
            { MoveVisualizer.MovementType.FlyCollision, "fly_collision.bin" }
        };

        public readonly struct NodeDefinition
        {
            public readonly string Name;
            public readonly Vector3 PreviewPose;

            public NodeDefinition(string name, float x, float y, float z)
            {
                Name = name;
                PreviewPose = new Vector3(x, y, z);
            }
        }

        public static readonly NodeDefinition[] NodesOrdered =
        {
            new("NHip_1", -19.577221f, -8.585417f, 84.134026f),
            new("NHip_2", -14.560858f, 8.122724f, 82.953896f),
            new("NStomach", -9.392555f, -1.314231f, 99.322510f),
            new("NChest", -0.985765f, -2.248583f, 114.576309f),
            new("NNeck", 8.144234f, 0.799165f, 129.391342f),
            new("NShoulder_1", 12.551178f, -15.790263f, 128.772690f),
            new("NShoulder_2", 6.237663f, 17.609047f, 134.210663f),
            new("NKnee_1", -50.975418f, -5.785246f, 45.456955f),
            new("NKnee_2", 25.844582f, 9.903176f, 58.044540f),
            new("NAnkle_1", -88.699814f, -3.378675f, 25.798462f),
            new("NAnkle_2", -13.250669f, 6.528876f, 50.287231f),
            new("NToe_1", -92.157410f, -0.610153f, 8.867973f),
            new("NHeel_1", -98.535316f, -3.505732f, 27.600554f),
            new("NToeTip_1", -87.669655f, 0.365667f, 2.316364f),
            new("NToeS_1", -92.283051f, -8.508365f, 7.602760f),
            new("NHeel_2", -22.572092f, 4.031524f, 52.909119f),
            new("NToe_2", -18.491583f, 7.329743f, 33.609646f),
            new("NToeTip_2", -16.405792f, 8.739633f, 26.016127f),
            new("NToeS_2", -20.299704f, 15.065866f, 34.549374f),
            new("NElbow_1", 23.573296f, -29.794552f, 106.561279f),
            new("NElbow_2", -22.879360f, 22.530788f, 132.434006f),
            new("NWrist_1", 47.289154f, -12.875849f, 114.187531f),
            new("NWrist_2", -24.266720f, 36.565292f, 107.673439f),
            new("NKnuckles_1", 56.037056f, -9.906665f, 115.244850f),
            new("NFingertips_1", 55.178741f, -1.261982f, 119.921249f),
            new("NKnucklesS_1", 50.209671f, -10.927604f, 120.081985f),
            new("NKnuckles_2", -27.603422f, 39.348595f, 98.328270f),
            new("NFingertips_2", -29.340508f, 31.007551f, 102.793465f),
            new("NKnucklesS_2", -21.749163f, 35.052471f, 98.640076f),
            new("NHead", 17.986877f, 0.118644f, 143.801025f),
            new("NTop", 22.405685f, -0.554002f, 160.715347f),
            new("NChestS_1", -0.897280f, -8.979884f, 116.031631f),
            new("NChestS_2", -2.560478f, 8.153231f, 115.891487f),
            new("NStomachS_1", -9.784924f, -9.954099f, 99.871407f),
            new("NStomachS_2", -9.076771f, 7.395645f, 99.385635f),
            new("NChestF", 4.352207f, 0.990542f, 110.784447f),
            new("NStomachF", -2.270447f, -1.816215f, 95.587852f),
            new("NPelvisF", -9.550920f, -2.755259f, 79.839340f),
            new("NHeadS_1", 17.150383f, -8.589858f, 143.676453f),
            new("NHeadS_2", 18.824371f, 8.826989f, 143.926865f),
            new("NHeadF", 26.410934f, -0.664772f, 141.568832f),
            new("NPivot", -17.069702f, -0.231332f, 83.542564f),
            new("DetectorH", -12f, 0f, 0f),
            new("DetectorV", 56f, 0f, 100f),
            new("COM", -8.691902f, 0.692974f, 91.093567f),
            new("Camera", 150.255203f, -9.272315f, 85f),
        };

        public static int NodeCount => NodesOrdered.Length;

        public static readonly Dictionary<string, int> NodeIndexByName;
        public readonly struct EdgeIndex { public readonly int A, B; public EdgeIndex(int a, int b) { A = a; B = b; } }
        public static readonly EdgeIndex[] Connections;

        static MoveVisualizerData()
        {
            NodeIndexByName = new Dictionary<string, int>(NodesOrdered.Length);
            for (int i = 0; i < NodesOrdered.Length; i++)
                NodeIndexByName[NodesOrdered[i].Name] = i;

            Connections = new EdgeIndex[Edges.Count];
            int k = 0;
            foreach (var kv in Edges)
            {
                var e = kv.Value;
                Connections[k++] = new EdgeIndex(NodeIndexByName[e.End1], NodeIndexByName[e.End2]);
            }
        }

        // ================= MODEL ================= //

        public readonly struct NodeData
        {
            public readonly float X, Y, Z;
            public NodeData(float x, float y, float z) { X = x; Y = y; Z = z; }
        }

        public readonly struct EdgeData
        {
            public readonly string End1, End2;
            public EdgeData(string e1, string e2) { End1 = e1; End2 = e2; }
        }

        public readonly struct CapsuleData
        {
            public readonly string Edge;
            public readonly float Radius1, Radius2, Margin1, Margin2;
            public CapsuleData(string edge, float r1, float r2, float m1, float m2)
            {
                Edge = edge; Radius1 = r1; Radius2 = r2; Margin1 = m1; Margin2 = m2;
            }
        }

        public static readonly Dictionary<string, NodeData> Nodes = new()
        {
            { "NHip_1", new NodeData(-9.266768f, 132.108688f, 14.625448f) },
            { "NHip_2", new NodeData(8.142151f, 133.403122f, 15.850622f) },
            { "NStomach", new NodeData(-1.627623f, 149.958023f, 12.201712f) },
            { "NChest", new NodeData(-1.486196f, 167.446991f, 12.815082f) },
            { "NNeck", new NodeData(-0.803613f, 183.813950f, 18.972168f) },
            { "NShoulder_1", new NodeData(-18.007612f, 183.097305f, 22.096067f) },
            { "NShoulder_2", new NodeData(16.199888f, 182.932495f, 23.016190f) },
            { "NKnee_1", new NodeData(-29.557184f, 120.429588f, 55.955250f) },
            { "NKnee_2", new NodeData(29.024626f, 119.443993f, 56.165882f) },
            { "NAnkle_1", new NodeData(-22.967264f, 81.585693f, 49.047199f) },
            { "NAnkle_2", new NodeData(22.506222f, 80.445786f, 50.112480f) },
            { "NToe_1", new NodeData(-23.201233f, 71.768608f, 63.532295f) },
            { "NHeel_1", new NodeData(-19.979870f, 73.565262f, 43.875370f) },
            { "NToeTip_1", new NodeData(-24.501301f, 71.094330f, 71.397072f) },
            { "NToeS_1", new NodeData(-30.834454f, 69.845413f, 62.105610f) },
            { "NHeel_2", new NodeData(20.294479f, 72.530899f, 44.414925f) },
            { "NToe_2", new NodeData(21.977358f, 70.064621f, 64.190826f) },
            { "NToeTip_2", new NodeData(22.523663f, 68.491554f, 72.015572f) },
            { "NToeS_2", new NodeData(29.776386f, 68.503067f, 63.332390f) },
            { "NElbow_1", new NodeData(-38.712898f, 164.523544f, 33.334621f) },
            { "NElbow_2", new NodeData(38.409973f, 164.166992f, 30.403212f) },
            { "NWrist_1", new NodeData(-45.704407f, 178.673538f, 58.847263f) },
            { "NWrist_2", new NodeData(48.091419f, 176.208130f, 56.118561f) },
            { "NKnuckles_1", new NodeData(-51.318787f, 180.364136f, 66.947914f) },
            { "NFingertips_1", new NodeData(-44.118351f, 178.160477f, 60.367832f) },
            { "NKnucklesS_1", new NodeData(-48.693672f, 188.710739f, 67.025299f) },
            { "NKnuckles_2", new NodeData(54.853020f, 177.517822f, 63.368752f) },
            { "NFingertips_2", new NodeData(47.417149f, 175.712723f, 56.930622f) },
            { "NKnucklesS_2", new NodeData(51.353676f, 185.320389f, 65.222755f) },
            { "NHead", new NodeData(-1.192883f, 197.480972f, 29.895226f) },
            { "NTop", new NodeData(-1.149044f, 214.873169f, 31.835779f) },
            { "NChestS_1", new NodeData(-10.223847f, 167.655441f, 13.230432f) },
            { "NChestS_2", new NodeData(7.251459f, 167.239243f, 12.399446f) },
            { "NStomachS_1", new NodeData(-10.376495f, 150.033432f, 12.081932f) },
            { "NStomachS_2", new NodeData(7.121254f, 149.883530f, 12.321614f) },
            { "NChestF", new NodeData(-1.170832f, 164.356522f, 20.994890f) },
            { "NStomachF", new NodeData(-1.750011f, 149.652817f, 20.945570f) },
            { "NPelvisF", new NodeData(-1.276793f, 134.229523f, 23.833523f) },
            { "NHeadS_1", new NodeData(-9.942322f, 197.513657f, 29.801481f) },
            { "NHeadS_2", new NodeData(7.556548f, 197.448502f, 29.989195f) },
            { "NHeadF", new NodeData(-1.289771f, 196.511444f, 38.590878f) },
            { "NPivot", new NodeData(-0.562361f, 132.756699f, 15.238030f) },
            { "DetectorH", new NodeData(93.580025f, 218.531372f, -118.542747f) },
            { "DetectorV", new NodeData(97.452927f, 216.336563f, -109.165031f) },
            { "Camera", new NodeData(95.594078f, 221.359787f, -98.272850f) },
        };

        public static readonly Dictionary<string, EdgeData> Edges = new()
        {
            { "EPelvis_2", new EdgeData("NStomach", "NHip_2") },
            { "EPelvis_1", new EdgeData("NStomach", "NHip_1") },
            { "EGroin", new EdgeData("NHip_2", "NHip_1") },
            { "EStomach", new EdgeData("NChest", "NStomach") },
            { "EChest", new EdgeData("NNeck", "NChest") },
            { "EClavicle_1", new EdgeData("NShoulder_1", "NNeck") },
            { "EClavicle_2", new EdgeData("NShoulder_2", "NNeck") },
            { "EThigh_1", new EdgeData("NKnee_1", "NHip_1") },
            { "EThigh_2", new EdgeData("NKnee_2", "NHip_2") },
            { "ECalf_1", new EdgeData("NAnkle_1", "NKnee_1") },
            { "ECalf_2", new EdgeData("NAnkle_2", "NKnee_2") },
            { "EInstep_1", new EdgeData("NToe_1", "NAnkle_1") },
            { "EHeel_1", new EdgeData("NHeel_1", "NAnkle_1") },
            { "EFoot_1", new EdgeData("NHeel_1", "NToe_1") },
            { "EToe_1", new EdgeData("NToe_1", "NToeTip_1") },
            { "EToeC_1", new EdgeData("NToe_1", "NToeS_1") },
            { "EToeS_1", new EdgeData("NToeTip_1", "NToeS_1") },
            { "EFootS_1", new EdgeData("NHeel_1", "NToeS_1") },
            { "EInstepS_1", new EdgeData("NAnkle_1", "NToeS_1") },
            { "EHeep_2", new EdgeData("NHeel_2", "NAnkle_2") },
            { "EInstep_2", new EdgeData("NToe_2", "NAnkle_2") },
            { "EFoot_2", new EdgeData("NToe_2", "NHeel_2") },
            { "EToe_2", new EdgeData("NToeTip_2", "NToe_2") },
            { "EToeC_2", new EdgeData("NToe_2", "NToeS_2") },
            { "EToeS_2", new EdgeData("NToeTip_2", "NToeS_2") },
            { "EFootS_2", new EdgeData("NToeS_2", "NHeel_2") },
            { "EInstepS_2", new EdgeData("NToeS_2", "NAnkle_2") },
            { "EArm_1", new EdgeData("NElbow_1", "NShoulder_1") },
            { "EForearm_1", new EdgeData("NWrist_1", "NElbow_1") },
            { "EHand_1", new EdgeData("NKnuckles_1", "NWrist_1") },
            { "EFingers_1", new EdgeData("NFingertips_1", "NKnuckles_1") },
            { "EHandC_1", new EdgeData("NKnuckles_1", "NKnucklesS_1") },
            { "EHandS_1", new EdgeData("NKnucklesS_1", "NWrist_1") },
            { "EFingersS_1", new EdgeData("NFingertips_1", "NKnucklesS_1") },
            { "EArm_2", new EdgeData("NElbow_2", "NShoulder_2") },
            { "EForearm_2", new EdgeData("NWrist_2", "NElbow_2") },
            { "EHand_2", new EdgeData("NKnuckles_2", "NWrist_2") },
            { "EFingers_2", new EdgeData("NFingertips_2", "NKnuckles_2") },
            { "EHandC_2", new EdgeData("NKnucklesS_2", "NKnuckles_2") },
            { "EHandS_2", new EdgeData("NKnucklesS_2", "NWrist_2") },
            { "EFingersS_2", new EdgeData("NFingertips_2", "NKnucklesS_2") },
            { "ENeck", new EdgeData("NNeck", "NHead") },
            { "EHead", new EdgeData("NHead", "NTop") },
            { "EChestHS_1", new EdgeData("NChest", "NChestS_1") },
            { "EChestHS_2", new EdgeData("NChestS_2", "NChest") },
            { "EStomachHS_1", new EdgeData("NStomach", "NStomachS_1") },
            { "EStomachHS_2", new EdgeData("NStomach", "NStomachS_2") },
            { "EChestS_1", new EdgeData("NNeck", "NChestS_1") },
            { "EChestS_2", new EdgeData("NChestS_2", "NNeck") },
            { "EStomachS_1", new EdgeData("NStomachS_1", "NChest") },
            { "EStomachS_2", new EdgeData("NStomachS_2", "NChest") },
            { "EChestH", new EdgeData("NChestS_2", "NChestS_1") },
            { "EStomachH", new EdgeData("NStomachS_2", "NStomachS_1") },
            { "EChestHD_1", new EdgeData("NChestS_1", "NChestF") },
            { "EChestHD_2", new EdgeData("NChestS_2", "NChestF") },
            { "EStomachHD_1", new EdgeData("NStomachF", "NStomachS_1") },
            { "EStomachHD_2", new EdgeData("NStomachF", "NStomachS_2") },
            { "EChestF", new EdgeData("NChestF", "NNeck") },
            { "EStomachF", new EdgeData("NStomachF", "NChest") },
            { "EChestHF", new EdgeData("NChest", "NChestF") },
            { "EStomachHF", new EdgeData("NStomach", "NStomachF") },
            { "EPelvisHD_1", new EdgeData("NPelvisF", "NHip_1") },
            { "EPelvisHD_2", new EdgeData("NHip_2", "NPelvisF") },
            { "EPelvisF", new EdgeData("NStomach", "NPelvisF") },
            { "EHeadHS_1", new EdgeData("NHead", "NHeadS_1") },
            { "EHeadHS_2", new EdgeData("NHeadS_2", "NHead") },
            { "EHeadS_1", new EdgeData("NTop", "NHeadS_1") },
            { "EHeadS_2", new EdgeData("NHeadS_2", "NTop") },
            { "EHeadH", new EdgeData("NHeadS_1", "NHeadS_2") },
            { "EHeadHF", new EdgeData("NHeadF", "NHead") },
            { "EHeadHD_1", new EdgeData("NHeadF", "NHeadS_1") },
            { "EHeadHD_2", new EdgeData("NHeadS_2", "NHeadF") },
            { "EHeadF", new EdgeData("NHeadF", "NTop") },
            { "EPelvisM", new EdgeData("NStomach", "NPivot") },
            { "EPelvisF_PelvisToPivot", new EdgeData("NPelvisF", "NPivot") },
            { "EPelvisHS_1", new EdgeData("NHip_2", "NPivot") },
            { "EPelvisHS_2", new EdgeData("NHip_1", "NPivot") },
        };

        public static readonly Dictionary<string, CapsuleData> Capsules = new()
        {
            { "Capsule_EToeS_1", new CapsuleData("EToeS_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EToeC_1", new CapsuleData("EToeC_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EInstepS_1", new CapsuleData("EInstepS_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EFootS_1", new CapsuleData("EFootS_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EToe_1", new CapsuleData("EToe_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EInstep_1", new CapsuleData("EInstep_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EHandC_1", new CapsuleData("EHandC_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EFoot_1", new CapsuleData("EFoot_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EHand_1", new CapsuleData("EHand_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EFingers_1", new CapsuleData("EFingers_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_ECalf_1", new CapsuleData("ECalf_1", 6.0f, 6.0f, 0.0f, 0.699999988079071f) },
            { "Capsule_EHeel_1", new CapsuleData("EHeel_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EHandS_1", new CapsuleData("EHandS_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EFingersS_1", new CapsuleData("EFingersS_1", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule-1_ECalf_1", new CapsuleData("ECalf_1", 10.0f, 10.0f, 0.5f, 0.0f) },
            { "Capsule_EForearm_1", new CapsuleData("EForearm_1", 5.0f, 5.0f, 0.0f, 0.699999988079071f) },
            { "Capsule-1_EForearm_1", new CapsuleData("EForearm_1", 7.0f, 7.0f, 0.5f, 0.0f) },
            { "Capsule_EThigh_1", new CapsuleData("EThigh_1", 10.0f, 10.0f, 0.0f, 0.699999988079071f) },
            { "Capsule_EArm_1", new CapsuleData("EArm_1", 7.0f, 7.0f, 0.0f, 0.699999988079071f) },
            { "Capsule-1_EThigh_1", new CapsuleData("EThigh_1", 12.0f, 12.0f, 0.400000005960464f, 0.0f) },
            { "Capsule-1_EArm_1", new CapsuleData("EArm_1", 10.0f, 10.0f, 0.5f, 0.0f) },
            { "Capsule_Muscle117", new CapsuleData("Muscle117", 8.0f, 8.0f, 0.0f, 0.0f) },
            { "Capsule_Muscle116", new CapsuleData("Muscle116", 10.0f, 10.0f, 0.0f, 0.0f) },
            { "Capsule_Muscle115", new CapsuleData("Muscle115", 10.0f, 10.0f, 0.0f, 0.0f) },
            { "Capsule_EGroin", new CapsuleData("EGroin", 12.0f, 12.0f, 0.0f, 0.0f) },
            { "Capsule_EToe_2", new CapsuleData("EToe_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EPelvisM", new CapsuleData("EPelvisM", 12.0f, 12.0f, 0.0f, 0.0f) },
            { "Capsule_EFoot_2", new CapsuleData("EFoot_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EClavicle_1", new CapsuleData("EClavicle_1", 10.0f, 10.0f, 0.0f, 0.0f) },
            { "Capsule_EInstep_2", new CapsuleData("EInstep_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EHeep_2", new CapsuleData("EHeep_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EToeS_2", new CapsuleData("EToeS_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EToeC_2", new CapsuleData("EToeC_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EFootS_2", new CapsuleData("EFootS_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EStomach", new CapsuleData("EStomach", 13.0f, 13.0f, 0.0f, 0.0f) },
            { "Capsule_EInstepS_2", new CapsuleData("EInstepS_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_ECalf_2", new CapsuleData("ECalf_2", 6.0f, 6.0f, 0.0f, 0.699999988079071f) },
            { "Capsule_EChest", new CapsuleData("EChest", 13.0f, 13.0f, 0.200000002980232f, 0.0f) },
            { "Capsule_Muscle120", new CapsuleData("Muscle120", 8.0f, 8.0f, 0.0f, 0.0f) },
            { "Capsule-1_EThigh_2", new CapsuleData("EThigh_2", 12.0f, 12.0f, 0.400000005960464f, 0.0f) },
            { "Capsule_ENeck", new CapsuleData("ENeck", 5.0f, 5.0f, 0.0f, 0.0f) },
            { "Capsule_Muscle119", new CapsuleData("Muscle119", 10.0f, 10.0f, 0.0f, 0.0f) },
            { "Capsule_EHead", new CapsuleData("EHead", 12.0f, 12.0f, 0.0f, 0.5f) },
            { "Capsule-1_ECalf_2", new CapsuleData("ECalf_2", 10.0f, 10.0f, 0.5f, 0.0f) },
            { "Capsule_EThigh_2", new CapsuleData("EThigh_2", 10.0f, 10.0f, 0.0f, 0.699999988079071f) },
            { "Capsule_EClavicle_2", new CapsuleData("EClavicle_2", 10.0f, 10.0f, 0.0f, 0.0f) },
            { "Capsule_Muscle118", new CapsuleData("Muscle118", 10.0f, 10.0f, 0.0f, 0.0f) },
            { "Capsule-1_EArm_2", new CapsuleData("EArm_2", 10.0f, 10.0f, 0.5f, 0.0f) },
            { "Capsule_EArm_2", new CapsuleData("EArm_2", 7.0f, 7.0f, 0.0f, 0.699999988079071f) },
            { "Capsule-1_EForearm_2", new CapsuleData("EForearm_2", 7.0f, 7.0f, 0.5f, 0.0f) },
            { "Capsule_EForearm_2", new CapsuleData("EForearm_2", 5.0f, 5.0f, 0.0f, 0.699999988079071f) },
            { "Capsule_EFingersS_2", new CapsuleData("EFingersS_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EHandS_2", new CapsuleData("EHandS_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EFingers_2", new CapsuleData("EFingers_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EHand_2", new CapsuleData("EHand_2", 3.5f, 3.5f, 0.0f, 0.0f) },
            { "Capsule_EHandC_2", new CapsuleData("EHandC_2", 3.5f, 3.5f, 0.0f, 0.0f) },
        };
    }
}
