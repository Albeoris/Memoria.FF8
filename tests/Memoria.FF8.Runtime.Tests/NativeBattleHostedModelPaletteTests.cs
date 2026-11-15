using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelPaletteTests
{
    private static readonly BattlePartyModelOwner OriginalParty = (BattlePartyModelOwner)OriginalOwner(0);
    private static readonly BattlePartyModelOwner OriginalPartyTwo = (BattlePartyModelOwner)OriginalOwner(1);
    private static readonly BattlePartyModelOwner AddedParty = new BattlePartyActorModel(new("added-party"));
    private static readonly BattlePartyModelOwner SecondAddedParty = new BattlePartyActorModel(new("second-added-party"));
    private static readonly BattlePartyModelOwner MissingParty = new BattlePartyActorModel(new("missing-party"));
    private static readonly BattleModelOwner Enemy = new BattleEnemyActorModel(new("enemy"));

    [Fact]
    public void NativePaletteWriterKeepsItsSharedBytesAndOmitsHostedState()
    {
        NativeBattleModelCpuResources original = NativeBattleModelCpuResources.FromColdProcess().BindPartyOwner(OriginalPartyTwo, NativeBattleModelSlot.Decode(1));
        NativeBattleModelCpuResources written = original.SavePalettes507400(Upload(2, 0x53));
        AssertFilled(written.ReadPartyStatusPalette(OriginalPartyTwo), 0, 512, 0x53);
        AssertFilled(written.ReadPartyStatusPalette(OriginalPartyTwo), 512, 512, 0);
        AssertFilled(original.ReadPartyStatusPalette(OriginalPartyTwo), 0, 1024, 0);
        string json = NativeBattleTestSnapshots.Json(written);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(nameof(written.PresentationPalettes), out _));
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(json).BindPartyOwner(OriginalPartyTwo, NativeBattleModelSlot.Decode(1));
        Assert.Equal(written.ReadPartyStatusPalette(OriginalPartyTwo), restored.ReadPartyStatusPalette(OriginalPartyTwo));
        Assert.Throws<InvalidDataException>(() => restored.ReadPartyStatusPalette(MissingParty));
        Assert.Throws<InvalidDataException>(() => restored.SavePalettes507400(Upload(0, 0x56), MissingParty));
    }

    [Fact]
    public void EachActorCanOverwriteTheSameCanonicalSlotWithoutChangingOtherPublications()
    {
        NativeBattleModelCpuResources initial = Hosted();
        NativeBattleModelCpuResources first = initial.SavePalettes507400(Upload(0, 0x21), OriginalParty);
        NativeBattleModelCpuResources second = first.SavePalettes507400(Upload(0, 0x47), AddedParty);
        NativeBattleModelCpuResources third = second.SavePalettes507400(Upload(0, 0x89), SecondAddedParty);
        Assert.Same(initial.Palettes, third.Palettes);
        Assert.Same(first.PresentationPalettes![OriginalParty], third.PresentationPalettes![OriginalParty]);
        Assert.Same(second.PresentationPalettes![AddedParty], third.PresentationPalettes![AddedParty]);
        AssertFilled(third.ReadPartyStatusPalette(OriginalParty), 0, 512, 0x21);
        AssertFilled(third.ReadPartyStatusPalette(AddedParty), 0, 512, 0x47);
        AssertFilled(third.ReadPartyStatusPalette(SecondAddedParty), 0, 512, 0x89);
        AssertFilled(second.ReadPartyStatusPalette(SecondAddedParty), 0, 1024, 0);

        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.Independent(third);
        restored.Validate();
        Assert.Equal(third.ReadPartyStatusPalette(AddedParty), restored.ReadPartyStatusPalette(AddedParty));
        Assert.Equal(
            third.SavePalettes507400(Upload(1, 0x73), AddedParty).ReadPartyStatusPalette(AddedParty),
            restored.SavePalettes507400(Upload(1, 0x73), AddedParty).ReadPartyStatusPalette(AddedParty));
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
        AssertFilled(hosted.ReadPartyStatusPalette(SecondAddedParty), 512, 512, 0x44);

        NativeBattleModelCpuResources changed = hosted.SavePalettes507400(Upload(0, 0x31), SecondAddedParty);
        NativeBattleModelCpuBank actor = changed.PresentationPalettes![SecondAddedParty];
        AssertFilled(changed.ReadPartyStatusPalette(SecondAddedParty), 0, 512, 0x31);
        AssertFilled(changed.ReadPartyStatusPalette(SecondAddedParty), 512, 512, 0x44);
        Assert.Equal(new byte[] { 0x65 }, actor.Read(3000, 1));
        Assert.Throws<NotSupportedException>(() => actor.Read(2999, 1));
        Assert.Same(legacy, changed.Palettes);
        AssertFilled(changed.ReadPartyStatusPalette(AddedParty), 0, 512, 0);
    }

    [Fact]
    public void LinkedAndFormUploadsLeaveSavedBodyPalettesIntact()
    {
        NativeBattleModelCpuResources hosted = Hosted().SavePalettes507400(Upload(0, 0x43), AddedParty);
        Assert.Same(hosted, hosted.SavePalettes507400(Upload(12, 0x60), AddedParty));
        Assert.Same(hosted, hosted.SavePalettes507400(Upload(6, 0x61), AddedParty));
        Assert.Same(hosted, hosted.SavePalettes507400(Upload(0, 0x62), Enemy));
        AssertFilled(hosted.ReadPartyStatusPalette(AddedParty), 0, 512, 0x43);
    }

    [Fact]
    public void PaletteMapsRequireBoundPartyRowsAndValidNativeStorage()
    {
        NativeBattleModelCpuResources hosted = Hosted();
        Assert.Throws<InvalidDataException>(() => hosted.ReadPartyStatusPalette(Enemy));
        Assert.Throws<InvalidDataException>(() => hosted.ReadPartyStatusPalette(MissingParty));
        Assert.Throws<InvalidDataException>(() => hosted.SavePalettes507400(Upload(0, 0x61), MissingParty));
        Assert.Same(hosted, hosted.SavePalettes507400(Upload(0, 0x61), Enemy));
        NativeBattleModelCpuBank bank = NativeBattleModelCpuBank.Unknown();
        AssertInvalidMap(MissingParty, bank);
        AssertInvalidMap(AddedParty, NativeBattleModelCpuBank.Unknown(1024));
        AssertInvalidMap(AddedParty, bank.PromoteResourceStorage());
        AssertInvalidMap(AddedParty, new([new(65536, [1])]));
        AssertInvalidMap(AddedParty, null!);
        NativeBattleModelCpuResources native = NativeBattleModelCpuResources.FromColdProcess();
        Assert.Throws<InvalidDataException>((native with { PresentationPalettes = ImmutableDictionary<BattlePartyModelOwner, NativeBattleModelCpuBank>.Empty }).Validate);

        void AssertInvalidMap(BattlePartyModelOwner owner, NativeBattleModelCpuBank invalid)
        {
            var candidate = hosted with { PresentationPalettes = ImmutableDictionary<BattlePartyModelOwner, NativeBattleModelCpuBank>.Empty.Add(owner, invalid) };
            Assert.Throws<InvalidDataException>(candidate.Validate);
        }
    }

    [Fact]
    public void InsufficientTotalSourceDoesNotPublishEarlierPaletteCopies()
    {
        NativeBattleModelCpuResources hosted = Hosted().SavePalettes507400(Upload(0, 0x43), AddedParty);
        NativeBattleModelTextureLoad first = Upload(0, 0x67);
        byte[] truncatedNext = new byte[20];
        BinaryPrimitives.WriteInt16LittleEndian(truncatedNext.AsSpan(14), 225);
        NativeBattleModelTextureLoad malformed = first with
        {
            Source = first.Source with { Data = first.Source.Data.AddRange(truncatedNext) },
            TimOffsets = [0, first.Source.Data.Length]
        };
        byte[] before = hosted.ReadPartyStatusPalette(AddedParty);
        Assert.Throws<InvalidDataException>(() => hosted.SavePalettes507400(malformed, AddedParty));
        Assert.Equal(before, hosted.ReadPartyStatusPalette(AddedParty));
        AssertFilled(hosted.ReadPartyStatusPalette(AddedParty), 0, 512, 0x43);
    }

    private static NativeBattleModelCpuResources Hosted()
    {
        BattleModelRoster roster = new([OriginalParty, OriginalPartyTwo, AddedParty, SecondAddedParty, Enemy]);
        NativeBattleModelAliases aliases = new(roster, [(NativeBattleModelSlot.Decode(0), OriginalParty), (NativeBattleModelSlot.Decode(1), OriginalPartyTwo)]);
        return NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(roster, aliases);
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
