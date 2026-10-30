using System.Text;
using Xunit;

namespace Memoria.FF8.App.Tests;

public sealed partial class OriginalSaveMetadataTests
{
    [Fact]
    public void Publication_updates_save_and_signature_and_removes_staged_files()
    {
        string directory = Profile();
        string destination = Path.Combine(directory, "slot2_save23.ff8");
        string temporary = Path.Combine(directory, "candidate.tmp");
        File.WriteAllText(temporary, "abc", new UTF8Encoding(false));
        OriginalSavePublication.Publish(temporary, destination, overwrite: false);
        Assert.Equal("abc", File.ReadAllText(destination));
        Assert.False(File.Exists(temporary));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
        System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(Path.Combine(directory, "metadata.xml"));
        Assert.Equal("933c26e888a5a6a9de3af0a0c7cb2c39", Slot(document, 2, 23).Element("signature")!.Value);
    }

    [Fact]
    public void Publication_rejects_invalid_metadata_before_replacing_native_destination()
    {
        string directory = Profile();
        string destination = Path.Combine(directory, "slot1_save01.ff8");
        string temporary = Path.Combine(directory, "candidate.tmp");
        string metadataPath = Path.Combine(directory, "metadata.xml");
        File.WriteAllText(destination, "old native");
        File.WriteAllText(metadataPath, "<broken>");
        File.WriteAllText(temporary, "new native");
        Assert.Throws<InvalidDataException>(() => OriginalSavePublication.Publish(temporary, destination, overwrite: true));
        Assert.Equal("old native", File.ReadAllText(destination));
        Assert.Equal("<broken>", File.ReadAllText(metadataPath));
        Assert.False(File.Exists(temporary));
        Assert.Equal(2, Directory.GetFiles(directory).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Publication_rolls_native_back_when_metadata_rename_is_denied(bool existing)
    {
        string directory = Profile();
        string destination = Path.Combine(directory, "slot1_save01.ff8");
        string temporary = Path.Combine(directory, "candidate.tmp");
        string metadataPath = Path.Combine(directory, "metadata.xml");
        const string metadata = "<gamestatus><extension>keep</extension></gamestatus>";
        if (existing)
        {
            File.WriteAllText(destination, "old native");
        }
        File.WriteAllText(metadataPath, metadata);
        File.WriteAllText(temporary, "new native");
        using (FileStream held = new(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Exception? failure = Record.Exception(() => OriginalSavePublication.Publish(temporary, destination, overwrite: true));
            Assert.True(failure is IOException or UnauthorizedAccessException);
        }
        if (existing)
        {
            Assert.Equal("old native", File.ReadAllText(destination));
        }
        else
        {
            Assert.False(File.Exists(destination));
        }
        Assert.Equal(metadata, File.ReadAllText(metadataPath));
        Assert.False(File.Exists(temporary));
        Assert.Equal(existing ? 2 : 1, Directory.GetFiles(directory).Length);
    }

    [Fact]
    public void Publication_preserves_existing_native_destination_when_overwrite_is_disabled()
    {
        string directory = Profile();
        string destination = Path.Combine(directory, "slot1_save01.ff8");
        string temporary = Path.Combine(directory, "candidate.tmp");
        File.WriteAllText(destination, "old native");
        File.WriteAllText(temporary, "new native");
        Assert.Throws<IOException>(() => OriginalSavePublication.Publish(temporary, destination, overwrite: false));
        Assert.Equal("old native", File.ReadAllText(destination));
        Assert.False(File.Exists(Path.Combine(directory, "metadata.xml")));
        Assert.False(File.Exists(temporary));
    }
}
