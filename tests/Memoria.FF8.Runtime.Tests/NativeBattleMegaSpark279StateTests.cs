using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMegaSpark279Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMegaSpark279StateTests
{
    [Fact]
    public void Cold_process_owns_only_sparse_scalars_and_inactive_root()
    {
        var state = NativeBattleMegaSpark279State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[12], state.Read(BaseAddress, 12));
        Assert.Equal(new byte[12], state.Read(CursorGlobal, 12));
        Assert.Equal(new byte[20], state.RootBankStorage!.Read(0, 20));
        Assert.Equal(new[] { 20, 20, 316, 316, 56, 64, 2868 }, Pools.Select(pool => (int)pool.Stride));
        Assert.Equal(new[] { 1, 16, 3, 16, 48, 64, 1 }, Pools.Select(pool => (int)pool.Capacity));
        Assert.Equal(RowsOffset, 0x3334 + 2868);
        Assert.Equal(RowHintOffset, RowsOffset + RowCount * RowStride);
        Assert.Equal(ScratchOffset, RowHintOffset + 4);
        Assert.Equal(GeneratedSineOffset, ScratchOffset + ScratchLength);
        Assert.Equal(FirstPacketOffset, GeneratedSineOffset + GeneratedSineLength);
    }

    [Fact]
    public void Partial_scalar_publications_preserve_unknown_neighbors_and_JSON_root_transfer()
    {
        var state = NativeBattleMegaSpark279State.FromUnknown().WriteDword(SeedGlobal, 1234) with { RootBankStorage = null };
        var view = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(4, new byte[] { 1, 2, 3, 4 });
        state = state.WriteView(BaseAddress, view, 12);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleMegaSpark279State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.Equal(1234u, restored.ReadDword(SeedGlobal));
        Assert.Equal(view.ReadSnapshot(0, 12).Known, restored.ReadView(BaseAddress, 12).ReadSnapshot(0, 12).Known);
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(InitializerGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(CursorGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(ArenaGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void Task_banks_descriptors_COMMON_special_rows_and_gaps_are_not_scalar_aliases()
    {
        var state = NativeBattleMegaSpark279State.FromColdProcess();
        foreach (uint address in new[] { BaseAddress + 12, RootBank, RootDescriptor, CursorGlobal - 1, CursorGlobal + 12, ArenaAddress, ArenaAddress + RowsOffset, ArenaAddress + ScratchOffset, ArenaAddress + FirstPacketOffset })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.MarkUnknown(12, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(20, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => state.Read(BaseAddress + 8, 5));
        Assert.Throws<InvalidDataException>(() => state.Read(BaseAddress, -1));
    }

    [Fact]
    public void Resource_windows_retain_workspace_suffixes_and_reject_unproduced_lookup_indices()
    {
        var resources = Resources();
        foreach ((NativeBattleGfAnimationSource source, int length) in new[] { (resources.ProgrammeC, ProgrammeCWorkspaceLength), (resources.ProgrammeD, ProgrammeDWorkspaceLength) })
        {
            var workspace = new NativeBattleGfAnimationWorkspace(Enumerable.Repeat((byte)0xA5, 272).ToImmutableArray()).Initialize7016B0(source, length);
            Assert.Equal(source.Address, workspace.ResourceAddress);
            Assert.All(NativeBattleStorageSnapshots.Capture(workspace).Data.Skip(length), value => Assert.Equal((byte)0xA5, value));
        }
        Assert.Equal(24, resources.ReadAttachmentRow(2, 7).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadAttachmentRow(3, 0));
        Assert.Throws<NotSupportedException>(() => resources.ReadAttachmentRow(0, 8));
        Assert.Throws<NotSupportedException>(() => resources.ReadDistanceByte(-1, 0));
        Assert.Throws<NotSupportedException>(() => resources.ReadDistanceByte(0, 128));
        Assert.Throws<NotSupportedException>(() => resources.ReadNormalizationWord(63));
        Assert.Throws<NotSupportedException>(() => resources.ReadNormalizationWord(256));
        foreach (int index in new[] { 0, 255, 257, 2049, 4095 })
        {
            Assert.Throws<NotSupportedException>(() => resources.ReadSineWord(index));
        }
        Assert.Equal(resources.ReadStatic(NormalizationAddress, NormalizationLength).ToArray(), resources.ReadStatic(BodyNormalizationAddress, NormalizationLength).ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(SineAddress, 8192));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(SineAddress + SineLength, 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(NormalizationAddress + NormalizationLength - 1, 2));
    }

    internal static NativeBattleMegaSpark279Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        byte[] sprite = new byte[SpriteLength];
        Put(sprite, 0, 0x0A010524);
        BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 8);
        for (int phase = 0; phase <= 8; phase++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + phase * 2), 64);
        }
        byte[] points = new byte[24];
        int[] selectors = [17, 18, 21];
        for (int index = 0; index < selectors.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(points.AsSpan(index * 8 + 6), (short)selectors[index]);
        }
        byte[] extra = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(extra.AsSpan(6), 10);
        byte[] normalization = new byte[NormalizationLength];
        for (int index = 0; index < NormalizationLength / 2; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(normalization.AsSpan(index * 2), 4096);
        }
        var resources = new NativeBattleMegaSpark279Resources(Source(ProgrammeAAddress, ProgrammeALength, 5, 27, 90), Source(ProgrammeBAddress, ProgrammeBLength, 3, 5, 16), Source(ProgrammeCAddress, ProgrammeCLength, 2, 2, 12), Source(ProgrammeDAddress, ProgrammeDLength, 2, 4, 18), new(0, SpriteAddress, 0, 0, sprite.ToImmutableArray()), new("FF8_EN:10B1A2C", camera.ToImmutableArray()), 0x641A3, points.ToImmutableArray(), extra.ToImmutableArray(), new byte[576].ToImmutableArray(), Enumerable.Repeat((byte)1, DistanceLength).ToImmutableArray(), normalization.ToImmutableArray(), new byte[SineLength].ToImmutableArray());
        resources.Validate();
        return resources;

        static NativeBattleGfAnimationSource Source(uint address, int length, int geometries, int nodes, int frames)
        {
            byte[] bytes = new byte[length];
            Put(bytes, 0, (uint)geometries);
            Put(bytes, 4, 0x100);
            for (int geometry = 0; geometry < geometries; geometry++)
            {
                Put(bytes, 8 + geometry * 4, 64);
            }
            Put(bytes, 64, 20);
            Put(bytes, 68, 1);
            Put(bytes, 72, 1);
            Put(bytes, 0x100, (uint)nodes);
            Put(bytes, 0x104, (uint)frames);
            for (int node = 0; node < nodes; node++)
            {
                Put(bytes, 0x108 + node * 4, (uint)(8 + nodes * 4 + node * 8));
            }
            return new(address, bytes.ToImmutableArray());
        }
    }

    private static void Put(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    }
}
