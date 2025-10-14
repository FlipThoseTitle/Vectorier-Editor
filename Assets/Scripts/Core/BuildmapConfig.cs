using System.Collections.Generic;
using UnityEngine;

namespace Vectorier.Core
{
    [System.Serializable]
    public class BuildmapConfig : ScriptableObject
    {
        public enum ExportType { Level, Objects }

        public ExportType exportType = ExportType.Level;

        // Common
        public string filePathDirectory = "";
        public string levelName = "";
        public bool fastBuild = false;

        // Sets
        public List<string> citySets = new List<string>();
        public List<string> groundSets = new List<string>();
        public List<string> librarySets = new List<string>();

        // Level-only
        public string musicName = "";
        public float musicVolume = 0.3f;
        public string commonModeModels = "";
        public string hunterModeModels = "";
        public int coinValue = 0;
    }
}
