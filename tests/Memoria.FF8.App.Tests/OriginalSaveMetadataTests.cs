using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Memoria.FF8.App.Tests;

public sealed partial class OriginalSaveMetadataTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Memoria.FF8.Metadata.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Prepared_profile_contains_complete_banks_and_exact_content_plus_user_signature()
    {
        string directory = Profile();
        OriginalSaveMetadataUpdate update = OriginalSaveMetadata.Prepare(Path.Combine(directory, "slot2_save23.ff8"), Encoding.ASCII.GetBytes("abc"), DateTimeOffset.FromUnixTimeMilliseconds(123456789))!;
        XDocument document = Read(update);
        Assert.Equal(Path.Combine(directory, "metadata.xml"), update.Path);
        Assert.Null(update.OriginalBytes);
        Assert.False(File.Exists(update.Path));
        Assert.Equal(61, document.Root!.Elements("savefile").Count());
        XElement target = Slot(document, 2, 23);
        Assert.Equal("123456789", target.Element("timestamp")!.Value);
        Assert.Equal("933c26e888a5a6a9de3af0a0c7cb2c39", target.Element("signature")!.Value);
        XElement empty = Slot(document, 1, 1);
        Assert.Empty(empty.Element("timestamp")!.Value);
        Assert.Equal("ef73781effc5774100f87fe2f437a435", empty.Element("signature")!.Value);
    }

    [Fact]
    public void Missing_metadata_hashes_existing_native_files_without_changing_them()
    {
        string directory = Profile();
        byte[] existing = Encoding.ASCII.GetBytes("abc");
        string existingPath = Path.Combine(directory, "slot1_save02.ff8");
        File.WriteAllBytes(existingPath, existing);
        File.WriteAllBytes(Path.Combine(directory, "chocorpg.ff8"), existing);
        File.SetLastWriteTimeUtc(existingPath, DateTimeOffset.FromUnixTimeMilliseconds(100000).UtcDateTime);
        OriginalSaveMetadataUpdate update = OriginalSaveMetadata.Prepare(Path.Combine(directory, "slot2_save30.ff8"), [9])!;
        XDocument document = Read(update);
        Assert.Equal("933c26e888a5a6a9de3af0a0c7cb2c39", Slot(document, 1, 2).Element("signature")!.Value);
        Assert.Equal("100000", Slot(document, 1, 2).Element("timestamp")!.Value);
        Assert.Equal("933c26e888a5a6a9de3af0a0c7cb2c39", document.Root!.Elements("savefile").Single(element => (string?)element.Attribute("type") == "choco").Element("signature")!.Value);
        Assert.Equal(existing, File.ReadAllBytes(existingPath));
        Assert.False(File.Exists(update.Path));
    }

    [Fact]
    public void Existing_metadata_preserves_unrelated_entries_and_unknown_nodes()
    {
        string directory = Profile();
        string metadataPath = Path.Combine(directory, "metadata.xml");
        string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gamestatus vendor="retained">
              <!--keep--><extension key="x"><value>unchanged</value></extension>
              <savefile num="1" type="ff8" slot="1" custom="retained"><timestamp>5</timestamp><signature>00000000000000000000000000000000</signature><extra>retained</extra></savefile>
              <savefile num="23" type="ff8" slot="2"><timestamp></timestamp><signature>11111111111111111111111111111111</signature><extra>target retained</extra></savefile>
            </gamestatus>
            """;
        byte[] original = Encoding.UTF8.GetBytes(xml);
        File.WriteAllBytes(metadataPath, original);
        OriginalSaveMetadataUpdate update = OriginalSaveMetadata.Prepare(Path.Combine(directory, "slot2_save23.ff8"), Encoding.ASCII.GetBytes("abc"), DateTimeOffset.FromUnixTimeMilliseconds(77))!;
        Assert.Equal(original, update.OriginalBytes!.Value.ToArray());
        Assert.Equal(original, File.ReadAllBytes(metadataPath));
        XDocument document = Read(update);
        Assert.Equal("retained", (string?)document.Root!.Attribute("vendor"));
        Assert.Equal("unchanged", document.Root.Element("extension")!.Element("value")!.Value);
        Assert.Contains(document.Root.Nodes(), node => node is XComment comment && comment.Value == "keep");
        Assert.Equal("00000000000000000000000000000000", Slot(document, 1, 1).Element("signature")!.Value);
        Assert.Equal("retained", (string?)Slot(document, 1, 1).Attribute("custom"));
        Assert.Equal("target retained", Slot(document, 2, 23).Element("extra")!.Value);
        Assert.Equal("77", Slot(document, 2, 23).Element("timestamp")!.Value);
    }

    [Fact]
    public void Missing_target_entry_is_added_to_existing_valid_metadata()
    {
        string directory = Profile();
        File.WriteAllText(Path.Combine(directory, "metadata.xml"), "<gamestatus><extension>retained</extension></gamestatus>");
        OriginalSaveMetadataUpdate update = OriginalSaveMetadata.Prepare(Path.Combine(directory, "slot1_save30.ff8"), [1, 2, 3])!;
        XDocument document = Read(update);
        Assert.Single(document.Root!.Elements("savefile"));
        Assert.NotNull(Slot(document, 1, 30));
        Assert.Equal("retained", document.Root.Element("extension")!.Value);
    }

    [Theory]
    [InlineData("<broken>")]
    [InlineData("<other />")]
    [InlineData("<gamestatus><savefile type='ff8' num='23' slot='2'><signature>00000000000000000000000000000000</signature></savefile></gamestatus>")]
    [InlineData("<gamestatus><savefile type='ff8' num='31' slot='2'><timestamp></timestamp><signature>00000000000000000000000000000000</signature></savefile></gamestatus>")]
    [InlineData("<gamestatus><savefile type='ff8' num='23' slot='2'><timestamp>bad</timestamp><signature>00000000000000000000000000000000</signature></savefile></gamestatus>")]
    [InlineData("<gamestatus><savefile type='ff8' num='23' slot='2'><timestamp></timestamp><signature>bad</signature></savefile></gamestatus>")]
    [InlineData("<!DOCTYPE gamestatus [<!ENTITY unsafe 'value'>]><gamestatus />")]
    public void Invalid_metadata_fails_during_preparation_without_mutation(string xml)
    {
        string directory = Profile();
        string metadataPath = Path.Combine(directory, "metadata.xml");
        File.WriteAllText(metadataPath, xml);
        string savePath = Path.Combine(directory, "slot2_save23.ff8");
        Assert.Throws<InvalidDataException>(() => OriginalSaveMetadata.Prepare(savePath, [1]));
        Assert.Equal(xml, File.ReadAllText(metadataPath));
        Assert.False(File.Exists(savePath));
    }

    [Fact]
    public void Duplicate_target_entry_is_rejected()
    {
        string directory = Profile();
        string entry = "<savefile type='ff8' num='23' slot='2'><timestamp></timestamp><signature>00000000000000000000000000000000</signature></savefile>";
        File.WriteAllText(Path.Combine(directory, "metadata.xml"), "<gamestatus>" + entry + entry + "</gamestatus>");
        Assert.Throws<InvalidDataException>(() => OriginalSaveMetadata.Prepare(Path.Combine(directory, "slot2_save23.ff8"), [1]));
    }

    [Fact]
    public void Raw_bank_without_metadata_needs_no_sidecar_but_existing_metadata_requires_a_profile()
    {
        string directory = Path.Combine(root, "native-bank");
        Directory.CreateDirectory(directory);
        string savePath = Path.Combine(directory, "slot1_save01.ff8");
        Assert.Null(OriginalSaveMetadata.Prepare(savePath, [1]));
        File.WriteAllText(Path.Combine(directory, "metadata.xml"), "<gamestatus />");
        Assert.Throws<InvalidDataException>(() => OriginalSaveMetadata.Prepare(savePath, [1]));
    }

    [Theory]
    [InlineData("slot3_save01.ff8")]
    [InlineData("slot1_save31.ff8")]
    [InlineData("slot1_save00.ff8")]
    [InlineData("other.ff8")]
    public void Profile_rejects_non_native_slot_names(string name)
    {
        Assert.Throws<InvalidDataException>(() => OriginalSaveMetadata.Prepare(Path.Combine(Profile(), name), [1]));
    }

    private string Profile()
    {
        string directory = Path.Combine(root, "user_1234abcd");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static XDocument Read(OriginalSaveMetadataUpdate update)
    {
        return XDocument.Parse(Encoding.UTF8.GetString(update.UpdatedBytes.AsSpan()));
    }

    private static XElement Slot(XDocument document, int bank, int number)
    {
        return document.Root!.Elements("savefile").Single(element => (string?)element.Attribute("type") == "ff8" && (int?)element.Attribute("slot") == bank && (int?)element.Attribute("num") == number);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
