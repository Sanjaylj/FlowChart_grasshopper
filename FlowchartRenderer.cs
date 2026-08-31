using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GrasshopperFlowchartPlugin
{
    public static class FlowchartRenderer
    {
        private static readonly Dictionary<string, string> CategoryColors = new Dictionary<string, string>
        {
            { "Params", "#e1f5fe" },
            { "Maths", "#fff9c4" },
            { "Sets", "#c8e6c9" },
            { "Vector", "#ffccbc" },
            { "Curve", "#f8bbd0" },
            { "Surface", "#d1c4e9" },
            { "Mesh", "#b2dfdb" },
            { "Intersect", "#ffe0b2" },
            { "Transform", "#b3e5fc" },
            { "Display", "#f0f4c3" },
            { "GrasshopperTeklaLink", "#ffcdd2" },
            { "Default", "#eeeeee" }
        };

        public static void RenderMermaid(GhDefinition definition, string outputPath)
        {
            string mermaid = GenerateMermaidString(definition);
            File.WriteAllText(outputPath, mermaid);
        }

        public static void RenderHtml(GhDefinition definition, string outputPath)
        {
            string mermaid = GenerateMermaidString(definition);
            string summary = GenerateSummaryTable(definition);
            string definitionSummary = GenerateDefinitionSummary(definition);
            Dictionary<string, GhParameter> paramMap = definition.Parameters.ToDictionary(p => p.Id);
            Dictionary<string, GhComponent> componentMap = definition.Components.ToDictionary(c => c.Id);

            // Precompute node index map
            Dictionary<string, int> nodeIndexMap = new Dictionary<string, int>();
            int idx = 0;
            foreach (GhComponent c in definition.Components)
            {
                if (!ShouldSkipComponent(c))
                {
                    nodeIndexMap[c.Id] = idx;
                    idx++;
                }
            }

            // Precompute adjacency: source component -> list of target components
            Dictionary<string, List<string>> downstreamMap = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> upstreamMap = new Dictionary<string, List<string>>();

            foreach (GhComponent targetComp in definition.Components)
            {
                if (ShouldSkipComponent(targetComp))
                    continue;

                foreach (GhParameter input in targetComp.Inputs)
                {
                    foreach (string sourceId in input.ConnectedTo)
                    {
                        string sourceComponentId = null;
                        if (paramMap.ContainsKey(sourceId))
                            sourceComponentId = paramMap[sourceId].OwnerId;
                        else if (componentMap.ContainsKey(sourceId))
                            sourceComponentId = sourceId;

                        if (string.IsNullOrEmpty(sourceComponentId) || sourceComponentId == targetComp.Id)
                            continue;

                        if (!downstreamMap.ContainsKey(sourceComponentId))
                            downstreamMap[sourceComponentId] = new List<string>();
                        if (!downstreamMap[sourceComponentId].Contains(targetComp.Id))
                            downstreamMap[sourceComponentId].Add(targetComp.Id);

                        if (!upstreamMap.ContainsKey(targetComp.Id))
                            upstreamMap[targetComp.Id] = new List<string>();
                        if (!upstreamMap[targetComp.Id].Contains(sourceComponentId))
                            upstreamMap[targetComp.Id].Add(sourceComponentId);
                    }
                }
            }

            StringBuilder html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset=\"utf-8\" />");
            html.AppendLine("<title>Grasshopper Definition Flowchart</title>");
            html.AppendLine("<script src=\"https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js\"></script>");
            html.AppendLine("<script>");
            html.AppendLine("mermaid.initialize({startOnLoad:true, securityLevel:'loose'});");
            html.AppendLine("window.addEventListener('load', function() {");
            html.AppendLine("  var tooltip = document.createElement('div');");
            html.AppendLine("  tooltip.id = 'gh-tooltip';");
            html.AppendLine("  tooltip.style.cssText = 'position:fixed;background:#333;color:#fff;padding:8px 12px;border-radius:4px;font-size:12px;max-width:250px;z-index:1000;display:none;pointer-events:none;box-shadow:0 2px 8px rgba(0,0,0,0.3);';");
            html.AppendLine("  document.body.appendChild(tooltip);");
            html.AppendLine("  var tooltips = {");

            int tipIndex = 0;
            List<string> tooltipEntries = new List<string>();
            foreach (GhComponent comp in definition.Components)
            {
                if (!ShouldSkipComponent(comp))
                {
                    string tooltipText = Escape(comp.Description);
                    if (!string.IsNullOrEmpty(tooltipText) && tooltipText != "Empty")
                    {
                        tooltipEntries.Add(string.Format("    'N{0}': \"{1}\"", tipIndex, tooltipText));
                    }
                }
                tipIndex++;
            }

            html.AppendLine(string.Join("," + Environment.NewLine, tooltipEntries.ToArray()));

            html.AppendLine("  };");

            // Build adjacency lists for highlighting
            html.AppendLine("  var downstream = {");
            List<string> downstreamEntries = new List<string>();
            List<string> upstreamEntries = new List<string>();

            foreach (GhComponent comp in definition.Components)
            {
                if (ShouldSkipComponent(comp))
                    continue;

                int nodeIndex = nodeIndexMap[comp.Id];

                if (downstreamMap.ContainsKey(comp.Id))
                {
                    List<string> ids = downstreamMap[comp.Id]
                        .Where(id => nodeIndexMap.ContainsKey(id))
                        .Select(id => "'N" + nodeIndexMap[id] + "'")
                        .Distinct()
                        .ToList();
                    if (ids.Count > 0)
                    {
                        downstreamEntries.Add(string.Format("    'N{0}': [{1}]", nodeIndex, string.Join(", ", ids.ToArray())));
                    }
                }

                if (upstreamMap.ContainsKey(comp.Id))
                {
                    List<string> ids = upstreamMap[comp.Id]
                        .Where(id => nodeIndexMap.ContainsKey(id))
                        .Select(id => "'N" + nodeIndexMap[id] + "'")
                        .Distinct()
                        .ToList();
                    if (ids.Count > 0)
                    {
                        upstreamEntries.Add(string.Format("    'N{0}': [{1}]", nodeIndex, string.Join(", ", ids.ToArray())));
                    }
                }
            }

            html.AppendLine(string.Join("," + Environment.NewLine, downstreamEntries.ToArray()));
            html.AppendLine("  };");
            html.AppendLine("  var upstream = {");
            html.AppendLine(string.Join("," + Environment.NewLine, upstreamEntries.ToArray()));
            html.AppendLine("  };");

            html.AppendLine("  function getNodeKey(node) {");
            html.AppendLine("    var match = node.id.match(/flowchart-(N\\d+)-/);");
            html.AppendLine("    return match ? match[1] : node.id;");
            html.AppendLine("  }");

            html.AppendLine("  function collectDownstream(nodeId, maxDepth, visited, depth) {");
            html.AppendLine("    if (depth > maxDepth) return;");
            html.AppendLine("    if (visited[nodeId] !== undefined && visited[nodeId] <= depth) return;");
            html.AppendLine("    visited[nodeId] = depth;");
            html.AppendLine("    if (downstream[nodeId]) {");
            html.AppendLine("      downstream[nodeId].forEach(function(neighbor) {");
            html.AppendLine("        collectDownstream(neighbor, maxDepth, visited, depth + 1);");
            html.AppendLine("      });");
            html.AppendLine("    }");
            html.AppendLine("  }");

            html.AppendLine("  function getEdgeEndpoints(edgeElement) {");
            html.AppendLine("    var id = edgeElement.id;");
            html.AppendLine("    if (!id) return null;");
            html.AppendLine("    var match = id.match(/^L-([A-Za-z0-9_]+)-([A-Za-z0-9_]+)-/);");
            html.AppendLine("    if (match) return { from: match[1], to: match[2] };");
            html.AppendLine("    match = id.match(/^L_([A-Za-z0-9_]+)_([A-Za-z0-9_]+)_/);");
            html.AppendLine("    if (match) return { from: match[1], to: match[2] };");
            html.AppendLine("    return null;");
            html.AppendLine("  }");

            html.AppendLine("  function highlight(nodeId) {");
            html.AppendLine("    var active = {};");
            html.AppendLine("    active[nodeId] = 0;");
            html.AppendLine("    collectDownstream(nodeId, 3, active, 1);");
            html.AppendLine("    document.querySelectorAll('.node').forEach(function(node) {");
            html.AppendLine("      var key = getNodeKey(node);");
            html.AppendLine("      var dist = active[key];");
            html.AppendLine("      node.classList.remove('node-selected', 'node-active-group', 'node-faint');");
            html.AppendLine("      if (dist !== undefined) {");
            html.AppendLine("        node.style.opacity = '1';");
            html.AppendLine("        node.classList.add('node-active-group');");
            html.AppendLine("        if (dist === 0) { node.classList.add('node-selected'); }");
            html.AppendLine("      } else {");
            html.AppendLine("        node.style.opacity = '1';");
            html.AppendLine("        node.classList.add('node-faint');");
            html.AppendLine("      }");
            html.AppendLine("    });");
            html.AppendLine("    document.querySelectorAll('.edgePath, .edgeLabel').forEach(function(edge) {");
            html.AppendLine("      var endpoints = getEdgeEndpoints(edge);");
            html.AppendLine("      edge.classList.remove('edge-highlight-active', 'edge-highlight-dim');");
            html.AppendLine("      if (endpoints && active[endpoints.from] !== undefined && active[endpoints.to] !== undefined) {");
            html.AppendLine("        var maxDist = Math.max(active[endpoints.from], active[endpoints.to]);");
            html.AppendLine("        edge.style.opacity = '1';");
            html.AppendLine("        edge.classList.add('edge-highlight-active');");
            html.AppendLine("      } else {");
            html.AppendLine("        edge.style.opacity = '0.12';");
            html.AppendLine("        edge.classList.add('edge-highlight-dim');");
            html.AppendLine("      }");
            html.AppendLine("    });");
            html.AppendLine("  }");

            html.AppendLine("  function resetHighlight() {");
            html.AppendLine("    document.querySelectorAll('.node').forEach(function(node) {");
            html.AppendLine("      node.style.opacity = '1';");
            html.AppendLine("      node.classList.remove('node-selected', 'node-active-group', 'node-faint');");
            html.AppendLine("    });");
            html.AppendLine("    document.querySelectorAll('.edgePath, .edgeLabel').forEach(function(edge) {");
            html.AppendLine("      edge.style.opacity = '1';");
            html.AppendLine("      edge.classList.remove('edge-highlight-active', 'edge-highlight-dim');");
            html.AppendLine("    });");
            html.AppendLine("  }");

            html.AppendLine("  var resetBtn = document.createElement('button');");
            html.AppendLine("  resetBtn.textContent = 'Reset Highlight';");
            html.AppendLine("  resetBtn.style.cssText = 'margin-bottom:10px;padding:6px 12px;background:#4CAF50;color:#fff;border:none;border-radius:4px;cursor:pointer;';");
            html.AppendLine("  resetBtn.addEventListener('click', resetHighlight);");
            html.AppendLine("  var diagramContainer = document.querySelector('.mermaid');");
            html.AppendLine("  if (diagramContainer) diagramContainer.parentNode.insertBefore(resetBtn, diagramContainer);");

            html.AppendLine("  function attachListeners() {");
            html.AppendLine("    document.querySelectorAll('.node').forEach(function(node) {");
            html.AppendLine("      var id = getNodeKey(node);");
            html.AppendLine("      node.addEventListener('click', function(e) {");
            html.AppendLine("        e.stopPropagation();");
            html.AppendLine("        highlight(id);");
            html.AppendLine("      });");
            html.AppendLine("      node.addEventListener('mouseenter', function(e) {");
            html.AppendLine("        var text = tooltips[id];");
            html.AppendLine("        if (text) {");
            html.AppendLine("          tooltip.textContent = text;");
            html.AppendLine("          tooltip.style.display = 'block';");
            html.AppendLine("        }");
            html.AppendLine("      });");
            html.AppendLine("      node.addEventListener('mousemove', function(e) {");
            html.AppendLine("        tooltip.style.left = (e.clientX + 12) + 'px';");
            html.AppendLine("        tooltip.style.top = (e.clientY + 12) + 'px';");
            html.AppendLine("      });");
            html.AppendLine("      node.addEventListener('mouseleave', function() {");
            html.AppendLine("        tooltip.style.display = 'none';");
            html.AppendLine("      });");
            html.AppendLine("    });");
            html.AppendLine("  }");
            html.AppendLine("  document.body.addEventListener('click', resetHighlight);");
            html.AppendLine("  function waitForNodesAndAttach(attempts) {");
            html.AppendLine("    if (document.querySelectorAll('.node').length > 0) {");
            html.AppendLine("      attachListeners();");
            html.AppendLine("      return;");
            html.AppendLine("    }");
            html.AppendLine("    if (attempts > 0) {");
            html.AppendLine("      setTimeout(function() { waitForNodesAndAttach(attempts - 1); }, 100);");
            html.AppendLine("    } else {");
            html.AppendLine("      console.warn('Flowchart nodes did not render in time.');");
            html.AppendLine("    }");
            html.AppendLine("  }");
            html.AppendLine("  waitForNodesAndAttach(50);");
            html.AppendLine("});");
            html.AppendLine("</script>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }");
            html.AppendLine("h1 { color: #333; }");
            html.AppendLine("h2 { color: #555; margin-top: 30px; }");
            html.AppendLine("h3 { color: #666; margin-top: 20px; }");
            html.AppendLine(".container { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 20px; }");
            html.AppendLine("table { border-collapse: collapse; width: 100%; }");
            html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            html.AppendLine("th { background: #4CAF50; color: white; }");
            html.AppendLine("tr:nth-child(even) { background: #f9f9f9; }");
            html.AppendLine(".legend-item { display: inline-block; padding: 5px 10px; margin: 3px; border-radius: 4px; border: 1px solid #999; }");
            html.AppendLine(".stat-box { display: inline-block; padding: 10px 15px; margin: 5px; background: #f0f8ff; border-radius: 6px; border: 1px solid #b0c4de; }");
            html.AppendLine(".stat-number { font-size: 1.4em; font-weight: bold; color: #2e7d32; }");
            html.AppendLine(".io-list { list-style: none; padding: 0; }");
            html.AppendLine(".io-list li { padding: 4px 0; border-bottom: 1px solid #eee; }");
            html.AppendLine(".purpose-box { background: #fffde7; border-left: 4px solid #fbc02d; padding: 12px; margin: 10px 0; }");
            html.AppendLine(".editable { border: 1px dashed #ccc; padding: 8px; background: #fafafa; }");
            html.AppendLine(".node-faint { opacity: 0.12 !important; filter: grayscale(0.5) saturate(0.4); }");
            html.AppendLine(".node-active-group:not(.node-selected) { filter: drop-shadow(0 0 10px rgba(255,140,0,0.95)); animation: active-group-pulse 0.9s steps(2, end) infinite; }");
            html.AppendLine(".node-active-group:not(.node-selected) rect, .node-active-group:not(.node-selected) circle, .node-active-group:not(.node-selected) ellipse, .node-active-group:not(.node-selected) polygon { stroke: #ff9800 !important; stroke-width: 3px !important; fill: rgba(255,152,0,0.16) !important; }");
            html.AppendLine(".node-selected { filter: drop-shadow(0 0 12px rgba(211,47,47,0.95)) !important; }");
            html.AppendLine(".node-selected rect, .node-selected circle, .node-selected ellipse, .node-selected polygon { stroke: #d32f2f !important; stroke-width: 4px !important; fill: rgba(211,47,47,0.18) !important; }");
            html.AppendLine(".node-selected:not(.node-active-group) { filter: drop-shadow(0 0 12px rgba(211,47,47,0.95)); }");
            html.AppendLine("@keyframes active-group-pulse { 0%,100% { filter: drop-shadow(0 0 8px rgba(255,140,0,0.9)); } 50% { filter: drop-shadow(0 0 18px rgba(255,140,0,1)); } }");
            html.AppendLine(".edge-highlight-active path { stroke: #d32f2f !important; stroke-width: 2.5px !important; filter: drop-shadow(0 0 3px rgba(211,47,47,0.7)); }");
            html.AppendLine(".edge-highlight-dim { opacity: 0.12; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<div class=\"container\">");
            html.AppendLine("<h1>Grasshopper Definition Flowchart</h1>");
            html.AppendLine("<p>This diagram shows how data flows between components. Hover over nodes and arrows to trace the logic.</p>");
            html.AppendLine(GenerateLegend());
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"container\">");
            html.AppendLine(definitionSummary);
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"container\">");
            html.AppendLine("<pre class=\"mermaid\">");
            html.AppendLine(mermaid);
            html.AppendLine("</pre>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"container\">");
            html.AppendLine(summary);
            html.AppendLine("</div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            File.WriteAllText(outputPath, html.ToString());
        }

        public static string GenerateMermaidString(GhDefinition definition)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("flowchart LR");
            sb.AppendLine();

            Dictionary<string, GhComponent> componentMap = definition.Components.ToDictionary(c => c.Id);
            Dictionary<string, GhParameter> paramMap = definition.Parameters.ToDictionary(p => p.Id);
            Dictionary<string, string> nodeIds = new Dictionary<string, string>();
            int nodeIndex = 0;

            List<GhComponent> visibleComponents = definition.Components
                .Where(c => !ShouldSkipComponent(c))
                .ToList();

            foreach (GhComponent comp in visibleComponents)
            {
                nodeIds[comp.Id] = "N" + nodeIndex;
                nodeIndex++;
            }

            var groupedByCategory = visibleComponents
                .GroupBy(c => GetCategoryName(c))
                .Select(g => new
                {
                    Category = g.Key,
                    Components = g.ToList(),
                    AverageX = g.Average(c => c.X)
                })
                .OrderBy(g => g.AverageX)
                .ToList();

            foreach (var group in groupedByCategory)
            {
                string subgraphId = Escape(group.Category).Replace(" ", "_");
                sb.AppendLine(string.Format("    subgraph {0}[\"{1}\"]", subgraphId, Escape(group.Category)));

                foreach (GhComponent comp in group.Components)
                {
                    string nodeId = nodeIds[comp.Id];
                    string label = Escape(comp.Nickname);
                    string color = GetComponentColor(comp);
                    string tooltip = Escape(comp.Description);

                    sb.AppendLine(string.Format("        {0}[\"{1}\"]", nodeId, label));
                    sb.AppendLine(string.Format("        style {0} fill:{1},stroke:#333,stroke-width:2px", nodeId, color));

                    // Tooltip is now handled by JavaScript hover events, not click alert.
                }

                sb.AppendLine("    end");
                sb.AppendLine();
            }

            foreach (GhComponent comp in visibleComponents)
            {
                foreach (GhParameter input in comp.Inputs)
                {
                    foreach (string sourceId in input.ConnectedTo)
                    {
                        string sourceComponentId = null;

                        if (paramMap.ContainsKey(sourceId))
                        {
                            sourceComponentId = paramMap[sourceId].OwnerId;
                        }
                        else if (componentMap.ContainsKey(sourceId))
                        {
                            sourceComponentId = sourceId;
                        }

                        if (!string.IsNullOrEmpty(sourceComponentId) &&
                            nodeIds.ContainsKey(sourceComponentId) &&
                            nodeIds.ContainsKey(comp.Id))
                        {
                            string arrowLabel = Escape(input.Nickname);
                            if (!string.IsNullOrEmpty(arrowLabel) && arrowLabel != "Empty")
                            {
                                sb.AppendLine(string.Format("    {0} --\"{1}\"--> {2}",
                                    nodeIds[sourceComponentId], arrowLabel, nodeIds[comp.Id]));
                            }
                            else
                            {
                                sb.AppendLine(string.Format("    {0} --> {1}",
                                    nodeIds[sourceComponentId], nodeIds[comp.Id]));
                            }
                        }
                    }
                }
            }

            return sb.ToString();
        }

        private static string GenerateSummaryTable(GhDefinition definition)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<h2>Component Summary</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>#</th><th>Component</th><th>Category</th><th>Inputs</th><th>Outputs</th></tr>");

            int index = 1;
            foreach (GhComponent comp in definition.Components)
            {
                if (ShouldSkipComponent(comp))
                    continue;

                string inputs = string.Join(", ", comp.Inputs.Select(p => Escape(p.Nickname)));
                string outputs = string.Join(", ", comp.Outputs.Select(p => Escape(p.Nickname)));

                sb.AppendLine(string.Format("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td></tr>",
                    index, Escape(comp.Nickname), GetCategoryName(comp), inputs, outputs));
                index++;
            }

            sb.AppendLine("</table>");
            return sb.ToString();
        }

        private static string GenerateLegend()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<h2>Legend</h2>");

            foreach (var kvp in CategoryColors)
            {
                sb.AppendLine(string.Format("<span class=\"legend-item\" style=\"background:{0}\">{1}</span>",
                    kvp.Value, kvp.Key));
            }

            return sb.ToString();
        }

        private static string GenerateDefinitionSummary(GhDefinition definition)
        {
            StringBuilder sb = new StringBuilder();

            List<GhComponent> visibleComponents = definition.Components
                .Where(c => !ShouldSkipComponent(c))
                .ToList();

            int totalComponents = visibleComponents.Count;
            int totalParameters = definition.Parameters.Count;
            int totalConnections = definition.Parameters.Sum(p => p.ConnectedTo.Count);

            List<GhComponent> inputs = GetDefinitionInputs(definition);
            List<GhComponent> outputs = GetDefinitionOutputs(definition);

            sb.AppendLine("<h2>Definition Summary</h2>");

            // Purpose description
            sb.AppendLine("<div class=\"purpose-box\">");
            sb.AppendLine("<strong>Detected Purpose:</strong>");
            sb.AppendLine(string.Format("<p>{0}</p>", GeneratePurposeDescription(definition, visibleComponents)));
            sb.AppendLine("</div>");

            // Editable user description
            sb.AppendLine("<h3>Description</h3>");
            sb.AppendLine("<div class=\"editable\" contenteditable=\"true\">");
            sb.AppendLine("Click here to add your own description of this Grasshopper definition...");
            sb.AppendLine("</div>");

            // Statistics
            sb.AppendLine("<h3>Statistics</h3>");
            sb.AppendLine(string.Format("<div class=\"stat-box\"><span class=\"stat-number\">{0}</span><br/>Components</div>", totalComponents));
            sb.AppendLine(string.Format("<div class=\"stat-box\"><span class=\"stat-number\">{0}</span><br/>Parameters</div>", totalParameters));
            sb.AppendLine(string.Format("<div class=\"stat-box\"><span class=\"stat-number\">{0}</span><br/>Connections</div>", totalConnections));
            sb.AppendLine(string.Format("<div class=\"stat-box\"><span class=\"stat-number\">{0}</span><br/>Inputs</div>", inputs.Count));
            sb.AppendLine(string.Format("<div class=\"stat-box\"><span class=\"stat-number\">{0}</span><br/>Outputs</div>", outputs.Count));

            // Category breakdown
            sb.AppendLine("<h3>Category Breakdown</h3>");
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Category</th><th>Count</th><th>Percentage</th></tr>");

            var categoryCounts = visibleComponents
                .GroupBy(c => GetCategoryName(c))
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            foreach (var cat in categoryCounts)
            {
                double percentage = totalComponents > 0 ? (100.0 * cat.Count / totalComponents) : 0;
                sb.AppendLine(string.Format("<tr><td>{0}</td><td>{1}</td><td>{2:F1}%</td></tr>",
                    Escape(cat.Category), cat.Count, percentage));
            }

            sb.AppendLine("</table>");

            // Inputs
            sb.AppendLine("<h3>Inputs</h3>");
            if (inputs.Count == 0)
            {
                sb.AppendLine("<p>No explicit input parameters detected.</p>");
            }
            else
            {
                sb.AppendLine("<ul class=\"io-list\">");
                foreach (GhComponent input in inputs)
                {
                    string typeHint = GetParameterTypeHint(input);
                    sb.AppendLine(string.Format("<li><strong>{0}</strong> — {1}</li>",
                        Escape(input.Nickname), typeHint));
                }
                sb.AppendLine("</ul>");
            }

            // Outputs
            sb.AppendLine("<h3>Outputs</h3>");
            if (outputs.Count == 0)
            {
                sb.AppendLine("<p>No explicit output parameters detected.</p>");
            }
            else
            {
                sb.AppendLine("<ul class=\"io-list\">");
                foreach (GhComponent output in outputs)
                {
                    string typeHint = GetParameterTypeHint(output);
                    sb.AppendLine(string.Format("<li><strong>{0}</strong> — {1}</li>",
                        Escape(output.Nickname), typeHint));
                }
                sb.AppendLine("</ul>");
            }

            return sb.ToString();
        }

        private static List<GhComponent> GetDefinitionInputs(GhDefinition definition)
        {
            HashSet<string> referencedOutputIds = new HashSet<string>();
            foreach (GhParameter param in definition.Parameters)
            {
                foreach (string sourceId in param.ConnectedTo)
                {
                    referencedOutputIds.Add(sourceId);
                }
            }

            return definition.Components
                .Where(c => !ShouldSkipComponent(c))
                .Where(c => GetCategoryName(c) == "Params")
                .Where(c => referencedOutputIds.Contains(c.Id))
                .OrderBy(c => c.Nickname)
                .ToList();
        }

        private static List<GhComponent> GetDefinitionOutputs(GhDefinition definition)
        {
            HashSet<string> referencedOutputIds = new HashSet<string>();
            foreach (GhParameter param in definition.Parameters)
            {
                foreach (string sourceId in param.ConnectedTo)
                {
                    referencedOutputIds.Add(sourceId);
                }
            }

            return definition.Components
                .Where(c => !ShouldSkipComponent(c))
                .Where(c => GetCategoryName(c) == "Params")
                .Where(c => !referencedOutputIds.Contains(c.Id))
                .OrderBy(c => c.Nickname)
                .ToList();
        }

        private static string GetParameterTypeHint(GhComponent paramComponent)
        {
            string name = (paramComponent.Name ?? "").Trim();
            string nickname = (paramComponent.Nickname ?? "").Trim();

            if (name.IndexOf("Point", StringComparison.OrdinalIgnoreCase) >= 0 ||
                nickname.IndexOf("Point", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Point";

            if (name.IndexOf("Curve", StringComparison.OrdinalIgnoreCase) >= 0 ||
                nickname.IndexOf("Curve", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Curve";

            if (name.IndexOf("Number", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Slider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                nickname.IndexOf("Slider", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Number";

            if (name.IndexOf("Integer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Integer";

            if (name.IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0 ||
                nickname.IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Plane";

            if (name.IndexOf("Vector", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Vector";

            if (name.IndexOf("Geometry", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Geometry";

            if (name.IndexOf("Brep", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Brep";

            if (name.IndexOf("Mesh", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Mesh";

            return "Data";
        }

        private static string GeneratePurposeDescription(GhDefinition definition, List<GhComponent> visibleComponents)
        {
            var categoryCounts = visibleComponents
                .GroupBy(c => GetCategoryName(c))
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            string topCategory = categoryCounts.Count > 0 ? categoryCounts[0].Category : "Default";
            int teklaCount = visibleComponents.Count(c => GetCategoryName(c) == "GrasshopperTeklaLink");
            int curveCount = visibleComponents.Count(c => GetCategoryName(c) == "Curve");
            int surfaceCount = visibleComponents.Count(c => GetCategoryName(c) == "Surface");
            int transformCount = visibleComponents.Count(c => GetCategoryName(c) == "Transform");
            int mathCount = visibleComponents.Count(c => GetCategoryName(c) == "Maths");

            List<string> purposes = new List<string>();

            if (teklaCount > 0)
                purposes.Add("Tekla structural modeling");

            if (surfaceCount > 0 && curveCount > 0)
                purposes.Add("geometry generation from curves and surfaces");
            else if (surfaceCount > 0)
                purposes.Add("surface generation and manipulation");
            else if (curveCount > 0)
                purposes.Add("curve processing and analysis");

            if (transformCount > 0)
                purposes.Add("transformations and orientations");

            if (mathCount > 0)
                purposes.Add("parametric calculations");

            if (purposes.Count == 0)
            {
                if (topCategory != "Default")
                    purposes.Add(string.Format("{0} operations", topCategory.ToLower()));
                else
                    purposes.Add("general Grasshopper logic");
            }

            string purposeText = string.Join(", ", purposes.ToArray());

            return string.Format(
                "This definition appears to perform {0}. It contains {1} visible components across {2} categories, with {3} input(s) and {4} output(s).",
                purposeText,
                visibleComponents.Count,
                categoryCounts.Count,
                GetDefinitionInputs(definition).Count,
                GetDefinitionOutputs(definition).Count);
        }

        private static bool ShouldSkipComponent(GhComponent comp)
        {
            if (comp == null)
                return true;

            string nickname = (comp.Nickname ?? "").Trim();

            // Skip empty panels and unnamed helper components
            if (string.IsNullOrEmpty(nickname))
                return true;

            // Skip common utility components that clutter the diagram
            string[] skipNames = { "Panel", "Group", "Scribble", "Data" };
            if (skipNames.Contains(nickname, StringComparer.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string GetComponentColor(GhComponent comp)
        {
            string category = GetCategoryName(comp);

            if (CategoryColors.ContainsKey(category))
                return CategoryColors[category];

            return CategoryColors["Default"];
        }

        private static string GetCategoryName(GhComponent comp)
        {
            if (comp == null)
                return "Default";

            string name = comp.Name ?? "";
            string nickname = comp.Nickname ?? "";
            string typeGuid = comp.TypeGuid ?? "";

            // Tekla components
            if (typeGuid.ToLower().Contains("tekla") ||
                name.Contains("Beam") || name.Contains("Part") || name.Contains("Position") ||
                nickname.Contains("TSV") || nickname.Contains("Vapaa-aukko"))
            {
                return "GrasshopperTeklaLink";
            }

            bool isParameterComponent = comp.Inputs.Count == 0 && comp.Outputs.Count == 0;

            // Standard Grasshopper categories by name
            string[] paramsNames = { "Point", "Curve", "Line", "Plane", "Vector", "Number", "Integer", "Slider", "Panel", "Geometry" };
            string[] curveNames = { "Curve", "Line", "Join Curves", "Perp Frames", "End Points" };
            string[] surfaceNames = { "Loft", "Cap Holes", "Cap Holes Ex", "Extrude" };
            string[] transformNames = { "Orient", "Flip Plane", "Rotate Plane", "Move", "Rotate", "Scale" };
            string[] vectorNames = { "XY Plane", "XZ Plane", "YZ Plane", "Unit Vector" };
            string[] setNames = { "Item", "List Item", "Tree Item", "Branch", "Path" };
            string[] mathNames = { "Addition", "Subtraction", "Multiplication", "Division", "Expression" };

            if (isParameterComponent && paramsNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                return "Params";
            if (curveNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                return "Curve";
            if (surfaceNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                return "Surface";
            if (transformNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                return "Transform";
            if (vectorNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                return "Vector";
            if (setNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                return "Sets";
            if (mathNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                return "Maths";

            return "Default";
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Empty";

            text = text.Trim();

            return text
                .Replace("\"", "#quot;")
                .Replace("[", "(")
                .Replace("]", ")")
                .Replace("{", "(")
                .Replace("}", ")")
                .Replace("<", "(")
                .Replace(">", ")")
                .Replace("&", "&amp;")
                .Replace("#", "#35;")
                .Replace("\n", " ")
                .Replace("\r", " ");
        }

        private static int GetNodeIndex(GhDefinition definition, GhComponent comp)
        {
            int index = 0;
            foreach (GhComponent c in definition.Components)
            {
                if (!ShouldSkipComponent(c))
                {
                    if (c.Id == comp.Id)
                        return index;
                    index++;
                }
            }
            return -1;
        }
    }
}
