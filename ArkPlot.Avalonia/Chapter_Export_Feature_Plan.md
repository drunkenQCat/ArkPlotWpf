# 功能规划：分章节导出（修订版 v3 - Flyout方案）

## 1. 目标

在 `ArkPlot.Avalonia` 应用中，将原有的“故事名”按钮修改为“当前活动”。用户点击此按钮后，会弹出一个浮动面板（Flyout），其中显示当前选定活动的所有章节。用户可以通过勾选来选择一个或多个特定章节进行导出。

## 2. 实现思路

经过对 SukiUI 控件的进一步分析，我们确定采用 `Flyout` 控件来实现此功能，因为它更符合“临时弹出菜单”的交互模式，且实现上更简洁。

1.  **ViewModel (`MainWindowViewModel.cs`)**:
    *   保留用于存储章节列表的 `ObservableCollection<ChapterSelectionViewModel>`。
    *   创建一个新的 `RelayCommand`，命名为 `LoadChaptersCommand`。此命令将在用户点击“当前活动”按钮时执行，负责异步加载章节列表以供 `Flyout` 显示。
    *   `LoadMdCommand`（“开始”按钮的命令）的逻辑保持不变，依然是只处理被勾选的章节。

2.  **View (`MainWindow.axaml`)**:
    *   将“故事名”按钮的 `Content` 修改为“当前活动”。
    *   将该按钮的 `Command` 绑定到新的 `LoadChaptersCommand`。
    *   为该按钮添加 `<Button.Flyout>` 定义。在 `Flyout` 内部，放置用于显示章节列表的 `ItemsControl`。
    *   这种方法无需修改窗口的整体布局（如从 `StackPanel` 改为 `Grid`），也无需在 ViewModel 中管理侧边栏的打开/关闭状态，大大简化了实现。

3.  **核心逻辑 (`ArkPlot.Core`)**:
    *   此部分计划保持不变。依然需要调整 `AkpStoryLoader` 或相关服务类，使其能够先获取章节名称列表，之后再根据给定的列表来处理具体内容。

## 3. 详细步骤

### 3.1. ViewModel 层修改 (`MainWindowViewModel.cs`)

1.  **创建/保留章节数据模型**:
    `ChapterSelectionViewModel` 类保持不变。

2.  **在 `MainWindowViewModel` 中修改/添加属性和命令**:

    ```csharp
    // In MainWindowViewModel.cs
    [ObservableProperty]
    private ObservableCollection<ChapterSelectionViewModel> _chapters = new();

    // 替换掉之前的 Toggle...Command
    [RelayCommand]
    private async Task LoadChapters()
    {
        // 如果当前活动没有章节，则加载它们
        if (CurrentAct != null && Chapters.Count == 0) 
        {
            await LoadChaptersForCurrentAct();
        }
    }

    private async Task LoadChaptersForCurrentAct()
    {
        Chapters.Clear();
        Status = "正在加载章节列表...";
        
        var storyLoader = new AkpStoryLoader(CurrentAct);
        var chapterNames = await storyLoader.GetChapterNamesAsync(); // 假设 AkpStoryLoader 有一个新方法

        foreach (var name in chapterNames)
        {
            Chapters.Add(new ChapterSelectionViewModel(name));
        }
        Status = "章节列表加载完成。";
    }

    // 当用户切换主活动时，需要清空旧的章节列表，以便下次点击按钮时重新加载
    partial void OnSelectedIndexChanged(int value)
    {
        Chapters.Clear();
    }
    ```

3.  **修改 `LoadMdCommand`**:
    此部分逻辑保持不变。

### 3.2. 核心逻辑层修改 (`ArkPlot.Core/Utilities/WorkFlow/AkpStoryLoader.cs`)

此部分的计划保持不变。

### 3.3. 视图层修改 (`Views/MainWindow.axaml`)

这是最关键的修改，现在变得非常简单和清晰。

1.  **修改按钮并添加 Flyout**:
    找到 `Content="故事名"` 的 `Button`，修改其 `Content`、`Command`，并为其添加 `<Button.Flyout>`。

    ```xml
    <!-- In MainWindow.axaml -->
    <Button Grid.Row="0" Grid.Column="0"
            Content="当前活动" 
            VerticalAlignment="Center" 
            HorizontalAlignment="Right"
            Command="{Binding LoadChaptersCommand}">
        <Button.Flyout>
            <Flyout>
                <StackPanel Margin="15" MaxWidth="300">
                    <TextBlock Text="选择导出章节" FontSize="16" FontWeight="Bold" Margin="0,0,0,10"/>
                    <Border BorderThickness="1" BorderBrush="LightGray" MaxHeight="400">
                        <ScrollViewer>
                            <ItemsControl ItemsSource="{Binding Chapters}">
                                <ItemsControl.ItemTemplate>
                                    <DataTemplate>
                                        <CheckBox 
                                            Content="{Binding ChapterName}" 
                                            IsChecked="{Binding IsSelected, Mode=TwoWay}" 
                                            Margin="4"
                                        />
                                    </DataTemplate>
                                </ItemsControl.ItemTemplate>
                            </ItemsControl>
                        </ScrollViewer>
                    </Border>
                </StackPanel>
            </Flyout>
        </Button.Flyout>
    </Button>
    ```

## 4. 总结

采用 `Flyout` 方案是目前最优的选择。它完美契合了“点击按钮弹出临时菜单”的交互需求，同时具有以下优点：
- **ViewModel 更简洁**：无需管理侧边栏的布尔状态。
- **View 更清晰**：无需重构主布局，只需在按钮上附加 `Flyout` 即可，代码更内聚。
- **交互更标准**：这是 Avalonia/WPF 中处理此类场景的标准做法。