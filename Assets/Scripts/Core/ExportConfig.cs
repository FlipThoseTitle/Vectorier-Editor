using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vectorier.Core
{
    [DisallowMultipleComponent]
    public class ExportConfig : MonoBehaviour
    {
        public enum ExportType { Level, Objects, Buildings }

        [Serializable]
        public class ModelDefinition
        {
            public string name = "NewModel";
            public int type = 0;
            public string color = "0";
            public string birthSpawn = "DefaultSpawn";
            public int ai = 0;
            public float time = 0f;

            public string forceBlasts = "";
            public bool trick = false;
            public bool victory = false;
            public bool lose = false;
            public string respawns = "";
            public string allowedSpawns = "";
            public string skins = "";
            public string murders = "";
            public string arrests = "";
            public bool item = false;
            public bool icon = false;
            public string stocks = "";
        }

        public const string DefaultCommonModeModels =
            @"<Model Name=""Player"" Type=""1"" Color=""0"" BirthSpawn=""DefaultSpawn"" AI=""0"" Time=""0"" Respawns=""Hunter"" ForceBlasts=""Hunter"" Trick=""1"" Item=""1"" Victory=""1"" Lose=""1""/>
<Model Name=""Hunter"" Type=""0"" Color=""0"" BirthSpawn=""DefaultSpawn"" AI=""1"" Time=""1.5"" AllowedSpawns=""Respawn"" Skins=""hunter"" Murders=""Player"" Arrests=""Player"" Icon=""1""/>";

        public const string DefaultHunterModeModels =
            @"<Model Name=""Player"" Type=""0"" Color=""0"" BirthSpawn=""DefaultSpawn"" AI=""5"" Time=""0"" Victory=""1"" Respawns=""Hunter""/>
<Model Name=""Hunter"" Type=""1"" Color=""0"" BirthSpawn=""DefaultSpawn"" AI=""0"" Time=""1.5"" Trick=""1"" Item=""1"" Skins=""hunter"" Murders=""Player"" Arrests=""Player"" Lose=""1"" AllowedSpawns=""Despawn""/>";

        public ExportType exportType = ExportType.Level;

        // Common
        public string filePathDirectory = "";
        public string fileName = "";
        public bool fastBuild = false;
        public bool exportAsXML = false;

        // Sets
        public List<string> citySets = new List<string>();
        public List<string> groundSets = new List<string>();
        public List<string> librarySets = new List<string>();

        // Level-only
        public string musicName = "music_dinamic";
        public float musicVolume = 0.3f;

        public string commonModeModels = DefaultCommonModeModels;
        public string hunterModeModels = DefaultHunterModeModels;

        public int coinValue = 50;

        // Editor-only helper data for model editor
        public List<ModelDefinition> commonModeModelDefinitions = new List<ModelDefinition>();
        public List<ModelDefinition> hunterModeModelDefinitions = new List<ModelDefinition>();
    }
}