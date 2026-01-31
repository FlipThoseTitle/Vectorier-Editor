using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine;
using Vectorier.XML;

namespace Vectorier.Dynamic
{
    [AddComponentMenu("Vectorier/Dynamic/Transformation")]
    public class DynamicTransform : MonoBehaviour
    {
        public string transformationName;

        public List<MoveData> moves = new();
        public List<SizeData> sizes = new();
        public List<RotateData> rotations = new();
        public List<ColorData> colors = new();

        [Serializable]
        public class MoveData
        {
            public int frames;         // frame
            public float delay;        // frame

            public Vector2 move;      // ordered pair
            public Vector2 support;   // ordered pair

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
        public class ColorData : ISerializationCallbackReceiver
        {
            private bool _init;
            public Color colorStart = Color.white;
            public Color colorFinish = Color.white;
            public int frames;
            public void EnsureDefaults()
            {
                if (_init) return;
                colorStart = Color.white;
                colorFinish = Color.white;
                _init = true;
            }
            public void OnAfterDeserialize() => EnsureDefaults();
            public void OnBeforeSerialize() { }
        }

        private void OnValidate()
        {
            for (int i = 0; i < colors.Count; i++)
            {
                if (colors[i] == null) colors[i] = new ColorData();
                colors[i].EnsureDefaults();
            }
        }

        // ================= XML Writer ================= //

        public XmlElement WriteToXML(XmlUtility xmlUtility, XmlElement parentElement)
        {
            if (xmlUtility == null || parentElement == null)
                return null;

            XmlElement dynamicElement = xmlUtility.GetOrCreateElement(parentElement, "Dynamic");
            XmlElement transformElement = xmlUtility.AddElement(dynamicElement, "Transformation");
            xmlUtility.SetAttribute(transformElement, "Name", transformationName);

            // -------- MOVE --------
            XmlElement moveElement = xmlUtility.AddElement(transformElement, "Move");

            for (int i = 0; i < moves.Count; i++)
            {
                var interval = moves[i];

                XmlElement intervalElem = xmlUtility.AddElement(moveElement, "MoveInterval");
                xmlUtility.SetAttribute(intervalElem, "Number", i + 1);
                xmlUtility.SetAttribute(intervalElem, "FramesToMove", interval.frames);
                xmlUtility.SetAttribute(intervalElem, "Delay", interval.delay);

                // Start (always 0,0)
                XmlElement startElem = xmlUtility.AddElement(intervalElem, "Point");
                xmlUtility.SetAttribute(startElem, "Name", "Start");
                xmlUtility.SetAttribute(startElem, "X", "0");
                xmlUtility.SetAttribute(startElem, "Y", "0");

                // Support
                XmlElement supportElem = xmlUtility.AddElement(intervalElem, "Point");
                xmlUtility.SetAttribute(supportElem, "Name", "Support");
                xmlUtility.SetAttribute(supportElem, "Number", 1);
                xmlUtility.SetAttribute(supportElem, "X", interval.support.x.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(supportElem, "Y", (-interval.support.y).ToString(CultureInfo.InvariantCulture));

                // Finish
                XmlElement finishElem = xmlUtility.AddElement(intervalElem, "Point");
                xmlUtility.SetAttribute(finishElem, "Name", "Finish");
                xmlUtility.SetAttribute(finishElem, "X", interval.move.x.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(finishElem, "Y", (-interval.move.y).ToString(CultureInfo.InvariantCulture));
            }

            // -------- SIZE --------
            foreach (var size in sizes)
            {
                XmlElement sizeElem = xmlUtility.AddElement(transformElement, "Size");
                xmlUtility.SetAttribute(sizeElem, "Frames", size.frames);
                xmlUtility.SetAttribute(sizeElem, "FinalWidth", size.finalWidth.ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(sizeElem, "FinalHeight", size.finalHeight.ToString(CultureInfo.InvariantCulture));
            }

            // -------- ROTATION --------
            foreach (var rotation in rotations)
            {
                XmlElement rotElem = xmlUtility.AddElement(transformElement, "Rotation");
                xmlUtility.SetAttribute(rotElem, "Angle", (-rotation.angle).ToString(CultureInfo.InvariantCulture));
                xmlUtility.SetAttribute(rotElem, "Anchor", $"{rotation.anchor.x.ToString(CultureInfo.InvariantCulture)}|{rotation.anchor.y.ToString(CultureInfo.InvariantCulture)}");
                xmlUtility.SetAttribute(rotElem, "Frames", rotation.frames);
            }

            // -------- COLOR --------
            foreach (var color in colors)
            {
                XmlElement colorElem = xmlUtility.AddElement(transformElement, "Color");
                xmlUtility.SetAttribute(colorElem, "ColorStart", "#" + ColorUtility.ToHtmlStringRGBA(color.colorStart));
                xmlUtility.SetAttribute(colorElem, "ColorFinish", "#" + ColorUtility.ToHtmlStringRGBA(color.colorFinish));
                xmlUtility.SetAttribute(colorElem, "Frames", color.frames);
            }

            return dynamicElement;
        }

        public static DynamicTransform WriteToScene(XmlElement transformationElement, GameObject gameObject)
        {
            if (transformationElement == null || gameObject == null)
                return null;

            DynamicTransform dynamic = gameObject.AddComponent<DynamicTransform>();

            dynamic.transformationName = transformationElement.GetAttribute("Name");
            dynamic.moves.Clear();
            dynamic.sizes.Clear();
            dynamic.rotations.Clear();
            dynamic.colors.Clear();

            // -------- MOVE --------
            XmlElement moveElement = transformationElement["Move"];

            if (moveElement != null)
            {
                foreach (XmlElement intervalElement in moveElement.GetElementsByTagName("MoveInterval"))
                {
                    MoveData move = new MoveData();

                    int frames = int.Parse(intervalElement.GetAttribute("FramesToMove"));
                    move.frames = frames;

                    move.delay = float.Parse(intervalElement.GetAttribute("Delay"), CultureInfo.InvariantCulture);

                    foreach (XmlElement point in intervalElement.GetElementsByTagName("Point"))
                    {
                        string name = point.GetAttribute("Name");

                        float x = float.Parse(point.GetAttribute("X"), CultureInfo.InvariantCulture);
                        float y = float.Parse(point.GetAttribute("Y"), CultureInfo.InvariantCulture);

                        if (name == "Support")
                            move.support = new Vector2(x, -y);
                        else if (name == "Finish")
                            move.move = new Vector2(x, -y);
                    }

                    dynamic.moves.Add(move);
                }
            }

            // -------- SIZE --------
            foreach (XmlElement sizeElement in transformationElement.GetElementsByTagName("Size"))
            {
                SizeData size = new SizeData();

                size.frames = int.Parse(sizeElement.GetAttribute("Frames"));
                size.finalWidth = float.Parse(sizeElement.GetAttribute("FinalWidth"), CultureInfo.InvariantCulture);
                size.finalHeight = float.Parse(sizeElement.GetAttribute("FinalHeight"), CultureInfo.InvariantCulture);

                dynamic.sizes.Add(size);
            }

            // -------- ROTATION --------
            foreach (XmlElement rotationElement in transformationElement.GetElementsByTagName("Rotation"))
            {
                RotateData rotation = new RotateData();

                rotation.angle = -float.Parse(rotationElement.GetAttribute("Angle"), CultureInfo.InvariantCulture);
                string[] anchor = rotationElement.GetAttribute("Anchor").Split('|');
                rotation.anchor = new Vector2(float.Parse(anchor[0], CultureInfo.InvariantCulture), float.Parse(anchor[1], CultureInfo.InvariantCulture));
                rotation.frames = int.Parse(rotationElement.GetAttribute("Frames"));

                dynamic.rotations.Add(rotation);
            }

            // -------- COLOR --------
            foreach (XmlElement colorElement in transformationElement.GetElementsByTagName("Color"))
            {
                ColorData color = new ColorData();

                ColorUtility.TryParseHtmlString(colorElement.GetAttribute("ColorStart"), out color.colorStart);
                ColorUtility.TryParseHtmlString(colorElement.GetAttribute("ColorFinish"), out color.colorFinish);

                color.frames = int.Parse(colorElement.GetAttribute("Frames"));

                dynamic.colors.Add(color);
            }

            return dynamic;
        }
    }
}