using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Runtime;
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

    private NativeBattleEffectSceneContext ReadPresentationSceneContext()
    {
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(host.VisualPlacement(ActorSlot).Position), host.VisualPlacement(ActorSlot).Yaw);
        return new(NativeBattleSummonFloorMode.BelowTargetPlane, NativeBattleTargetBoundsMode.AllContacts)
        {
            MirroredSurfaceMode = NativeBattleMirroredSurfaceMode.InitialCapture,
            ReadOpposingTransforms = () => host.Actors.Where(actor => actor.Actor.Slot >= 4 && (actor.Actor.Conditions & 8) != 0).Select(actor => ReadAbilityForeign(placement, actor.Actor.Slot).Transform).ToImmutableArray()
        };
    }

    public ImmutableArray<NativeBattleArenaReplacement> ArenaReplacements => fire?.Presentation.Effect?.ArenaReplacements ?? [];
    public ImmutableArray<NativeBattleArenaPrimitiveId> SuppressedArenaPrimitives => fire?.Presentation.Effect?.SuppressedArenaPrimitives ?? [];
    public NativeBattleUiServices? PresentationUi => fire?.Presentation.Effect?.Ui;
    public int? CurrentRelativeMusicGain => fire?.Presentation.Effect?.MusicGain;
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
