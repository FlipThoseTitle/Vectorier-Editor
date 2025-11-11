using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine;
using Vectorier.XML;

namespace Vectorier.Dynamic
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Dynamic/Dynamic Transform")]
    public class DynamicTransform : MonoBehaviour
    {
        [SerializeField]
        private List<TransformationContainer> transformations = new();
        public List<TransformationContainer> Transformations => transformations;

        [Serializable]
        public class TransformationContainer
        {
            public string name;
            public List<MoveData> moves = new();
            public List<SizeData> sizes = new();
            public List<RotateData> rotations = new();
            public List<ColorData> colors = new();
        }

        [Serializable]
        public class MoveData
        {
            public List<MoveInterval> intervals = new();
        }

        [Serializable]
        public class MoveInterval
        {
            public int framesToMove;
            public float delay;

            public MovePoint start = new(Vector2.zero);
            public MovePoint support = new(Vector2.zero);
            public MovePoint finish = new(Vector2.zero);
        }

        [Serializable]
        public class MovePoint
        {
            public Vector2 position;

            public MovePoint(Vector2 position)
            {
                this.position = position;
            }
        }

        [Serializable]
        public class SizeData
        {
            public int frames;
            public float finalWidth;
            public float finalHeight;
        }

        [Serializable]
        public class RotateData
        {
            public float angle;
            public Vector2 anchor;
            public int frames;
        }

        [Serializable]
        public class ColorData
        {
            public Color colorStart = Color.white;
            public Color colorFinish = Color.white;
            public int frames;
        }

        // -------------------------------------------------------------------
        // XML Writer
        // -------------------------------------------------------------------
        public XmlElement WriteToXML(XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (xmlUtility == null || parentElement == null || transformations.Count == 0)
                return null;

            XmlElement dynamicElement = xmlUtility.GetOrCreateElement(parentElement, "Dynamic");

            foreach (var t in transformations)
            {
                XmlElement transformElem = xmlUtility.AddElement(dynamicElement, "Transformation");
                xmlUtility.SetAttribute(transformElem, "Name", t.name);

                // --- Move ---
                foreach (var move in t.moves)
                {
                    XmlElement moveElem = xmlUtility.AddElement(transformElem, "Move");

                    for (int i = 0; i < move.intervals.Count; i++)
                    {
                        var interval = move.intervals[i];
                        XmlElement intervalElem = xmlUtility.AddElement(moveElem, "MoveInterval");
                        xmlUtility.SetAttribute(intervalElem, "Number", i + 1);
                        xmlUtility.SetAttribute(intervalElem, "FramesToMove", interval.framesToMove);
                        xmlUtility.SetAttribute(intervalElem, "Delay", interval.delay.ToString("F1", CultureInfo.InvariantCulture));

                        // Start
                        XmlElement startElem = xmlUtility.AddElement(intervalElem, "Point");
                        xmlUtility.SetAttribute(startElem, "Name", "Start");
                        xmlUtility.SetAttribute(startElem, "X", 0.0f.ToString(CultureInfo.InvariantCulture));
                        xmlUtility.SetAttribute(startElem, "Y", 0.0f.ToString(CultureInfo.InvariantCulture));

                        // Support
                        XmlElement supportElem = xmlUtility.AddElement(intervalElem, "Point");
                        xmlUtility.SetAttribute(supportElem, "Name", "Support");
                        xmlUtility.SetAttribute(supportElem, "Number", 1);
                        xmlUtility.SetAttribute(supportElem, "X", interval.support.position.x.ToString(CultureInfo.InvariantCulture));
                        xmlUtility.SetAttribute(supportElem, "Y", interval.support.position.y.ToString(CultureInfo.InvariantCulture));

                        // Finish
                        XmlElement finishElem = xmlUtility.AddElement(intervalElem, "Point");
                        xmlUtility.SetAttribute(finishElem, "Name", "Finish");
                        xmlUtility.SetAttribute(finishElem, "X", interval.finish.position.x.ToString(CultureInfo.InvariantCulture));
                        xmlUtility.SetAttribute(finishElem, "Y", interval.finish.position.y.ToString(CultureInfo.InvariantCulture));
                    }
                }

                // --- Size ---
                foreach (var s in t.sizes)
                {
                    XmlElement sizeElem = xmlUtility.AddElement(transformElem, "Size");
                    xmlUtility.SetAttribute(sizeElem, "Frames", s.frames);
                    xmlUtility.SetAttribute(sizeElem, "FinalWidth", s.finalWidth.ToString(CultureInfo.InvariantCulture));
                    xmlUtility.SetAttribute(sizeElem, "FinalHeight", s.finalHeight.ToString(CultureInfo.InvariantCulture));
                }

                // --- Rotation ---
                foreach (var r in t.rotations)
                {
                    XmlElement rotElem = xmlUtility.AddElement(transformElem, "Rotation");
                    xmlUtility.SetAttribute(rotElem, "Angle", r.angle.ToString(CultureInfo.InvariantCulture));
                    xmlUtility.SetAttribute(rotElem, "Anchor", $"{r.anchor.x.ToString(CultureInfo.InvariantCulture)}|{r.anchor.y.ToString(CultureInfo.InvariantCulture)}");
                    xmlUtility.SetAttribute(rotElem, "Frames", r.frames);
                }

                // --- Color ---
                foreach (var c in t.colors)
                {
                    XmlElement colorElem = xmlUtility.AddElement(transformElem, "Color");
                    xmlUtility.SetAttribute(colorElem, "ColorStart", "#" + ColorUtility.ToHtmlStringRGBA(c.colorStart));
                    xmlUtility.SetAttribute(colorElem, "ColorFinish", "#" + ColorUtility.ToHtmlStringRGBA(c.colorFinish));
                    xmlUtility.SetAttribute(colorElem, "Frames", c.frames);
                }
            }

            return dynamicElement;
        }
    }
}
