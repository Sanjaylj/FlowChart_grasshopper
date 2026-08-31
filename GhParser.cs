using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using GH_IO.Serialization;

namespace GrasshopperFlowchartPlugin
{
    public class GhComponent
    {
        public string Id { get; set; }
        public string TypeGuid { get; set; }
        public string Name { get; set; }
        public string Nickname { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public List<GhParameter> Inputs { get; set; }
        public List<GhParameter> Outputs { get; set; }

        public GhComponent()
        {
            Inputs = new List<GhParameter>();
            Outputs = new List<GhParameter>();
        }
    }

    public class GhParameter
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Nickname { get; set; }
        public bool IsInput { get; set; }
        public string OwnerId { get; set; }
        public List<string> ConnectedTo { get; set; }

        public GhParameter()
        {
            ConnectedTo = new List<string>();
        }
    }

    public class GhDefinition
    {
        public List<GhComponent> Components { get; set; }
        public List<GhParameter> Parameters { get; set; }

        public GhDefinition()
        {
            Components = new List<GhComponent>();
            Parameters = new List<GhParameter>();
        }
    }

    public static class GhParser
    {
        public static GhDefinition Parse(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            string xmlContent;

            if (extension == ".ghx")
            {
                xmlContent = File.ReadAllText(filePath);
            }
            else if (extension == ".gh")
            {
                xmlContent = ExtractGhXml(filePath);
            }
            else
            {
                throw new NotSupportedException("Only .gh and .ghx files are supported.");
            }

            return ParseXml(xmlContent);
        }

        private static string ExtractGhXml(string filePath)
        {
            GH_Archive archive = new GH_Archive();
            if (!archive.ReadFromFile(filePath))
            {
                throw new InvalidDataException("Failed to read .gh file with Grasshopper GH_Archive.");
            }

            return archive.Serialize_Xml();
        }

        private static GhDefinition ParseXml(string xmlContent)
        {
            XDocument doc = XDocument.Parse(xmlContent);

            GhDefinition definition = new GhDefinition();
            Dictionary<string, GhParameter> paramMap = new Dictionary<string, GhParameter>();

            // Find all Object chunks
            var objectChunks = doc.Descendants("chunk")
                .Where(e => GetAttribute(e, "name") == "Object");

            foreach (XElement objChunk in objectChunks)
            {
                // Quick skip for groups and panels before heavy parsing
                string quickName = GetItemValue(objChunk, "Name") ?? "";
                if (quickName == "Group" || quickName == "Panel" || quickName == "Scribble")
                    continue;

                GhComponent comp = ParseComponent(objChunk);
                if (comp == null || ShouldSkipComponent(comp))
                    continue;

                definition.Components.Add(comp);

                // Find Container chunk
                XElement container = objChunk.Descendants("chunk")
                    .FirstOrDefault(e => GetAttribute(e, "name") == "Container");

                if (container == null)
                    continue;

                // Parse input and output parameters and build connections in one pass
                foreach (XElement paramChunk in container.Descendants("chunk"))
                {
                    string paramName = GetAttribute(paramChunk, "name");
                    if (paramName != "param_input" && paramName != "param_output")
                        continue;

                    bool isInput = paramName == "param_input";
                    GhParameter param = ParseParameter(paramChunk, comp.Id, isInput);
                    if (param == null)
                        continue;

                    if (isInput)
                        comp.Inputs.Add(param);
                    else
                        comp.Outputs.Add(param);

                    definition.Parameters.Add(param);
                    paramMap[param.Id] = param;

                    // Build connections from Source items immediately
                    foreach (XElement item in paramChunk.Descendants("item"))
                    {
                        string itemName = GetAttribute(item, "name");
                        if (itemName != "Source")
                            continue;

                        string sourceId = item.Value;
                        if (!string.IsNullOrEmpty(sourceId))
                            param.ConnectedTo.Add(sourceId);
                    }
                }
            }

            return definition;
        }

        private static GhComponent ParseComponent(XElement objChunk)
        {
            string id = GetItemValue(objChunk, "InstanceGuid");
            if (string.IsNullOrEmpty(id))
                id = GetItemValue(objChunk, "GUID");

            if (string.IsNullOrEmpty(id))
                return null;

            XElement container = objChunk.Descendants("chunk")
                .FirstOrDefault(e => GetAttribute(e, "name") == "Container");

            string name = GetItemValue(container ?? objChunk, "Name") ?? "Unknown";
            string nickname = GetItemValue(container ?? objChunk, "NickName") ?? name;
            string description = GetItemValue(container ?? objChunk, "Description") ?? "";

            GhComponent comp = new GhComponent
            {
                Id = id,
                TypeGuid = GetItemValue(objChunk, "GUID"),
                Name = name,
                Nickname = nickname,
                Description = description,
                Category = "",
                SubCategory = ""
            };

            XElement attributes = container != null
                ? container.Descendants("chunk").FirstOrDefault(e => GetAttribute(e, "name") == "Attributes")
                : null;

            if (attributes != null)
            {
                XElement pivot = attributes.Descendants("item")
                    .FirstOrDefault(e => GetAttribute(e, "name") == "Pivot");
                if (pivot != null)
                {
                    comp.X = ParseDouble(GetItemValue(pivot, "X"));
                    comp.Y = ParseDouble(GetItemValue(pivot, "Y"));
                }
            }

            return comp;
        }

        private static GhParameter ParseParameter(XElement paramChunk, string ownerId, bool isInput)
        {
            string id = GetItemValue(paramChunk, "InstanceGuid");
            if (string.IsNullOrEmpty(id))
                return null;

            return new GhParameter
            {
                Id = id,
                Name = GetItemValue(paramChunk, "Name") ?? "Unknown",
                Nickname = GetItemValue(paramChunk, "NickName") ?? "Unknown",
                IsInput = isInput,
                OwnerId = ownerId
            };
        }

        private static string GetItemValue(XElement parent, string itemName)
        {
            if (parent == null)
                return null;

            XElement item = parent.Descendants("item")
                .FirstOrDefault(e => GetAttribute(e, "name") == itemName);

            return item != null ? item.Value : null;
        }

        private static string GetAttribute(XElement elem, string name)
        {
            if (elem == null)
                return null;

            XAttribute attr = elem.Attribute(name);
            return attr != null ? attr.Value : null;
        }

        private static double ParseDouble(string value)
        {
            double result;
            double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result);
            return result;
        }

        public static bool ShouldSkipComponent(GhComponent comp)
        {
            return comp.Category == "Group" || comp.Category == "Panel" || comp.Category == "Scribble";
        }
    }
}
