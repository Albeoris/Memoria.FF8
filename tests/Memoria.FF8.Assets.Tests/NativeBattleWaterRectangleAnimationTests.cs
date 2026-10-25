using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWaterRectangleAnimationTests
{
    [Theory]
    [InlineData(1, 0x298)]
    [InlineData(12, 0x612)]
    public void Original_selected_sequences_use_actual_rectangles_and_independent_json_next(int sequence, int instructionOffset)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources are optional.");
        using GameAssets game = new(directory);
        NativeBattleWater222Resources resources = game.ReadNativeWater222Resources();
        NativeBattleEffectHeapAllocation map = game.ReadNativeWaterMeshResourceMap();
        const int file0 = 0x10000;
        const int file1 = 0x30000;
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear().Write(file0, resources.Files[0].Data.AsSpan()).Write(file1, resources.Files[1].Data.AsSpan()), new(0x700000, new byte[0x180].ToImmutableArray(), Enumerable.Repeat(true, 0x180).ToImmutableArray()), new(1));
        frame = CoreDword(frame, 0x27973EC, 0x2797690);
        frame = CoreDword(frame, 0x27973E8, Address(0x2000));
        frame = CoreDword(frame, 0x279773C, 0x186C8CC);
        frame = CoreDword(frame, 0x2798A68, Address(file0));
        frame = CoreDword(frame, 0x2798A6C, Address(file1));
        frame = CoreDword(frame, 0x279770C, 0x300000);
        frame = CoreDword(frame, 0x2797450, Address(file0 + instructionOffset));
        frame = frame with { Core = frame.Core.Write(0x2797554, Enumerable.Repeat((byte)0xFF, 128).ToArray()) };
        frame = frame.WriteTemporaryWord(0x14A, BinaryPrimitives.ReadUInt16LittleEndian(resources.Files[0].Data.AsSpan().Slice(instructionOffset))).WriteTemporaryByte(0x142, 0).WriteTemporaryByte(0x145, 0).WriteTemporaryDword(0x14C, 0x1D8E058);
        frame = SlotWord(frame, 0xCA, 0x400);
        frame = SlotWord(frame, 0x8C, 256);
        frame = SlotWord(frame, 0x8E, 256);
        frame = CoreMatrix(frame, 0x27979E8, 1024);
        frame = CoreMatrix(frame, 0x2797968, 0);
        NativeBattleWaterSlotBank bank = new(0x2000, 0x100, 1, 0x2000);
        frame = NativeBattleWaterRectangleCommands.Execute(frame, new(file0, resources.Files[0].Data.Length, Address(file0)), bank, 0, resources, map);
        Assert.Equal((ushort)sequence, BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(file0 + instructionOffset + 2, 2)));
        int[] rectangles = sequence == 1 ? [0x22E0] : [0x7258, 0x7284, 0x72B0, 0x72DC, 0x7308, 0x7334, 0x7360, 0x738C];
        uint first = SlotDword(frame, 0xD0);
        int callbacks = sequence == 1 ? 2 : 10;
        for (int i = 0; i < callbacks; i++)
        {
            NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
            (NativeBattleWaterExecutionFrame next, NativeBattleGfGeometryBatch? packets) = Advance(frame);
            (NativeBattleWaterExecutionFrame coldNext, NativeBattleGfGeometryBatch? coldPackets) = Advance(restored);
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(coldNext));
            Assert.Equal(JsonSerializer.Serialize(packets), JsonSerializer.Serialize(coldPackets));
            if (sequence == 1 && i == 1)
            {
                Assert.Null(packets);
                Assert.Equal((byte)0, next.Arena.Read(0x201C, 1)[0]);
                Assert.Equal((byte)0xFF, next.Core.Read(0x2797554, 1)[0]);
            }
            else
            {
                Assert.NotNull(packets);
                Assert.Equal(Address(file0 + rectangles[i % rectangles.Length]), next.ReadTemporaryDword(0x90));
                Assert.Equal(sequence == 1 ? 1 : 2, packets.Attempts.Length);
                Assert.All(packets.Attempts, attempt => Assert.NotNull(attempt.Primitive));
                Assert.Equal((byte)5, next.Arena.Read(0x201C, 1)[0]);
            }
            if (sequence == 12 && i == 7)
            {
                Assert.Equal(first, SlotDword(next, 0xD0));
                Assert.Equal(Address(file0 + 0x738C), SlotDword(next, 0xD8));
            }
            frame = next;
        }
    }

    private static (NativeBattleWaterExecutionFrame Frame, NativeBattleGfGeometryBatch? Packets) Advance(NativeBattleWaterExecutionFrame frame)
    {
        NativeBattleGfGeometryBatch? packets = null;
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        frame = NativeBattleWaterRectangleCommands.AdvanceDraw5(frame, (_, _) => 0x27979E8, (current, matrix) =>
        {
            NativeBattleWaterParticleRectangleResult result = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(current, registers, new(null, 256, 160, 116), 341, 0, 0, _ => 17);
            registers = result.Registers;
            packets = result.Packets;
            return result.Frame;
        }, _ => registers, (current, step) =>
        {
            registers = step;
            return current;
        });
        return (frame, packets);
    }
    private static uint Address(int offset) => 0x20DFAB8u + (uint)offset;
    private static uint SlotDword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(0x2000 + offset, 4));
    private static NativeBattleWaterExecutionFrame SlotWord(NativeBattleWaterExecutionFrame frame, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return frame with { Arena = frame.Arena.Write(0x2000 + offset, bytes) };
    }
    private static NativeBattleWaterExecutionFrame CoreDword(NativeBattleWaterExecutionFrame frame, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }
    private static NativeBattleWaterExecutionFrame CoreMatrix(NativeBattleWaterExecutionFrame frame, uint address, int z)
    {
        NativeBattleActionMatrix identity = NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0));
        byte[] bytes = new byte[32];
        for (int i = 0; i < 9; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2 * i), identity.Rotation[i]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), z);
        return frame with { Core = frame.Core.Write(address, bytes) };
    }
}
