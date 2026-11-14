using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelPaletteTests
{
    [Fact]
    public void NativePaletteWriterKeepsItsSharedBytesAndOmitsHostedState()
    {
        NativeBattleModelCpuResources original = NativeBattleModelCpuResources.FromColdProcess();
        NativeBattleModelCpuResources written = original.SavePalettes507400(Upload(2, 0x53));
        AssertFilled(written.ReadPartyStatusPalette(1), 0, 512, 0x53);
        AssertFilled(written.ReadPartyStatusPalette(1), 512, 512, 0);
        AssertFilled(original.ReadPartyStatusPalette(1), 0, 1024, 0);
        string json = NativeBattleTestSnapshots.Json(written);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(nameof(written.PresentationPalettes), out _));
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(json);
        Assert.Equal(written.ReadPartyStatusPalette(1), restored.ReadPartyStatusPalette(1));
        Assert.Throws<InvalidDataException>(() => restored.ReadPartyStatusPalette(7));
        Assert.Throws<InvalidDataException>(() => restored.SavePalettes507400(Upload(0, 0x56), 0));
    }

    [Fact]
    public void EachActorCanOverwriteTheSameCanonicalSlotWithoutChangingOtherPublications()
    {
        NativeBattleModelCpuResources initial = Hosted();
        NativeBattleModelCpuResources first = initial.SavePalettes507400(Upload(0, 0x21), 0);
        NativeBattleModelCpuResources second = first.SavePalettes507400(Upload(0, 0x47), 7);
        NativeBattleModelCpuResources third = second.SavePalettes507400(Upload(0, 0x89), 24);
        Assert.Same(initial.Palettes, third.Palettes);
        Assert.Same(first.PresentationPalettes![0], third.PresentationPalettes![0]);
        Assert.Same(second.PresentationPalettes![7], third.PresentationPalettes![7]);
        AssertFilled(third.ReadPartyStatusPalette(0), 0, 512, 0x21);
        AssertFilled(third.ReadPartyStatusPalette(7), 0, 512, 0x47);
        AssertFilled(third.ReadPartyStatusPalette(24), 0, 512, 0x89);
        AssertFilled(second.ReadPartyStatusPalette(24), 0, 1024, 0);

        string json = NativeBattleTestSnapshots.Json(third);
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(json);
        restored.Validate();
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        Assert.Equal(
            NativeBattleTestSnapshots.Json(third.SavePalettes507400(Upload(1, 0x73), 7)),
            NativeBattleTestSnapshots.Json(restored.SavePalettes507400(Upload(1, 0x73), 7)));
    }

    [Fact]
    public void FirstActorWriteRetainsLegacyFactsAndUnknownBytes()
    {
        NativeBattleModelCpuBank legacy = NativeBattleModelCpuBank.Unknown()
            .Write(0, new byte[1024])
            .Write(512, Enumerable.Repeat((byte)0x44, 512).ToArray())
            .Write(3000, [0x65]);
        NativeBattleModelCpuResources hosted = Hosted() with { Palettes = legacy };
        Assert.Null(hosted.PresentationPalettes);
        AssertFilled(hosted.ReadPartyStatusPalette(24), 512, 512, 0x44);

        NativeBattleModelCpuResources changed = hosted.SavePalettes507400(Upload(0, 0x31), 24);
        NativeBattleModelCpuBank actor = changed.PresentationPalettes![24];
        AssertFilled(changed.ReadPartyStatusPalette(24), 0, 512, 0x31);
        AssertFilled(changed.ReadPartyStatusPalette(24), 512, 512, 0x44);
        Assert.Equal(new byte[] { 0x65 }, actor.Read(3000, 1));
        Assert.Throws<NotSupportedException>(() => actor.Read(2999, 1));
        Assert.Same(legacy, changed.Palettes);
        AssertFilled(changed.ReadPartyStatusPalette(7), 0, 512, 0);
    }

    [Fact]
    public void LinkedAndFormUploadsLeaveSavedBodyPalettesIntact()
    {
        NativeBattleModelCpuResources hosted = Hosted().SavePalettes507400(Upload(0, 0x43), 7);
        Assert.Same(hosted, hosted.SavePalettes507400(Upload(12, 0x60), 7));
        Assert.Same(hosted, hosted.SavePalettes507400(Upload(6, 0x61), 7));
        Assert.Same(hosted, hosted.SavePalettes507400(Upload(0, 0x62), 8));
        AssertFilled(hosted.ReadPartyStatusPalette(7), 0, 512, 0x43);
    }

    [Fact]
    public void PaletteMapsRequireBoundPartyRowsAndValidNativeStorage()
    {
        NativeBattleModelCpuResources hosted = Hosted();
        Assert.Throws<InvalidDataException>(() => hosted.ReadPartyStatusPalette(8));
        Assert.Throws<InvalidDataException>(() => hosted.ReadPartyStatusPalette(16));
        Assert.Throws<InvalidDataException>(() => hosted.SavePalettes507400(Upload(0, 0x61), 25));
        Assert.Throws<InvalidDataException>(() => hosted.SavePalettes507400(Upload(0, 0x61), 16));
        NativeBattleModelCpuBank bank = NativeBattleModelCpuBank.Unknown();
        AssertInvalidMap(8, bank);
        AssertInvalidMap(16, bank);
        AssertInvalidMap(25, bank);
        AssertInvalidMap(7, NativeBattleModelCpuBank.Unknown(1024));
        AssertInvalidMap(7, bank.PromoteResourceStorage());
        AssertInvalidMap(7, new([new(65536, [1])]));
        AssertInvalidMap(7, null!);
        NativeBattleModelCpuResources native = NativeBattleModelCpuResources.FromColdProcess();
        Assert.Throws<InvalidDataException>((native with { PresentationPalettes = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty }).Validate);

        void AssertInvalidMap(int row, NativeBattleModelCpuBank invalid)
        {
            var candidate = hosted with { PresentationPalettes = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty.Add(row, invalid) };
            Assert.Throws<InvalidDataException>(candidate.Validate);
        }
    }

    [Fact]
    public void InsufficientTotalSourceDoesNotPublishEarlierPaletteCopies()
    {
        NativeBattleModelCpuResources hosted = Hosted().SavePalettes507400(Upload(0, 0x43), 7);
        NativeBattleModelTextureLoad first = Upload(0, 0x67);
        byte[] truncatedNext = new byte[20];
        BinaryPrimitives.WriteInt16LittleEndian(truncatedNext.AsSpan(14), 225);
        NativeBattleModelTextureLoad malformed = first with
        {
            Source = first.Source with { Data = first.Source.Data.AddRange(truncatedNext) },
            TimOffsets = [0, first.Source.Data.Length]
        };
        string before = NativeBattleTestSnapshots.Json(hosted);
        Assert.Throws<InvalidDataException>(() => hosted.SavePalettes507400(malformed, 7));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(hosted));
        AssertFilled(hosted.ReadPartyStatusPalette(7), 0, 512, 0x43);
    }

    private static NativeBattleModelCpuResources Hosted()
    {
        NativeBattleGraphicsActor?[] rows = new NativeBattleGraphicsActor?[25];
        foreach (int row in new[] { 0, 1, 7, 24 })
        {
            rows[row] = new($"party-{row}", true);
        }
        rows[8] = new("enemy", false);
        return NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(new(rows.ToImmutableArray()));
    }

    private static NativeBattleModelTextureLoad Upload(int slot, byte value)
    {
        byte[] bytes = new byte[20 + 512];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), checked((short)(224 + slot)));
        bytes.AsSpan(20).Fill(value);
        return new(new("palette", bytes.ToImmutableArray()), new(ImmutableArray.CreateRange(new byte?[16])), checked((ushort)(1 << slot)), [0]);
    }

    private static void AssertFilled(byte[] bytes, int offset, int length, byte expected)
    {
        Assert.Equal(1024, bytes.Length);
        Assert.All(bytes.AsSpan(offset, length).ToArray(), value => Assert.Equal(expected, value));
    }
}
