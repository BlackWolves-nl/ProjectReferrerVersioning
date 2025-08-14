# Project Referrer Chain Explorer

[![Visual Studio Marketplace](https://img.shields.io/visual-studio-marketplace/v/WolvePack.WolvePack-VS-Extensions-ProjectReferrerVersioning?style=flat-square&logo=visual-studio&label=VS%20Marketplace)](https://marketplace.visualstudio.com/items?itemName=WolvePack.WolvePack-VS-Extensions-ProjectReferrerVersioning)
[![License](https://img.shields.io/badge/License-WolvePack%20Custom-blue?style=flat-square)](LICENSE)
[![Version](https://img.shields.io/badge/Version-2.3.0.0-green?style=flat-square)](CHANGELOG.md)

> **Visualize, understand, and manage complex project dependencies in your Visual Studio solutions with automated version coordination.**

*Transform chaotic dependency management into clear, actionable insights.*

![Project Referrer Chain Explorer Preview](preview.png)

## 🎯 **Why This Extension Exists**

### **The Problem We Solve**

Working with large .NET solutions is **painful** when you need to:

- **🔍 Understand Dependencies**: *"Which projects depend on this library?"*
- **📦 Coordinate Version Updates**: *"If I update this project, what else needs updating?"*
- **🔄 Track Change Impact**: *"How do my Git changes affect the dependency chain?"*
- **⏰ Save Development Time**: *"Why does version management take hours across 20+ projects?"*

### **Manual Dependency Management is Broken**

**Before this extension:**
- ❌ Hunt through Solution Explorer to find dependencies
- ❌ Manually track which projects reference each other
- ❌ Update versions one-by-one across multiple projects
- ❌ Miss critical dependencies during releases
- ❌ Spend hours coordinating version bumps

**With Project Referrer Chain Explorer:**
- ✅ **Instant visual dependency mapping** - See the entire chain at a glance
- ✅ **Automated version propagation** - Update root projects, children follow automatically
- ✅ **Git-integrated change tracking** - Know exactly what's been modified
- ✅ **One-click mass operations** - Select, update, and coordinate effortlessly
- ✅ **Save hours per release cycle** - Focus on coding, not dependency management

---

## 🚀 **Quick Start**

### **Installation**

**Option 1: Visual Studio Marketplace (Recommended)**
1. Open Visual Studio 2022
2. Go to **Extensions → Manage Extensions**
3. Search for **"Project Referrer Chain Explorer"**
4. Click **Download** and restart Visual Studio

**Option 2: Direct Download**
- Download from [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=WolvePack.WolvePack-VS-Extensions-ProjectReferrerVersioning)

### **First Use (30 seconds)**

1. **Open any .NET solution** in Visual Studio
2. **Right-click on a project** in Solution Explorer (for direct project selection) or open the Extensions menu
3. **Select "Show Referrer Chain"** from context menu
4. Select project and click generate
5. **Watch the magic happen** ✨

*That's it! Your dependency tree appears instantly.*

---

## 💡 **How to Use**

### **Basic Workflow**

```
📁 Open Solution → 🎯 Select Projects → 🌳 Generate Tree → 📈 Manage Versions → 💾 Update Files
```

### **Step-by-Step Guide**

#### **1. 🎯 Select Your Projects**
- Open the extension via **Extensions → Project Referrer Chain Explorer**
- **Check projects** you want to analyze
- Use **filters** to quickly find specific projects
- **Click rows** to toggle selection (or use checkboxes)

#### **2. 🌳 Visualize Dependencies**
- Click **"Generate Tree"** to create your dependency map
- See **who references what** in an interactive tree view
- **Zoom and scroll** to navigate large dependency chains
- **Hover nodes** to highlight related projects

#### **3. 📊 Understand Changes**
- **Color-coded status indicators** show Git changes:
  - 🟢 **Clean** - No uncommitted changes
  - 🟡 **Modified** - Has local modifications
  - 🔵 **NuGet/Refs** - Package or reference changes
  - 🟣 **Version** - Version-only changes
- **Git badges** display changed files/lines count
- **Tooltips** reveal detailed change information

#### **4. 📈 Coordinate Version Updates**
- **Right-click any project** in the tree
- **Select version bump type**: Major, Minor, Patch, or Revision
- **Watch automatic propagation** to dependent projects
- **Root badge indicators** show which projects were originally selected

#### **5. 💾 Apply Changes**
- Review version changes in the tree view
- Click **"Update Versions"** to write changes to project files
- Updates both **.csproj** and **AssemblyInfo.cs** files
- Get **detailed success/error reporting**

### **Pro Tips**

- **🔄 Minimize Chain Drawing**: Enable in settings to simplify complex trees
- **🎨 Theme Selection**: Choose Dark or Slate theme for better visibility
- **📤 Export Trees**: Save dependency diagrams as PNG for documentation
- **⚙️ Exclude Projects**: Prevent certain projects from version updates
- **🔍 Advanced Filtering**: Use text filters to focus on specific project subsets

---

## ✨ **Key Features**

### **🌳 Intelligent Dependency Visualization**
- **Interactive tree views** with zoom, scroll, and multiple layout modes
- **Smart chain minimization** eliminates redundant visualization
- **Root badges** clearly mark originally selected projects
- **Real-time highlighting** shows relationships on hover

### **🔧 Automated Version Management**
- **One-click version bumping** (Major, Minor, Patch, Revision)
- **Intelligent propagation** to dependent projects
- **Exclusion support** for projects that shouldn't be updated
- **Atomic updates** across multiple project files

### **🔄 Git Integration**
- **Change detection** for NuGet packages and project references
- **Uncommitted change tracking** with file/line counts
- **Status-based coloring** for instant change visibility
- **Detailed tooltips** showing what changed

### **🎨 Professional User Experience**
- **Multiple themes** (Dark, Slate) with full theming support
- **Responsive interface** optimized for large solutions
- **Export capabilities** for documentation and sharing
- **Persistent settings** per solution

### **⚡ Performance Optimized**
- **Efficient Git analysis** with 97% reduction in Git calls
- **Background processing** keeps UI responsive
- **Memory optimized** for large solutions (34+ projects tested)
- **Fast project discovery** and dependency mapping

---

## 🎬 **See It In Action**

### **Before vs After Comparison**

**🔴 Before: The Manual Way**
```
1. Open Solution Explorer
2. Click through each project
3. Check References manually
4. Track dependencies in notepad
5. Update versions one by one
6. Hope you didn't miss anything
```

**🟢 After: The Smart Way**
```
1. Right-click → Show Referrer Chain
2. View complete dependency tree
3. Right-click → Select version bump
4. Watch automatic propagation
5. Click "Update Versions"
6. Done! ✨
```

### **Real-World Impact**

> *"What used to take our team 3 hours of careful coordination now takes 10 minutes. We can focus on building features instead of managing dependencies."*  
> — Development Team Lead

> *"The visual dependency tree instantly revealed circular references we didn't know existed. Game changer for our architecture planning."*  
> — Senior Architect

---

## 📋 **Requirements**

- **Visual Studio 2022** (Community, Professional, or Enterprise)
- **.NET Framework 4.7.2** or higher
- **Git repository** (optional, for change detection features)
- **C# projects** (.csproj format support)

---

## 🆘 **Common Use Cases**

### **📦 Library Maintenance**
- Update a core library and propagate versions to all consumers
- See impact radius before making breaking changes
- Coordinate multi-project releases

### **🔍 Architecture Analysis**
- Understand project dependencies in inherited codebases
- Identify circular dependencies and architectural issues
- Document system architecture with visual diagrams

### **🚀 Release Planning**
- Plan version updates across complex solutions
- Understand Git change impact across project boundaries
- Coordinate team releases with visual dependency insights

### **🧹 Technical Debt Management**
- Identify overly coupled projects
- Plan dependency reduction strategies
- Visualize refactoring impact

---

## 🤝 **Community & Support**

### **Get Help**
- 📖 **[Documentation](Documentation/)** - Comprehensive guides and tutorials
- ❓ **[FAQ](Documentation/FAQ.md)** - Common questions and solutions
- 💬 **[GitHub Discussions](https://github.com/BlackWolves-nl/ProjectReferrerVersioning/discussions)** - Community support
- 🐛 **[Issue Tracker](https://github.com/BlackWolves-nl/ProjectReferrerVersioning/issues)** - Bug reports and feature requests

### **Contributing**
- 🔧 **Code contributions** - See [Contributing Guide](CONTRIBUTING.md)
- 📝 **Documentation improvements** - Help make guides clearer
- 🧪 **Testing and feedback** - Try new features and report issues
- 💡 **Feature suggestions** - Share your workflow needs

---

## 📜 **License**

This project is licensed under the WolvePack Custom License - see the [LICENSE](LICENSE) file for details.

---

## 🏆 **Why Choose Project Referrer Chain Explorer?**

### **Built by Developers, for Developers**
- **Real-world tested** on enterprise solutions with 50+ projects
- **Performance optimized** for daily development workflows
- **Continuously improved** based on developer feedback

### **Professional Grade Quality**
- **Robust error handling** for production reliability
- **Comprehensive testing** across different solution types
- **Professional support** and active maintenance

### **Time-Saving Impact**
- **Hours to minutes** - Transform dependency management overhead
- **Visual clarity** - Eliminate guesswork in complex solutions
- **Automated coordination** - Reduce human error in version management

---

**🚀 Ready to transform your dependency management? [Get started now!](https://marketplace.visualstudio.com/items?itemName=WolvePack.WolvePack-VS-Extensions-ProjectReferrerVersioning)**


---

<div align="center">

**[⬇️ Download from Marketplace](https://marketplace.visualstudio.com/items?itemName=WolvePack.WolvePack-VS-Extensions-ProjectReferrerVersioning)** • 
**[📖 Read the Docs](Documentation/)** • 
**[🐛 Report Issues](https://github.com/BlackWolves-nl/ProjectReferrerVersioning/issues)** • 
**[💬 Join Discussion](https://github.com/BlackWolves-nl/ProjectReferrerVersioning/discussions)**

*Made with ❤️ by the WolvePack team*

</div>