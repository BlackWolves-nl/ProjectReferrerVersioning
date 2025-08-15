# Project Referrer Versioning Extension Features

This Visual Studio extension provides advanced project referrer visualization and version management for .NET C# solutions.

## Features

- **Project Referrer Tree Visualization**
  - Displays project referrer chains in a zoomable, scrollable tree view.
  - Supports multiple layout modes (Standard Tree, Compact Horizontal, Compact Vertical).
  - Color-coded nodes for modified, clean, visited, and NuGet/project referrer/version changes.
  - Status-based node coloring, including new states for NuGet/project reference and version changes.
  - Tooltips on nodes show detailed NuGet and project reference changes (added, removed, edited).
  - **Hover effect**: Project list items highlight with a lighter background on mouseover for better visibility.
  - **Edge and Node Highlighting**: Hovering a node highlights all edges in its referrer path and all nodes representing the same project across the graph.
  - **Unique Path-based Tagging**: Nodes and edges are tagged with unique path-based objects, ensuring correct highlighting even for duplicate project names.
  - **Git badge**: Each node shows a badge with the total number of changed files and lines (uncommitted changes). On hover, the badge expands into a pill showing files and lines separately.
  - **Badge and pill**: Styled and positioned according to the current theme, with perfect text centering and rightward expansion.

- **Version Management**
  - Allows bumping project versions (Major, Minor, Patch, Revision) directly from the UI via context menu.
  - Propagates version changes to dependent projects; child nodes automatically bump revision when all roots are set.
  - Updates AssemblyVersion, AssemblyFileVersion, and Version properties in project files and AssemblyInfo.cs.
  - Version changes are clearly displayed (e.g., "V 1.2.3.4 → V 1.2.4.0").
  - **Exclusion support**: Projects can be excluded from version updates via right-click context menu or settings. Excluded projects are not updated and are skipped in child node calculations. Excluded projects appear in dull grey and italic in the UI.

- **Git Integration for Change Detection**
  - Detects uncommitted changes in NuGet packages and project references using Git.
  - Differentiates between added, removed, and edited references, showing tooltips for these changes.
  - Shows number of changed files and changed lines for each project (uncommitted changes).

- **Export and Sharing**
  - Export the referrer tree canvas to PNG at full size.
  - Copy graph information to clipboard for documentation or sharing.

- **Filtering and Sorting**
  - Filter and sort projects by name or status.
  - Select all, none, or only modified projects with one click.
  - "Select Modified" button automatically skips excluded projects for improved workflow efficiency.
  
- **Legend and Status Indicators**
  - Visual legend for node types and project statuses.
  - Status bar with project and graph statistics.

- **Theme Support**
  - Switch between Dark and Slate themes for better visibility.
  - All UI elements, including ScrollViewer and canvas, use the theme's background and foreground colors.

- **Safe File Updates**
  - Uses robust file editing for version updates (XML for .csproj, regex for AssemblyInfo.cs).

- **Settings Management**
  - User settings for default theme, layout, and excluded projects are persisted per solution.

## Usage

1. Select projects to visualize and manage.
2. Exclude projects from version updates as needed.
3. Generate the referrer tree.
4. Bump versions as needed and update all affected projects.
5. Export or share the tree and version information.
