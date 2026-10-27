using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleIfritContinuationTests
{
    [Theory]
    [InlineData(512, -62)]
    [InlineData(2048, 52)]
    public void Scene_dependencies_derive_motion_from_source_operands_without_advancing_random_or_callbacks(ushort acceleration, int expectedCameraAngle)
    {
        NativeBattleWaterExecutionFrame input = Fixture(acceleration);
        NativeBattleWaterInitializationResource first = new(0x1000, 0x1000, NativeBattleEffectNodeMemory.ArenaAddress + 0x1000);
        NativeBattleWaterInitializationResource second = new(0x2000, 0x10000, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000);
        NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame) => NativeBattleIfritContinuation.InitializeSceneDependencies(frame, first, second, NativeBattleEffectNodeMemory.ArenaAddress + 0x70000);
        NativeBattleWaterExecutionFrame result = Run(input);
        Assert.Equal(expectedCameraAngle << 16, unchecked((int)WaterSchedulerFixture.ReadSlotDword(result, 4, 0x60)));
        Assert.Equal(1747 << 16, unchecked((int)WaterSchedulerFixture.ReadSlotDword(result, 5, 0x5C)));
        Assert.Equal(1910 << 16, unchecked((int)WaterSchedulerFixture.ReadSlotDword(result, 5, 0x60)));
        Assert.Equal(-12 << 16, unchecked((int)WaterSchedulerFixture.ReadSlotDword(result, 5, 0x78)));
        Assert.Equal(32 << 16, unchecked((int)WaterSchedulerFixture.ReadSlotDword(result, 7, 0x60)));
        Assert.Equal(48 << 16, unchecked((int)WaterSchedulerFixture.ReadSlotDword(result, 15, 0x60)));
        Assert.Equal((ushort)(7 * 128), WaterSchedulerFixture.ReadSlotWord(result, 4, 0xC));
        Assert.Equal((ushort)6, (ushort)(WaterSchedulerFixture.ReadSlotDword(result, 5, 0x18) >> 24));
        Assert.Equal(first.Address + 0x90Cu, WaterSchedulerFixture.ReadSlotDword(result, 19, 0));
        Assert.Equal(first.Address + 0x92Eu, WaterSchedulerFixture.ReadSlotDword(result, 19, 4));
        Assert.Equal((ushort)0x800E, WaterSchedulerFixture.ReadSlotWord(result, 19, 0x14));
        Assert.Equal(input.Random, result.Random);
        Assert.Equal(input.ReadTemporaryByte(0x145), result.ReadTemporaryByte(0x145));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Run(NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(input))!)));
    }

    [Theory]
    [InlineData(0x19B6, 0x0022)]
    [InlineData(0x19B6, 0x002B)]
    [InlineData(0x19B6, 0x1C12)]
    [InlineData(0x8F6, 0x0059)]
    [InlineData(0x8FA, 0x002B)]
    public void Unsupported_late_initialization_opcode_is_rejected_before_any_external_service(int sourceOffset, ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = InitializationFixture();
        frame = frame with { Arena = frame.Arena.Write(0x10000 + sourceOffset, BitConverter.GetBytes(instruction)) };
        int calls = 0;
        NativeBattleCinematicModelServices model = new((_, _) => throw new Xunit.Sdk.XunitException("Unexpected model preparation."), (_, _) => throw new Xunit.Sdk.XunitException("Unexpected animation read."));
        Assert.Contains("initialization instruction", Assert.Throws<InvalidDataException>(() => NativeBattleIfritContinuation.Initialize(frame, new(0x10000, 0x2000, NativeBattleEffectNodeMemory.ArenaAddress + 0x10000), new(0x2000, 0x10000, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000), NativeBattleEffectNodeMemory.ArenaAddress + 0x70000, model, (current, offset) =>
        {
            calls++;
            return current;
        })).Message);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Initialization_rejects_random_consumption_instead_of_rewinding_it()
    {
        NativeBattleWaterExecutionFrame frame = InitializationFixture();
        int calls = 0;
        NativeBattleCinematicModelServices model = new((_, _) => throw new Xunit.Sdk.XunitException("Unexpected model preparation."), (_, _) => throw new Xunit.Sdk.XunitException("Unexpected animation read."));
        Assert.Contains("random state", Assert.Throws<InvalidDataException>(() => NativeBattleIfritContinuation.Initialize(frame, new(0x10000, 0x2000, NativeBattleEffectNodeMemory.ArenaAddress + 0x10000), new(0x2000, 0x10000, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000), NativeBattleEffectNodeMemory.ArenaAddress + 0x70000, model, (current, offset) =>
        {
            calls++;
            return current with { Random = current.Random.Draw(out _) };
        })).Message);
        Assert.Equal(1, calls);
    }

    private static NativeBattleWaterExecutionFrame InitializationFixture()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(512);
        byte[] source = new byte[0x2000];
        frame.Arena.Read(0x1000, 0x1000).CopyTo(source, 0);
        foreach ((int offset, ushort instruction) in new (int, ushort)[]
        {
            (0x112, 0x006D), (0x114, 0x013D), (0x118, 0x0031), (0x3EC, 0x013D),
            (0x246, 0x0027), (0x24A, 0x0027), (0x24E, 0x0029), (0x252, 0x0029), (0x256, 0x0209), (0x258, 0x0027),
            (0x25C, 0x0028), (0x260, 0x0028), (0x264, 0x0028), (0x268, 0x0209), (0x26A, 0x0028), (0x26E, 0x0028),
            (0x272, 0x0028), (0x276, 0x0028), (0x27A, 0x0028), (0x27E, 0x0028), (0x282, 0x0209),
            (0x286, 0x8406), (0x288, 0x8029), (0x28E, 0x8606), (0x290, 0x0029), (0x298, 0x0231),
            (0x2A4, 0x8806), (0x2A6, 0x8029), (0x2AE, 0x8A06), (0x8F6, 0x0058), (0x8FA, 0x0052), (0x8FE, 0x0223), (0x900, 0x0068), (0x19B6, 0x0039)
        })
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(offset), instruction);
        }
        return frame with { Arena = frame.Arena.Write(0x10000, source) };
    }

    private static NativeBattleWaterExecutionFrame Fixture(ushort acceleration)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973C0, 0x2797628);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000);
        frame = WaterSchedulerFixture.CoreWord(frame, NativeBattleScriptedEffectMemory.Address + 0x38, 8);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797634, 128);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797636, 1024);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x18, 0x06000000);
        byte[] source = new byte[0x1000];
        Word(0xFE, 0x21);
        Word(0x106, 0x1021);
        Word(0x10E, 0xB1);
        Word(0x110, 4);
        Word(0x11C, 0x7D);
        Word(0x11E, 96);
        foreach (int offset in new[] { 0x138, 0x13A, 0x140, 0x322, 0x612, 0x8D2 })
        {
            Word(offset, 0x809);
        }
        Word(0x14A, 0x409);
        Word(0x14C, 0x409);
        Word(0x386, 0x3009);
        Word(0x3F2, 50);
        Word(0x3FA, 50);
        Word(0x3FE, unchecked((ushort)-100));
        Word(0x404, 0x409);
        Word(0x408, acceleration);
        Word(0x40A, 0x409);
        Word(0x416, 0x1809);
        Word(0x4A8, 51);
        Word(0x4AE, 50);
        Word(0x4B8, 80);
        Word(0x4C0, 1000);
        Word(0x4C2, 2000);
        Word(0x4C4, 3000);
        Word(0x4CC, 64);
        Word(0x4CE, 0xC09);
        Word(0x4D2, unchecked((ushort)-256));
        Word(0x4D4, unchecked((ushort)-512));
        Word(0x4E4, 0x1809);
        Word(0x59C, 10);
        Word(0x5A0, 1);
        Word(0x5A2, 2);
        Word(0x5A4, 3);
        Word(0x5F8, 11);
        Word(0x5FC, 10);
        Word(0x5FE, 20);
        Word(0x600, 30);
        Word(0x610, 3);
        Word(0x626, 4);
        Word(0x628, 5);
        Word(0x62A, 6);
        Word(0x7E2, 5500);
        Word(0x7F8, 90);
        Word(0x8C2, 30);
        Word(0x8C4, 40);
        Word(0x8C6, 50);
        Word(0x8D0, 2);
        return frame with { Arena = frame.Arena.Write(0x1000, source) };

        void Word(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(offset), value);
        }
    }
}
