# Frequently Asked Questions (FAQ)

## General Questions

### What is Project Referrer Chain Explorer?
Project Referrer Chain Explorer is a Visual Studio extension that helps developers visualize project dependencies and manage versions across .NET solutions. It provides interactive dependency trees, automated version management, and Git integration for change detection.

### Which Visual Studio versions are supported?
Currently, the extension supports Visual Studio 2022 (all editions: Community, Professional, and Enterprise). Support for Visual Studio 2019 is planned for a future release.

### What project types are supported?
The extension works with C# projects (.csproj files). It supports both .NET Framework and .NET Core/.NET 5+ projects. VB.NET and F# projects are not currently supported.

### Is the extension free?
Yes, Project Referrer Chain Explorer is completely free and open-source under the MIT License.

## Installation and Setup

### How do I install the extension?
You can install the extension in several ways:
1. **Visual Studio Marketplace**: Search for "Project Referrer Chain Explorer" in Extensions ? Manage Extensions
2. **Manual Installation**: Download the VSIX file and double-click to install
3. **Command Line**: Use VSIXInstaller.exe with the downloaded VSIX file

For detailed installation instructions, see our [Installation Guide](Installation.md).

### Why doesn't the extension appear in my context menu?
The extension only appears for C# projects (.csproj files). Make sure you're:
- Right-clicking on a C# project (not a solution or folder)
- Using Visual Studio 2022
- Have the extension enabled in Extensions ? Manage Extensions

### The extension installed but isn't working. What should I do?
Try these troubleshooting steps:
1. Restart Visual Studio completely
2. Check Extensions ? Manage Extensions to ensure it's enabled
3. Verify you're using Visual Studio 2022
4. Try right-clicking on a C# project specifically

## Using the Extension

### How do I get started with the extension?
1. Open a solution with C# projects in Visual Studio 2022
2. Right-click on any C# project in Solution Explorer
3. Select "Show Referrer Chain" from the context menu
4. Select projects you want to analyze
5. Click "Generate Tree" to visualize dependencies

### What do the different colors in the tree mean?
The color-coding indicates project status:
- ?? **Green**: Clean projects (no changes)
- ?? **Yellow**: NuGet or project reference changes
- ?? **Blue**: Version-only changes
- ?? **Orange**: Both reference and version changes
- ?? **Red**: Other modifications detected
- ?? **Grey**: Excluded from version updates

### How does Git integration work?
The extension automatically analyzes your Git repository to:
- Detect uncommitted changes in project files
- Identify NuGet package modifications
- Track project reference changes
- Display change information in tooltips and status indicators

You don't need to configure anything - just ensure your solution is in a Git repository.

### Can I exclude projects from version updates?
Yes! You can exclude projects in two ways:
1. **Right-click** on a project in the project list and select "Exclude from Version Updates"
2. **Settings tab**: Manage exclusions in the Settings tab

Excluded projects appear grey and italic in the UI and are skipped during version updates.

## Version Management

### How does automatic version bumping work?
When you bump a project's version:
1. The selected project gets the specified version increment (Major, Minor, Patch, or Revision)
2. Projects that depend on the updated project automatically get revision bumps
3. The changes propagate through the dependency chain
4. Excluded projects are skipped in the propagation

### What files are updated during version changes?
The extension updates version information in:
- **.csproj files**: `<Version>`, `<AssemblyVersion>`, and `<FileVersion>` elements
- **AssemblyInfo.cs files**: `AssemblyVersion` and `AssemblyFileVersion` attributes

### What happens if there are version conflicts?
If the extension detects conflicting version changes (different files showing different versions), it will:
1. Display a detailed conflict dialog
2. Show all conflicting files and their versions
3. Allow you to resolve conflicts manually
4. Prevent automatic updates until conflicts are resolved

### Can I undo version changes?
Version changes are made to your working directory and tracked by Git. You can:
- Use Git to revert the changes: `git checkout -- .`
- Use Visual Studio's undo functionality
- Manually edit the files to restore previous versions

## Features and Functionality

### What layout options are available?
The extension offers three layout options:
1. **Standard (Tree)**: Traditional hierarchical layout, best for small to medium solutions
2. **Compact Horizontal**: Horizontal arrangement, good for wide screens and many parallel dependencies
3. **Compact Vertical**: Vertical arrangement, optimal for deep dependency chains

### How do I export dependency trees?
Click the "Export PNG" button on the Tree Output tab. This creates a high-quality PNG image of the entire dependency tree at full resolution, regardless of screen size.

### What themes are available?
The extension includes three themes:
- **Dark**: Dark backgrounds with light text (default)
- **Light**: Light backgrounds with dark text
- **Slate**: Blue-grey modern theme

You can change themes in the Settings tab.

### How do I filter and sort projects?
Use the controls on the Project Selection tab:
- **Filter box**: Type to search by project name (case-insensitive)
- **Sort dropdown**: Choose "Name" or "Status" sorting
- **Bulk selection**: Use "Select All", "Select None", or "Select Modified" buttons

## Performance and Limitations

### How well does the extension perform with large solutions?
The extension is optimized for solutions with up to 100+ projects. For very large solutions (500+ projects):
- Use project filtering to work with subsets
- Consider the Compact layout options for better performance
- Exclude unnecessary projects (tests, tools) from analysis

### Are there any memory limitations?
The extension is designed to be memory-efficient, but very large dependency trees may consume significant memory. If you experience issues:
- Filter to smaller project subsets
- Use Compact layout options
- Close other Visual Studio windows to free up memory
- Restart Visual Studio if memory usage becomes excessive

### What are the system requirements?
- **OS**: Windows 10 (1903+) or Windows 11
- **Visual Studio**: 2022 (any edition)
- **Memory**: 4GB minimum, 8GB+ recommended for large solutions
- **Disk**: 50MB for extension files
- **Git**: Required for change detection features

## Troubleshooting

### Git status isn't being detected correctly
Ensure that:
- Your solution is in a Git repository (has a .git folder)
- Git is installed and accessible from the command line
- The repository isn't corrupted
- You have proper permissions to access the repository

### Tree generation fails or is very slow
Try these solutions:
- Reduce the number of selected projects
- Use project filtering to work with smaller subsets
- Check available system memory
- Ensure project files aren't corrupted
- Try restarting Visual Studio

### Version updates aren't working
Verify that:
- Project files aren't read-only
- You have write permissions to project directories
- No other applications have the files open
- The project file format is supported (.csproj)

### The extension crashes or causes Visual Studio instability
If you experience crashes:
1. Restart Visual Studio
2. Try with a smaller solution or fewer projects
3. Check the Visual Studio Activity Log for error details
4. Report the issue on our [GitHub Issues page](https://github.com/wolvepack/project-referrer-versioning/issues)

## Advanced Usage

### Can I integrate this with CI/CD pipelines?
While the extension is designed for interactive use in Visual Studio, you can:
- Export dependency trees for documentation
- Use Git integration to understand change impacts
- Coordinate version updates with your CI/CD process

### How do I handle circular dependencies?
The extension detects and handles circular dependencies by:
- Showing warning indicators for circular references
- Preventing infinite loops in tree generation
- Displaying circular paths in tooltips

### Can I customize the visualization colors or themes?
Currently, customization is limited to the three built-in themes. Custom theme support is planned for a future release.

### Is there an API for programmatic access?
The extension doesn't currently provide a public API, but this is being considered for future versions to enable integration with other tools.

## Getting Help

### Where can I get support?
- **Documentation**: Check this FAQ and our comprehensive guides
- **GitHub Issues**: Report bugs and request features
- **GitHub Discussions**: Ask questions and get community help
- **Email**: Contact support@wolvepack.dev for direct assistance

### How do I report bugs or request features?
1. Visit our [GitHub Issues page](https://github.com/wolvepack/project-referrer-versioning/issues)
2. Search existing issues to avoid duplicates
3. Use our issue templates to provide detailed information
4. Include system information, steps to reproduce, and screenshots when relevant

### How can I contribute to the project?
We welcome contributions! See our [Contributing Guide](CONTRIBUTING.md) for details on:
- Code contributions
- Documentation improvements
- Bug reports and testing
- Feature suggestions

### Where can I find the source code?
The project is open-source and available on GitHub:
[https://github.com/wolvepack/project-referrer-versioning](https://github.com/wolvepack/project-referrer-versioning)

---

*Don't see your question here? Check our [User Guide](UserGuide.md) for more detailed information, or ask in our [GitHub Discussions](https://github.com/wolvepack/project-referrer-versioning/discussions).*