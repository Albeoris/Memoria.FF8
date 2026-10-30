using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect65Presentation
{
    internal static NativeBattleEffect65Inputs ForTask(NativeBattleEffect65Frame frame, uint address, NativeBattleEffect65Inputs inputs)
    {
        return inputs.CapturePresentation ? inputs with { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(65, NativeBattleEffect65Controller.Read(frame, address)) } : inputs;
    }

    internal static NativeBattlePilotVisualIdentity? Source(NativeBattleEffect65Frame frame, NativeBattleEffect65Inputs inputs, uint source, int copy)
    {
        if (!inputs.CapturePresentation)
        {
            return null;
        }
        NativeBattlePilotVisualIdentity owner = inputs.PilotIdentity ?? throw new InvalidDataException("Effect65 capture requires its actual executing task.");
        return owner with { Primitive = unchecked(owner.Primitive * 397 + copy), ResourceGeneration = SourceGeneration(frame, source) };
    }

    private static long SourceGeneration(NativeBattleEffect65Frame frame, uint source)
    {
        uint start = checked(frame.State.ReadDword(NativeBattleEffect65Profile.ArenaGlobal) + NativeBattleEffect65Profile.FileOffset);
        bool loaded = source >= start && source < checked(start + (uint)NativeBattleEffect65Profile.BundleLengths.Values.Max());
        return loaded ? unchecked(NativeBattlePilotVisualLifetimes.Identity(65, frame.Memory.Arena!, start, -1).Generation * 397 ^ source) : source;
    }

    internal static NativeBattlePilotVisualIdentity? Row(NativeBattleEffect65Frame frame, NativeBattleEffect65Inputs inputs, int offset, int row, uint source)
    {
        if (!inputs.CapturePresentation)
        {
            return null;
        }
        uint bank = checked(frame.State.ReadDword(NativeBattleEffect65Profile.ArenaGlobal) + (uint)offset);
        return NativeBattlePilotVisualLifetimes.Identity(65, frame.Memory.Arena!, bank, row) with { ResourceGeneration = SourceGeneration(frame, source) };
    }

    internal static NativeBattlePilotVisualIdentity Segment(NativeBattleEffect65Frame frame, uint recordBank, int record, uint first, uint second)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        uint points = checked(frame.State.ReadDword(NativeBattleEffect65Profile.ArenaGlobal) + 0x3B530);
        int a = checked((int)((first - points) / 24));
        int b = checked((int)((second - points) / 24));
        NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(65, arena, recordBank, record, checked(a * 256 + b));
        long firstBirth = NativeBattlePilotVisualLifetimes.Identity(65, arena, points, a).Generation;
        long secondBirth = NativeBattlePilotVisualLifetimes.Identity(65, arena, points, b).Generation;
        return identity with { ResourceGeneration = unchecked(firstBirth * 397 ^ secondBirth) };
    }

    internal static Vector3 LoadedWorld(NativeBattleGeometryRegisterStep registers, NativeBattleVector point, NativeBattleCameraProjection projection)
    {
        return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(NativeBattleGeometry.CaptureMatrix(registers.Geometry), point), projection);
    }

    internal static NativeBattleGfGeometryBatch FinalizeCapturedBatch(NativeBattleGfGeometryBatch batch)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { Words.Length: > 2, PilotGeometry: { Identity.Effect: 65, UseSourceDepths: false } } primitive ? attempt with { Primitive = primitive with { PilotGeometry = primitive.PilotGeometry with { SourceDepths = Depths(primitive), UseSourceDepths = true } } } : attempt).ToImmutableArray() };
    }

    private static Vector4 Depths(NativeBattleGeometryPrimitive primitive)
    {
        return primitive.VertexPayload4.Length switch
        {
            0 => Vector4.Zero,
            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
            _ => throw new InvalidDataException("Effect65 capture requires its actual empty or four-lane native depth payload.")
        };
    }
}
