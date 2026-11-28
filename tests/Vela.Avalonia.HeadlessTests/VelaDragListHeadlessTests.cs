using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Motion;
using Xunit;

namespace Vela.Avalonia.HeadlessTests;

public class VelaDragListHeadlessTests
{
    private static FuncDataTemplate<string> CreateCardTemplate() =>
        new((value, _) => new Border
        {
            Height = 44,
            Child = new TextBlock { Text = value },
        });

    [AvaloniaFact]
    public void CommitMove_ReordersWithinSameList()
    {
        var items = new ObservableCollection<string> { "A", "B", "C" };
        var list = new VelaDragList
        {
            Width = 220,
            ItemsSource = items,
            ItemTemplate = CreateCardTemplate(),
        };

        var window = new Window { Width = 400, Height = 400, Content = list };
        window.Show();
        window.UpdateLayout();

        var moved = VelaDragList.CommitMove(list, sourceIndex: 0, targetList: list, targetIndex: 2);

        Assert.True(moved);
        Assert.Equal(["B", "C", "A"], items);
        window.Close();
    }

    [AvaloniaFact]
    public void CommitMove_TransfersAcrossListsInSameDragGroup()
    {
        var colA = new ObservableCollection<string> { "Task 1", "Task 2" };
        var colB = new ObservableCollection<string> { "Task 3" };

        var listA = new VelaDragList
        {
            Width = 180,
            DragGroup = "kanban",
            ItemsSource = colA,
            ItemTemplate = CreateCardTemplate(),
        };
        var listB = new VelaDragList
        {
            Width = 180,
            DragGroup = "kanban",
            ItemsSource = colB,
            ItemTemplate = CreateCardTemplate(),
        };

        var transferredCount = 0;
        listB.ItemTransferred += (_, _) => transferredCount++;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        Grid.SetColumn(listA, 0);
        Grid.SetColumn(listB, 1);
        grid.Children.Add(listA);
        grid.Children.Add(listB);

        var window = new Window { Width = 420, Height = 400, Content = grid };
        window.Show();
        window.UpdateLayout();

        var moved = VelaDragList.CommitMove(listA, sourceIndex: 0, targetList: listB, targetIndex: 1);

        Assert.True(moved);
        Assert.Equal(["Task 2"], colA);
        Assert.Equal(["Task 3", "Task 1"], colB);
        Assert.Equal(1, transferredCount);
        window.Close();
    }

    [AvaloniaFact]
    public void AcceptsPayload_EnforcesDragGroupAndCanAcceptDrop()
    {
        var source = new VelaDragList { DragGroup = "board-a" };
        var sameGroup = new VelaDragList { DragGroup = "board-a" };
        var otherGroup = new VelaDragList { DragGroup = "board-b" };
        var guarded = new VelaDragList
        {
            DragGroup = "board-a",
            CanAcceptDrop = p => !Equals(p.Item, "Blocked"),
        };

        var panel = new StackPanel();
        panel.Children.Add(source);
        panel.Children.Add(sameGroup);
        panel.Children.Add(otherGroup);
        panel.Children.Add(guarded);

        var window = new Window { Width = 300, Height = 400, Content = panel };
        window.Show();
        window.UpdateLayout();

        var okPayload = new VelaDragPayload("Card", source, 0, "board-a");
        var blockedPayload = new VelaDragPayload("Blocked", source, 0, "board-a");

        Assert.True(sameGroup.AcceptsPayload(okPayload));
        Assert.False(otherGroup.AcceptsPayload(okPayload));
        Assert.True(guarded.AcceptsPayload(okPayload));
        Assert.False(guarded.AcceptsPayload(blockedPayload));

        window.Close();
    }

    [AvaloniaFact]
    public void PointerDrag_MovesCardAcrossColumnsViaOverlayLayer()
    {
        var previousReduced = VelaMotionOptions.ReducedMotion;
        VelaMotionOptions.ReducedMotion = true;

        try
        {
            var colA = new ObservableCollection<string> { "Alpha", "Beta" };
            var colB = new ObservableCollection<string> { "Gamma" };

            var listA = new VelaDragList
            {
                Width = 180,
                Height = 260,
                DragGroup = "board",
                ItemsSource = colA,
                ItemTemplate = CreateCardTemplate(),
            };
            var listB = new VelaDragList
            {
                Width = 180,
                Height = 260,
                DragGroup = "board",
                ItemsSource = colB,
                ItemTemplate = CreateCardTemplate(),
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Children = { listA, listB },
            };

            var window = new Window { Width = 440, Height = 320, Content = panel };
            window.Show();
            window.UpdateLayout();

            // Start drag on first item in listA (around x=60, y=26) and drop into listB (x=280, y=100)
            window.MouseDown(new Point(60, 26), MouseButton.Left);
            window.MouseMove(new Point(75, 30), RawInputModifiers.LeftMouseButton);
            window.MouseMove(new Point(280, 100), RawInputModifiers.LeftMouseButton);
            window.MouseUp(new Point(280, 100), MouseButton.Left);

            Assert.Equal(["Beta"], colA);
            Assert.Equal(["Gamma", "Alpha"], colB);

            window.Close();
        }
        finally
        {
            VelaMotionOptions.ReducedMotion = previousReduced;
        }
    }

    [AvaloniaFact]
    public void PointerDrag_EscapeCancelsWithoutMutatingCollections()
    {
        var previousReduced = VelaMotionOptions.ReducedMotion;
        VelaMotionOptions.ReducedMotion = true;

        try
        {
            var colA = new ObservableCollection<string> { "Alpha", "Beta" };
            var colB = new ObservableCollection<string> { "Gamma" };

            var listA = new VelaDragList
            {
                Width = 180,
                Height = 260,
                DragGroup = "board",
                ItemsSource = colA,
                ItemTemplate = CreateCardTemplate(),
            };
            var listB = new VelaDragList
            {
                Width = 180,
                Height = 260,
                DragGroup = "board",
                ItemsSource = colB,
                ItemTemplate = CreateCardTemplate(),
            };

            var canceled = false;
            listA.DragEnded += (_, e) => canceled = e.Canceled;

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Children = { listA, listB },
            };

            var window = new Window { Width = 440, Height = 320, Content = panel };
            window.Show();
            window.UpdateLayout();

            window.MouseDown(new Point(60, 26), MouseButton.Left);
            window.MouseMove(new Point(75, 30), RawInputModifiers.LeftMouseButton);
            window.MouseMove(new Point(280, 100), RawInputModifiers.LeftMouseButton);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            window.MouseUp(new Point(280, 100), MouseButton.Left);

            Assert.True(canceled);
            Assert.Equal(["Alpha", "Beta"], colA);
            Assert.Equal(["Gamma"], colB);

            window.Close();
        }
        finally
        {
            VelaMotionOptions.ReducedMotion = previousReduced;
        }
    }

    [AvaloniaFact]
    public void PointerDrag_InsideVelaWindow_MovesCardAcrossColumns()
    {
        var previousReduced = VelaMotionOptions.ReducedMotion;
        VelaMotionOptions.ReducedMotion = true;

        try
        {
            var colA = new ObservableCollection<string> { "Alpha", "Beta" };
            var colB = new ObservableCollection<string> { "Gamma" };

            var listA = new VelaDragList
            {
                Width = 180,
                Height = 260,
                DragGroup = "vela-window-board",
                ItemsSource = colA,
                ItemTemplate = CreateCardTemplate(),
            };
            var listB = new VelaDragList
            {
                Width = 180,
                Height = 260,
                DragGroup = "vela-window-board",
                ItemsSource = colB,
                ItemTemplate = CreateCardTemplate(),
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Children = { listA, listB },
            };

            var window = new VelaWindow { Width = 480, Height = 380, Content = panel };
            window.Show();
            window.UpdateLayout();

            var dragStarted = false;
            var dragEnded = false;
            listA.DragStarted += (_, _) => dragStarted = true;
            listA.DragEnded += (_, _) => dragEnded = true;

            var cardOrigin = listA.Containers[0].TranslatePoint(new Point(20, 20), window);
            var targetOrigin = listB.TranslatePoint(new Point(40, 100), window);
            Assert.NotNull(cardOrigin);
            Assert.NotNull(targetOrigin);

            window.MouseDown(cardOrigin.Value, MouseButton.Left);
            window.MouseMove(new Point(cardOrigin.Value.X + 16, cardOrigin.Value.Y + 4), RawInputModifiers.LeftMouseButton);
            Assert.True(dragStarted, $"Drag did not start at {cardOrigin.Value}; listA bounds={listA.Bounds}, card bounds={listA.Containers[0].Bounds}");
            window.MouseMove(targetOrigin.Value, RawInputModifiers.LeftMouseButton);
            window.MouseUp(targetOrigin.Value, MouseButton.Left);
            Assert.True(dragEnded, "Drag did not end");

            Assert.Equal(["Beta"], colA);
            Assert.Equal(["Gamma", "Alpha"], colB);

            window.Close();
        }
        finally
        {
            VelaMotionOptions.ReducedMotion = previousReduced;
        }
    }
}
