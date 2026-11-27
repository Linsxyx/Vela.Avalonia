using System.Globalization;
using Avalonia;
using Avalonia.Controls;

namespace Vela.Avalonia.Gallery.Localization;

public enum AppLanguage
{
    En,
    Zh,
}

public sealed class Loc
{
    private IResourceProvider? _activeDictionary;

    public static Loc Instance { get; } = new();

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.En;
    public bool IsChinese => CurrentLanguage == AppLanguage.Zh;

    public event Action<AppLanguage>? LanguageChanged;

    public void InitializeFromSystem()
    {
        var isZh = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("zh", StringComparison.OrdinalIgnoreCase);
        SetLanguage(isZh ? AppLanguage.Zh : AppLanguage.En, force: true);
    }

    public void SetLanguage(AppLanguage language, bool force = false)
    {
        if (!force && CurrentLanguage == language && _activeDictionary is not null)
            return;

        if (Application.Current is { } app)
        {
            // Direct constructors keep both compiled dictionaries reachable during WASM/AOT trimming.
            IResourceProvider dict = language == AppLanguage.Zh ? new ChineseStrings() : new EnglishStrings();
            var merged = app.Resources.MergedDictionaries;
            if (_activeDictionary is not null)
            {
                var idx = merged.IndexOf(_activeDictionary);
                if (idx >= 0)
                    merged[idx] = dict;
                else
                    merged.Add(dict);
            }
            else
            {
                merged.Add(dict);
            }
            _activeDictionary = dict;
        }

        CurrentLanguage = language;
        LanguageChanged?.Invoke(language);
    }

    public void ToggleLanguage() =>
        SetLanguage(CurrentLanguage == AppLanguage.Zh ? AppLanguage.En : AppLanguage.Zh);

    public string Get(string key)
    {
        if (Application.Current is { } app &&
            app.Resources.TryGetResource(key, app.ActualThemeVariant, out var value) &&
            value is string text)
        {
            return text;
        }
        return key;
    }

    public string Format(string key, params object[] args) => string.Format(Get(key), args);

    public string Pick(string en, string zh) => IsChinese ? zh : en;
}
