# Grasshopper Flowchart Plugin

A separate Grasshopper plugin that adds a `FlowChart` component for generating Markdown/Mermaid and HTML flowcharts directly from `.gh` or `.ghx` files.

## Component

**FlowChart** — `Bababuilts > Flowchart`

Generates flowchart files from a Grasshopper definition.

### Inputs

| Name | Nickname | Type | Description |
|------|----------|------|-------------|
| File Path | `Path` | FilePath | Path to a `.gh` or `.ghx` file. |
| Run | `Run` | Boolean | Set to true to generate outputs. Default: `true`. |
| Output Folder | `Dir` | Text | Optional output folder. If empty, files are written next to the input file. |
| Markdown | `MD` | Boolean | Generate a Markdown/Mermaid file. Default: `true`. |
| HTML | `HTML` | Boolean | Generate an interactive HTML file. Default: `true`. |

### Outputs

| Name | Nickname | Type | Description |
|------|----------|------|-------------|
| Markdown Path | `MD` | Text | Path to the generated `.md` file. |
| HTML Path | `HTML` | Text | Path to the generated `.html` file. |
| Component Count | `N` | Integer | Number of components found in the definition. |
| Markdown Text | `Txt` | Text | Raw Mermaid diagram text. |

## Building

Use MSBuild 4.0 (same as the rest of the solution):

```powershell
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' "GrasshopperFlowchartPlugin\GrasshopperFlowchartPlugin.csproj" /p:Configuration=Debug
```

The post-build event copies the resulting `.gha` to `%APPDATA%\Grasshopper\Libraries\` in Debug configuration.

## Files

- `PluginInfo.cs` — Plugin metadata.
- `Components/FlowChartComponent.cs` — The Grasshopper component.
- `GhParser.cs` / `FlowchartRenderer.cs` — Shared parsing and rendering logic (copied from `GrasshopperFlowchartTool`).
