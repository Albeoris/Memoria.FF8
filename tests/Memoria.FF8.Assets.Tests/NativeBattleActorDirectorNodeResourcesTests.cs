using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleActorDirectorNodeResourcesTests
{
    [Theory]
    [InlineData(16, 8)]
    [InlineData(61, 6)]
    public void Original_node_adapter_fixes_all_channels_with_independent_retained_bank(ushort effect, int expectedNodes)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to check generic node materialization.");
        using GameAssets game = new(directory!);
        NativeBattleActorDirectorResources original = game.ReadNativeActorDirectorResources(effect);
        NativeBattleCuragaResources resources = original.GenericNodes();
        Assert.Equal(effect, resources.Effect);
        Assert.Same(original.NodeSource, resources.Executable);
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(effect);
        NativeBattleCuragaExecutableState retained = NativeBattleCuragaExecutableState.FromColdProcess(effect).Initialize87EA20(resources.Executable!);
        Assert.Equal(1u, retained.InitializationFlag);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(original.NodeSource.Data.AsSpan()));
        int nodes = 0;
        for (int index = 0; index < 16; index++)
        {
            uint pointer = Read(profile.ExecutableAddress + 4 + (uint)index * 4);
            if (pointer == 0)
            {
                continue;
            }
            nodes++;
            for (int field = 0xC8; field <= 0x144; field += 4)
            {
                uint channel = Read(pointer + (uint)field);
                Assert.Equal(80, retained.Read(channel, 80).Length);
            }
        }
        Assert.Equal(expectedNodes, nodes);
        NativeBattleEffectMemory memory = profile.WithExecutable(Assert.IsType<NativeBattleEffectMemory>(game.ReadColdProcessNativeBattleMemory().EffectMemory), retained);
        Assert.Same(retained, profile.RequireExecutable(memory));
        Assert.Equal(0u, memory.CuragaExecutable!.InitializationFlag);
        Assert.Equal(0u, memory.DrawStock15Executable!.InitializationFlag);
        Assert.Equal(0u, memory.Potion52Executable!.InitializationFlag);

        uint Read(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(retained.Read(address, 4));
    }
}
