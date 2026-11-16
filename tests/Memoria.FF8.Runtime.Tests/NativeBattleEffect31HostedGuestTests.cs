using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect31HostedGuestTests
{
    [Fact]
    public void Target_stack_addresses_resolve_current_distinct_guest_owners_across_resource_row_reuse()
    {
        BattlePartyActorModel target = new(new("target"));
        BattlePartyActorModel other = new(new("other"));
        BattleModelRoster roster = new([target, other]);
        NativeBattleModelAliases aliases = new(roster, []);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown().WithPresentationRoster(roster, aliases);
        NativeBattleEffectHeapAllocation emptyPack = new("empty-texture-pack", [0, 0, 0, 0, 0, 0, 0, 0]);
        NativeBattleModelTextureCacheLoad load = NativeBattleModelTextureCache.FromColdProcess().WithPresentationRoster(roster).LoadPlayer(0, 0, emptyPack, target);
        NativeBattleModelTextureCacheLoad otherLoad = load.Cache.LoadPlayer(0, 0, emptyPack, other);
        NativeBattleModelTextureCache cache = otherLoad.Cache;
        byte[] rawSkeleton = new byte[64];
        rawSkeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(rawSkeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteUInt16LittleEndian(rawSkeleton.AsSpan(50), 0xBEEF);
        NativeBattlePoseSource pose = new(rawSkeleton.ToImmutableArray(), [0]);
        var (withTarget, targetReference) = BindSkeleton(cpu, load.ResourceIndex, target, pose, 0x80);
        var (withBoth, otherReference) = BindSkeleton(withTarget, otherLoad.ResourceIndex, other, pose, 0x80);
        cpu = withBoth;
        NativeBattleActionMatrix world = new([0x1000, 0, 0, 0, 0x1000, 0, 0, 0, 0x1000], 11, 22, 33, 0x1000);
        NativeBattleGraphicsModel model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(new(2) { ModelId4 = 0, ActiveResourceRow100 = load.ResourceIndex, GeometryWorldMatrix = world }, pose);
        model = model with { CurrentBodyBoneMatrices = model.CurrentBodyBoneMatrices! with { Matrices = [world with { X = 47 }] } };
        BattleActorModels models = new([new(target, model), new(other, model with { ActiveResourceRow100 = otherLoad.ResourceIndex })]);
        NativeBattleModelBoundary boundary = new(Enumerable.Repeat(new NativeBattleGraphicsModel(0), NativeBattleModelBoundary.CellCount));
        NativeBattleGraphicsState graphics = new(models, boundary, 0, 0, NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.ExpandableArena));
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleHostedGuestModel first = NativeBattleHostedGuestModel.Open(cpu, cache, staging, graphics, target, pose, ReadModel);
        NativeBattleHostedGuestModel second = NativeBattleHostedGuestModel.Open(cpu, cache, staging, graphics, other, pose, ReadModel);
        NativeBattleResourcePointer selected = new(targetReference.Advance(32), null, 32);
        NativeBattleStackBytes stack = NativeBattleEffect31Stack.ObserveHostedTarget(NativeBattleEffect31Stack.Begin(), 0x2000000, first, selected);
        uint worldAddress = stack.ReadUInt32(-0xD4);
        uint boneAddress = stack.ReadUInt32(-0xD0);

        Assert.Equal(worldAddress, stack.ReadUInt32(-0xFC));
        Assert.Equal(first.ModelAddress + 0x40, worldAddress);
        Assert.Equal(11u, BinaryPrimitives.ReadUInt32LittleEndian(first.Read(worldAddress, 32).Read(20, 4)));
        Assert.Equal(47u, BinaryPrimitives.ReadUInt32LittleEndian(first.Read(boneAddress, 32).Read(20, 4)));
        Assert.Equal(0xBEEF, BinaryPrimitives.ReadUInt16LittleEndian(first.Read(boneAddress + 18, 2).Read(0, 2)));
        Assert.True(NativeBattleHostedGuestModel.TryResolveBank(boneAddress, out int bank));
        Assert.Equal(targetReference.Bank, bank);
        Assert.NotEqual(worldAddress, second.RequireWorldMatrixAddress());
        Assert.Throws<InvalidDataException>(() => second.RequireBoneMatrixAddress(selected));
        Assert.Throws<InvalidDataException>(() => first.Read(second.RequireBoneMatrixAddress(new(otherReference.Advance(32), null, 32)), 32));
        Assert.Throws<InvalidDataException>(() => first.Read(first.ModelAddress + NativeBattleGraphicsModelAddress.RowSize - 1, 2));
        Assert.Equal(new uint[] { 0x50221A, 0x50221B, 0x502221, 0x45DEF5 }, stack.Observations.Take(4).Select(observation => observation.Instruction));
        Assert.False(NativeBattleGraphicsModelAddress.TryDecodeOriginalCell(first.ModelAddress, out _));
        Assert.False(NativeBattlePresentationTaskStorage.IsHostedBank(first.ModelAddress, 16, 1));
        Assert.False(NativeBattleModelResourceRowAddress.TryDecode(first.ModelAddress, out _, NativeBattleModelResourceRowAddress.MaximumEncodedRowCount));

        OwnerGraph detached = NativeBattleStorageSnapshots.Fork(new OwnerGraph(cpu, cache, graphics));
        NativeBattleHostedGuestModel restored = NativeBattleHostedGuestModel.Open(detached.Cpu, detached.Cache, staging, detached.Graphics, target, pose, ReadModel);
        Assert.Equal(worldAddress, restored.RequireWorldMatrixAddress());
        Assert.Equal(boneAddress, restored.RequireBoneMatrixAddress(selected));
        var (reloaded, replacement) = BindSkeleton(detached.Cpu, load.ResourceIndex, target, pose, 0x100);
        NativeBattleHostedGuestModel current = NativeBattleHostedGuestModel.Open(reloaded, detached.Cache, staging, detached.Graphics, target, pose, ReadModel);
        Assert.Equal(worldAddress, current.RequireWorldMatrixAddress());
        Assert.Equal(boneAddress + 0x80, current.RequireBoneMatrixAddress(new(replacement.Advance(32), null, 32)));
        Assert.Throws<InvalidDataException>(() => current.RequireBoneMatrixAddress(selected));
        Assert.Equal(boneAddress, stack.ReadUInt32(-0xD0));
    }

    private static (NativeBattleModelCpuResources Cpu, NativeBattleModelCpuReference Reference) BindSkeleton(NativeBattleModelCpuResources cpu, int row, BattlePartyModelOwner owner, NativeBattlePoseSource pose, int offset)
    {
        int index = cpu.Cpu.PartyBankIndex(owner);
        NativeBattleModelCpuBank bank = cpu.Cpu.GetBank(index).BindPartyResource(offset, pose.Skeleton, 0, pose.Skeleton.Length);
        NativeBattleModelCpuReference reference = new(index, offset) { ResourceOnly = true };
        NativeBattleModelCpuResourceRow resource = cpu.Rows[row].Set(12, reference) with { Lengths = cpu.Rows[row].Lengths.SetItem(12, pose.Skeleton.Length) };
        return (cpu with { Cpu = cpu.Cpu.WithBank(index, bank), Rows = cpu.Rows.SetItem(row, resource) }, reference);
    }

    private static NativeBattleEffectArena ReadModel(NativeBattleGraphicsModel model)
    {
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(model.GeometryWorldMatrix!, null);
        return NativeBattleEffectArena.Clear().WriteWithKnownness(0x40, matrix.Bytes.AsSpan(), matrix.Known.AsSpan());
    }

    private sealed record OwnerGraph(NativeBattleModelCpuResources Cpu, NativeBattleModelTextureCache Cache, NativeBattleGraphicsState Graphics);
}
