using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleGraphicsResource
{
    [System.Text.Json.Serialization.JsonIgnore]
    internal NativeBattleModelCpuReference? PilotGeometryResource { get; init; }
}

internal static class NativeBattleGilgameshVisuals
{
    internal static NativeBattlePilotVisualIdentity? Resource(NativeBattlePilotVisualIdentity? parent, object resource, uint address, int role = 0)
    {
        return parent is { } identity ? identity with { ResourceGeneration = unchecked(((NativeBattlePilotVisualLifetimes.EpochFor(resource) * 397 ^ identity.ResourceGeneration) * 397 ^ address) * 397 ^ role), Primitive = unchecked(identity.Primitive * 397 + role) } : null;
    }

    internal static NativeBattlePilotVisualIdentity? ActorResource(NativeBattlePilotVisualIdentity? parent, NativeBattleGraphicsResource resource, int actor)
    {
        if (parent is null)
        {
            return null;
        }
        NativeBattleModelCpuReference? cpu = resource.PilotGeometryResource;
        object geometry = cpu is not null ? cpu : System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsArray(resource.Geometry!.Value)!;
        NativeBattlePilotVisualIdentity identity = Resource(parent, geometry, cpu?.PhysicalAddress ?? 0, actor)!.Value;
        if (resource.LinkedGeometry is { } linked && !linked.IsEmpty && resource.LinkedGeometryUsesBodySkeleton == true)
        {
            identity = Resource(identity, System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsArray(linked)!, 0, 1)!.Value;
        }
        return identity;
    }

    internal static NativeBattlePilotVisualIdentity? Address(NativeBattlePilotVisualIdentity? parent, uint address, int role = 0)
    {
        return parent is { } identity ? identity with { ResourceGeneration = unchecked((identity.ResourceGeneration * 397 ^ identity.Generation) * 397 ^ address), Primitive = unchecked(identity.Primitive * 397 + role) } : null;
    }

    internal static NativeBattlePilotVisualIdentity? Row(NativeBattlePilotVisualIdentity? parent, NativeBattleEffectArena arena, uint bank, int slot, object resource, uint address)
    {
        if (parent is not { } owner)
        {
            return null;
        }
        NativeBattlePilotVisualIdentity row = NativeBattlePilotVisualLifetimes.Identity(owner.Effect, arena, bank, slot);
        return Resource(row with { ResourceGeneration = owner.Generation }, resource, address);
    }

    internal static NativeBattleGeometryPrimitive Capture(NativeBattleGeometryPrimitive primitive, NativeBattlePilotVisualIdentity? parent, IReadOnlyList<System.Numerics.Vector3>? world)
    {
        if (parent is not { } identity || world is null)
        {
            return primitive;
        }
        return primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + (int)(primitive.Words[1] >> 24)) }, world[0], world[1], world[2], world.Count == 4 ? world[3] : default, checked((byte)world.Count)) { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } };
    }

    internal static NativeBattleGfGeometryBatch Depths(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { } geometry } primitive ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    internal static NativeBattleModulePacketBatch Depths(NativeBattleModulePacketBatch batch)
    {
        return batch with { Primitives = batch.Primitives.Select(item => item.Primitive.PilotGeometry is { } geometry ? item with { Primitive = item.Primitive with { PilotGeometry = geometry with { SourceDepths = new(item.Primitive.VertexPayload4[0], item.Primitive.VertexPayload4[1], item.Primitive.VertexPayload4[2], item.Primitive.VertexPayload4[3]), UseSourceDepths = true } } } : item).ToImmutableArray(), CapturedWrites = batch.CapturedWrites is { } writes ? Depths(writes) : null };
    }

    internal static NativeBattleGfGeometryBatch FixedFlash(NativeBattleGfGeometryBatch batch, NativeBattlePilotVisualIdentity? parent)
    {
        if (Address(parent, 0x592C60) is not { } identity)
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { Words.Length: 4 } primitive && (byte)(primitive.Words[1] >> 24) == 0x62 ? attempt with { Primitive = primitive with { PilotGeometry = new(identity, default, new((ushort)primitive.Words[3], 0, 0), new(0, (ushort)(primitive.Words[3] >> 16), 0), new((ushort)primitive.Words[3], (ushort)(primitive.Words[3] >> 16), 0), 4, false) { ScreenSpace = true, ScreenScale = 8, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    internal static NativeBattleGfGeometryBatch Focal(NativeBattleGfGeometryBatch batch, ushort focal)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { } geometry } primitive ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { FocalLengthOverride = focal } } } : attempt).ToImmutableArray() };
    }
}
