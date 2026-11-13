using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    private Func<Ff7BattleArenaGeometry>? readArenaGeometry;

    public void BindArena(Func<Ff7BattleArenaGeometry> geometry, Func<INativeBattleForeignMaterial, NativeBattleForeignRenderMaterial>? materialResolver = null)
    {
        if (Invocation is not null)
        {
            throw new InvalidOperationException("Arena services must bind before the host starts an action.");
        }
        readArenaGeometry = geometry;
        compositor.ForeignMaterialResolver = materialResolver;
        abilityCapabilities.Clear();
        capabilityVersion++;
    }

    private NativeBattleArenaHost? CreateArena(CrossoverGunbladeHostPlacement placement)
    {
        return readArenaGeometry is null ? null : new(CrossoverNativeArena.Read(readArenaGeometry(), placement.Matrix));
    }

    public ImmutableArray<NativeBattleArenaReplacement> ArenaReplacements => fire?.Presentation.Effect?.ArenaReplacements ?? [];
}

internal static class CrossoverNativeArena
{
    public static NativeBattleArenaGeometry Read(Ff7BattleArenaGeometry geometry, Matrix4x4 originToHost)
    {
        if (!Matrix4x4.Invert(originToHost, out Matrix4x4 hostToOrigin))
        {
            throw new InvalidDataException("Arena geometry requires an invertible source placement.");
        }
        return new(geometry.Faces.Select(face => new NativeBattleArenaFace(face.Id, Vector3.Transform(face.A, hostToOrigin), Vector3.Transform(face.B, hostToOrigin), Vector3.Transform(face.C, hostToOrigin), face.Surface)).ToImmutableArray());
    }
}
