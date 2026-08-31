using System;
using System.IO;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

namespace GrasshopperFlowchartPlugin.Components
{
    public class FlowChartComponent : GH_Component
    {
        public FlowChartComponent()
          : base("FlowChart", "FlowChart",
              "Generates Markdown and HTML flowcharts from a .gh or .ghx Grasshopper definition.",
              "Bababuilts", "Flowchart")
        {
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("C4D5E6F7-B8A9-0123-EF01-456789012346"); }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get { return null; }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new Param_FilePath(), "File Path", "Path", "Path to a .gh or .ghx file.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run", "Run", "Set to true to generate outputs.", GH_ParamAccess.item, true);
            pManager.AddTextParameter("Output Folder", "Dir", "Optional output folder. If empty, files are written next to the input file.", GH_ParamAccess.item, string.Empty);
            pManager.AddBooleanParameter("Markdown", "MD", "Generate a Markdown/Mermaid file.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("HTML", "HTML", "Generate an interactive HTML file.", GH_ParamAccess.item, true);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Markdown Path", "MD", "Path to the generated Markdown file.", GH_ParamAccess.item);
            pManager.AddTextParameter("HTML Path", "HTML", "Path to the generated HTML file.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Component Count", "N", "Number of components found in the definition.", GH_ParamAccess.item);
            pManager.AddTextParameter("Markdown Text", "Txt", "Raw Mermaid diagram text.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filePath = string.Empty;
            bool run = true;
            string outputFolder = string.Empty;
            bool generateMarkdown = true;
            bool generateHtml = true;

            if (!DA.GetData(0, ref filePath)) return;
            DA.GetData(1, ref run);
            DA.GetData(2, ref outputFolder);
            DA.GetData(3, ref generateMarkdown);
            DA.GetData(4, ref generateHtml);

            if (!run)
            {
                DA.SetData(0, null);
                DA.SetData(1, null);
                DA.SetData(2, 0);
                DA.SetData(3, string.Empty);
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "File path is empty.");
                return;
            }

            filePath = filePath.Trim('"');

            if (!File.Exists(filePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "File not found: " + filePath);
                return;
            }

            string extension = Path.GetExtension(filePath).ToLower();
            if (extension != ".gh" && extension != ".ghx")
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Only .gh and .ghx files are supported.");
                return;
            }

            try
            {
                GhDefinition definition = GhParser.Parse(filePath);

                string folder;
                if (!string.IsNullOrWhiteSpace(outputFolder))
                {
                    folder = outputFolder.Trim('"');
                    if (!Directory.Exists(folder))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Output folder does not exist: " + folder);
                        return;
                    }
                }
                else
                {
                    folder = Path.GetDirectoryName(Path.GetFullPath(filePath));
                }

                string baseName = Path.GetFileNameWithoutExtension(filePath);
                string mdPath = Path.Combine(folder, baseName + "_flowchart.md");
                string htmlPath = Path.Combine(folder, baseName + "_flowchart.html");
                string mermaidText = FlowchartRenderer.GenerateMermaidString(definition);

                if (generateMarkdown)
                {
                    FlowchartRenderer.RenderMermaid(definition, mdPath);
                }

                if (generateHtml)
                {
                    FlowchartRenderer.RenderHtml(definition, htmlPath);
                }

                DA.SetData(0, generateMarkdown ? mdPath : string.Empty);
                DA.SetData(1, generateHtml ? htmlPath : string.Empty);
                DA.SetData(2, definition.Components.Count);
                DA.SetData(3, mermaidText);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }
    }
}
