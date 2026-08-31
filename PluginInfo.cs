using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace GrasshopperFlowchartPlugin
{
    public class GrasshopperFlowchartPluginInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get { return "Flowchart"; }
        }

        public override Bitmap Icon
        {
            get { return null; }
        }

        public override string Description
        {
            get { return "Grasshopper components for generating flowcharts from .gh and .ghx definitions."; }
        }

        public override Guid Id
        {
            get { return new Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678902"); }
        }

        public override string AuthorName
        {
            get { return "Bababuilts"; }
        }

        public override string AuthorContact
        {
            get { return ""; }
        }

        public override string Version
        {
            get { return "1.0.0"; }
        }
    }
}
