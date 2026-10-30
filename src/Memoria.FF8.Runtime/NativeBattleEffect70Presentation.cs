using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect70Presentation
{
    internal static NativeBattleEffect70Inputs ForTask(NativeBattleEffect70Frame frame, uint address, NativeBattleEffect70Inputs inputs)
    {
        return inputs.CapturePresentation ? inputs with { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(70, NativeBattleEffect70Controller.Read(frame, address)) } : inputs;
    }

    internal static NativeBattlePilotVisualIdentity? Source(NativeBattleEffect70Inputs inputs, uint source, int copy)
    {
        if (!inputs.CapturePresentation)
        {
            return null;
        }
        NativeBattlePilotVisualIdentity owner = inputs.PilotIdentity ?? throw new InvalidDataException("Effect70 capture requires its actual executing task.");
        return owner with { Primitive = unchecked(owner.Primitive * 397 + copy), ResourceGeneration = source };
    }

    internal static NativeBattleEffect70DrawActor693170 Actor(NativeBattleEffect70Inputs inputs, uint record, int actor, NativeBattleEffectArena rotation, NativeBattleEffectArena offset, short scale)
    {
        NativeBattlePilotVisualIdentity? identity = Source(inputs, 0x693170, unchecked((int)record * 7 + actor));
        return new(actor, rotation, offset, scale) { PilotIdentity = identity };
    }

    internal static NativeBattlePilotVisualIdentity? Row(NativeBattleEffect70Frame frame, NativeBattleEffect70Inputs inputs, int bankOffset, int row, uint source)
    {
        if (!inputs.CapturePresentation)
        {
            return null;
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new InvalidDataException("Effect70 capture requires its actual particle bank.");
        uint bank = checked(frame.State.ReadDword(NativeBattleEffect70Profile.ArenaGlobal) + (uint)bankOffset);
        return NativeBattlePilotVisualLifetimes.Identity(70, arena, bank, row) with { ResourceGeneration = source };
    }

    internal static NativeBattleGfGeometryBatch Flash(NativeBattleEffect70Inputs inputs, int factor)
    {
        NativeBattleGfGeometryBatch batch = NativeBattleRinoa89FadeDrawing.Draw7040B0(255, 255, 255, factor, pilotIdentity: Source(inputs, 0x7040B0, 0));
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { } geometry } primitive ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { ScreenScale = 1 } } } : attempt).ToImmutableArray() };
    }

    internal static NativeBattleGfGeometryBatch FinalizeCapturedBatch(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { Words.Length: > 2, PilotGeometry: { Identity.Effect: 70, UseSourceDepths: false } } primitive ? attempt with { Primitive = primitive with { PilotGeometry = primitive.PilotGeometry with { SourceDepths = Depths(primitive), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    private static Vector4 Depths(NativeBattleGeometryPrimitive primitive)
    {
        return primitive.VertexPayload4.Length switch
        {
            0 => Vector4.Zero,
            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
            _ => throw new InvalidDataException("Effect70 capture requires its actual empty or four-lane native depth payload.")
        };
    }
}
