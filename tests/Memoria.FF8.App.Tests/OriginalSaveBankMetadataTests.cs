using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.App.Tests;

public sealed partial class OriginalSaveBankTests
{
    [Fact]
    public void Store_transfer_to_original_profile_updates_signature_and_keeps_unrelated_xml()
    {
        string original = Directory.CreateDirectory(Path.Combine(directory, "user_123abcd")).FullName;
        string metadataPath = Path.Combine(original, "metadata.xml");
        File.WriteAllText(metadataPath, "<gamestatus><extension>unchanged</extension></gamestatus>");
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "memoria"), originalDirectory: original, loadOriginal: LoadOriginal);
        store.Save(1, new GameSession(Defaults()));
        byte[] source = File.ReadAllBytes(store.GetPath(1));
        store.Transfer(1, 131);
        string destination = store.GetPath(131);
        byte[] native = File.ReadAllBytes(destination);
        byte[] salted = native.Concat(Encoding.ASCII.GetBytes("123abcd")).ToArray();
        XDocument metadata = XDocument.Load(metadataPath);
        XElement target = metadata.Root!.Elements("savefile").Single();
        Assert.Equal("2", (string?)target.Attribute("slot"));
        Assert.Equal("1", (string?)target.Attribute("num"));
        Assert.Equal(Convert.ToHexStringLower(MD5.HashData(salted)), target.Element("signature")!.Value);
        Assert.True(long.TryParse(target.Element("timestamp")!.Value, out long timestamp));
        Assert.True(timestamp > 0);
        Assert.Equal("unchanged", metadata.Root.Element("extension")!.Value);
        Assert.Equal(source, File.ReadAllBytes(store.GetPath(1)));
        Assert.False(File.Exists(destination + ".tmp"));
        Assert.Equal(2, Directory.GetFiles(original).Length);
    }

    [Fact]
    public void Store_keeps_previous_native_slot_when_profile_metadata_is_malformed()
    {
        string original = Directory.CreateDirectory(Path.Combine(directory, "user_123abcd")).FullName;
        PlayerSaveStore store = new(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "memoria"), originalDirectory: original, loadOriginal: LoadOriginal);
        store.Save(101, new GameSession(Defaults()));
        byte[] native = File.ReadAllBytes(store.GetPath(101));
        string metadataPath = Path.Combine(original, "metadata.xml");
        File.WriteAllText(metadataPath, "<broken>");
        Assert.Throws<InvalidDataException>(() => store.Save(101, new GameSession(Defaults() with { Playtime = new(999, 0, 0) })));
        Assert.Equal(native, File.ReadAllBytes(store.GetPath(101)));
        Assert.Equal("<broken>", File.ReadAllText(metadataPath));
        Assert.False(File.Exists(store.GetPath(101) + ".tmp"));
        Assert.Equal(2, Directory.GetFiles(original).Length);
    }
}
