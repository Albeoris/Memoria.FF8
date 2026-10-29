using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTextPreferenceTests
{
    [Fact]
    public void OriginalPresetPersistsNativeProfileAndSystemSwitchRestoresAdaptive()
    {
        string path = Path.Combine(Path.GetTempPath(), "memoria-font-preferences-" + Guid.NewGuid().ToString("N"), "ui.json");
        try
        {
            UiPreferences preferences = new UiPreferences().WithSetting("Native font profile", "multilingual-atlases").WithSetting("Text layout", "Original-compatible");
            preferences.Save(path);
            UiPreferences loaded = UiPreferences.Load(path);
            Assert.Equal(GameTextFontMode.Native, loaded.TextFontMode);
            Assert.Equal(GameTextLayoutMode.OriginalCompatible, loaded.TextLayoutMode);
            Assert.Equal("multilingual-atlases", loaded.NativeFontProfileId);
            loaded.WithSetting("Text fonts", "System").Save(path);
            loaded = UiPreferences.Load(path);
            Assert.Equal(GameTextFontMode.System, loaded.TextFontMode);
            Assert.Equal(GameTextLayoutMode.Adaptive, loaded.TextLayoutMode);
            Assert.Equal("multilingual-atlases", loaded.NativeFontProfileId);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    [Fact]
    public void InvalidPersistedNativePresentationIsRejected()
    {
        string path = Path.Combine(Path.GetTempPath(), "memoria-font-invalid-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, "{\"TextFontMode\":100}");
            Assert.Throws<InvalidDataException>(() => UiPreferences.Load(path));
            File.WriteAllText(path, "{\"TextLayoutMode\":1,\"TextFontMode\":0}");
            Assert.Throws<InvalidDataException>(() => UiPreferences.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
