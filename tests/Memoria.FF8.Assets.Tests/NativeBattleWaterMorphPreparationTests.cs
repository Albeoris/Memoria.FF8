using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWaterMorphPreparationTests
{
    [Fact]
    public void Original_shape4_and_four_current_morph_templates_complete_the_closed_loop_with_unknown_W_and_JSON_next()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources are optional.");
        using GameAssets game = new(directory);
        NativeBattleWater222Resources resources = game.ReadNativeWater222Resources();
        NativeBattleEffectHeapAllocation map = game.ReadNativeWaterMeshResourceMap();
        NativeBattleWaterMeshShape selected = NativeBattleWaterMeshes.ReadShape(resources.Files[0].Data, 4);
        Assert.Equal(170, selected.VertexCount);
        Assert.Equal(0x2600, selected.FileOffset);
        Assert.Equal(16, map.Data.Length);
        const int file0 = 0x1000;
        const int file1 = 0x20000;
        const int work = 0x60000;
        const uint arenaBase = 0x20DFAB8;
        const uint manager = 0x2797690;
        NativeBattleWaterSlotBank bank = new(file1, resources.Files[1].Data.Length, 8, file1 + 0x58);
        Assert.Equal(0x58u, BinaryPrimitives.ReadUInt32LittleEndian(resources.Files[1].Data.AsSpan().Slice(0x1C)));
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(file0, resources.Files[0].Data.AsSpan()).Write(file1, resources.Files[1].Data.AsSpan());
        arena = NativeBattleWaterSlots.InitializeB65160(arena, bank, 0, 1, 1, 0);
        arena = NativeBattleWaterSlots.InitializeB65160(arena, bank, 1, 1, 1, 0);
        arena = Word(arena, bank.SlotBaseOffset + 0x12, 7);
        arena = arena.Write(bank.SlotBaseOffset + 0x1B, [0]);
        arena = Word(arena, bank.SlotBaseOffset + 0x100 + 0x12, 5021);
        for (int lane = 0; lane < 3; lane++)
        {
            arena = Word(arena, bank.SlotBaseOffset + 0x100 + 0x94 + lane * 2, 256);
        }
        arena = arena.Write(work, Enumerable.Repeat((byte)0xA5, 170 * 8).ToArray()).Write(work + 170 * 8, [0xEF, 0xBE, 0xAD, 0xDE]);
        int[] shapeOffsets = [0x5048, 0x55C8, 0x5B48, 0x60C8];
        foreach (int offset in shapeOffsets)
        {
            Assert.Equal(2u, Dword(arena, file0 + offset));
            Assert.Equal(170u, Dword(arena, file0 + offset + 0x18));
            Assert.Equal(0x30u, Dword(arena, file0 + offset + 0x14));
            for (int vertex = 0; vertex < 170; vertex++)
            {
                arena = arena.MarkUnknown(file0 + offset + 0x30 + vertex * 8 + 6, 2);
            }
        }
        for (int vertex = 0; vertex < 170; vertex++)
        {
            arena = arena.MarkUnknown(work + vertex * 8 + 6, 2);
        }
        NativeBattleScriptedEffectMemory core = NativeBattleScriptedEffectMemory.FromColdProcess().Write(0x2797454, Enumerable.Repeat((byte)0xFF, 128).ToArray()).Write(0x2797454, [0, 1]);
        core = CoreDword(core, 0x27973EC, manager);
        core = CoreDword(core, 0x27973E8, arenaBase + (uint)bank.SlotBaseOffset);
        core = CoreDword(core, 0x2798A68, arenaBase + file0);
        core = CoreDword(core, 0x2798A6C, arenaBase + file1);
        core = CoreDword(core, manager + 0xAC, 0x186C8CC);
        core = CoreDword(core, manager + 0x74, arenaBase + work);
        NativeBattleWaterExecutionFrame frame = new(core, arena, new(0x700000, new byte[0x180].ToImmutableArray(), new bool[0x180].ToImmutableArray()), new(1));
        int selectedVertices = checked((int)Dword(arena, file0 + selected.FileOffset + 0x14));
        frame = frame.WriteTemporaryDword(0x7C, arenaBase + work).WriteTemporaryDword(0x74, arenaBase + (uint)(file0 + selected.FileOffset + selectedVertices));
        int[] tuples = [0x420, 0x42A, 0x434, 0x43E];
        int[] starts = [5, 6, 7, 8];
        int[] ends = [6, 7, 8, 5];
        for (int step = 0; step < tuples.Length; step++)
        {
            int instruction = file0 + tuples[step];
            Assert.Equal((ushort)0x003E, U16(frame.Arena, instruction));
            int tuple = instruction + 2;
            Assert.Equal((ushort)5021, U16(frame.Arena, tuple));
            Assert.Equal((ushort)starts[step], U16(frame.Arena, tuple + 2));
            Assert.Equal((ushort)ends[step], U16(frame.Arena, tuple + 4));
            frame = frame.WriteTemporaryDword(0x4C, arenaBase + (uint)tuple);
            NativeBattleWaterExecutionFrame cold = NativeBattleSnapshotJson.Deserialize<NativeBattleWaterExecutionFrame>(NativeBattleSnapshotJson.Serialize(frame))!;
            NativeBattleWaterExecutionFrame actual = NativeBattleWaterMeshPreparation.MorphSelected(frame, bank, 0, resources, map, selected.VertexCount);
            NativeBattleWaterExecutionFrame restored = NativeBattleWaterMeshPreparation.MorphSelected(cold, bank, 0, resources, map, selected.VertexCount);
            Assert.Equal(NativeBattleSnapshotJson.Serialize(actual), NativeBattleSnapshotJson.Serialize(restored));
            Assert.Equal(arenaBase + (uint)(file0 + shapeOffsets[starts[step] - 5] + 0x30), actual.ReadTemporaryDword(0xF0));
            Assert.Equal(arenaBase + (uint)(file0 + shapeOffsets[ends[step] - 5] + 0x30), actual.ReadTemporaryDword(0xF4));
            Assert.Equal(170u, actual.ReadTemporaryDword(0xF8));
            for (int vertex = 0; vertex < 170; vertex++)
            {
                int target = file0 + shapeOffsets[ends[step] - 5] + 0x30 + vertex * 8;
                Assert.Equal(actual.Arena.Read(target, 6), actual.Arena.Read(work + vertex * 8, 6));
                Assert.Throws<NotSupportedException>(() => actual.Arena.Read(work + vertex * 8 + 6, 2));
            }
            Assert.Equal(0xDEADBEEFu, Dword(actual.Arena, work + 170 * 8));
            frame = actual;
        }
        Assert.Equal(resources.Files[0].Data.AsSpan().Slice(0x5048 + 0x30, 6).ToArray(), frame.Arena.Read(work, 6));
        int firstSource = file0 + 0x5048 + 0x30;
        short changedX = unchecked((short)(Signed(frame.Arena, firstSource) + 123));
        NativeBattleWaterExecutionFrame changed = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        changed = changed with { Arena = Word(changed.Arena, firstSource, unchecked((ushort)changedX)) };
        changed = changed.WriteTemporaryDword(0x4C, arenaBase + (uint)(file0 + tuples[3] + 2));
        NativeBattleWaterExecutionFrame observed = NativeBattleWaterMeshPreparation.MorphSelected(changed, bank, 0, resources, map, selected.VertexCount);
        Assert.Equal(changedX, Signed(observed.Arena, work));
        Assert.NotEqual(Signed(frame.Arena, work), Signed(observed.Arena, work));
        Assert.Equal(resources.Files[0].Data.AsSpan().Slice(0x5048 + 0x30, 2).ToArray(), frame.Arena.Read(firstSource, 2));
    }

    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort U16(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short Signed(NativeBattleEffectArena arena, int offset) => unchecked((short)U16(arena, offset));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleScriptedEffectMemory CoreDword(NativeBattleScriptedEffectMemory core, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return core.Write(address, bytes);
    }
}
