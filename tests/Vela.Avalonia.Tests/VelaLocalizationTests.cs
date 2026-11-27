using Avalonia.Controls;
using Vela.Avalonia.Gallery.Localization;
using Xunit;

namespace Vela.Avalonia.Tests;

public sealed class VelaLocalizationTests
{
    [Theory]
    [InlineData(AppLanguage.En, "Vela.Avalonia Studio", "Search...")]
    [InlineData(AppLanguage.Zh, "Vela.Avalonia 演示工作室", "搜索...")]
    public void CompiledDictionary_LoadsLocalizedStartupStrings(
        AppLanguage language, string expectedTitle, string expectedSearch)
    {
        ResourceDictionary dictionary = language == AppLanguage.Zh
            ? new ChineseStrings()
            : new EnglishStrings();

        Assert.Equal(expectedTitle, dictionary["StrAppTitle"]);
        Assert.Equal(expectedSearch, dictionary["StrSearch"]);
        Assert.All(dictionary.Values, value => Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(value))));
    }

    [Fact]
    public void CompiledDictionaries_ProvideTheSameResourceKeys()
    {
        var english = new EnglishStrings();
        var chinese = new ChineseStrings();

        Assert.NotEmpty(english.Keys);
        Assert.Equal(
            english.Keys.Cast<string>().Order(StringComparer.Ordinal),
            chinese.Keys.Cast<string>().Order(StringComparer.Ordinal));
    }
}
