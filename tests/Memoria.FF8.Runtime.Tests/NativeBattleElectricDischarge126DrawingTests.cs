using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectricDischarge126DrawingTests
{
    [Fact]
    public void Delivery_observes_physical_88_and_GTE_before_release_and_keeps_returned_frame()
    {
        NativeBattleElectricDischarge126Frame frame = Frame();
        int calls = 0;
        NativeBattleElectricDischarge126Frame Deliver(NativeBattleElectricDischarge126Frame current, NativeBattleGfGeometryBatch batch)
        {
            calls++;
            Assert.Equal(NativeBattleTemporaryMemory.Address + 88, U32(current.Staging!, NativeBattleTemporaryMemory.Cursor));
            Assert.Equal(0x1000224u + 16, U32(current.Staging!, NativeBattleTemporaryMemory.Address));
            Assert.Equal(2000, current.Memory.GeometryRegisters!.Translation[2]);
            Assert.Equal(52, batch.AllocationBytes);
            return current with { State = current.State.WriteDword(CursorGlobal, 0x12345678) };
        }
        NativeBattleElectricDischarge126Frame result = Draw(frame, Source(4), Node(0), Deliver);
        Assert.Equal(1, calls);
        Assert.Equal(0x12345678u, result.State.ReadDword(CursorGlobal));
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(result.Staging!, NativeBattleTemporaryMemory.Cursor));
        NativeBattleElectricDischarge126Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleElectricDischarge126Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Draw(result, Source(4), Node(0), (next, _) => next))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Draw(restored, Source(4), Node(0), (next, _) => next))));
    }

    [Fact]
    public void Interpolation_uses_actual_working_relative_408_bytes_and_preserves_W_and_packet_boundary()
    {
        NativeBattleElectricDischarge126Frame frame = Frame();
        int morph = 0x400 + MorphOffset;
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(morph, MorphBytes).Write(morph + MorphBytes, new byte[] { 1, 2, 3, 4 }) } };
        NativeBattleGfAnimationNode node = Node(0);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        NativeBattleElectricDischarge126Frame result = Draw(frame, Source(51), node with { Data = bytes.ToImmutableArray() }, (next, _) => next);
        Assert.Equal((short)50, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena!.Read(morph + 50 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena!.Read(morph + 50 * 8 + 6, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Memory.Arena!.Read(morph + MorphBytes, 4));
        Assert.Throws<InvalidDataException>(() => Draw(frame, Source(52), node, (next, _) => next));
    }

    [Fact]
    public void Invisible_node_does_not_allocate_or_require_unreached_staging_or_controls()
    {
        NativeBattleElectricDischarge126Frame frame = Frame() with { Staging = null };
        NativeBattleElectricDischarge126Frame result = Draw(frame, new(0, []), new(0, 0, new byte[44].ToImmutableArray()), (_, _) => throw new InvalidOperationException());
        Assert.Same(frame, result);
    }

    private static NativeBattleElectricDischarge126Frame Draw(NativeBattleElectricDischarge126Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, Func<NativeBattleElectricDischarge126Frame, NativeBattleGfGeometryBatch, NativeBattleElectricDischarge126Frame> deliver)
    {
        uint morph = frame.State.ReadDword(WorkingGlobal) + MorphOffset;
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, new byte[6]).Write(8, BitConverter.GetBytes(morph));
        return NativeBattleElectricDischarge126Drawing.EmitNode(frame, source, context, node, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, deliver);
    }

    private static NativeBattleElectricDischarge126Frame Frame()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) } };
        return new(memory, new(126, 126, new(126, Entry, Initializer), null, null), new(1), NativeBattleElectricDischarge126State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress + 0x400))
        {
            Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown())
        };
    }

    private static uint U32(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));

    private static NativeBattleGfAnimationNode Node(short x)
    {
        byte[] node = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(8), x);
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24 + axis * 2), 4096);
        }
        return new(0, 0, node.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(int count)
    {
        int faces = 12 + count * 16;
        byte[] bytes = new byte[16 + faces + 68];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), faces);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        short[] coordinates = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                int at = 28 + (shape * count + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), (short)((vertex < 4 ? coordinates[vertex * 2] : 0) + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), (short)(vertex < 4 ? coordinates[vertex * 2 + 1] : 0));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 0x1234);
            }
        }
        int group = 16 + faces + 28;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(group), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(group + 4), 0x3C332211);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(group + 8 + index * 2), (ushort)(index * 2));
        }
        return new(0x1000224, bytes.ToImmutableArray());
    }

}
