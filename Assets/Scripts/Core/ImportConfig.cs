using System.Collections.Generic;
using UnityEngine;

namespace Vectorier.Core
{
    public class ImportConfig : ScriptableObject
    {
        public enum ImportType
        {
            Level,
            Objects,
            Buildings
        }

        public string filePathDirectory = "";
        public string xmlName = "";
        public string selectedObject = "";
        public bool untagChildren = false;
        public List<string> textureFolders = new List<string>();
    }
}
