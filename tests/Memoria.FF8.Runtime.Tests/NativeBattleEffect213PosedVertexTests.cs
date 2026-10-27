using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect213PosedVertexTests
{
    [Fact]
    public void Selected_one_past_vertex_publishes_current_MAC_IR_and_retains_both_padding_words()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectArena temporary = Temporary(0).Write(0x2E, BitConverter.GetBytes((ushort)0xA55A)).Write(0x36, BitConverter.GetBytes((ushort)0xB66B));
        NativeBattleActionMatrix world = NativeBattleCuragaMatrices.Identity8DD770() with { X = 40000, Y = -40000 };
        NativeBattleEffect213PosedVertexStep result = NativeBattleEffect213PosedVertex.Select6CE880(memory, temporary, [Mesh()], [world]);
        Assert.True(result.Found);
        Assert.Equal(2, Dword(result.Temporary56, 0x24));
        Assert.Equal(new[] { short.MaxValue, short.MinValue, (short)90 }, Enumerable.Range(0, 3).Select(axis => Word(result.Temporary56, 0x28 + axis * 2)));
        Assert.Equal(unchecked((short)0xA55A), Word(result.Temporary56, 0x2E));
        Assert.Equal(unchecked((short)0xB66B), Word(result.Temporary56, 0x36));
        Assert.Equal(new int?[] { 40070, -39920, 90 }, result.Memory.ScriptedColorRegisters!.Mac);
        Assert.NotEqual(0u, result.Memory.ScriptedColorRegisters.Flags);
        Assert.Equal((byte)0x6B, result.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
        Assert.Equal((byte)0xB6, result.Memory.ScriptedColorRegisters.NormalInput!.Value[7]);
        Assert.Null(result.Memory.GeometryRegisters!.Rotation20[18]);
        NativeBattleEffect213PosedVertexStep restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect213PosedVertexStep>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(result.Temporary56.Read(0x24, 20), restored.Temporary56.Read(0x24, 20));
    }

    [Fact]
    public void Unknown_local_W_survives_packed_load_without_consuming_or_fabricating_output_W()
    {
        NativeBattleEffectArena temporary = Temporary(2).MarkUnknown(0x2E, 2).MarkUnknown(0x36, 2);
        NativeBattleEffect213PosedVertexStep result = NativeBattleEffect213PosedVertex.Select6CE880(Memory(), temporary, [Mesh()], [NativeBattleCuragaMatrices.Identity8DD770()]);
        Assert.True(result.Found);
        Assert.DoesNotContain(true, result.Temporary56.ReadSnapshot(0x2E, 2).Known);
        Assert.DoesNotContain(true, result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value.AsSpan().Slice(6, 2).ToArray());
        Assert.Equal(10, Word(result.Temporary56, 0x28));
    }

    [Fact]
    public void Failed_primary_walk_keeps_actual_remaining_ordinal_and_all_output_registers_for_linked_selection()
    {
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffectArena temporary = Temporary(3).Write(0x28, Enumerable.Repeat((byte)0xA5, 16).ToArray());
        NativeBattleEffect213PosedVertexStep primary = NativeBattleEffect213PosedVertex.Select6CE880(memory, temporary, [Mesh()], [NativeBattleCuragaMatrices.Identity8DD770()]);
        Assert.False(primary.Found);
        Assert.Equal(1, Dword(primary.Temporary56, 0x24));
        Assert.Same(memory, primary.Memory);
        Assert.Equal(temporary.Read(0x28, 16), primary.Temporary56.Read(0x28, 16));
        NativeBattleEffect213PosedVertexStep linked = NativeBattleEffect213PosedVertex.Select6CE880(primary.Memory, primary.Temporary56, [Mesh()], [NativeBattleCuragaMatrices.Identity8DD770() with { Z = 100 }]);
        Assert.True(linked.Found);
        Assert.Equal(1, Dword(linked.Temporary56, 0x24));
        Assert.Equal(160, Word(linked.Temporary56, 0x2C));
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
    private static NativeBattleEffectArena Temporary(int ordinal) => NativeBattleEffectArena.Clear().MarkUnknown(0, 56).Write(0x20, BitConverter.GetBytes(1u)).Write(0x24, BitConverter.GetBytes(ordinal));
    private static NativeBattleRawMesh Mesh()
    {
        byte[] vertices = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(vertices.AsSpan(index * 2), (short)((index + 1) * 10));
        }
        return new([], []) { PackedVertexGroups = [new(0, 2, vertices.ToImmutableArray())] };
    }
    private static int Dword(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.Read(offset, 4));
    private static short Word(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.Read(offset, 2));
}
