using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    public CrossoverNativeActorColor? ReadForeignColor(int slot)
    {
        if (PresentedFire is not { Presentation.Effect: { } effect } active)
        {
            return null;
        }
        CrossoverNativeMagicParticipant? participant = active.Presentation.BoundParticipants.SingleOrDefault(participant => participant.HostSlot == slot);
        NativeBattleGraphicsModel? model = participant is null ? null : effect.Graphics.Models[participant.ActorIndex];
        return model is { Color40: { } color } && (model.Flags0 & 0x800) != 0 ? new(color) : null;
    }

    private NativeBattleForeignActor ReadPresentationParticipant(int slot, int index)
    {
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(host.VisualPlacement(ActorSlot).Position), host.VisualPlacement(ActorSlot).Yaw);
        if (readForeignGeometry is not null || encounter.GuestAt(slot)?.Visual is not null)
        {
            CrossoverNativeForeignTarget foreign = ReadAbilityForeign(placement, slot);
            return foreign.ToActor(index) with { ReadGeometry = () => foreign.Anchors };
        }
        NativeBattleModuleAnchor anchor = ReadAbilityAnchor(placement, slot);
        NativeBattleVector impact = new(anchor.X, anchor.Y, anchor.Z);
        bool canReact = host.CanPresentGuestReaction(slot);
        return new(index, ReadAbilityTransform(placement, slot), anchor, (selector, fraction) => CrossoverNativeForeignTarget.ReadSemanticBone(selector, fraction, impact, () => placement.ResolvePosition(host.GuestEffectTargetBone(slot, 0))))
        {
            IsParty = slot < 3,
            CanReact = canReact,
            AtStandingPose = canReact ? host.IsGuestTargetStanding(slot) : null
        };
    }

    private CrossoverNativeForeignTarget ReadAbilityForeign(CrossoverGunbladeHostPlacement placement, int slot)
    {
        CrossoverGuestHostActions? guest = encounter.GuestAt(slot);
        CrossoverFf8BattleActorVisual? visual = guest?.Visual;
        Ff7BattleActorGeometry geometry = visual is null ? (readForeignGeometry ?? throw new NotSupportedException("The target has no posed actor geometry provider."))(slot) : CrossoverFf8BattleActorRenderer.CaptureActorGeometry(visual);
        CrossoverNativeForeignTarget foreign = CrossoverNativeForeignTarget.Read(host, slot, geometry, placement.Matrix, graphicsRules);
        if (visual is null)
        {
            return foreign;
        }
        Matrix4x4 basis = visual.OriginToHost * foreign.HostToOrigin;
        NativeBattleGraphicsModel model = visual.Model;
        NativeBattleModelTransform transform = model.Transform ?? throw new InvalidDataException("The native participant has no current transform.");
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new InvalidDataException("The native participant has no current world matrix.");
        Matrix4x4 matrix = ToMatrix(world) * basis;
        NativeBattleVector position = Transform(transform.Position28);
        short yaw = unchecked((short)NativePrecision53.RoundInteger(Math.Atan2(basis.M31, basis.M33) * 4096 / Math.Tau));
        return foreign with
        {
            NativeResource = guest!.limitResource,
            NativeModel = model with
            {
                Transform = transform with { Initial20 = Transform(transform.Initial20), Position28 = position, Vertical34 = checked((short)(transform.Vertical34 + position.Y - transform.Position28.Y)) },
                Rotation14 = unchecked((short)((model.Rotation14 ?? 0) + yaw)),
                GeometryWorldMatrix = new(new[] { Word(matrix.M11), Word(matrix.M21), Word(matrix.M31), Word(matrix.M12), Word(matrix.M22), Word(matrix.M32), Word(matrix.M13), Word(matrix.M23), Word(matrix.M33) }.ToImmutableArray(), checked((int)NativePrecision53.RoundInteger(matrix.M41)), checked((int)NativePrecision53.RoundInteger(matrix.M42)), checked((int)NativePrecision53.RoundInteger(matrix.M43)), world.Scale)
            }
        };

        NativeBattleVector Transform(NativeBattleVector point)
        {
            Vector3 transformed = Vector3.Transform(new(point.X, point.Y, point.Z), basis);
            return new(checked((short)NativePrecision53.RoundInteger(transformed.X)), checked((short)NativePrecision53.RoundInteger(transformed.Y)), checked((short)NativePrecision53.RoundInteger(transformed.Z)));
        }

        static short Word(float value) => checked((short)NativePrecision53.RoundInteger(value * 4096));

        static Matrix4x4 ToMatrix(NativeBattleActionMatrix value)
        {
            var rotation = value.Rotation;
            return new(rotation[0] / 4096f, rotation[3] / 4096f, rotation[6] / 4096f, 0, rotation[1] / 4096f, rotation[4] / 4096f, rotation[7] / 4096f, 0, rotation[2] / 4096f, rotation[5] / 4096f, rotation[8] / 4096f, 0, value.X, value.Y, value.Z, 1);
        }
    }
}
