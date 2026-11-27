using Avalonia;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Tokens;
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

    [Fact]
    public void VelaGlassCard_HasSukiInspiredDefaults()
    {
        var card = new VelaGlassCard();
        Assert.Equal(80.0, card.BlurRadius);
        Assert.True(card.BlurBackground);
        Assert.Equal(VelaGlassTintLevel.HighContrast, card.TintLevel);
        Assert.Equal(new Thickness(1.2), card.RimThickness);
    }

    [Fact]
    public void VelaGlassRimBorder_ComputesAspectCompensatedDiagonalGradient()
    {
        var (sqStart, sqEnd) = VelaGlassRimBorder.ComputeDiagonalGradientPoints(new Size(300, 300));
        var (wideStart, wideEnd) = VelaGlassRimBorder.ComputeDiagonalGradientPoints(new Size(440, 220));
        var (emptyStart, emptyEnd) = VelaGlassRimBorder.ComputeDiagonalGradientPoints(new Size(0, 0));

        // Wide rectangle (aspect 2:1) expands vertical gradient endpoints further than square
        Assert.Equal(0.2, sqStart.Point.X, 4);
        Assert.Equal(0.8, sqEnd.Point.X, 4);
        Assert.True(wideStart.Point.Y < sqStart.Point.Y);
        Assert.True(wideEnd.Point.Y > sqEnd.Point.Y);

        // Degenerate zero bounds fall back safely without NaN/Infinity
        Assert.True(double.IsFinite(emptyStart.Point.Y));
        Assert.True(double.IsFinite(emptyEnd.Point.Y));
    }

    [Fact]
    public void VelaDialogHost_ResolvesDefaultAndOverrideMaterialAndStyle()
    {
        var host = new VelaDialogHost();
        Assert.Equal(VelaSurfaceMaterial.Solid, host.DefaultMaterial);
        Assert.Equal(VelaDialogStyle.Standard, host.DefaultStyle);

        host.DefaultMaterial = VelaSurfaceMaterial.GlassCard;
        host.DefaultStyle = VelaDialogStyle.Centered;

        Assert.Equal(VelaSurfaceMaterial.GlassCard, VelaDialogHost.ResolveMaterial(null, host.DefaultMaterial));
        Assert.Equal(VelaSurfaceMaterial.LiquidGlass, VelaDialogHost.ResolveMaterial(VelaSurfaceMaterial.LiquidGlass, host.DefaultMaterial));

        Assert.Equal(VelaDialogStyle.Centered, VelaDialogHost.ResolveStyle(null, host.DefaultStyle));
        Assert.Equal(VelaDialogStyle.Standard, VelaDialogHost.ResolveStyle(VelaDialogStyle.Standard, host.DefaultStyle));

        var opts = new VelaDialogOptions();
        Assert.Null(opts.Material);
        Assert.Null(opts.Style);
        Assert.Equal(VelaDialogTone.Neutral, opts.Tone);
        Assert.Equal(80.0, opts.GlassBlurRadius);
    }
}
