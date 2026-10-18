using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterInitializationTests
{
    private const int File0 = 0x1000;
    private const int File1 = 0x2000;
    private const uint Manager = 0x2797690;
    private const uint Command = 0x2797628;

    [Fact]
    public void InitializationUsesCurrentDirectoriesAndPreservesSevenPayloadsAndWarmCoreFields()
    {
        var fixture = Fixture();
        NativeBattleScriptedEffectMemory warm = new NativeBattleScriptedEffectMemory(NativeBattleEffectArena.Clear()).Write(NativeBattleScriptedEffectMemory.Address, Enumerable.Repeat((byte)0xA5, NativeBattleScriptedEffectMemory.Length).ToArray());
        NativeBattleWaterInitializationResult result = NativeBattleWaterInitialization.InitializeA9B200(warm, fixture.Arena, fixture.Profile, fixture.Inputs);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + File0 + 0x70u, Dword(result.Core, Manager + 0x94));
        Assert.Equal(fixture.Inputs.InitializationDirectoryAddress + 0x123u, Dword(result.Core, Manager + 0x98));
        Assert.Equal(fixture.Inputs.InitializationDirectoryAddress + 0x369u, Dword(result.Core, Manager + 0xB0));
        Assert.Equal(0xA5A5A5A5u, Dword(result.Core, Manager + 0x78));
        Assert.Equal(0xA5A5A5A5u, Dword(result.Core, Manager + 0x80));
        Assert.Equal(0xA5A5A5A5u, Dword(result.Core, 0x27975E8));
        Assert.Equal(0xA5A5A5A5u, Dword(result.Core, 0x27975EC));
        Assert.Equal((byte)0xA5, result.Core.Read(0x27974D4, 1)[0]);
        Assert.Equal((ushort)0, Word(result.Core, Manager));
        Assert.Equal((ushort)0x0200, Word(result.Core, Manager + 2));
        Assert.Equal((ushort)0x0233, Word(result.Core, Manager + 0xA0));
        Assert.Equal(fixture.Inputs.FirstHeaderCount, result.Core.Read(Manager + 0xD1, 1)[0]);
        Assert.Equal((byte)0, result.Core.Read(Manager + 0xD0, 1)[0]);
        Assert.Equal(fixture.Inputs.HeaderBaseAddress, Dword(result.Core, Manager + 0xCC));
        Assert.Equal(fixture.Inputs.CurrentHeaderIndex, result.CurrentHeaderIndex);
        Assert.Equal((ushort)1, Word(result.Core, Command));
        Assert.Equal(0x80000000u, Dword(result.Core, Command + 8));
        Assert.Equal(0x04000080u, Dword(result.Core, Command + 0xC));
        Assert.Equal(new byte[] { 0xFF, 0 }, result.Core.Read(0x2798218, 2));
        Assert.Equal((ushort)0x3814, Word(result.Core, NativeBattleScriptedEffectMemory.Address + 0x1C));
        Assert.Equal((ushort)8, Word(result.Core, NativeBattleScriptedEffectMemory.Address + 0x38));
        Assert.Equal((ushort)0x8000, Word(result.Core, NativeBattleScriptedEffectMemory.Address));
        Assert.Equal((byte)0, result.Core.Read(0x2797454, 1)[0]);
        Assert.All(result.Core.Read(0x2797455, 127), value => Assert.Equal(0xFF, value));
        Assert.All(result.Core.Read(0x2797554, 128), value => Assert.Equal(0xFF, value));
        Assert.All(result.Core.Read(0x2797204, 128), value => Assert.Equal(0, value));
        for (int index = 1; index < 8; index++)
        {
            int slot = File1 + 0x58 + index * 0x100;
            Assert.Equal(new byte[4], result.Arena.Read(slot, 4));
            Assert.All(result.Arena.Read(slot + 4, 0xFC), value => Assert.Equal(0xA5, value));
        }
        int first = File1 + 0x58;
        Assert.Equal(Dword(result.Core, Manager + 0x94), BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(first, 4)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(first + 0x12, 2)));
        Assert.Equal((byte)4, result.Arena.Read(first + 0x1B, 1)[0]);
        Assert.Equal((ushort)0x80, BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(first + 0xC8, 2)));
        Assert.Equal((ushort)0x400, BinaryPrimitives.ReadUInt16LittleEndian(result.Arena.Read(first + 0xCA, 2)));
        Assert.Equal(File1 + 0x58, result.SlotBank.SlotBaseOffset);
        Assert.Equal(new NativeBattleWaterInitialPoolRequest(0x2796BA8, 0x2796BB8, 0x10, 1, 0xA9B870), result.PoolRequest);
    }

    [Fact]
    public void MatrixCopiesPreserveUnknownPaddingAndTemporaryFrameWritesAreExact()
    {
        var fixture = Fixture();
        NativeBattleScriptedEffectMemory unknown = NativeBattleScriptedEffectMemory.Unknown();
        NativeBattleWaterInitializationResult result = NativeBattleWaterInitialization.InitializeA9B200(unknown, fixture.Arena, fixture.Profile, fixture.Inputs);
        uint[] matrices = [0x2797968, 0x2797988, 0x27979A8, 0x27979C8];
        ushort[][] expected = [[4096, 0, 0, 0, 4096, 0, 0, 0, 4096], [0xF000, 0, 0, 0, 4096, 0, 0, 0, 4096], [4096, 0, 0, 0, 0xF000, 0, 0, 0, 4096], [0xF000, 0, 0, 0, 0xF000, 0, 0, 0, 4096]];
        for (int i = 0; i < matrices.Length; i++)
        {
            for (int element = 0; element < 9; element++)
            {
                Assert.Equal(expected[i][element], Word(result.Core, matrices[i] + (uint)(2 * element)));
            }
            uint matrix = matrices[i];
            Assert.Throws<NotSupportedException>(() => result.Core.Read(matrix + 0x12, 2));
            Assert.Equal(new byte[12], result.Core.Read(matrix + 0x14, 12));
        }
        Assert.Throws<NotSupportedException>(() => result.Core.Read(Manager + 0x78, 4));
        Assert.Throws<NotSupportedException>(() => result.Core.Read(0x27975E8, 4));
        Assert.Equal(new byte[32], result.Core.Read(0x2798BF8, 32));
        Assert.Equal(new byte[32], result.Core.Read(0x27971E4, 32));
        byte[] frame = result.TemporaryFrame.Bytes.ToArray();
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, frame.AsSpan(0, 4).ToArray());
        Assert.All(result.TemporaryFrame.Known.Take(4), value => Assert.False(value));
        Assert.Equal(fixture.Inputs.TemporaryFrame.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(0x100)));
        Assert.Equal(fixture.Inputs.TemporaryFrame.Address + 0x100, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(0x104)));
        Assert.Equal(fixture.Profile.OwnerAddress(0), BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(0x108)));
        Assert.Equal(new byte[8], frame.AsSpan(0xF0, 8).ToArray());
        Assert.Equal((byte)0xA5, frame[0x144]);
        Assert.False(result.TemporaryFrame.Known[0x144]);
        Assert.Equal((byte)0, frame[0x145]);
        Assert.True(result.TemporaryFrame.Known[0x145]);
        Assert.False(result.TemporaryFrame.Known[0x120]);
        Assert.All(result.TemporaryFrame.Known.Skip(0x100).Take(4), value => Assert.True(value));
        Assert.Equal(fixture.Inputs.TemporaryFrame.Address, Dword(result.Core, 0x2797624));
        Assert.Equal(fixture.Inputs.TemporaryFrame.Address + 0x100, Dword(result.Core, 0x27973B8));
        NativeBattleWaterTemporaryFrame warm = fixture.Inputs.TemporaryFrame with { Known = Enumerable.Repeat(true, 0x180).ToImmutableArray() };
        NativeBattleWaterInitializationResult warmResult = NativeBattleWaterInitialization.InitializeA9B200(unknown, fixture.Arena, fixture.Profile, fixture.Inputs with { TemporaryFrame = warm });
        Assert.Equal(warm.Bytes.AsSpan(0, 4).ToArray(), warmResult.TemporaryFrame.Bytes.AsSpan(0, 4).ToArray());
        Assert.All(warmResult.TemporaryFrame.Known.Take(4), value => Assert.True(value));
    }

    [Fact]
    public void WarmResetReadsMutatedArenaHeaderAndJsonPreservesAllOwners()
    {
        var fixture = Fixture();
        NativeBattleWaterInitializationResult first = NativeBattleWaterInitialization.InitializeA9B200(NativeBattleScriptedEffectMemory.FromColdProcess(), fixture.Arena, fixture.Profile, fixture.Inputs);
        first = first with { Arena = PutDword(first.Arena, File0 + 4, 0x99) };
        NativeBattleWaterInitializationResult restored = JsonSerializer.Deserialize<NativeBattleWaterInitializationResult>(JsonSerializer.Serialize(first))!;
        NativeBattleWaterInitializationResult a = NativeBattleWaterInitialization.InitializeA9B200(first.Core, first.Arena, fixture.Profile, fixture.Inputs with { TemporaryFrame = first.TemporaryFrame });
        NativeBattleWaterInitializationResult b = NativeBattleWaterInitialization.InitializeA9B200(restored.Core, restored.Arena, fixture.Profile, fixture.Inputs with { TemporaryFrame = restored.TemporaryFrame });
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + File0 + 0x99u, Dword(b.Core, Manager + 0x94));
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.Equal((byte)0xA5, b.Arena.Read(File1 + 0x158 + 0x40, 1)[0]);
    }

    [Fact]
    public void MissingConsumedOwnerFailsAndUnconsumedUnknownResourcePayloadIsPreserved()
    {
        var fixture = Fixture();
        NativeBattleEffectArena arena = fixture.Arena.MarkUnknown(File1 + 0x158 + 4, 0xFC);
        NativeBattleWaterInitializationResult result = NativeBattleWaterInitialization.InitializeA9B200(NativeBattleScriptedEffectMemory.FromColdProcess(), arena, fixture.Profile, fixture.Inputs);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(File1 + 0x158 + 4, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterInitialization.InitializeA9B200(NativeBattleScriptedEffectMemory.FromColdProcess(), fixture.Arena.MarkUnknown(File0 + 4, 4), fixture.Profile, fixture.Inputs));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterInitialization.InitializeA9B200(NativeBattleScriptedEffectMemory.FromColdProcess(), PutDword(fixture.Arena, File1 + 0x1C, 0x800), fixture.Profile, fixture.Inputs));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterInitialization.InitializeA9B200(NativeBattleScriptedEffectMemory.FromColdProcess(), fixture.Arena, fixture.Profile, fixture.Inputs with { TemporaryFrame = fixture.Inputs.TemporaryFrame with { Bytes = [] } }));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterInitialization.InitializeA9B200(NativeBattleScriptedEffectMemory.FromColdProcess(), fixture.Arena, fixture.Profile, fixture.Inputs with { Resource0 = fixture.Inputs.Resource0 with { Address = 0x80000000 } }));
    }

    private static (NativeBattleEffectArena Arena, NativeBattleWater222Profile Profile, NativeBattleWaterInitializationInputs Inputs) Fixture()
    {
        NativeBattleEffectArena arena = PutDword(PutDword(PutDword(PutDword(NativeBattleEffectArena.Clear(), File0 + 4, 0x70), File0 + 0x10, 0x80), File0 + 0x18, 0x90), File1 + 0x1C, 0x58);
        arena = arena.Write(File1 + 0x58, Enumerable.Repeat((byte)0xA5, 0x800).ToArray());
        byte[] directory = new byte[0x1C];
        for (int i = 0; i < 7; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(4 * i), (uint)(0x123 + i * 0x61));
        }
        byte[] owners = new byte[20];
        uint[] addresses = [Manager, 0x803FF000, NativeBattleScriptedEffectMemory.Address, 0x27975D8, Command];
        for (int i = 0; i < addresses.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(owners.AsSpan(4 * i), addresses[i]);
        }
        NativeBattleWater222Profile profile = new(new("tables", new byte[0x70].ToImmutableArray()), new("directory", directory.ToImmutableArray()), new("owners", owners.ToImmutableArray()));
        ImmutableArray<NativeBattleGraphicsModel> graphics = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, (ushort)(0x100 + index)) { Transform = new(new((short)index, 0, 0), new((short)(10 + index), (short)(20 + index), (short)(30 + index)), (short)(40 + index), 50, 4096), Rotation12 = (short)(60 + index), Rotation14 = 70, Rotation16 = 80 }).ToImmutableArray();
        NativeBattleWaterTemporaryFrame frame = new(0x1D9A000, Enumerable.Repeat((byte)0xA5, 0x180).ToImmutableArray(), new bool[0x180].ToImmutableArray());
        NativeBattleWaterInitializationInputs inputs = new(new(File0, 0x100, NativeBattleEffectNodeMemory.ArenaAddress + File0), new(File1, 0x858, NativeBattleEffectNodeMemory.ArenaAddress + File1), frame, 0x1D99A78, 0x1D99B00, 0x186C84C, 1, 2, 6, [4, 4, 2], 3, 9, graphics);
        return (arena, profile, inputs);
    }

    private static uint Dword(NativeBattleScriptedEffectMemory core, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
    private static ushort Word(NativeBattleScriptedEffectMemory core, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(core.Read(address, 2));

    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
