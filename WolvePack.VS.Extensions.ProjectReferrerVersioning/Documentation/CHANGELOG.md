# Changelog

## [2.3.0.0] - Root Badge and Enhanced Context Menu Support

### Visual Enhancements
- **Root Badge with Version Feedback**: Added distinctive "R" badge in top-left corner for all originally selected projects
  - Appears on all originally selected projects, even when they become child nodes in minimized chain drawing
  - Dynamic border color: Green when version is selected, white when pending
  - Perfectly centered text using Grid layout for consistency with other badges
  - Clear visual indication of project selection status and version progress

### Context Menu Improvements
- **Universal Context Menu Access**: Version selection context menus now available for all originally selected projects
  - Fixed limitation where only drawn root nodes had context menus
  - All originally selected projects can now be right-clicked for version selection regardless of chain position
  - Consistent behavior whether minimized chain drawing is enabled or not
  - Major, Minor, Patch, and Revision version options available for all root projects

### Version Management Enhancements
- **Improved Version Bump Logic**: Fixed version propagation logic for minimized chain drawing scenarios
  - Version bumps now trigger when ALL originally selected projects have versions (not just drawn root nodes)
  - Enhanced tracking of originally selected projects throughout chain filtering process
  - Proper version propagation to child projects regardless of chain minimization
  - Added `WasOriginallySelected` property to `ReferrerChainNode` for accurate tracking

### User Experience Improvements
- **DataGrid Row Click Selection**: Enhanced project selection usability
  - Click anywhere on a DataGrid row to toggle project selection
  - Smart click detection preserves checkbox functionality
  - Intuitive selection behavior following standard UI patterns
  - Dual selection methods: both row clicks and checkboxes work seamlessly
  - No conflicts with existing checkbox behavior

### Technical Improvements
- **Better State Management**: Separated UI selection state from originally selected tracking
  - Added `WasOriginallySelected` property to prevent state corruption
  - Improved chain filtering logic to preserve originally selected project information
  - Enhanced version bump calculations for complex dependency scenarios
  - Proper visual tree traversal for accurate click target detection

### Bug Fixes
- **Fixed Context Menu Accessibility**: Resolved issue where originally selected projects lost context menus when becoming child nodes
- **Fixed Version Bump Timing**: Corrected logic where version bumps only triggered for drawn roots instead of all selected projects
- **Fixed Root Badge Display**: Eliminated issue where all projects showed root badge due to state corruption

## [2.2.2.5] - Git Service Refactor

## Git Service Refactor
- **Refactored Git service**: Major overhaul of Git service to improve performance and reliability

## [2.2.2.2] - Debug and Troubleshooting Enhancements

### Debug and Troubleshooting Enhancements
- **User-controllable debug logging**: Added debug logging toggle in Settings tab for easy troubleshooting
- **Enhanced Git root detection**: Comprehensive logging and multiple fallback methods for Git repository detection
- **Real-time debug control**: Debug logging can be enabled/disabled immediately without restarting the extension
- **Improved error diagnostics**: Enhanced logging throughout Git analysis and project discovery processes
- **Debug log file location**: Logs are written to `C:\Temp\WolvePack.PRV.txt` with timestamp and thread information
- **Git worktree support**: Enhanced Git root detection now supports Git worktrees (`.git` files) in addition to standard repositories
- **Fallback mechanisms**: Multiple strategies for finding Git root when primary method fails
  - Solution file directory analysis
  - Project file directory traversal  
  - Current working directory fallback
- **Comprehensive path logging**: Detailed logging of directory paths during Git root search for troubleshooting
- **Settings persistence**: Debug logging preference is saved per user and persists across sessions

## [2.2.2.0] - UI and Performance Improvements

### DataGrid Enhancements
- **Fixed DataGrid row highlighting issues**: Row highlighting now works consistently across all scenarios
- **Improved custom ControlTemplate**: Implemented reliable hover highlighting that bypasses child element interference  
- **Enhanced theme integration**: DataGrid now properly uses `DataGridRowHoverBrush` and `DataGridRowSelectedBrush` for consistent theming
- **Disabled row selection**: DataGrid rows and cells are no longer selectable, reducing user confusion while maintaining all functionality
- **Applied ExcludedProjectForegroundConverter**: Text columns now use proper converters for excluded project styling
- **Fixed excluded project visibility**: Improved contrast between excluded project text and hover background colors
  - Dark theme: Excluded text now uses `#A0A0A0` (was `#808080`) for better visibility on hover
  - Slate theme: Excluded text now uses `#B0B8C0` (was `#808080`) for excellent contrast

### Performance Optimizations
- **Major Git analysis performance improvement**: Optimized `AnalyzeGitStatusAsync` to find Git root once from solution location instead of per-project
- **Reduced Git command executions**: Git status is now fetched once and shared across all projects (97% reduction in Git calls)
- **Enhanced Git service methods**: Added `AnalyzeProjectWithChangedFilesAsync` for efficient analysis using pre-fetched file lists
- **Improved startup performance**: Significantly faster project loading, especially noticeable with large solutions (34+ projects)

### User Experience
- **Better visual feedback**: Excluded projects maintain readability while being visually distinct (dimmed and italic)
- **Consistent hover behavior**: Rows highlight properly while preserving alternating row colors
- **Improved accessibility**: Better contrast ratios for better readability across all themes
- **Checkbox functionality preserved**: All interactive controls remain fully functional

## [2.2.1.0] - Theme Improvements

- Theme switching now updates all controls and backgrounds live, including ScrollViewer, TabControl, TabItem, Borders, and status bar.

## [2.2.0.0] - Initial Release

- Project Referrer Tree Visualization: Zoomable, scrollable tree view with multiple layouts (Standard, Compact Horizontal, Compact Vertical).
- Color-coded nodes for project status (modified, clean, visited, NuGet/project ref/version changes).
- Status-based node coloring and unique path-based tagging for correct highlighting.
- Tooltips for NuGet and project reference changes (added, removed, edited).
- Hover effect: Highlights nodes and edges in the referrer path and all nodes for the same project.
- Git integration: Badge shows total changed files/lines; expands to pill on hover for details.
- Version management: Bump versions (Major, Minor, Patch, Revision) from UI, propagate to children, and update project files.
- Exclusion support: Exclude projects from version updates, shown in dull grey/italic.
- Export tree as PNG, copy graph info to clipboard.
- Filtering, sorting, and quick selection (all/none/modified).
- Visual legend and status bar for project/graph statistics.
- Theme support: Dark, Light, Slate. All UI elements use theme background/foreground.
- Robust file editing for version updates.
- User settings for theme, layout, and exclusions are persisted per solution.

---

**Extension Version:** 2.3.0.0
