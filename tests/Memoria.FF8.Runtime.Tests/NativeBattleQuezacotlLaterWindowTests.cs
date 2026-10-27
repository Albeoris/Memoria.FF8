using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlLaterWindowTests
{
    [Fact]
    public void AddressedBanksRejectUnknownPointersAndDoNotAliasFivePairsIntoTen()
    {
        byte[] pair = new byte[80];
        for (int offset = 2; offset < pair.Length; offset += 4)
        {
            BinaryPrimitives.WriteInt16LittleEndian(pair.AsSpan(offset), 1);
        }
        NativeBattleQuezacotlRibbonResources resources = new(pair.ToImmutableArray(), new byte[24].ToImmutableArray(), new ushort[256].ToImmutableArray(), new byte[16384].ToImmutableArray());
        Assert.Throws<NotSupportedException>(() => resources.ResolveBank(0x12AF800, 0x12AF828));
        NativeBattleQuezacotlRibbonBank bank = new(0x12AF800, 0x12AF828, pair.AsSpan(0, 40).ToArray().ToImmutableArray(), new byte[24].ToImmutableArray());
        resources = resources with { SecondaryBank = bank };
        resources.Validate();
        for (uint index = 0; index < 5; index++)
        {
            Assert.Same(bank, resources.ResolveBank(0x12AF800 + index * 8, 0x12AF828));
        }
        Assert.Throws<InvalidDataException>(() => resources.ResolveBank(0x12AF828, 0x12AF828));
        Assert.Throws<InvalidDataException>(() => resources.ResolveBank(0x12AF801, 0x12AF828));
        Assert.Throws<InvalidDataException>(() => resources.ResolveBank(0x12AF800, 0x12AE840));
        Assert.Equal(NativeBattleTestSnapshots.Json(resources), NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleQuezacotlRibbonResources>(NativeBattleTestSnapshots.Json(resources))));
    }

    [Theory]
    [InlineData(-128, 0)]
    [InlineData(-128, 32768)]
    [InlineData(-128, -32767)]
    public void LaterContextUsesYTimesXThenPriorViewAndCurrentTarget(int pitch, int yaw)
    {
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 0, 4096, 0, -4096, 0], 100, 200, 300, 4096);
        NativeBattleVector target = new(-90, 40, 70);
        NativeBattleActionMatrix actual = NativeBattleGfMatrices.LaterWindowContext6C64C0(view, pitch, yaw, target);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Euler701310(new(0, unchecked((short)yaw), 0)), NativeBattleGfMatrices.Euler701310(new(unchecked((short)pitch), 0, 0)));
        Assert.Equal(NativeBattleGfMatrices.Multiply56C090(view, local).Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((10, 270, 260), (actual.X, actual.Y, actual.Z));
    }

    [Theory]
    [InlineData(0x1A0, 0x12A1164u)]
    [InlineData(0x1D8, 0x12A9664u)]
    [InlineData(0x128, 0x129BA94u)]
    [InlineData(0xB4, 0x1299718u)]
    [InlineData(0x124, 0x12A9664u)]
    public void LaterWorkspaceReadsCurrentSubregionAndWritesOnlyItsActualPrefix(int length, uint address)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        byte[] current = new byte[NativeBattleQuezacotlExecutableMemory.Length];
        int offset = checked((int)(address - NativeBattleQuezacotlExecutableMemory.Address));
        current[offset] = 0xA7;
        memory = memory with { QuezacotlExecutableMemory = new(current.ToImmutableArray()), Arena = memory.Arena!.Write(0xD4 + length, [0xCC, 0xDD]) };
        using NativeBattleGfSourceReader reader = NativeBattleQuezacotlWorkspace.Source(memory, address).OpenReader();
        Assert.Equal((byte)0xA7, reader.Byte(0));
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (pool, _) = pool.Append(0x6C3940);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [pool] };
        NativeBattleQuezacotlWorkspaceWrite result = NativeBattleQuezacotlWorkspace.Write(memory, effect, new(Enumerable.Repeat((byte)0x5A, length).ToImmutableArray()));
        Assert.Equal(new byte[] { 0xCC, 0xDD }, result.Memory.Arena!.Read(0xD4 + length, 2));
        Assert.Equal(Enumerable.Repeat((byte)0x5A, length), result.Memory.Arena.Read(0xD4, length));
    }

    [Theory]
    [InlineData(255, 256)]
    [InlineData(256, 257)]
    [InlineData(65535, 0)]
    public void NativePhaseIncrementWritesTheFullWordToBothPhysicalOwners(ushort phase, ushort expected)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (pool, _) = pool.Append(0x6C3940);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteWord(12, phase)) });
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [pool] };
        NativeBattleQuezacotlWorkspaceWrite result = NativeBattleQuezacotlWorkspace.AdvancePhase6C51F1(memory, effect);
        Assert.Equal(expected, result.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(expected, BinaryPrimitives.ReadUInt16LittleEndian(result.Memory.Arena!.Read(12, 2)));
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleQuezacotlWorkspaceWrite>(NativeBattleTestSnapshots.Json(result))));
    }

    [Theory]
    [InlineData(246, 3690, 4313, -885, 6926, -1134, -711, 3426)]
    [InlineData(256, 3690, -4000, -4000, 7690, 0, 0, 3690)]
    [InlineData(246, 30000, 4313, -885, -32300, -1134, -711, 29736)]
    [InlineData(318, 3690, 4096, -4096, -406, 0, 0, 3690)]
    [InlineData(318, -30000, 4096, -4096, 31440, 0, 0, -30000)]
    public void LaterCameraWritesOnlyOriginalXyzWithSignedWordWrapping(ushort phase, int targetZ, short eyeX, short eyeY, short eyeZ, short targetX, short targetY, short targetDepth)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { FocalLength = 288, Roll = 7, EyePaddingF6 = 0x1234, TargetPaddingFE = 0x5678 };
        NativeBattleCameraState changed = NativeBattleQuezacotlLaterCamera.Apply6C4F80(camera, phase, targetZ);
        Assert.Equal(new NativeBattleCameraPoint(eyeX, eyeY, eyeZ), changed.CurrentPose!.Eye);
        Assert.Equal(new NativeBattleCameraPoint(targetX, targetY, targetDepth), changed.CurrentPose.Target);
        Assert.Equal(camera.Projection, changed.Projection);
        Assert.Equal(camera.EyePaddingF6, changed.EyePaddingF6);
        Assert.Equal(camera.TargetPaddingFE, changed.TargetPaddingFE);
        Assert.Equal(camera.FocalLength, changed.FocalLength);
        Assert.Equal(camera.Roll, changed.Roll);
    }
}
