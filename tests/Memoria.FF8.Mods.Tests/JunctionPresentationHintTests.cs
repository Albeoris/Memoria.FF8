using System.Security;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionPresentationHintTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "junction-presentation-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("ff8_use_gamepad_icons", "1", "true")]
    [InlineData("ff8_use_gamepad_icons", "false", "false")]
    [InlineData("enable_bilinear", "true", "true")]
    [InlineData("internal_resolution_scale", "04", "4")]
    [InlineData("internal_resolution_scale", "0", "0")]
    public void Active_driver_hint_is_validated_retained_and_diagnosed_without_changing_resource_settings(string name, string value, string expected)
    {
        JunctionModPackage package = Open($"<{name}>{value}</{name}>");
        Assert.Equal(expected, package.Manifest.PresentationHints[name]);
        Assert.Null(package.Manifest.EnableFfmpegVideos);
        Assert.Null(package.Manifest.UseExternalMusic);
        Assert.Contains(package.Diagnostics, message => message.Contains(name, StringComparison.Ordinal) && message.Contains("does not override user preferences", StringComparison.Ordinal));
    }

    [Fact]
    public void Equivalent_active_declarations_coalesce_but_conflicting_values_are_rejected()
    {
        JunctionModPackage package = Open("<enable_bilinear>true</enable_bilinear><enable_bilinear>1</enable_bilinear><internal_resolution_scale>4</internal_resolution_scale><internal_resolution_scale>04</internal_resolution_scale>");
        Assert.Equal(2, package.Manifest.PresentationHints.Count);
        Assert.Equal(2, package.Diagnostics.Count);
        Assert.Throws<InvalidDataException>(() => Open("<internal_resolution_scale>4</internal_resolution_scale><internal_resolution_scale>2</internal_resolution_scale>"));
    }

    [Fact]
    public void Inactive_hint_has_no_effect_and_undeclared_condition_is_rejected()
    {
        string option = "<ConfigOption><ID>Enabled</ID><Type>Bool</Type><Default>0</Default></ConfigOption>";
        JunctionModPackage package = Open("<enable_bilinear Enabled='1'>true</enable_bilinear>", option);
        Assert.Empty(package.Manifest.PresentationHints);
        Assert.Empty(package.Diagnostics);
        Assert.Throws<NotSupportedException>(() => Open("<enable_bilinear Missing='1'>true</enable_bilinear>"));
    }

    [Theory]
    [InlineData("internal_resolution_scale", "-1")]
    [InlineData("internal_resolution_scale", "1.5")]
    [InlineData("internal_resolution_scale", "2147483648")]
    [InlineData("enable_bilinear", "sometimes")]
    [InlineData("ff8_use_gamepad_icons", "2")]
    public void Invalid_hint_values_are_not_silently_accepted(string name, string value)
    {
        Assert.ThrowsAny<Exception>(() => Open($"<{name}>{SecurityElement.Escape(value)}</{name}>"));
    }

    private JunctionModPackage Open(string settings, string options = "")
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "mod.xml"), $"<ModInfo><Name>Example</Name>{options}<FFNxConfig>{settings}</FFNxConfig></ModInfo>");
        return JunctionModImporter.Open(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
