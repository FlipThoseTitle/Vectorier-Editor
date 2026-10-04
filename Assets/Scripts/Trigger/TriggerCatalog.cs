using System.Collections.Generic;

namespace Vectorier.Trigger
{
    // how a parameter wants to be edited, the value itself is always a TValue
    public enum ParamKind { Value, Text, Int, VarName }

    public class ParamDef
    {
        public string attr;
        public string label;
        public ParamKind kind;
        public string[] choices;    // quick picks shown on the little dropdown
        public string def;
        public bool optional;
        public string hint;

        public ParamDef(string attr, string label, ParamKind kind = ParamKind.Value,
                        string def = "", string[] choices = null, string hint = "", bool optional = false)
        {
            this.attr = attr;
            this.label = label;
            this.kind = kind;
            this.def = def;
            this.choices = choices;
            this.hint = hint;
            this.optional = optional;
        }
    }

    public class EntryDef
    {
        public string id;       // unique, usually the tag. camera variants are "Camera.Follow" etc
        public string tag;      // the xml element that gets written
        public string title;    // what the user reads
        public string hint;
        public string menu;     // "Group/Label" path in the add menu
        public ParamDef[] ps;

        public EntryDef(string id, string tag, string title, string menu, string hint, params ParamDef[] ps)
        {
            this.id = id;
            this.tag = tag;
            this.title = title;
            this.menu = menu;
            this.hint = hint;
            this.ps = ps ?? new ParamDef[0];
        }
    }

    public static class TriggerCatalog
    {
        public static readonly string[] Keys = { "Up", "Down", "Left", "Right" };
        public static readonly string[] Bools = { "0", "1" };
        public static readonly string[] Results = { "Win", "Loss", "Death" };
        public static readonly string[] Styles = { "None", "Fade", "Blink", "Scale" };
        public static readonly string[] Switches = { "On", "Off" };
        public static readonly string[] Orientations = { "Vertical", "Horizontal" };

        public static readonly string[] ModelProps =
        {
            "direction", "isControlled", "isCameraFollow", "animationName", "animationFrame", "condition", "AI"
        };

        public static readonly string[] NodeCoords =
        {
            "worldPositionX", "worldPositionY", "localPositionX", "localPositionY"
        };

        public static readonly string[] CommonModels = { "Player", "Hunter", "Helper" };
        public static readonly string[] CommonNodes = { "COM", "DetectorH", "Head", "Foot" };

        // variables the trigger system hands you, they are never declared in Init
        public static readonly string[] BuiltInVars = { "$Model", "$Key", "$ActionID" };

        // ---------------- Events ----------------

        public static readonly EntryDef[] Events =
        {
            new EntryDef("Enter", "Enter", "Model Enters the Trigger", "Model Enters the Trigger",
                "Fires once when a model's node crosses into the trigger area."),

            new EntryDef("Exit", "Exit", "Model Leaves the Trigger", "Model Leaves the Trigger",
                "Fires once when the model leaves the trigger area."),

            new EntryDef("Collision", "Collision", "Model is Touching the Trigger", "Model is Touching the Trigger",
                "Same as Enter."),

            new EntryDef("KeyPressed", "KeyPressed", "A key is Pressed", "A key is Pressed",
                "Fires on arrow keys press inside the Trigger."),

            new EntryDef("Timeout", "Timeout", "Timer Finished", "Timer Finished",
                "Fires when the timer started by the 'Start Timer' action runs out."),

            new EntryDef("Activate", "Activate", "Signal Received", "Signal Received",
                "Fires when another loop sends a signal. Compare $ActionID in 'Only If' to pick which one.",
                new ParamDef("ActionID", "Only this signal", ParamKind.Value, "", null,
                    "Leave empty to catch every signal and filter it in 'Only If'.", true)),

            new EntryDef("ValueChange", "ValueChange", "A Value Changes", "A Value Changes",
                "Fires whenever the value becomes different.",
                new ParamDef("Value", "Watch", ParamKind.Value, "", null, "Usually a model property, e.g. Player's animation name.")),

            new EntryDef("OnShow", "OnShow", "Trigger Appears on Screen", "Trigger Appears on Screen", ""),
            new EntryDef("OnHide", "OnHide", "Trigger Leaves the Screen", "Trigger Leaves the Screen", ""),

            new EntryDef("OnStartGame", "OnStartGame", "Level Starts", "Level Starts",
                "Fires once when the level loads."),

            new EntryDef("Line", "Line", "Model Crosses a Line", "Model Crosses a Line", "",
                new ParamDef("Type", "Line", ParamKind.Text, "Vertical", Orientations),
                new ParamDef("Position", "Position", ParamKind.Int, "0")),
        };

        // ---------------- Actions ----------------

        public static readonly EntryDef[] Actions =
        {
            // model & animation
            new EntryDef("ForceAnimation", "ForceAnimation", "Force Animation", "Model/Force Animation",
                "Play Animation on a Model.",
                new ParamDef("Name", "Animation", ParamKind.Value, "RunForward"),
                new ParamDef("Model", "On model", ParamKind.Value, "_$Model", CommonModels),
                new ParamDef("Frame", "Start frame", ParamKind.Int, "1"),
                new ParamDef("Reversed", "Reversed", ParamKind.Value, "0", Bools, "", true)),

            new EntryDef("ModelExecute", "ModelExecute", "Play Object Animation", "Model/Play Object Animation",
                "Runs an animation on the object this Trigger belongs to.",
                new ParamDef("AnimName", "Animation", ParamKind.Value, "_AnimName"),
                new ParamDef("AnimFrame", "Frame", ParamKind.Value, "_AnimFrame")),

            new EntryDef("Kill", "Kill", "Kill Model", "Model/Kill Model", "",
                new ParamDef("Model", "Model", ParamKind.Value, "_$Model", CommonModels)),

            new EntryDef("Control", "Control", "Player Control On/Off", "Model/Player Control On Off",
                "Off takes the controls away from the Player, On gives them back.",
                new ParamDef("Switch", "Control", ParamKind.Text, "Off", Switches),
                new ParamDef("Model", "Model", ParamKind.Value, "_$Model", CommonModels)),

            new EntryDef("Press", "Press", "Press a Key for the model", "Model/Press a Key for the model",
                "Makes the model act as if that key was pressed, used to control bots.",
                new ParamDef("Key", "Key", ParamKind.Value, "Up", Keys),
                new ParamDef("Model", "Model", ParamKind.Value, "_$Model", CommonModels)),

            new EntryDef("Spawn", "Spawn", "Spawn Model", "Model/Spawn Model", "",
                new ParamDef("Model", "Model", ParamKind.Value, "Hunter", CommonModels),
                new ParamDef("Spawn", "At spawn point", ParamKind.Text, "Respawn")),

            // camera, all of these write a <Camera> with one attribute
            new EntryDef("Camera.Follow", "Camera", "Camera Follows Model", "Camera/Follow Model", "",
                new ParamDef("Follow", "Model", ParamKind.Value, "_$Model", CommonModels)),

            new EntryDef("Camera.Zoom", "Camera", "Camera Zoom", "Camera/Zoom", "",
                new ParamDef("Zoom", "Zoom", ParamKind.Text, "1")),

            new EntryDef("Camera.Smoothness", "Camera", "Camera Smoothness", "Camera/Smoothness",
                "Higher is lazier, the camera lags further behind the model.",
                new ParamDef("Smoothness", "Smoothness", ParamKind.Int, "100")),

            new EntryDef("Camera.Stop", "Camera", "Camera Stops Following", "Camera/Stop Following", "",
                new ParamDef("Stop", "Stop", ParamKind.Text, "1", Bools)),

            // variables
            new EntryDef("SetVariable", "SetVariable", "Set Variable", "Variables/Set Variable", "",
                new ParamDef("Name", "Variable", ParamKind.VarName, "Flag1"),
                new ParamDef("Value", "To", ParamKind.Value, "1")),

            new EntryDef("AppendValue", "AppendValue", "Add to Variable", "Variables/Add to Variable",
                "Adds to a number, or sticks text onto the end of a string.",
                new ParamDef("Name", "Variable", ParamKind.VarName, "Flag1"),
                new ParamDef("Value", "Add", ParamKind.Value, "1")),

            // flow
            new EntryDef("Wait", "Wait", "Wait", "Flow/Wait",
                "Holds the rest of the actions for this many frames.",
                new ParamDef("Frames", "Frames", ParamKind.Int, "30")),

            new EntryDef("SetTimer", "SetTimer", "Start Timer", "Flow/Start Timer",
                "Starts the countdown that the 'Timer finished' event listens for.",
                new ParamDef("Frames", "Frames", ParamKind.Int, "60")),

            new EntryDef("Activate", "Activate", "Send Signal", "Flow/Send Signal",
                "Wakes up any loop listening for this signal, here or in another trigger.",
                new ParamDef("ActionID", "Signal", ParamKind.Value, "MySignal")),

            // object & world
            new EntryDef("Transform", "Transform", "Play Transformation", "Object/Play Transformation",
                "Runs a transformation defined on the object, by name.",
                new ParamDef("Name", "Transformation", ParamKind.Value, "")),

            new EntryDef("Execute", "Execute", "Execute This Object", "Object/Execute This Object",
                "Runs whatever behaviour the trigger's own object has."),

            new EntryDef("Use", "Use", "Use Item", "Object/Use Item", "",
                new ParamDef("Object", "Item", ParamKind.Value, "")),

            new EntryDef("Sound", "Sound", "Play Sound", "Game/Play Sound", "",
                new ParamDef("Name", "Sound", ParamKind.Value, "")),

            new EntryDef("MessageOnScreen", "MessageOnScreen", "Show Message", "Game/Show Message", "",
                new ParamDef("Text", "Text", ParamKind.Text, "Hello World!"),
                new ParamDef("Color", "Color", ParamKind.Text, "#FFFFFFFF"),
                new ParamDef("Frames", "Frames", ParamKind.Int, "120"),
                new ParamDef("AppearStyle", "Appear", ParamKind.Text, "Fade", Styles),
                new ParamDef("DisappearStyle", "Disappear", ParamKind.Text, "Fade", Styles)),

            new EntryDef("EndGame", "EndGame", "End The Level", "Game/End The Level", "",
                new ParamDef("Result", "Result", ParamKind.Text, "Win", Results),
                new ParamDef("Model", "Model", ParamKind.Value, "_$Model", CommonModels),
                new ParamDef("Frames", "Frames", ParamKind.Int, "120")),
        };

        public static EntryDef FindEvent(string id)
        {
            for (int i = 0; i < Events.Length; i++) if (Events[i].id == id) return Events[i];
            return null;
        }

        public static EntryDef FindAction(string id)
        {
            for (int i = 0; i < Actions.Length; i++) if (Actions[i].id == id) return Actions[i];
            return null;
        }

        // <Camera> carries one attribute at a time, so the attribute picks the entry
        public static EntryDef MatchAction(string tag, IEnumerable<string> attrNames)
        {
            if (tag == "Camera")
            {
                foreach (var a in attrNames)
                {
                    var d = FindAction("Camera." + a);
                    if (d != null) return d;
                }
                return FindAction("Camera.Follow");
            }
            return FindAction(tag);
        }
    }
}