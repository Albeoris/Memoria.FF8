using System.Text;
using System.Text.Json;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class FieldCharacterCatalogTests
{
    [Fact]
    public void NativeDescriptorsShareExactModelsAndClipsWhileKeepingAliasesAndSlots()
    {
        using LgpArchive fields = new(Archive(("first", Field()), ("second", Field())));
        using LgpArchive characters = new(Archive(("aaaa.hrc", Encoding.ASCII.GetBytes(":HEADER_BLOCK 2\n:SKELETON example\n:BONES 1\nhip\nroot\n1\n1 BBBB\n")), ("bbbb.rsd", Encoding.ASCII.GetBytes("@RSD940102\nPLY=CCCC.PLY\nNTEX=0\n")), ("cccc.p", new byte[] { 1 }), ("dddd.a", Animation())));
        FieldCharacterCatalog catalog = FieldCharacterCatalog.Create(fields, characters, TestContext.Current.CancellationToken);
        Assert.Empty(catalog.Diagnostics);
        Assert.Single(catalog.Models);
        Assert.Single(catalog.Animations);
        Assert.Equal(2, catalog.Sources.Count);
        Assert.Equal(2, catalog.Animations[0].Bindings.Count);
        FieldCharacterAnimationBank bank = catalog.CreateAnimationBank(catalog.Sources[0].Id);
        Assert.Single(bank.Animations);
        Assert.Equal(2, bank.NativeBindings.Count);
        Assert.Equal(0, bank.ResolveSlot(catalog.Sources[1].Id, 0));
        Assert.Equal("DDDD.aki", catalog.Sources[0].Clips[0].DescriptorName);
        Assert.Equal("dddd.a", catalog.Sources[0].Clips[0].ArchiveMember);
        Assert.Equal(catalog.GetAnimation(catalog.Sources[0].Id, 0).Id, catalog.GetAnimation(catalog.Sources[1].Id, 0).Id);
    }

    [Fact]
    public void MalformedMembersRemainDiagnosticsAndCancellationStopsTraversal()
    {
        using LgpArchive fields = new(Archive(("bad", new byte[] { 1, 2 })));
        using LgpArchive characters = new(Archive());
        FieldCharacterCatalog catalog = FieldCharacterCatalog.Create(fields, characters, TestContext.Current.CancellationToken);
        Assert.Empty(catalog.Sources);
        Assert.Single(catalog.Diagnostics);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => FieldCharacterCatalog.Create(fields, characters, cancellation.Token));
    }

    [Fact]
    public void OptionalInstalledAllFieldCatalogPreservesCloudOpeningAndCompatibleBindings()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF7_GAME_DIR for read-only original field catalog discovery.");
        FieldCharacterCatalog catalog = FieldCharacterCatalog.Discover(Ff7Installation.Discover(root), TestContext.Current.CancellationToken);
        Assert.NotEmpty(catalog.Models);
        FieldCharacterSource cloud = catalog.Sources.Single(source => source.FieldMember.Equals("md1stin", StringComparison.OrdinalIgnoreCase) && source.Descriptor.Name.Contains("cloud", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(4, cloud.Clips.Count);
        Assert.Equal(new[] { 1, 30, 15, 80 }, cloud.Clips.Select(clip => clip.NativeFrameCount));
        Assert.True(catalog.CompatibleAnimations(cloud.Id).Count > cloud.Clips.Count);
        string? report = Environment.GetEnvironmentVariable("MEMORIA_FIELD_CHARACTER_CATALOG_REPORT");
        if (!string.IsNullOrWhiteSpace(report))
        {
            File.WriteAllText(report, JsonSerializer.Serialize(new { Sources = catalog.Sources.Count, Models = catalog.Models.Count, Rigs = catalog.Models.Select(model => model.RigId).Distinct().Count(), Animations = catalog.Animations.Count, Diagnostics = catalog.Diagnostics, Cloud = cloud, Compatible = catalog.CompatibleAnimations(cloud.Id), CloudSources = catalog.Sources.Where(source => source.RigId == cloud.RigId) }, new JsonSerializerOptions { WriteIndented = false }));
        }
    }

    private static MemoryStream Archive(params (string Name, byte[] Data)[] members)
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.ASCII, true);
        writer.Write((ushort)0);
        writer.Write(Encoding.ASCII.GetBytes("SQUARESOFT"));
        writer.Write(members.Length);
        int offset = 16 + members.Length * 27 + 3600 + 2;
        foreach (var member in members)
        {
            Name(member.Name);
            writer.Write(offset);
            writer.Write((byte)14);
            writer.Write((ushort)0);
            offset += 24 + member.Data.Length;
        }
        writer.Write(new byte[3600]);
        writer.Write((ushort)0);
        foreach (var member in members)
        {
            Name(member.Name);
            writer.Write(member.Data.Length);
            writer.Write(member.Data);
        }
        stream.Position = 0;
        return stream;

        void Name(string name)
        {
            byte[] bytes = new byte[20];
            Encoding.ASCII.GetBytes(name).CopyTo(bytes, 0);
            writer.Write(bytes);
        }
    }

    private static byte[] Field()
    {
        using MemoryStream section = new();
        using (BinaryWriter writer = new(section, Encoding.ASCII, true))
        {
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)512);
            writer.Write((ushort)5);
            writer.Write(Encoding.ASCII.GetBytes("actor"));
            writer.Write((ushort)0);
            writer.Write(Encoding.ASCII.GetBytes("AAAA.HRC"));
            writer.Write(Encoding.ASCII.GetBytes("512\0"));
            writer.Write((ushort)1);
            writer.Write(new byte[30]);
            writer.Write((ushort)8);
            writer.Write(Encoding.ASCII.GetBytes("DDDD.aki"));
            writer.Write((ushort)0);
        }
        using MemoryStream field = new();
        using (BinaryWriter writer = new(field, Encoding.ASCII, true))
        {
            writer.Write((ushort)0);
            writer.Write(9);
            int offset = 42;
            for (int i = 0; i < 9; i++)
            {
                writer.Write(offset);
                offset += 4 + (i == 2 ? (int)section.Length : 0);
            }
            for (int i = 0; i < 9; i++)
            {
                byte[] bytes = i == 2 ? section.ToArray() : [];
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }
        using MemoryStream compressed = new();
        byte[] decoded = field.ToArray();
        for (int i = 0; i < decoded.Length; i += 8)
        {
            compressed.WriteByte(255);
            compressed.Write(decoded, i, Math.Min(8, decoded.Length - i));
        }
        using MemoryStream framed = new();
        using BinaryWriter frameWriter = new(framed);
        frameWriter.Write((int)compressed.Length);
        frameWriter.Write(compressed.ToArray());
        return framed.ToArray();
    }

    private static byte[] Animation()
    {
        byte[] bytes = new byte[72];
        BitConverter.GetBytes(1).CopyTo(bytes, 0);
        BitConverter.GetBytes(1).CopyTo(bytes, 4);
        BitConverter.GetBytes(1).CopyTo(bytes, 8);
        return bytes;
    }
}
