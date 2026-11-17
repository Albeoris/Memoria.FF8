using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFixtureInspectionTests
{
    [Fact]
    public void Binary_identity_collection_uses_the_actual_executing_assemblies()
    {
        Dictionary<string, string> hashes = NativeBattleFixtureInspection.CollectBinaryHashes();
        Assert.Equal(5, hashes.Count);
        Assert.Contains(typeof(BattleScenePresentation).Assembly.GetName().Name!, hashes.Keys);
        Assert.Contains(typeof(Memoria.FF8.Runtime.BattleSession).Assembly.GetName().Name!, hashes.Keys);
        Assert.Contains(typeof(Memoria.FF8.Assets.GameAssets).Assembly.GetName().Name!, hashes.Keys);
        Assert.Contains(typeof(Memoria.FF8.Rendering.ITriangleRenderer).Assembly.GetName().Name!, hashes.Keys);
        Assert.Contains(typeof(NativeBattleFixtureInspectionTests).Assembly.GetName().Name!, hashes.Keys);
        Assert.All(hashes.Values, value => Assert.Equal(32, Convert.FromHexString(value).Length));
    }

    [Fact]
    public void Missing_owner_observable_is_rejected_without_reconstruction()
    {
        JsonElement source = JsonSerializer.SerializeToElement(new { NativeEffectMemory = (object?)null });
        Assert.Throws<InvalidDataException>(() => NativeBattleFixtureInspection.ReadPointer(source, "/NativeEffectMemory/Owner"));
        Assert.Equal("{\"NativeEffectMemory\":null}", source.GetRawText());
    }

    [Fact]
    public void Stale_completed_effect_cannot_admit_an_unrelated_queued_action()
    {
        const ushort effect = 1;
        const uint entry = 1;
        NativeBattleOriginalEffectState stale = new(effect, 0, new(effect, entry, 0), null, 0);
        Assert.False(NativeBattleFixtureInspection.IsInitialized(stale, effect, entry));
        NativeBattleActionPacketsState packets = Packets(2);
        byte[] descriptor = new byte[12];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(4), 0x1D280C4);
        NativeBattleDescriptorPacket queued = new(descriptor.ToImmutableArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleFixtureInspection.ValidateActionIdentity(queued, packets, null, effect));
        packets = Packets(1);
        NativeBattleFixtureInspection.ValidateActionIdentity(queued, packets, null, effect);
        NativeBattleActionGraphicsState otherHeader = new(new BattleActionHeaderReference(1), BattleRetainedModelReference.Capture(OriginalOwner(0), OriginalAliases), new BattleContactCursor(0), BattleRetainedModelSelection.Empty, BattleRetainedModelSelection.Empty, null, null, 0, null, 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleFixtureInspection.ValidateActionIdentity(queued, packets, otherHeader, effect));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(4), 0x1D280C5);
        Assert.Throws<InvalidDataException>(() => NativeBattleFixtureInspection.ValidateActionIdentity(new(descriptor.ToImmutableArray()), packets, null, effect));

        static NativeBattleActionPacketsState Packets(ushort selectedEffect)
        {
            (NativeBattleActionPacketsState state, _) = OriginalPackets().Prepare(new(OriginalOwner(0), 0, 0, 0, 0, selectedEffect, null));
            return state.Publish(0).State;
        }
    }

    [Fact]
    public void Snapshot_pointer_selects_arrays_and_escaped_property_names()
    {
        using JsonDocument source = JsonDocument.Parse("{\"a/b\":{\"~owner\":[12,34]}}");
        Assert.Equal(34, NativeBattleFixtureInspection.ReadPointer(source.RootElement, "/a~1b/~0owner/1").GetInt32());
        Assert.Throws<InvalidDataException>(() => NativeBattleFixtureInspection.ReadPointer(source.RootElement, "/a~1b/~0owner/2"));
    }

}
