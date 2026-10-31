namespace Memoria.FF8.Runtime;

/// <summary>Presentation lifetimes follow physical cinematic slot initialization in the current arena authority.</summary>
internal static class NativeBattleCinematicVisualIdentity
{
    internal static void Allocated(NativeBattleEffectArena arena, NativeBattleWaterSlotBank bank, int index)
    {
        NativeBattlePilotVisualLifetimes.Allocated(arena, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotBaseOffset), index);
    }

    internal static NativeBattlePilotVisualIdentity Identity(int effect, NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, int index, int primitive = 0)
    {
        _ = bank.SlotOffset(frame.Arena, index);
        return NativeBattlePilotVisualLifetimes.Identity(checked((ushort)effect), frame.Arena, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotBaseOffset), index, primitive);
    }

    internal static NativeBattlePilotVisualIdentity? SelectedResource(NativeBattlePilotVisualIdentity? identity, uint resource)
    {
        return identity is { } selected && (selected.Effect is 219 or 228 or 236 or 237 or 243 or 244 or 245 or 246 or 247 || NativeBattleCinematic30Presentation.Owns(selected.Effect)) ? selected with { ResourceGeneration = unchecked(selected.ResourceGeneration * 397 ^ resource) } : identity;
    }

    internal static NativeBattleGeometryPrimitive Capture(NativeBattleGeometryPrimitive primitive, IReadOnlyList<System.Numerics.Vector3>? world, NativeBattlePilotVisualIdentity? pilotIdentity, ReadOnlySpan<byte> face, int indices, int corners, int faceIndex, float depthScale = 1)
    {
        if (pilotIdentity is not { } identity || world is null)
        {
            return primitive;
        }
        System.Numerics.Vector3[] vertices = new System.Numerics.Vector3[corners];
        for (int corner = 0; corner < corners; corner++)
        {
            int index = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(indices + corner * 2)) / 8;
            vertices[corner] = world[index];
        }
        return primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + faceIndex) }, vertices[0], vertices[1], vertices[2], corners == 4 ? vertices[3] : default, checked((byte)corners)) { DepthScale = depthScale } };
    }
}
