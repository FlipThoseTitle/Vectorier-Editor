using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Vectorier.XML
{
    public class XmlUtility
    {
        private XmlDocument xmlDocument;
        public XmlElement RootElement { get; private set; }

        public XmlUtility()
        {
            xmlDocument = new XmlDocument();
        }

        // Create a new XML document with a <root> node
        public void Create(string rootName)
        {
            xmlDocument = new XmlDocument();

            XmlDeclaration declaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDocument.AppendChild(declaration);

            RootElement = xmlDocument.CreateElement(rootName);
            xmlDocument.AppendChild(RootElement);
        }

        // Load an existing XML file
        public void Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("XML file not found: " + filePath);

            xmlDocument.Load(filePath);
            RootElement = xmlDocument.DocumentElement;
        }

        // Save the XML document to a file
        public void Save(string filePath)
        {
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineOnAttributes = false,
                Encoding = System.Text.Encoding.UTF8,
            };

            using (XmlWriter writer = XmlWriter.Create(filePath, settings))
            {
                xmlDocument.Save(writer);
            }
        }

        // Add a new element under a parent element
        public XmlElement AddElement(XmlElement parent, string elementName)
		{
			XmlElement newElement = xmlDocument.CreateElement(elementName);
			parent.AppendChild(newElement);
			return newElement;
		}

        // Remove an element from its parent
        public void RemoveElement(XmlElement element)
        {
            element.ParentNode?.RemoveChild(element);
        }

        // Set an attribute on an element
        public void SetAttribute(XmlElement element, string attributeName, object value)
        {
            if (value == null)
                return;

            element.SetAttribute(attributeName, value.ToString());
        }

        // Get Element if it exists, and create a new one if there isn't
        public XmlElement GetOrCreateElement(XmlElement parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
                return null;

            // Check if a child with this name already exists
            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node is XmlElement element && element.Name == name)
                    return element;
            }

            // If not found, create one
            return AddElement(parent, name);
        }

        // Removes empty elements recursively
        public void RemoveEmptyElements(XmlElement parent)
        {
            var toRemove = new List<XmlElement>();

            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node is XmlElement elem)
                {
                    RemoveEmptyElements(elem); // recursive
                    if (!elem.HasAttributes && elem.ChildNodes.Count == 0)
                        toRemove.Add(elem);
                }
            }

            foreach (var elem in toRemove)
                parent.RemoveChild(elem);
        }

        // Get an attribute as a string
        public string GetAttribute(XmlElement element, string attributeName, string defaultValue = "")
        {
            if (element.HasAttribute(attributeName))
                return element.GetAttribute(attributeName);

            return defaultValue;
        }

        // Return the internal XmlDocument
        public XmlDocument GetDocument()
        {
            return xmlDocument;
        }

        // ============================================================
        // FORMATTING UTILITIES
        // ============================================================

        // Format and rewrite XML with indentation and self-closing empty tags
        public static void FormatXML(string inputPath, string outputPath)
        {
            if (!File.Exists(inputPath))
                throw new FileNotFoundException("XML file not found: " + inputPath);

            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = false;
            doc.Load(inputPath);

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                Encoding = System.Text.Encoding.UTF8,
                OmitXmlDeclaration = false,
                NewLineHandling = NewLineHandling.Replace
            };

            using (XmlWriter writer = XmlWriter.Create(outputPath, settings))
            {
                foreach (XmlNode node in doc.ChildNodes)
                {
                    if (node.NodeType == XmlNodeType.XmlDeclaration)
                    {
                        writer.WriteProcessingInstruction("xml", "version=\"1.0\" encoding=\"utf-8\"");
                    }
                    else
                    {
                        WriteNodeCompact(node, writer);
                    }
                }
            }
        }

        // Recursive node writer that collapses empty tags into self-closing
        private static void WriteNodeCompact(XmlNode node, XmlWriter writer)
        {
            switch (node.NodeType)
            {
                case XmlNodeType.Element:
                    writer.WriteStartElement(node.Prefix, node.LocalName, node.NamespaceURI);

                    // Write attributes
                    if (node.Attributes != null)
                    {
                        foreach (XmlAttribute attr in node.Attributes)
                            writer.WriteAttributeString(attr.Prefix, attr.LocalName, attr.NamespaceURI, attr.Value);
                    }

                    // Children
                    if (node.HasChildNodes)
                    {
                        bool hasNonEmptyChild = false;
                        foreach (XmlNode child in node.ChildNodes)
                        {
                            if (child.NodeType != XmlNodeType.Text &&
                                child.NodeType != XmlNodeType.CDATA &&
                                child.NodeType != XmlNodeType.Comment)
                            {
                                hasNonEmptyChild = true;
                            }
                            WriteNodeCompact(child, writer);
                        }

                        if (hasNonEmptyChild)
                            writer.WriteFullEndElement();
                        else
                            writer.WriteEndElement();
                    }
                    else
                    {
                        // No children -> self-closing
                        writer.WriteEndElement();
                    }
                    break;

                case XmlNodeType.Text:
                    writer.WriteString(node.Value);
                    break;

                case XmlNodeType.CDATA:
                    writer.WriteCData(node.Value);
                    break;

                case XmlNodeType.Comment:
                    writer.WriteComment(node.Value);
                    break;

                default:
                    break;
            }
        }
    }
}