using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBreath129Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath129StateTests
{
    [Fact]
    public void Pending_cold_and_imported_unknown_sources_keep_distinct_scalar_and_task_knownness()
    {
        NativeBattleBreath129State cold = NativeBattleBreath129State.FromColdProcess();
        NativeBattleBreath129State unknown = NativeBattleBreath129State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.Equal(0u, cold.ReadDword(WorkingGlobal));
        Assert.Null(cold.MutableSources);
        Assert.Throws<NotSupportedException>(() => cold.ReadWord(MeshAddress + 14));
        Assert.Throws<NotSupportedException>(() => unknown.ReadWord(MeshAddress + 14));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(WorkingGlobal));
        Assert.Same(unknown, unknown.RequireSeeds(null!));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(RootBank));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(RootDescriptor));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(0x20DFAB8));
    }

    [Fact]
    public void Current_vertex_payload_and_inactive_root_bank_roundtrip_without_reseeding_or_arena_mirrors()
    {
        NativeBattleBreath129State state = NativeBattleBreath129State.FromUnknown().WriteWord(MeshAddress + 14, 0xCAFE).WriteDword(WorkingGlobal, 0x20DFAB8);
        NativeBattleBreath129State restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBreath129State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Equal((ushort)0xCAFE, restored.ReadWord(MeshAddress + 14));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(MeshAddress + 12));
        Assert.Same(restored, restored.RequireSeeds(null!));
        Assert.Throws<NotSupportedException>(() => restored.RootBankStorage!.Read(0, 1));
        (restored with { RootBankStorage = null }).Validate();
        Assert.Throws<InvalidDataException>(() => (restored with { RootBankStorage = restored.RootBankStorage!.Write(RootBankLength, new byte[1]) }).Validate());
        Assert.Throws<InvalidDataException>(() => restored.Read(MeshAddress + MutableSourcesLength - 1, 2));
    }

    [Fact]
    public void Native_preprocessing_scan_includes_directory_and_actual_camera_word_without_minimum_subtraction()
    {
        byte[] bytes = Sources(0, 0, -100, 0);
        for (int vertex = 0; vertex < 88; vertex++)
        {
            BitConverter.GetBytes((short)-200).CopyTo(bytes, MeshLength + 12 + vertex * 8);
        }
        byte[] camera = new byte[0x128];
        BitConverter.GetBytes((short)32).CopyTo(camera, 4);
        NativeBattleBreath129Resources resources = Resources(bytes, camera, new short[8192]);
        NativeBattleBreath129Frame frame = Frame(bytes);
        NativeBattleBreath129Frame after = NativeBattleBreath129Preprocessing.Initialize(frame, resources);
        Assert.Equal(unchecked((ushort)-4096), after.State.ReadWord(MeshAddress + 14));
        Assert.Equal(unchecked((ushort)((-200 << 12) / 232)), after.State.ReadWord(IndexedVerticesAddress + 14));
        Assert.Equal(unchecked((ushort)-100), after.State.ReadWord(MeshAddress + 12));
        Assert.Equal(camera, resources.Camera.Data.ToArray());
        Assert.Equal(NativeBattleTestSnapshots.Json(frame.Memory), NativeBattleTestSnapshots.Json(after.Memory));
    }

    [Theory]
    [InlineData(0, 17)]
    [InlineData(-1, 0)]
    [InlineData(-32768, -1)]
    public void Deformation_uses_masked_actual_tables_preserves_output_W_and_publishes_GTE_before_CPU_offsets(short w, int phase)
    {
        byte[] bytes = Sources(100, 200, 300, w);
        short[] trig = new short[8192];
        trig[w & 0xFFE] = 64;
        int selector = (w + phase) & 4095;
        trig[selector * 2] = -4096;
        trig[selector * 2 + 1] = 8192;
        NativeBattleBreath129Resources resources = Resources(bytes, new byte[0x128], trig);
        NativeBattleBreath129Frame frame = Frame(bytes);
        NativeBattleBreath129Frame after = NativeBattleBreath129Deformation.Transform(frame, MeshAddress, phase, resources);
        for (int vertex = 0; vertex < VertexCount; vertex++)
        {
            int offset = SelectedVerticesOffset + vertex * 8;
            Assert.Equal((short)108, BitConverter.ToInt16(after.Memory.Arena!.Read(offset, 2)));
            Assert.Equal((short)204, BitConverter.ToInt16(after.Memory.Arena.Read(offset + 2, 2)));
            Assert.Equal((short)300, BitConverter.ToInt16(after.Memory.Arena.Read(offset + 4, 2)));
            Assert.Throws<NotSupportedException>(() => after.Memory.Arena.Read(offset + 6, 2));
        }
        Assert.Equal(new int?[] { 100, 200, 300 }, after.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(new int?[] { 100, 200, 300 }, after.Memory.ScriptedColorRegisters.Ir);
        Assert.Equal(BitConverter.GetBytes(w), after.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6, 2).ToArray());
        Assert.Equal(NativeBattleTestSnapshots.Json(frame.State), NativeBattleTestSnapshots.Json(after.State));
    }

    private static NativeBattleBreath129Frame Frame(byte[] bytes)
    {
        NativeBattleBreath129State state = NativeBattleBreath129State.FromUnknown() with { MutableSources = NativeBattleEffectArena.Clear().Write(0, bytes) };
        state = state.WriteDword(WorkingGlobal, 0x20DFAB8);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(SelectedVerticesOffset, VertexCount * 8), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null) };
        return new(memory, new(129, 128, new(129, Entry, Initializer), null, 0), new(0), state);
    }
    private static byte[] Sources(short x, short y, short z, short w)
    {
        byte[] bytes = new byte[MutableSourcesLength];
        BitConverter.GetBytes(8 + VertexCount * 8).CopyTo(bytes, 0);
        BitConverter.GetBytes(VertexCount).CopyTo(bytes, 4);
        BitConverter.GetBytes(IndexedVerticesLength).CopyTo(bytes, MeshLength);
        BitConverter.GetBytes(88).CopyTo(bytes, MeshLength + 4);
        for (int vertex = 0; vertex < VertexCount; vertex++)
        {
            BitConverter.GetBytes(x).CopyTo(bytes, 8 + vertex * 8);
            BitConverter.GetBytes(y).CopyTo(bytes, 10 + vertex * 8);
            BitConverter.GetBytes(z).CopyTo(bytes, 12 + vertex * 8);
            BitConverter.GetBytes(w).CopyTo(bytes, 14 + vertex * 8);
        }
        return bytes;
    }
    private static NativeBattleBreath129Resources Resources(byte[] sources, byte[] camera, short[] trig) => new(new("FF8_EN:12272DC", sources.ToImmutableArray()), new(FirstActAddress, []), new(SecondActAddress, []), new(ThirdActAddress, []), new(FourthActAddress, []), new(0, SpriteAddress, 0, 0, []), new("FF8_EN:12291CC", camera.ToImmutableArray()), [], [], [], [], [], [], trig.ToImmutableArray(), 0x641DA);
}
