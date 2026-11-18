using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleStorageDefinitionSharingTests
{
    private sealed record Inputs(NativeGuardianForceMenuDefinitions Menu, NativeStatDefinitions Stats, int[] Values, ImmutableDictionary<int, int> Lookup);
    private readonly record struct ReferencedValue(byte[] Bytes);
    private sealed record Geometry(NativeBattleRawMesh Mesh, ImmutableArray<NativeBattleVector> Points, NativeBattleVector[] MutablePoints, ReferencedValue Reference);
    private sealed record ImmutableRow(int Id, ImmutableArray<byte> Bytes);
    private sealed record ImmutableCollections(ImmutableArray<ImmutableRow> Rows, ImmutableDictionary<int, ImmutableRow> Lookup);
    private sealed record StorageNode(bool IsSnapshot) : INativeBattleStorageNode
    {
        public object CaptureDetached(NativeBattleStorageCaptureContext context)
        {
            return this with { IsSnapshot = true };
        }

        public object RestoreOwned(NativeBattleStorageRestoreContext context)
        {
            return this with { IsSnapshot = false };
        }
    }

    private readonly record struct ValueStorageNode(bool IsSnapshot) : INativeBattleStorageNode
    {
        public object CaptureDetached(NativeBattleStorageCaptureContext context)
        {
            return this with { IsSnapshot = true };
        }

        public object RestoreOwned(NativeBattleStorageRestoreContext context)
        {
            return this with { IsSnapshot = false };
        }
    }

    [Fact]
    public void DeeplyImmutableCollectionsShareAndFrozenStorageStillRestoresItsOwner()
    {
        ImmutableRow row = new(1, [17, 23]);
        ImmutableCollections source = new([row], ImmutableDictionary<int, ImmutableRow>.Empty.Add(1, row));
        Assert.Same(source, NativeBattleStorageSnapshots.Fork(source));
        StorageNode storage = new(false);
        StorageNode captured = NativeBattleStorageSnapshots.CapturePrivate(storage);
        Assert.True(captured.IsSnapshot);
        StorageNode restored = NativeBattleStorageSnapshots.Restore(captured);
        Assert.False(restored.IsSnapshot);
        Assert.NotSame(captured, restored);
        ValueStorageNode capturedValue = NativeBattleStorageSnapshots.CapturePrivate(new ValueStorageNode(false));
        Assert.True(capturedValue.IsSnapshot);
        Assert.False(NativeBattleStorageSnapshots.Restore(capturedValue).IsSnapshot);
    }

    [Fact]
    public void NewBattleStillClearsLiveCameraAliases()
    {
        BattleModelOwner owner = new BattlePartyActorModel(new("party"));
        NativeBattleCameraAliases source = new(1, 2) { Source = owner, Target = owner };
        NativeBattleCameraAliases captured = NativeBattleStorageSnapshots.CapturePrivate(source);
        NativeBattleCameraAliases restored = new NativeBattleStorageRestoreContext(NativeBattleStorageRestorePurpose.NewBattle).Map(captured);
        Assert.Null(restored.Source);
        Assert.Null(restored.Target);
        Assert.Equal((byte)1, restored.Actor1D99B95);
        Assert.Equal((byte)2, restored.Target1D99AAA);
        Assert.Same(owner, source.Source);
    }

    [Fact]
    public void ImmutableGeometrySharesButValueTypesWithReferencesStillDetach()
    {
        NativeBattleRawMesh mesh = new([new(new(1, 2, 3), 0)], [new(false, [1, 2, 3])]);
        Geometry source = new(mesh, [new(1, 2, 3)], [new(4, 5, 6)], new([17]));
        Geometry fork = NativeBattleStorageSnapshots.Fork(source);
        Assert.Same(mesh, fork.Mesh);
        Assert.Equal(source.Points, fork.Points);
        Assert.NotSame(source.MutablePoints, fork.MutablePoints);
        Assert.NotSame(source.Reference.Bytes, fork.Reference.Bytes);
        fork.MutablePoints[0] = new(7, 8, 9);
        fork.Reference.Bytes[0] = 99;
        Assert.Equal(new NativeBattleVector(4, 5, 6), source.MutablePoints[0]);
        Assert.Equal(17, source.Reference.Bytes[0]);
    }

    [Fact]
    public void ForkSharesImmutableDefinitionsAndDetachesMutableArrays()
    {
        NativeGuardianForceMenuDefinitions menu = new([], [], [], []);
        NativeStatDefinitions stats = new([], [], [], [], []);
        Inputs source = new(menu, stats, [17, 23], ImmutableDictionary<int, int>.Empty.Add(1, 2));

        Inputs captured = NativeBattleStorageSnapshots.CapturePrivate(source);
        Inputs first = NativeBattleStorageSnapshots.Restore(captured);
        Inputs second = NativeBattleStorageSnapshots.Restore(captured);
        Assert.Same(menu, first.Menu);
        Assert.Same(stats, first.Stats);
        Assert.Same(source.Lookup, first.Lookup);
        Assert.NotSame(source.Values, captured.Values);
        Assert.NotSame(captured.Values, first.Values);
        Assert.NotSame(first.Values, second.Values);
        first.Values[0] = 99;
        Assert.Equal(17, source.Values[0]);
        Assert.Equal(17, captured.Values[0]);
        Assert.Equal(17, second.Values[0]);
    }
}
