# ArkPlot WPF to Avalonia 迁移计划

将 `ArkPlotWpf` 迁移到 Avalonia 是一个很好的选择，可以实现跨平台（Windows, macOS, Linux）并利用现代 UI 技术。由于 Avalonia 的 API 和 XAML 语法与 WPF 非常相似，大部分业务逻辑和视图模型代码都可以重用。

**核心策略:**

1. **创建共享库:** 创建一个新的 `.NET Standard` 或 `.NET 8` 类库项目（例如 `ArkPlot.Core`），用于存放所有非 UI 相关的代码，包括 Models, ViewModels, Services, Data Access 等。
2. **新建 Avalonia 项目:** 在解决方案中添加一个新的 Avalonia 应用项目（例如 `ArkPlot.Avalonia`）。
3. **分层迁移:**
   - 首先，将业务逻辑和数据层代码迁移到共享库 `ArkPlot.Core`。
   - 然后，逐个将 XAML 视图和样式从 `ArkPlotWpf` 迁移到 `ArkPlot.Avalonia`，并进行适配。
   - 最后，处理平台相关的特性和依赖。

---

## 迁移 TODO List

#### Phase 1: 项目设置和基础架构

- [x] **1. 创建共享核心库:**
  - [x] 在解决方案中创建一个新的 **类库** 项目，命名为 `ArkPlot.Core`。
  - [x] 将 `ArkPlotWpf` 项目中的 `Model`, `Data`, `Services`, `Utilities` 文件夹下的所有 C# 文件复制到 `ArkPlot.Core` 项目中。
- [x] **2. 调整 `ArkPlot.Core` 依赖:**
  - [x] 复制`ArkPlotWpf.csproj`到 `ArkPlot.Core`，改名并调整其引用，使其脱离WPF相关的依赖，成为一个纯粹的Core
  - [x] 编写powershell脚本，将 `ArkPlot.Core` 项目中的代码所有的NameSpace修正，并移除所有 WPF 特定的引用（如 `PresentationFramework`, `System.Xaml` 等）。
  - [x] 修复因移除 WPF 引用而产生的编译错误。例如，`System.Windows.Media.Color` 可能需要替换为 Avalonia 或其他图形库中的 `Color` 类型。
- [ ] **3. 创建新的 Avalonia 项目:**
  - [ ] 在解决方案中使用avalonia.mvvm模板创建一个新的 **Avalonia App** 项目，命名为 `ArkPlot.Avalonia`。
  - [ ] 让 `ArkPlot.Avalonia` 项目引用 `ArkPlot.Core` 项目。
- [ ] **4. 迁移xaml为axaml:**
  - [ ] 将 `./ArkPlotWpf/View`中的xaml迁移为新工程中的axaml
  - [ ] 将 `./ArkPlotWpf/ViewModel/`中的ViewModel迁移为新工程中的ViewModel，
  - [ ] 修复因wpf与avalonia差异造成的编译错误。

#### Phase 2: UI 和视图迁移

- [ ] **1. 迁移样式和资源:**
  - [ ] 将 `ArkPlotWpf/Styles` 中的样式文件内容迁移到 Avalonia 的样式系统。Avalonia 的样式选择器比 WPF 更强大，可能需要一些重构。
  - [ ] 将 `ArkPlotWpf/Properties/Resources.resx` 中的资源迁移到 Avalonia 项目。
- [ ] **2. 迁移视图 (Views):**
  - [ ] **原则:** 从最简单的视图开始，最后迁移主窗口。
  - [ ] **TagEditor:**
    - [ ] 将 `ArkPlotWpf/View/TagEditor.xaml` 复制到 `ArkPlot.Avalonia/View/TagEditor.axaml`。
    - [ ] 修改 XAML 命名空间，从 `http://schemas.microsoft.com/winfx/2006/xaml/presentation` 改为 `https://github.com/avaloniaui`。
    - [ ] 调整控件。大部分 WPF 控件在 Avalonia 中有直接对应，但部分属性名或行为可能不同。
    - [ ] 迁移 `TagEditor.xaml.cs` 的代码。
  - [ ] **MainWindow:**
    - [ ] 将 `ArkPlotWpf/View/MainWindow.xaml` 复制到 `ArkPlot.Avalonia/View/MainWindow.axaml`。
    - [ ] 进行与 `TagEditor` 相同的命名空间和控件适配。
    - [ ] 迁移 `MainWindow.xaml.cs` 的代码。
- [ ] **3. 迁移静态资源:**
  - [ ] 将 `ArkPlotWpf/assets` 文件夹中的图片、html 等文件复制到 `ArkPlot.Avalonia/Assets`。
  - [ ] 确保这些资源在 `.csproj` 文件中被正确标记为 `AvaloniaResource`。

#### Phase 3: 处理平台依赖和特殊逻辑

- [ ] **1. 窗口和对话框:**
  - [ ] `Services/OpenWindowMessage.cs` 似乎是用于窗口通信的。需要用 Avalonia 的窗口管理机制或 MVVM 框架（如 CommunityToolkit.Mvvm）的 Messenger 来重构此功能。
- [ ] **3. 自定义或第三方控件:**
  - [ ] `Styles/Properties/AutoScroll.cs` 和 `ScrollViewerBinding.cs` 是附加属性的实现。需要检查 Avalonia 中是否有内置的等效功能，或者是否需要重新实现。
- [ ] **4. 文件和网络IO:**
  - [ ] `Utilities/AkpProcess.cs` 和 `NetworkUtility.cs` 中的逻辑大多是通用的，应该可以在 `ArkPlot.Core` 中正常工作。需要进行一次检查。

