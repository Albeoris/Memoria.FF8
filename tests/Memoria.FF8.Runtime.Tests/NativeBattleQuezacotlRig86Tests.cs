using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlRig86Tests
{
    [Fact]
    public void RigWritesActualDirectoryPoseCursorSkeletonAndChildAliasesWithoutClearingOtherFields()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { Arena = memory.Arena!.Write(0x2A, [0xA5, 0x5A]) };
        NativeBattleQuezacotlRigWrite result = NativeBattleQuezacotlRig86.InitializeBeforeSecondWindow(memory, Effect());
        Assert.Equal(new byte[] { 0xA5, 0x5A }, result.Memory.Arena!.Read(0x2A, 2));
        Assert.Equal(0x20DFB7Cu, U32(0x8C));
        Assert.Equal(0x20DFB7Cu, U32(0x90));
        Assert.Equal(0x2111154u + 24, U32(0xC4));
        Assert.Equal(0x2111154u + 88, U32(0xC8));
        Assert.Equal(new byte[] { 0, 4, 3, 0, 3, 0, 1, 1 }, result.Memory.Arena.Read(0x94, 8));
        int skeleton = NativeBattleQuezacotlRig86.Resource + 24;
        Assert.Equal(new byte[6], result.Memory.Arena.Read(skeleton + 8, 6));
        Assert.Equal(new byte[6], result.Memory.Arena.Read(skeleton + 16 + 4, 6));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 6), result.Memory.Arena.Read(skeleton + 16 + 10, 6));
        Assert.Equal((short)256, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena.Read(skeleton + 32, 2)));
        Assert.Equal(result.Memory.Arena.Read(12, 0x8D0 - 12), result.Effect.Pools[0].Slots[0].Payload.ToArray());

        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(result.Memory.Arena.Read(offset, 4));
    }

    [Fact]
    public void AfterWindowClearsWholeNativeBanksButPreservesSentinelGapsAndCopiesActualCameraPadding()
    {
        NativeBattleEffectMemory memory = Memory();
        memory = memory with { Arena = memory.Arena!.Write(0x3BA5C, Enumerable.Repeat((byte)0xA5, 0x43A04 - 0x3BA5C).ToArray()) };
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = new(new(1, 2, 3), new(4, 5, 6), 288, 0), EyePaddingF6 = 7, TargetPaddingFE = 8 };
        NativeBattleQuezacotlRigWrite result = NativeBattleQuezacotlRig86.InitializeAfterSecondWindow(memory, Effect(), camera);
        Assert.Equal(4, result.Effect.Pools.Length);
        foreach (NativeBattleEffectTaskPool pool in result.Effect.Pools.Skip(1))
        {
            Assert.Null(pool.Head);
            Assert.All(result.Memory.Arena!.Read(checked((int)(pool.BankAddress - 0x20DFAB8)), pool.Stride * pool.Slots.Length), value => Assert.Equal((byte)0, value));
        }
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xFF, 0xFF, 0xA5, 0xA5 }, result.Memory.Arena!.Read(0x3CA00, 6));
        Assert.Equal(new byte[4], result.Memory.Arena.Read(0x3F1FC, 4));
        Assert.Equal(new byte[4], result.Memory.Arena.Read(0x43A00, 4));
        Assert.Equal(new byte[] { 4, 0, 5, 0, 6, 0, 8, 0, 1, 0, 2, 0, 3, 0, 7, 0 }, result.Memory.Arena.Read(0x18, 16));
        NativeBattleQuezacotlRigWrite restored = JsonSerializer.Deserialize<NativeBattleQuezacotlRigWrite>(JsonSerializer.Serialize(result))!;
        Assert.Equal(result.Memory.Arena.Read(0x18, 16), restored.Memory.Arena!.Read(0x18, 16));
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlRig86.InitializeAfterSecondWindow(memory, Effect(), camera with { EyePaddingF6 = null }));
    }

    private static NativeBattleEffectMemory Memory()
    {
        byte[] resource = new byte[104];
        int[] directory = [4, 24, 88, 92, 104];
        for (int index = 0; index < directory.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(index * 4), directory[index]);
        }
        resource[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(resource.AsSpan(26), 256);
        BinaryPrimitives.WriteInt16LittleEndian(resource.AsSpan(40), -1);
        resource.AsSpan(50, 6).Fill(0xA5);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(92), 1);
        BinaryPrimitives.WriteInt32LittleEndian(resource.AsSpan(96), 8);
        resource[100] = 1;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        return memory with { Arena = memory.Arena!.Write(NativeBattleQuezacotlRig86.Resource, resource) };
    }

    private static NativeBattleOriginalEffectState Effect()
    {
        NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (child, _) = child.Append(0x6C3940);
        return new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [child], Quezacotl = new(1, 0, 3690) };
    }
}
