using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleEffect218Presentation
{
    internal static NativeBattlePilotVisualIdentity? Resource(NativeBattleEffect218Frame frame, uint resource, int primitive = 0)
    {
        return frame.PresentationIdentity is { } identity ? identity with { ResourceGeneration = resource, Primitive = unchecked(identity.Primitive * 397 + primitive) } : null;
    }

    internal static NativeBattlePilotVisualIdentity? Row(NativeBattleEffect218Frame frame, uint bank, int slot, uint resource)
    {
        return frame.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(218, frame.State.ParticleGlobals, bank, slot) with { ResourceGeneration = resource } : null;
    }

    internal static NativeBattleModuleSpriteDraw DrawSprite(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary180, Func<uint, NativeBattleModuleResource> readResource, NativeBattleCameraProjection projection, short cameraRoll, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        NativeBattleModuleResource? selected = null;
        NativeBattleModuleResource ReadSelected(uint address)
        {
            NativeBattleModuleResource resource = readResource(address);
            selected = resource;
            return resource;
        }
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(memory, temporary180, ReadSelected, projection, cameraRoll, pilotIdentity: pilotIdentity);
        if (pilotIdentity is null)
        {
            return drawn;
        }
        NativeBattleModuleResource actual = selected ?? throw new InvalidDataException("Effect218 sprite presentation requires the actual native resource resolution.");
        ImmutableArray<NativeBattleGfGeometryAttempt> attempts = drawn.Batch.Attempts.Select(attempt =>
        {
            if (attempt.Primitive is not { PilotGeometry: { } visual } primitive)
            {
                return attempt;
            }
            NativeBattlePilotVisualGeometry geometry = visual with { Identity = visual.Identity with { ResourceGeneration = actual.Address } };
            return attempt with { Primitive = primitive with { PilotGeometry = geometry } };
        }).ToImmutableArray();
        return drawn with { Batch = drawn.Batch with { Attempts = attempts } };
    }
}
