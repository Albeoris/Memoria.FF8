using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectResourceResolverTests
{
    [Theory]
    [InlineData((ushort)2)]
    [InlineData((ushort)116)]
    [InlineData((ushort)144)]
    public void SharedSelectionResolvesTheCurrentNativeEffectWithoutCommandOrActorClassification(ushort requested)
    {
        ImmutableArray<NativeBattleEffectBinding> catalog = Catalog();
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(requested, catalog);
        int calls = 0;
        NativeBattleOriginalEffectState resolved = NativeBattleEffectResourceResolver.MaterializeSelected(selected, effect =>
        {
            calls++;
            Assert.Equal(requested, effect);
            return catalog[effect - 1] with { InitializerAsset = new("fixture.tim", [7, 8, 9]) };
        });
        Assert.Equal(1, calls);
        Assert.Equal(requested - 1, resolved.SelectedIndex);
        Assert.Null(catalog[requested - 1].InitializerAsset);
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), resolved);
        Assert.Equal(new byte[] { 7, 8, 9 }, initialized.Memory.Arena!.Read(0, 3));
        Assert.Equal(resolved.Binding, initialized.Effect.Binding);
    }

    [Theory]
    [InlineData((ushort)17, 16)]
    [InlineData((ushort)0, 0)]
    [InlineData(ushort.MaxValue, 0)]
    public void ResolverReceivesActualFallbackBindingWhileNativeRequestedIndexIsPreserved(ushort requested, int selectedIndex)
    {
        ImmutableArray<NativeBattleEffectBinding> catalog = Catalog();
        catalog = catalog.SetItem(16, catalog[16] with { Entry = 0 });
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(requested, catalog);
        NativeBattleOriginalEffectState resolved = NativeBattleEffectResourceResolver.MaterializeSelected(selected, effect =>
        {
            Assert.Equal((ushort)1, effect);
            return catalog[0] with { InitializerAsset = new("fixture.tim", [1]) };
        });
        Assert.Equal(requested, resolved.RequestedEffect);
        Assert.Equal(selectedIndex, resolved.SelectedIndex);
        Assert.Equal((ushort)1, resolved.Binding.Effect);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void ResolverCannotReplaceOriginalSelectedMetadata(int changed)
    {
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(144, Catalog());
        NativeBattleEffectBinding original = selected.Binding;
        NativeBattleEffectBinding replacement = changed switch
        {
            0 => original with { Effect = 1 },
            1 => original with { Entry = original.Entry + 1 },
            2 => original with { Initializer = original.Initializer + 1 },
            3 => original with { InitializerIsRet = true },
            4 => original with { InitializerResource = new("other.tim", original.InitializerResource!.GlobalAddress) },
            5 => original with { InitializerResource = new(original.InitializerResource!.Name, original.InitializerResource.GlobalAddress + 4) },
            _ => throw new ArgumentOutOfRangeException(nameof(changed))
        };
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectResourceResolver.MaterializeSelected(selected, _ => replacement));
        Assert.Same(original, selected.Binding);
        Assert.Null(selected.ReturnedDescriptor);
    }

    [Fact]
    public void UnsupportedHostRejectsBeforeInitializerCommitAndDoesNotConsumeTheCurrentRequest()
    {
        BattleSession session = new(Definition(), BattleAtbMode.Normal);
        int calls = 0;
        session.AttachNativeEffectResourceResolver(_ =>
        {
            calls++;
            throw new NotSupportedException("controlled original resource unavailable");
        });
        string before = JsonSerializer.Serialize(session.Capture());
        NativeBattleOriginalEffectState previous = NativeBattleOriginalEffects.Select(2, Catalog());
        NativeBattleGraphicsState graphics = new([], [], 0, 0) { OriginalEffect = previous };
        string requestBefore = JsonSerializer.Serialize(graphics);
        MethodInfo begin = typeof(BattleSession).GetMethod("BeginNativeOriginalEffect", BindingFlags.Instance | BindingFlags.NonPublic)!;
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => begin.Invoke(session, [graphics, (ushort)144, false]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Equal(1, calls);
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        Assert.Equal(requestBefore, JsonSerializer.Serialize(graphics));
        Assert.Same(previous, graphics.OriginalEffect);
        session.AttachNativeEffectResourceResolver(effect => Catalog()[effect - 1] with { InitializerAsset = new("fixture.tim", [0xA7]) });
        NativeBattleGraphicsState admitted = (NativeBattleGraphicsState)begin.Invoke(session, [graphics, (ushort)144, false])!;
        Assert.Equal((ushort)144, admitted.OriginalEffect!.Binding.Effect);
        Assert.Equal(143, session.NativeEffectMemory!.SelectedIndex);
        Assert.Equal(new byte[] { 0xA7 }, session.NativeEffectMemory.Arena!.Read(0, 1));
    }

    [Fact]
    public void ResolverAttachmentAndSnapshotRestorePerformNoMaterializationOrDefinitionCacheWrite()
    {
        BattleSession session = new(Definition(), BattleAtbMode.Normal);
        string before = JsonSerializer.Serialize(session.Capture());
        session.AttachNativeEffectResourceResolver(_ => throw new InvalidOperationException("No native request has executed."));
        Assert.Equal(before, JsonSerializer.Serialize(session.Capture()));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(before)!);
        restored.AttachNativeEffectResourceResolver(_ => throw new InvalidOperationException("Restore must not hydrate."));
        Assert.Equal(before, JsonSerializer.Serialize(restored.Capture()));
        Assert.Throws<ArgumentNullException>(() => restored.AttachNativeEffectResourceResolver(null!));
    }

    [Fact]
    public void CurrentBindingAndInitializedMemoryRoundTripWithoutAnyHostResolver()
    {
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(2, Catalog());
        selected = NativeBattleEffectResourceResolver.MaterializeSelected(selected, effect => Catalog()[effect - 1] with { InitializerAsset = new("fixture.tim", [4, 5]) });
        var initialized = NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected);
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 1, 2, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState active = NativeBattleFireEffect.InitializePools(initialized.Effect with { Parameters = new(0, 0, 8, 0, 0, 0) }, packets);
        SavedEffect saved = new(initialized.Memory, active);
        SavedEffect restored = JsonSerializer.Deserialize<SavedEffect>(JsonSerializer.Serialize(saved))!;
        Assert.Equal(new byte[] { 4, 5 }, restored.Effect.Binding.InitializerAsset!.Data);
        Assert.Equal(new byte[] { 4, 5 }, restored.Memory.Arena!.Read(0, 2));
        Assert.Same(restored.Effect, NativeBattleEffectResourceResolver.MaterializeSelected(restored.Effect, null));
        NativeBattleOriginalEffectState completed = NativeBattleFireEffect.Advance(restored.Effect, (pool, _, _) => new(pool, 2));
        Assert.Equal(0u, completed.ReturnedDescriptor);
    }

    [Fact]
    public void MissingHostResourcesOrNullReturnedBindingRemainStrict()
    {
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(144, Catalog());
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectResourceResolver.MaterializeSelected(selected, _ => null!));
        Assert.Throws<NotSupportedException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), NativeBattleEffectResourceResolver.MaterializeSelected(selected, null)));
    }

    private sealed record SavedEffect(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);

    private static ImmutableArray<NativeBattleEffectBinding> Catalog() => Enumerable.Range(1, 400).Select(effect => new NativeBattleEffectBinding((ushort)effect, 0x6298A0, 0x629880) { InitializerIsRet = false, InitializerResource = new("fixture.tim", 0x24CBD68) }).ToImmutableArray();

    private static BattleDefinition Definition() => new()
    {
        NativeEffectCatalog = Catalog(),
        NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess() with { EffectMemory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(8).LoadIntoArena([0x71, 0x72]) },
        Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 100, CurrentHp = 100 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 100, CurrentHp = 100 }]
    };
}
