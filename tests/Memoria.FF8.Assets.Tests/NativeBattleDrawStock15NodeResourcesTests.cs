using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDrawStock15NodeResourcesTests
{
    private const uint Address = 0x17FC68C;

    [Fact]
    public void Original_node_bank_retains_all_four_directories_and_fixes_every_channel_including_zero()
    {
        NativeBattleCuragaExecutableSource source = ReadSource();
        Assert.Equal(Address, source.ResourceAddress);
        Assert.Equal(0x5944, source.Data.Length);
        Assert.Equal(0u, Word(source.Data.AsSpan(), 0));
        NativeBattleCuragaExecutableState fixedBank = NativeBattleCuragaExecutableState.FromColdProcess(15).Initialize87EA20(source);
        Assert.Equal(1u, fixedBank.InitializationFlag);
        int[] counts = new int[4];
        for (int table = 0; table < 4; table++)
        {
            for (int index = 0; index < 16; index++)
            {
                int offset = 4 + table * 64 + index * 4;
                uint relative = Word(source.Data.AsSpan(), offset);
                uint actual = Word(fixedBank.Read(Address + (uint)offset, 4), 0);
                Assert.Equal(relative == 0 ? 0 : Address + relative, actual);
                if (relative == 0)
                {
                    continue;
                }
                counts[table]++;
                if (table != 0)
                {
                    Assert.InRange(relative, 0x104u, 0x5943u);
                    continue;
                }
                Assert.InRange(relative, 0x4E84u, 0x57ECu);
                for (int field = 0xC8; field <= 0x144; field += 4)
                {
                    uint channel = Word(source.Data.AsSpan(), checked((int)relative + field));
                    Assert.Equal(Address + channel, Word(fixedBank.Read(actual + (uint)field, 4), 0));
                    Assert.InRange(channel, 0u, 0x5944u - 80);
                    Assert.Equal(80, fixedBank.Read(Address + channel, 80).Length);
                }
            }
        }
        Assert.Equal(new[] { 8, 2, 5, 0 }, counts);
        Assert.Same(fixedBank, fixedBank.Initialize87EA20(source));
        NativeBattleCuragaExecutableState restored = JsonSerializer.Deserialize<NativeBattleCuragaExecutableState>(JsonSerializer.Serialize(fixedBank))!;
        Assert.Equal(fixedBank.Read(Address, 0x5944), restored.Initialize87EA20(source).Read(Address, 0x5944));
    }

    [Fact]
    public void Original_generic_birth_uses_its_own_source_and_smaller_arena_storage_with_repeatable_next_step()
    {
        NativeBattleCuragaExecutableState executable = NativeBattleCuragaExecutableState.FromColdProcess(15).Initialize87EA20(ReadSource());
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x273AA38, 0x273A248, 676, 3)]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, 0x8B0DA0, 676);
        NativeBattleModulePoolTask task = appended.Hierarchy.Read(appended.Address)
            .WriteDword(0x258, Address + 4)
            .WriteDword(0x25C, Address + 0x44)
            .WriteDword(0x260, Address + 0x84)
            .WriteDword(0x264, Address + 0xC4)
            .WriteWord(0x50, 1);
        hierarchy = appended.Hierarchy.Write(appended.Address, task);
        NativeBattleOriginalEffectState effect = new(15, 14, new(15, 0x8B0910, 0x8B08F0), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        const int emitters = 0x12000;
        const int geometry = emitters + 0x438;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), DrawStock15Executable = executable };
        NativeBattleCuragaState state = new(0, null, null, geometry + 0x6900, NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 }) { Auxiliary25F8 = emitters, Auxiliary16F80 = geometry, Counter26D76C0 = 0, Counter26D82E8 = 0 };
        NativeBattleCuragaFrame frame = new(memory, effect, state);
        NativeBattleCuragaNodeCycleStep first = NativeBattleCuragaNodeKinematics.AdvanceList8804D0(frame, appended.Address, new(1), ReadModel, true);
        NativeBattleModulePoolTask current = new NativeBattleEffectHierarchy(first.Frame.Effect.Pools).Read(appended.Address);
        Assert.InRange(current.ReadWord(0x48), (ushort)1, (ushort)10);
        Assert.InRange(current.ReadWord(0x4A), (ushort)1, (ushort)40);
        Assert.Equal(Address, first.Frame.Memory.DrawStock15Executable!.ResourceAddress);
        Assert.Same(memory.CuragaExecutable, first.Frame.Memory.CuragaExecutable);
        Assert.Same(memory.Potion52Executable, first.Frame.Memory.Potion52Executable);
        NativeBattleCuragaFrame restored = JsonSerializer.Deserialize<NativeBattleCuragaFrame>(JsonSerializer.Serialize(first.Frame))!;
        NativeBattleCuragaNodeCycleStep expected = NativeBattleCuragaNodeKinematics.AdvanceList8804D0(first.Frame, appended.Address, first.Random, ReadModel, false);
        NativeBattleCuragaNodeCycleStep actual = NativeBattleCuragaNodeKinematics.AdvanceList8804D0(restored, appended.Address, first.Random, ReadModel, false);
        Assert.Equal(expected.Random, actual.Random);
        Assert.Equal(JsonSerializer.Serialize(expected.Frame), JsonSerializer.Serialize(actual.Frame));
    }

    private static NativeBattleGraphicsModel ReadModel(int index) => new(index, 2)
    {
        Rotation14 = 0,
        Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096),
        GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770()
    };

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset));

    private static NativeBattleCuragaExecutableSource ReadSource()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the Draw Stock node contract.");
        return NativeBattleDrawStock15NodeResourcesReader.Read(File.ReadAllBytes(Path.Combine(directory!, "FF8_EN.exe")));
    }
}
