# Component catalog · 组件目录

[English overview](../README.md) · [中文说明](../README.zh-CN.md) · [Interactive gallery](https://linsxyx.github.io/Vela.Avalonia/)

The catalog follows the current source tree. All components below belong to `Vela.Avalonia` unless the charts package is specified. The gallery links open working XAML examples; their adjacent `.axaml.cs` files and [view models](../samples/Vela.Avalonia.Gallery.Shared/ViewModels) contain the associated behavior.

本目录以当前源码为准。除图表外，以下组件均属于 `Vela.Avalonia`。示例链接指向 Gallery 的 XAML，交互逻辑位于同名 `.axaml.cs` 文件和对应的 ViewModel 中。

## Window and navigation · 窗口与导航

| Components | Purpose / 用途 | Example |
| --- | --- | --- |
| `VelaWindow`, `VelaTrayIcon` | Optional window chrome and system-tray integration / 可选窗口外壳与托盘集成 | [Window](../samples/Vela.Avalonia.Gallery.Shared/MainWindow.axaml), [tray](../samples/Vela.Avalonia.Gallery.Shared/Views/SystemTrayPage.axaml) |
| `VelaNavigationView`, `VelaNavItem`, `VelaNavSectionHeader` | Hierarchical application navigation / 层级应用导航 | [MainView](../samples/Vela.Avalonia.Gallery.Shared/MainView.axaml) |
| `VelaPageDeck`, `IVelaNavigationAware` | Page caching and activation callbacks / 页面缓存与激活回调 | [Page factory](../samples/Vela.Avalonia.Gallery.Shared/MainView.axaml.cs), [lifecycle](../samples/Vela.Avalonia.Gallery.Shared/Views/HeavyStressPage.cs) |
| `VelaTabs`, `VelaTabItem`, `VelaBreadcrumb`, `VelaPagination`, `VelaStepper` | Tabs, location, paging, and progress through steps / 标签、路径、分页与步骤 | [Layout](../samples/Vela.Avalonia.Gallery.Shared/Views/LayoutPage.axaml) |
| `VelaDockBar`, `VelaDockItem`, `VelaFloatingBar` | Compact navigation and floating actions / 紧凑导航与浮动操作栏 | [Layout](../samples/Vela.Avalonia.Gallery.Shared/Views/LayoutPage.axaml) |
| `VelaCommandPalette` | Searchable commands with an optional global shortcut / 可搜索命令与可选全局快捷键 | [Host](../samples/Vela.Avalonia.Gallery.Shared/MainView.axaml), [commands](../samples/Vela.Avalonia.Gallery.Shared/ViewModels/MainViewModel.cs) |

`VelaPageDeck` supports `KeepAlive`, `Lru`, and `None`. Use `IVelaNavigationAware` on a page or its view model to pause page-owned work when navigating away. A cached page remains alive until evicted or cleared.

## Actions and forms · 操作与表单

| Components | Purpose / 用途 | Example |
| --- | --- | --- |
| `VelaButton`, `VelaCopyButton`, `VelaSplitButton` | Primary actions, clipboard feedback, and grouped commands / 操作、复制反馈与组合命令 | [Actions](../samples/Vela.Avalonia.Gallery.Shared/Views/ActionsPage.axaml) |
| `VelaConfirmMorph`, `VelaHoldToConfirm` | Inline confirmation and hold-to-confirm / 行内确认与长按确认 | [Actions](../samples/Vela.Avalonia.Gallery.Shared/Views/ActionsPage.axaml) |
| `VelaSwitch`, `VelaCheckBox`, `VelaSegmentedControl` | Boolean and segmented selection / 开关、勾选与分段选择 | [Inputs](../samples/Vela.Avalonia.Gallery.Shared/Views/InputsPage.axaml) |
| `VelaInput`, `VelaScrubInput`, `VelaTagInput` | Text, numeric scrubbing, and tags / 文本、拖动调值与标签 | [Inputs](../samples/Vela.Avalonia.Gallery.Shared/Views/InputsPage.axaml) |
| `VelaElasticSlider`, `VelaRulerSlider` | Continuous and ruler-style value selection / 弹性滑动与标尺选择 | [Inputs](../samples/Vela.Avalonia.Gallery.Shared/Views/InputsPage.axaml) |
| `VelaSelect`, `VelaComboBox`, `VelaDropdownButton`, `VelaMenuItem`, `VelaPopover` | Selection, menus, and contextual content / 选择、菜单与浮层内容 | [Pickers](../samples/Vela.Avalonia.Gallery.Shared/Views/PickersPage.axaml) |
| `VelaRadioCards`, `VelaRadioCardItem`, `VelaCalendar`, `VelaDatePicker`, `VelaColorPicker` | Card, date, and color selection / 卡片、日期与颜色选择 | [Pickers](../samples/Vela.Avalonia.Gallery.Shared/Views/PickersPage.axaml) |

## Data views · 数据视图

| Components | Purpose / 用途 | Example |
| --- | --- | --- |
| `VelaListBox`, `VelaListBoxItem` | Styled list selection / 列表选择 | [Data views](../samples/Vela.Avalonia.Gallery.Shared/Views/DataViewsPage.axaml) |
| `VelaDataGrid` | Tabular data, sortable columns, and column resizing / 表格、列排序与列宽调整 | [Data views](../samples/Vela.Avalonia.Gallery.Shared/Views/DataViewsPage.axaml) |
| `VelaTreeView`, `VelaTreeNode` | Hierarchies, filtering, and cascading checkboxes / 层级、筛选与级联勾选 | [Data views](../samples/Vela.Avalonia.Gallery.Shared/Views/DataViewsPage.axaml) |
| `VelaTreeTable` | Hierarchical rows with multiple columns / 多列表格中的层级行 | [Data views](../samples/Vela.Avalonia.Gallery.Shared/Views/DataViewsPage.axaml) |

See [DataViewsViewModel](../samples/Vela.Avalonia.Gallery.Shared/ViewModels/DataViewsViewModel.cs) for sample data and configuration. Published package versions can lag behind the catalog.

## Layout, surfaces, and feedback · 布局、材质与反馈

| Components | Purpose / 用途 | Example |
| --- | --- | --- |
| `VelaSettingsGroup`, `VelaSettingsRow`, `VelaResizablePanels`, `VelaResizablePane` | Settings and adjustable layouts / 设置项与可调整布局 | [Layout](../samples/Vela.Avalonia.Gallery.Shared/Views/LayoutPage.axaml) |
| `VelaAccordion`, `VelaAccordionItem`, `VelaCollapsePanel` | Expandable sections / 可展开区域 | [Layout](../samples/Vela.Avalonia.Gallery.Shared/Views/LayoutPage.axaml) |
| `VelaDialogHost`, `VelaDrawerHost`, `VelaToastHost` | Dialogs, drawers, and notifications / 对话框、抽屉与通知 | [Feedback](../samples/Vela.Avalonia.Gallery.Shared/Views/FeedbackPage.axaml), [host calls](../samples/Vela.Avalonia.Gallery.Shared/Services/GalleryShellService.cs) |
| `VelaBusyOverlay`, `VelaSpinnerRing`, `VelaProgress`, `VelaSkeleton`, `VelaAlert` | Busy, progress, loading, and status feedback / 忙碌、进度、加载与状态反馈 | [Feedback](../samples/Vela.Avalonia.Gallery.Shared/Views/FeedbackPage.axaml) |
| `VelaAvatar`, `VelaAvatarGroup`, `VelaBadge`, `VelaEmptyState`, `VelaAnimatedCounter`, `VelaMetricCard` | Identity and compact data presentation / 身份与轻量数据展示 | [Data display](../samples/Vela.Avalonia.Gallery.Shared/Views/DataDisplayPage.axaml) |
| `VelaGlassCard`, `VelaLiquidGlass`, `VelaShaderBackground` | Glass surfaces and animated backgrounds / 玻璃材质与动态背景 | [Shaders & glass](../samples/Vela.Avalonia.Gallery.Shared/Views/ShadersPage.axaml) |
| `VelaPopupSurface`, `VelaMorphSlotPanel` | Reusable popup and animated-layout primitives / 浮层与动效布局基础组件 | [Source](../src/Vela.Avalonia/Controls) |

Vela also supplies themes for standard Avalonia `ContextMenu`, `MenuItem`, `ScrollBar`, and `ToolTip` controls.

## Charts · 图表

Install **[`Vela.Avalonia.Charts`](https://www.nuget.org/packages/Vela.Avalonia.Charts)** for `VelaLineChart`, `VelaBarChart`, `VelaDonutChart`, `VelaSparkline`, `VelaGauge`, and `VelaActivityHeatmap`.

The charts use the core theme and motion system. Start with the [chart gallery](../samples/Vela.Avalonia.Gallery.Shared/Views/ChartsPage.axaml) and its [view model](../samples/Vela.Avalonia.Gallery.Shared/ViewModels/ChartsViewModel.cs) for data-shape and binding examples.
