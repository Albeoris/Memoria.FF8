using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlFinalWindowTests
{
    [Fact]
    public void ArenaSourceReadsOnlyTheCurrentObservedExtentAndPreservesUnknownUnrelatedPackets()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        memory = memory with { QuezacotlExecutableMemory = null, Arena = memory.Arena!.Write(0x28E9C, [1, 2, 3, 4]).MarkUnknown(0x100, 3) };
        NativeBattleGfAnimationSource source = NativeBattleQuezacotlWorkspace.Source(memory, NativeBattleQuezacotlWorkspace.TenthSourceAddress, 4);
        Assert.Equal(0x2108954u, source.Address);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Data.ToArray());
        NativeBattleEffectMemory changed = memory with { Arena = memory.Arena!.Write(0x28E9D, [0xA5]) };
        Assert.Equal(new byte[] { 1, 0xA5, 3, 4 }, NativeBattleQuezacotlWorkspace.Source(changed, source.Address, 4).Data.ToArray());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, source.Data.ToArray());
        Assert.Throws<NotSupportedException>(() => memory.Arena!.Read(0x100, 3));
        changed = changed with { Arena = changed.Arena!.MarkUnknown(0x28E9F, 1) };
        Assert.Equal(3, NativeBattleQuezacotlWorkspace.Source(changed, source.Address, 3).Data.Length);
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlWorkspace.Source(changed, source.Address, 4));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Equal(source.Data.ToArray(), NativeBattleQuezacotlWorkspace.Source(restored, source.Address, 4).Data.ToArray());
    }

    [Theory]
    [InlineData(0x20DFAB7u, 4)]
    [InlineData(0x21DFAB8u, 4)]
    [InlineData(0x2108954u, 0)]
    [InlineData(0x2108954u, -1)]
    [InlineData(0x21DFAB7u, 2)]
    public void ArenaSourceRejectsUnobservedOrOutOfArenaExtents(uint address, int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleQuezacotlWorkspace.Source(NativeBattleEffectMemory.FromColdProcess(), address, length));
    }

    [Fact]
    public void FinalWorkspaceWritesOnlyIts354PrefixAndMirrorsTheExistingChildPayload()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        memory = memory with { Arena = memory.Arena!.Write(0xD2, [0x11, 0x22]).Write(0x428, [0x33, 0x44]) };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
        (pool, _) = pool.Append(0x6C3940);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { Pools = [pool] };
        NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA7, NativeBattleQuezacotlWorkspace.TenthLength).ToImmutableArray());
        NativeBattleQuezacotlWorkspaceWrite written = NativeBattleQuezacotlWorkspace.Write(memory, effect, workspace);
        Assert.Equal(workspace.Data.ToArray(), NativeBattleQuezacotlWorkspace.Read(written.Memory, NativeBattleQuezacotlWorkspace.TenthLength).Data.ToArray());
        Assert.Equal(new byte[] { 0x11, 0x22 }, written.Memory.Arena!.Read(0xD2, 2));
        Assert.Equal(new byte[] { 0x33, 0x44 }, written.Memory.Arena.Read(0x428, 2));
        Assert.Equal(written.Memory.Arena.Read(12, 0x8D0 - 12), written.Effect.Pools[0].Slots[0].Payload.ToArray());
    }

    [Theory]
    [InlineData(0, 128)]
    [InlineData(1, 125)]
    [InlineData(18, 64)]
    [InlineData(35, 4)]
    public void FinalCameraUsesUnsignedNativeDivisionAndSignedWordStoresOnly(int localFrame, int delta)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = new(new(32760, -32760, -100), new(12, 23, 34), 288, 27), FocalLength = 288, Roll = 27, EyePaddingF6 = 0x2345, TargetPaddingFE = 0x3456 };
        NativeBattleCameraState shifted = NativeBattleQuezacotlLaterCamera.Shift6C48F5(camera, localFrame, 0);
        Assert.Equal(new NativeBattleCameraPoint(unchecked((short)(32760 + delta)), unchecked((short)(-32760 - delta)), unchecked((short)(-100 - delta))), shifted.CurrentPose!.Eye);
        Assert.Equal(camera.CurrentPose!.Target, shifted.CurrentPose.Target);
        Assert.Equal(camera.Roll, shifted.Roll);
        Assert.Equal(camera.FocalLength, shifted.FocalLength);
        Assert.Equal(camera.EyePaddingF6, shifted.EyePaddingF6);
        Assert.Equal(camera.TargetPaddingFE, shifted.TargetPaddingFE);
        Assert.Equal(camera.Projection, shifted.Projection);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void PausedFinalCameraDoesNotReadUnknownCoordinates(int frozen)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess() with { CurrentPose = null };
        Assert.Same(camera, NativeBattleQuezacotlLaterCamera.Shift6C48F5(camera, 0, frozen));
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlLaterCamera.Shift6C48F5(camera, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleQuezacotlLaterCamera.Shift6C48F5(camera, 36, frozen));
    }

    [Fact]
    public void DetachedFragmentWalkRequiresOnlyTheDescriptorAndDoesNotReadRestoredUnusedExeRows()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116) with { QuezacotlExecutableMemory = null, QuezacotlSecondaryHeads = [0, 0, 0, 0, 0] };
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleQuezacotlFragmentPool.Descriptor, NativeBattleQuezacotlFragmentPool.Bank, NativeBattleQuezacotlFragmentPool.Stride, NativeBattleQuezacotlFragmentPool.Capacity);
        NativeBattleQuezacotlFragmentPool.ValidateHead(memory, pool);
        Assert.Null(pool.Head);
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlFragmentPool.Validate(memory, pool));
        Assert.Throws<InvalidDataException>(() => NativeBattleQuezacotlFragmentPool.ValidateHead(memory with { QuezacotlSecondaryHeads = [0, 0, 0, 0, NativeBattleQuezacotlFragmentPool.Bank] }, pool));
    }
}
