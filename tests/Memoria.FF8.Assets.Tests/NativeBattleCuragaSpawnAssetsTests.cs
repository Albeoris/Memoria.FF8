using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCuragaSpawnAssetsTests
{
    [Theory]
    [InlineData(0, new byte[] { 5 })]
    [InlineData(12, new byte[] { 2, 3, 4, 11, 12, 13, 14, 15 })]
    [InlineData(30, new byte[] { 6 })]
    public void Actual_Curaga_executable_birth_scan_selects_literal_records_in_original_directory_order(ushort counter, byte[] expected)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleCuragaResources resources = game.ReadNativeCuragaResources();
        NativeBattleCuragaExecutableState executable = NativeBattleCuragaExecutableState.FromColdProcess().Initialize87EA20(resources.Executable!);
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x26D7EC0, 0x26D76D0, 676, 3)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 0x87E1A0, 676);
        NativeBattleModulePoolTask task = root.Hierarchy.Read(root.Address).WriteDword(0x34 + 0x224, NativeBattleCuragaExecutableState.Address + 4).WriteWord(0x34 + 0x18, counter).WriteWord(0x34 + 0x1C, 1);
        hierarchy = root.Hierarchy.Write(root.Address, task);
        NativeBattleOriginalEffectState effect = new(28, 27, new(28, 0x87DB10, 0x87DAF0), new(0, 1, 8, 0, 0, 0), null) { Pools = hierarchy.Pools };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(27) with { CuragaExecutable = executable };
        NativeBattleCuragaState state = new(0, null, null, 0xC000 + 0x25F8 + 0x16F80, null) { Auxiliary25F8 = 0xC000, Auxiliary16F80 = 0xC000 + 0x25F8, Counter26D76C0 = 0, Counter26D82E8 = 0 };
        NativeBattleCuragaFrame result = NativeBattleCuragaNodeEngine.Birth882E20(new(memory, effect, state), root.Address);
        byte[] actual = Enumerable.Range(0, expected.Length).Select(index => result.Memory.Arena!.Read(0xC000 + 108 * index + 0x6A, 1)[0]).ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal((ushort)expected.Length, new NativeBattleEffectHierarchy(result.Effect.Pools).Read(root.Address).ReadWord(0x34 + 0x14));
        if (counter == 0)
        {
            result = NativeBattleCuragaNodeKinematics.InitializeEmitter882990(result, root.Address, 0xC000, _ => throw new InvalidOperationException());
            Assert.Equal(new byte[] { 33, 0 }, result.Memory.Arena!.Read(0xC000 + 0x64, 2));
        }
    }
}
