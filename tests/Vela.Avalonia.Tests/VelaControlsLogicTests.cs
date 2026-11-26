using Vela.Avalonia.Controls;
using Xunit;

namespace Vela.Avalonia.Tests;

public class VelaControlsLogicTests
{
    [Fact]
    public void VelaAvatar_DerivesInitials_AndRespectsOverride()
    {
        var avatar = new VelaAvatar { DisplayName = "Ada Lovelace" };
        Assert.Equal("AL", avatar.ComputedInitials);

        avatar.DisplayName = "Vela";
        Assert.Equal("VE", avatar.ComputedInitials);

        avatar.Initials = " custom ";
        Assert.Equal("CUSTOM", avatar.ComputedInitials);

        avatar.Initials = null;
        avatar.DisplayName = "   ";
        Assert.Equal("?", avatar.ComputedInitials);
    }

    [Fact]
    public void VelaAvatar_MapsPixelSizesCorrectly()
    {
        var avatar = new VelaAvatar { Size = VelaAvatarSize.Small };
        Assert.Equal(28, avatar.PixelSize);

        avatar.Size = VelaAvatarSize.Medium;
        Assert.Equal(36, avatar.PixelSize);

        avatar.Size = VelaAvatarSize.Large;
        Assert.Equal(48, avatar.PixelSize);

        avatar.Size = VelaAvatarSize.XLarge;
        Assert.Equal(64, avatar.PixelSize);
    }

    [Fact]
    public void VelaRulerSlider_HandlesOutOfOrderMinMaxPropertiesSafely()
    {
        // Regression test: XAML sets Minimum before Maximum (while Maximum still defaults to 100)
        var ruler = new VelaRulerSlider();
        var ex = Record.Exception(() =>
        {
            ruler.Minimum = 2500;
            ruler.Maximum = 8500;
            ruler.Step = 100;
            ruler.Value = 5600;
        });

        Assert.Null(ex);
        Assert.Equal(5600, ruler.Value);
        Assert.Equal("5600", ruler.FormattedValue);
    }

    [Fact]
    public void VelaDockBar_SyncsSelectedIndexAndSelectedItem()
    {
        var dock = new VelaDockBar();
        var home = new VelaDockItem { Header = "Home" };
        var saved = new VelaDockItem { Header = "Saved", Badge = "3" };
        var profile = new VelaDockItem { Header = "Profile" };

        dock.Items.Add(home);
        dock.Items.Add(saved);
        dock.Items.Add(profile);

        dock.SelectedIndex = 1;
        Assert.Same(saved, dock.SelectedItem);

        dock.SelectedItem = profile;
        Assert.Equal(2, dock.SelectedIndex);
    }

    [Fact]
    public void VelaNavigationView_IndexesNestedChildrenAndFooterItems_SkippingSectionHeadersAndFolders()
    {
        var nav = new VelaNavigationView();
        var overview = new VelaNavItem { Header = "Overview", PageContent = "p0" };
        var section = new VelaNavSectionHeader { Header = "Workspace" };
        var folder = new VelaNavItem { Header = "Components" }; // pure folder: no PageContent/PageFactory
        var childA = new VelaNavItem { Header = "Buttons", PageContent = "p1" };
        var childB = new VelaNavItem { Header = "Inputs", PageContent = "p2" };
        folder.Children.Add(childA);
        folder.Children.Add(childB);
        var settings = new VelaNavItem { Header = "Settings", PageContent = "p3" };

        nav.Items.Add(overview);
        nav.Items.Add(section);
        nav.Items.Add(folder);
        nav.FooterItems.Add(settings);

        nav.SelectedIndex = 0;
        Assert.Same(overview, nav.SelectedItem);

        nav.SelectedIndex = 1;
        Assert.Same(childA, nav.SelectedItem);
        Assert.Same(folder, childA.ParentItem);
        Assert.Equal("Workspace", childA.SectionTitle);

        nav.SelectedIndex = 2;
        Assert.Same(childB, nav.SelectedItem);

        nav.SelectedIndex = 3;
        Assert.Same(settings, nav.SelectedItem);
        Assert.True(settings.IsFooterItem);
    }

    [Fact]
    public void VelaStepper_AcceptsStepsAndMaintainsCurrentStep()
    {
        var stepper = new VelaStepper();
        stepper.Steps.Add(new VelaStepperItem { Title = "Account" });
        stepper.Steps.Add(new VelaStepperItem { Title = "Deploy" });

        stepper.CurrentStep = 1;
        Assert.Equal(1, stepper.CurrentStep);
        Assert.Equal(2, stepper.Steps.Count);
    }

    [Fact]
    public void VelaHoldToConfirm_ResetClearsConfirmedState()
    {
        var hold = new VelaHoldToConfirm
        {
            Label = "Hold to delete",
            ConfirmedLabel = "Deleted",
            IsConfirmed = true,
        };

        Assert.Equal("Deleted", hold.DisplayLabel);

        hold.Reset();
        Assert.False(hold.IsConfirmed);
        Assert.Equal("Hold to delete", hold.DisplayLabel);
    }
}
