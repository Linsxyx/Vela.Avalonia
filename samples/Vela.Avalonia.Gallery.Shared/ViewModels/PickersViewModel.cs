using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Vela.Avalonia.Gallery.Services;

namespace Vela.Avalonia.Gallery.ViewModels;

public partial class PickersViewModel : ViewModelBase
{
    private readonly IGalleryShellService _shell;

    public IReadOnlyList<string> Frameworks { get; } =
    [
        "Avalonia 12.2",
        "CommunityToolkit.Mvvm",
        "SkiaSharp",
        ".NET 10",
        "Vela.Avalonia",
        "ReactiveUI",
    ];

    [ObservableProperty]
    public partial string? SelectedFramework { get; set; } = "Avalonia 12.2";

    [ObservableProperty]
    public partial int SelectedRegionIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RadioCardsLayout))]
    public partial int RadioLayoutIndex { get; set; }

    public VelaRadioCardsLayout RadioCardsLayout =>
        RadioLayoutIndex == 1 ? VelaRadioCardsLayout.List : VelaRadioCardsLayout.Grid;

    [ObservableProperty]
    public partial int SelectedPlanCardIndex { get; set; } = 1;

    [ObservableProperty]
    public partial DateTime? SelectedCalendarDate { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial DateTime? SelectedReleaseDate { get; set; } = DateTime.Today.AddDays(5);

    [ObservableProperty]
    public partial bool IsPopoverOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewBrush))]
    [NotifyPropertyChangedFor(nameof(PreviewHex))]
    public partial Color InlineColor { get; set; } = Color.Parse("#2F6BFF");

    [ObservableProperty]
    public partial Color SecondaryColor { get; set; } = Color.Parse("#10A870");

    public IBrush PreviewBrush => new SolidColorBrush(InlineColor);

    public string PreviewHex => InlineColor.A == 255
        ? $"#{InlineColor.R:X2}{InlineColor.G:X2}{InlineColor.B:X2}"
        : $"#{InlineColor.A:X2}{InlineColor.R:X2}{InlineColor.G:X2}{InlineColor.B:X2}";

    public PickersViewModel(IGalleryShellService shell)
    {
        _shell = shell;
    }

    [RelayCommand]
    private void ClosePopover() => IsPopoverOpen = false;

    [RelayCommand]
    private void MenuAction(string action)
    {
        var loc = Loc.Instance;
        switch (action)
        {
            case "rename":
                _shell.ShowToast(loc.Get("StrRename"), loc.Pick("Document rename triggered.", "已触发文档重命名。"), VelaToastTone.Info);
                break;
            case "duplicate":
                _shell.ShowToast(loc.Pick("Duplicated", "已创建副本"), loc.Pick("Copy created.", "副本已生成。"), VelaToastTone.Success);
                break;
            case "share":
                _shell.ShowToast(loc.Pick("Link copied", "已复制链接"), loc.Pick("Share URL copied.", "分享链接已复制。"), VelaToastTone.Info);
                break;
            case "delete":
                _shell.ShowToast(loc.Get("StrDeleted"), loc.Pick("Moved to Trash.", "已移至废纸篓。"), VelaToastTone.Danger, loc.Get("StrUndo"),
                    () => _shell.ShowToast(loc.Pick("Restored", "已恢复"), loc.Pick("Document restored.", "文档已恢复。"), VelaToastTone.Success));
                break;
            case "ctx-edit":
                _shell.ShowToast(loc.Get("StrEdit"), loc.Pick("Context menu edit.", "右键菜单编辑。"), VelaToastTone.Info);
                break;
            case "ctx-copy":
                _shell.ShowToast(loc.Pick("Copied", "已复制"), loc.Pick("Path copied.", "路径已复制。"), VelaToastTone.Success);
                break;
            case "ctx-delete":
                _shell.ShowToast(loc.Get("StrDeleted"), loc.Pick("Removed permanently.", "已永久删除。"), VelaToastTone.Danger);
                break;
        }
    }
}
