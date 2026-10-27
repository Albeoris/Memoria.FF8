using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBadBreath131Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath131StripDrawingTests
{
    private const uint Working = ArenaAddress + 0x400;
    private const uint Vertices = Working + ScratchOffset;
    private const int Scratch = 0x400 + ScratchOffset;

    [Fact]
    public void Current_mutable_source_and_working_relative_XYZW_publish_GTE_and_92_without_allocating_or_delivering()
    {
        NativeBattleBadBreath131Frame frame = Frame();
        var result = Draw(frame);
        Assert.Equal(156, result.Batch.AllocationBytes);
        Assert.Equal(10, result.Batch.Attempts.Length);
        Assert.Equal(new byte[] { 0xE2, 0xE1, 0x2E, 0xE2, 0xE2, 0xE1, 0x3E, 0xE2 }, result.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => unchecked((byte)(attempt.Primitive!.Words[1] >> 24))));
        Assert.Equal(new[] { 40, 52, 0, 60, 124, 136, 72, 144 }, result.Batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => attempt.ByteOffsetRelative));
        Assert.Equal(MeshAAddress + 0x58Cu, U32(result.Frame.Staging!, NativeBattleTemporaryMemory.Address + 0x24));
        Assert.Equal(500u, U32(result.Frame.Staging!, NativeBattleTemporaryMemory.Address + 0x30));
        Assert.Equal(NativeBattleTemporaryMemory.Address + 92, U32(result.Frame.Staging!, NativeBattleTemporaryMemory.Cursor));
        Assert.Equal(frame.State, result.Frame.State);
        Assert.Same(frame.Memory.Arena, result.Frame.Memory.Arena);
        Assert.Equal(frame.Memory.GeometryRegisters!.Rotation20, result.Frame.Memory.GeometryRegisters!.Rotation20);
        Assert.Equal(frame.Memory.GeometryRegisters!.Translation, result.Frame.Memory.GeometryRegisters!.Translation);
        Assert.Equal(0x7F123456u, result.Frame.Memory.ScriptedColorRegisters!.Material);
        Assert.False(result.Frame.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Frame.Memory.ScriptedColorRegisters!.NormalInputKnown.Value[7]);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Frame.Staging!, NativeBattleTemporaryMemory.Address, 92);
        Assert.Throws<NotSupportedException>(() => temporary.Read(0x2C, 4));
        Assert.Throws<NotSupportedException>(() => temporary.Read(0x38, 36));
    }

    [Fact]
    public void Near_rejection_keeps_lazy_fourth_XYZ_color_window_and_AVSZ_unknown()
    {
        NativeBattleBadBreath131Frame frame = Frame(0);
        frame = frame with
        {
            Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(Scratch + 24, 6), ScriptedGraphics = frame.Memory.ScriptedGraphics! with { Zsf4 = null } },
            Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, NativeBattleTemporaryMemory.Address, Context().MarkUnknown(4, 28), 92)
        };
        var result = Draw(frame);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Length);
        Assert.All(result.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        Assert.Null(result.Frame.Memory.GeometryRegisters!.Otz);
        Assert.NotEqual(0u, U32(result.Frame.Staging!, NativeBattleTemporaryMemory.Address + 0x34) & 0x60000);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Frame.Staging!, NativeBattleTemporaryMemory.Address, 92);
        Assert.Throws<NotSupportedException>(() => temporary.Read(0x30, 4));
    }

    [Fact]
    public void Unknown_reached_XYZ_or_missing_warm_source_fails_and_other_scratch_owner_is_rejected()
    {
        NativeBattleBadBreath131Frame frame = Frame();
        Assert.Throws<InvalidDataException>(() => NativeBattleBadBreath131StripDrawing.Draw(frame, NativeBattleTemporaryMemory.Address, MeshAAddress, Vertices + 8, new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270));
        Assert.Throws<NotSupportedException>(() => Draw(frame with { State = frame.State with { MeshA = null, PendingColdSources = false } }));
        Assert.Throws<NotSupportedException>(() => Draw(frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(Scratch, 2) } }));
    }

    [Fact]
    public void JSON_continuation_uses_current_mutable_source_without_hydration_or_a_second_register_owner()
    {
        NativeBattleBadBreath131Frame frame = Frame();
        byte[] changed = frame.State.ReadMesh(MeshAAddress).ToArray();
        changed[0x540] = 64;
        frame = frame with { State = frame.State.WriteMesh(MeshAAddress, changed.ToImmutableArray()) };
        NativeBattleBadBreath131Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBadBreath131Frame>(NativeBattleTestSnapshots.Json(frame))!;
        var expected = Draw(frame);
        var actual = Draw(restored);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Frame), NativeBattleTestSnapshots.Json(actual.Frame));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Batch), NativeBattleTestSnapshots.Json(actual.Batch));
        Assert.Equal(new byte[] { 16, 32, 48 }, actual.Batch.Attempts[0].Data.AsSpan().Slice(4, 3).ToArray());
        Assert.Equal((byte)64, actual.Frame.State.ReadMesh(MeshAAddress)[0x540]);
    }

    private static (NativeBattleBadBreath131Frame Frame, NativeBattleGfGeometryBatch Batch) Draw(NativeBattleBadBreath131Frame frame) => NativeBattleBadBreath131StripDrawing.Draw(frame, NativeBattleTemporaryMemory.Address, MeshAAddress, Vertices, new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270);

    private static NativeBattleBadBreath131Frame Frame(int depth = 2000)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = depth }, true, true, unchecked((short)0xBEEF));
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x32000);
        short[] coordinates = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int index = 0; index < 4; index++)
        {
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, coordinates[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), coordinates[index * 2 + 1]);
            arena = arena.Write(Scratch + index * 8, xyz);
        }
        memory = memory with { Arena = arena, GeometryRegisters = geometry, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Material = 0x7F123456 }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), 92);
        NativeBattleBadBreath131State state = NativeBattleBadBreath131State.FromColdProcess().WriteDword(WorkingGlobal, Working) with { MeshA = Source(), PendingColdSources = false };
        return new(memory, new(131, 131, new(131, Entry, Initializer), null, null), new(1), state)
        {
            Staging = NativeBattleTemporaryMemory.PublishView(allocated.Staging, allocated.Address, Context(), 92)
        };
    }

    private static NativeBattleEffectArena Context()
    {
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 92).Write(0, BitConverter.GetBytes(MeshAAddress)).Write(4, new byte[] { 32, 64, 96 });
        context = context.Write(0x0C, new byte[] { 10, 0 }).Write(0x20, BitConverter.GetBytes(Vertices));
        context = context.Write(0x10, new byte[] { 0, 0, 0, 0, 0, 1, 0, 1 });
        return context.Write(0x18, new byte[] { 0, 0, 0, 0, 128, 0, 0, 1 });
    }

    private static ImmutableArray<byte> Source()
    {
        byte[] bytes = new byte[0x1480];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x530);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 165);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x53C), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x564), 1);
        foreach (int at in new[] { 0x540, 0x568 })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), at == 0x540 ? 0x2E000080u : 0x3E000080u);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at + 4 + corner * 2), (ushort)(corner * 2));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 12), 0x123401FA);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 16), 0x567802F0);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at + 20), 0x04FA03FF);
            if (at == 0x568)
            {
                bytes[at + 24] = 64;
                bytes[at + 28] = 255;
            }
        }
        return bytes.ToImmutableArray();
    }

    private static uint U32(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
}
