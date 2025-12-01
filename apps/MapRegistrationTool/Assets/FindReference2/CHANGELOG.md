# Changelog
All notable changes to this project are documented in this file.

## [2.6.10] - 2025-11-18
### Added / Dev
- **FR2_Scope System** - new scoping system for code readability and organization
- **Hierarchy Usage Count Display** - show reference counts in Unity hierarchy
- **Scene Reference Grouping** - group similar scene references (same script & target)
- **Hierarchy Path Clipping** - auto-clip hierarchy paths when space limited
- **Selection Change Debouncing** - debounced selection updates for performance

### Fixed
- **Model Asset Scanning** - model assets constantly added/scanned & removed
- **FileInfo Timestamp Issues** - critical FileInfo timestamp handling
- **Scene Cache Performance** - multiple scene cache improvements

### Technical Improvements
- **Memory Optimization** - reduced GC allocation, reused arrays, eliminated LINQ
- **Performance Enhancements** - capacity checks, cached selection, optimized rendering
- **Code Organization** - applied FR2_Scope throughout codebase
- **Settings Layout** - reorganized settings panel

---

## [2.6.9] - 2025-11-09
### Added / Dev
- **Enhanced Cache Management** - FR2_Cache lifecycle and project management
- **Performance Monitoring** - profiler logic for performance tracking

### Technical Improvements
- **Cache Architecture** - cleaned up and reorganized FR2_Cache structure
- **Performance Optimizations** - check capacity before operations for speed
- **Code Cleanup** - removed obsolete cache management code

---

## [2.6.8] - 2025-11-07
### Added / Dev
- **Prefab Reference Filtering** - filter prefab asset references from children GameObjects & components
- **Enhanced Profiler Integration** - comprehensive profiler logic for performance analysis

### Fixed
- **Cache Initialization** - refactored FR2_Cache initialization flow for reliability
- **Scene Cache Performance** - FR2_SceneCache operations performance

### Technical Improvements
- **Content Loader Enhancements** - asset content loading mechanisms
- **Async Processing** - async processor handling in FR2_Cache
- **Various Stability Fixes** - multiple stability improvements

---

## [2.6.7] - 2025-10-10
### Added / Dev
- **SerializedObject Extensions** - extension methods for Unity integration
- **GUI Improvements** - refactored GUI system with extension method support

### Technical Improvements
- **Scene Cache Performance** - initial SceneCache performance improvements
- **Extension Method Architecture** - refactored GUI & extension methods for maintainability
- **Performance Profiling** - initial profiler logic for performance monitoring

---

## [2.6.6] - 2025-09-28
### Added / Dev
- **ClippedLabel System** - new component for handling long asset paths with proper text clipping
- **Scene Reference Info Area** - enhanced SceneRef UI with reference information display
- **Sibling Action Support** - apply same action (select/unselect + expand/collapse) to sibling items
- **Light Theme Icon Improvements** - better icon colors and theming for light theme users

### Fixed
- **SplitView Width Persistence** - panels now properly preserve width after Unity reloads
- **SplitView Refresh Issues** - corrected refresh behavior and layout calculations
- **Sort Mode Functionality** - fixed sorting not working properly in reference trees
- **Bookmark Panel Issues** - removed inappropriate "show detail" button and scene object support
- **Addressable Label Layout** - improved layout and spacing for addressable asset labels
- **Asset Drawing Performance** - optimized drawing with new caching system
- **Usage Count Display** - fixed messed up usage count rendering
- **Detail & Properties Buttons** - corrected button drawing and interaction
- **Prefab Editing** - now uses PrefabUtility to maintain prefab connections
- **Enum Selector Mouse Events** - fixed selector not working due to swallowed mouse events
- **Themed Color Consistency** - unified color usage across asset folders and extensions
- **Available Space Handling** - improved layout calculations for long asset paths
- **Inconsistent UI Coloring** - standardized colors across different UI components

### Technical Improvements
- **Asset Draw Cache System** - new caching mechanism for improved drawing performance
- **Enhanced Theme System** - expanded theme support with better color management
- **Code Organization** - restructured drawing logic for better maintainability
- **Performance Optimizations** - reduced redundant calculations and improved rendering speed

---

## [2.6.5] - 2025-09-04
### Added / Dev
- **Delayed Auto Refresh** - auto refresh now waits until FR2 panel is in focus for better performance
- **Enhanced Scene Refresh** - scene refresh is now delayed until focus for improved responsiveness

### Fixed
- **Flexible Splits** - FR2_Split now properly respects minPixel constraints
- **UI Spacing Adjustments** - improved layout consistency across panels
- **Detail Panel Issues** - fixed detail panel not working sometimes for scene to asset navigation
- **Layout Clicking Issues** - fixed popping layout that made it hard to click properties and detail buttons

### Technical Improvements
- **Performance Optimization** - improved performance by caching dirty status
- **Code Clean Up** - various code improvements and optimizations

---

## [2.6.4] - 2025-01-25
### Added / Dev
- **Persistent Panel Size Settings** to remember user preferences
- **Selection Shortcuts** for easy multi-selection workflows
- **Enhanced Bookmark UX** with improved navigation and management

### Fixed
- **SplitView Resize Error** when some panels are hidden
- **RefDrawer Context Issues** with improved configuration handling

### Technical Improvements
- **Various Fixes and Clean Up** for better stability
- **Improved Configuration Management** for RefDrawer components

---

## [2.6.3] - 2025-01-24
### Added / Dev
- **Properties Panel** with detail button for enhanced asset inspection
- **Assembly Reload Support** for better development workflow
- **Asset IncrementalRefresh Improvements** for better performance

### Fixed
- **UIToolkit Relative Path Dependencies** now properly supported
- **Debug Logging** cleanup and FR2_Dev.NoLog improvements

### Technical Improvements
- **Enhanced Properties Display** with better UI integration
- **Improved Development Tools** for easier debugging

---

## [2.6.2] - 2025-01-23
### Added / Dev
- **Enhanced Asset Cache Management** - only critical assets with references are saved
- **Improved Empty Result Messages** with better user guidance
- **Auto-Expand Asset Info** in FR2_CacheEditor for easier inspection
- **Package Assets Reading** - no longer excludes packages assets by default

### Fixed
- **FR2_Define Logic** errors resolved
- **Ignore List Management** - can now properly remove items
- **Scene Incremental Refresh** in prefab mode
- **Force Refresh Issues** for first-time use and menu selection
- **EOL Handling** for FR2_TreeUI and missing FR2_Readme.pdf
- **Various Null Checks** for better stability

### Technical Improvements
- **PingRow Configuration** removal and theme color adjustments
- **Initial Refresh Logic** improvements
- **UI/UX Enhancements** for non-scanned or dirty assets

---

## [2.6.1] - 2025-07-31
### Added / Dev
- **Incremental Refresh System** for Assets panel - only processes dirty/unscanned assets instead of full refresh
- **Enhanced Visual Feedback** with improved dirty state indicators and status messages
- **FR2_Theme System** with centralized UI constants for Light/Dark themes
- **Conditional Debug Logging** with FR2_LOG class for cleaner release builds
- **Persistent Dirty State** across Unity recompiles for better UX
- **Smart Asset Scanning Status** detection to distinguish never-scanned vs. no-references
- **Toggle FR2 Debug** option for development builds

### Fixed
- **Selection Panel** refresh button incorrectly showing (now hidden as intended)
- **Asset Panel Tooltips** not matching visual state (yellow title but "ready" tooltip)
- **Confusing Status Messages** improved to be more actionable ("hit Refresh for complete results")
- **HasChanged Flag** now properly serialized and persistent across recompiles
- **FR2_Define Logic** errors after modifying csc.rsp files
- **FR2_SelectionManager** initialization issues in some scenarios
- **Scene Scan Stuck** issue with better state management

### Technical Improvements
- **Message Type Detection** for warning vs info boxes based on content
- **Refresh Button Sizing** increased width for better text visibility
- **Asset Dirty State Logic** enhanced to include unscanned assets
- **Conditional Compilation** setup for debug/development builds
- **AssetDatabase Validation** integration for better accuracy

---

## [2.6.0] - 2025-01-20
### Added / Dev
- **Smart Lock** system for better selection handling  
- **Selection Navigation History** with back/forward buttons  
- **Unified Selection Manager** for centralized selection state  
- **Scene Reference UI revamp** with improved drawing & interaction  
- **Unity 6 compatibility** with new window focus API  
- **Basic shader references** support  
- **Highlight for out-of-sync selection** status  
- **UIToolkit resources** reference finding improvements  
- **Recursive unused asset checking** option in Tools panel  
- **Improved empty result messages** with contextual feedback  
- **Lock icon visibility** improvements when selection is locked  

### Fixed 
- **Folder usage visibility** in FR2 panel  
- **SetEditorsExpanded workaround** & serialized property path expansion  
- **Extensions normalization** before grouping  
- **Font assets reference** detection  
- **UsedBy panel refresh** issues  
- **Unity 2019.4 compatibility** fixes  
- **Selection cache improvements** & synchronization  

### Technical Improvements
- **Restructured selection logic** with proper separation of concerns  
- **Enhanced scene cache system** with intelligent change tracking  
- **Better Unity version compatibility** handling  
- **Improved performance** for selection operations  

---

## [2.5.13] - 2025-06-16
### Added / Dev
- Project-architecture docs & cursor rules  
- Refactor and code formatting  
- **Remove Missing Scripts** tool  
- Hide tool warning banner  
- Recursive *unused-asset* checking  
- Git integration  
- Duplicate-tab improvements  
- **AssetOrganizer** & *Delete Empty Folders*

### Fixed
- Scene did not finish refreshing in Play Mode  
- Unknown assets wrongly marked *unused*  
- Parser crash when reference was missing or empty

---

## [2.5.12] - 2025-05-04
### Added / Dev
- Option to write **FR2 Import Log** to file (only for assets ≥ 1 MB)  
- More aggressive RAM clean-up after load  
- `AssetDatabase` references to speed up **LoadContent**  
- Buffered file reading for faster I/O  
- Serialize only *critical* (referenced) assets  
- Import-process UX tweaks

---

## [2.5.11] - 2024-12-06
### Added / Dev
- Remove *Scan Priority* GUI  
- Extra view-customisation options in *Usage / Used-By* tree  
- README & FR2 version update

### Fixed
- “Ping asset” could fire twice

---

## [2.5.10] - 2024-11-10
### Added / Dev
- Hide-ignore-root grouping & layout tweaks  
- Customisable **Show Full Path**

### Fixed
- FR2 inactive in Play Mode even when *Enable = true*  
- Grouping for files without extensions

---

## [2.5.9] - 2024-10-13
### Added / Dev
- Basic support for built-in assets  
- “+” indicator for Sprite Atlases included in build  
- **Show Full Path** toggle  
- Hide-ignore-root grouping

### Fixed
- Sprite Atlas *Force Include* handling  
- Sprite Atlases with all sprites unused now marked *unused*  
- More generous GUID detection  
- Wrong GUID extracted for `.asmdef`  
- Various fixes in `FR2_Asset` & `FR2_Addressable`

---

## [2.5.8] - 2024-09-01
### Added / Dev
- Addressables support  
- Item-count on groups  
- Even spacing in **TabView**  
- Light-map support  
- Classes marked `internal` for tighter API

### Fixed
- `TabRect` calculation  
- UI spacing in extension  
- Crash on re-import / save in Unity 2020.x

---

## [2.5.7] - 2024-08-11
### Added / Dev
- UIToolkit (`.uss`, `.uxml`, `.tss`) support  
- `.spriteLib` support  
- Asset-extension column  
- Cleaned YAML/JSON parser

### Fixed
- GUID/FileID replacement no longer alters line endings (Windows)  
- Unity 2019.4 compatibility fixes

---

## [2.5.6] - 2024-07-04
### Added / Dev
- Exclude *Packages/* assets by default  
- `.shadergraph` & `.playable` support  
- 64-bit (`long`) `LocalFileID`  
- LazyInit **FR2_Unity** & faster start-up  
- Hide sub-asset Local IDs by default  
- Play-Mode optimisations  
- Icon update

### Fixed
- Null exceptions (tool focus, `TerrainTextureData`)  
- Layout error when exiting Play Mode  
- Various null checks & missing namespaces

---

## [2.5.5] - 2024-06-26
### Added / Dev
- All tools moved to **Tool** tab  
- Selection History  
- Layout improvements  
- Draw *use count* only on main asset  
- Separate **Group Mode** for tools  
- Smarter auto show/hide (asset + scene + detail)

### Fixed
- Light-map assets no longer listed as *unused*  
- Misc. null-reference fixes

---

## [2.5.4] - 2024-05-25
### Added / Dev
- Initial public release of FR2 (core functionality)

---

_This file follows [Keep a Changelog](https://keepachangelog.com) and [SemVer](https://semver.org/)._