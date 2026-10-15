using System.Collections.Generic;
using UnityEngine;

namespace Vectorier.Parallax
{
    public class ParallaxLayerStore : ScriptableObject
    {
        public List<Vector3> positions = new List<Vector3>();
        public List<Vector3> scales = new List<Vector3>();
    }
}