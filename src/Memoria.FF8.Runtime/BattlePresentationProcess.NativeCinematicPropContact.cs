using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleWaterExecutionFrame TransportPairedCinematicProp(NativeBattleWaterExecutionFrame frame, uint model, NativeBattleGraphicsState graphics, NativeBattleCameraProjection projection)
    {
        if (graphics.OriginalEffect?.Binding.Effect != 261 || frame.Arena.ReadUInt16(ArenaOffset(model + 8, 2)) != 13)
        {
            return frame;
        }
        if (!frame.Arena.TryReadExecutionReference<BattleExecutionReference.Model>(SlotOffset(frame, 0x1B, 1), 1, out var owner))
        {
            return frame;
        }
        NativeBattleGraphicsModel actor = graphics.ActorModels.Get(owner.Owner);
        if (actor.SequenceState?.DecodedPose?.HandAContact is null)
        {
            return frame;
        }
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, owner.Owner).PoseSource ?? throw new InvalidDataException("An actor-owned paired prop requires its current physical body rig.");
        NativeBattleVector? cameraEye = EncounterPresentationCamera?.Eye is { } eye ? new(eye.X, eye.Y, eye.Z) : null;
        NativeBattleGraphicsModel posed = NativeBattlePoseMatrices.ComposeGeometryMatrices502D40(actor, source, projection.View ?? throw new InvalidDataException("An actor-owned paired prop requires its current view."), cameraEye);
        NativeBattlePosedSemanticContact? contact = NativeBattlePoseMatrices.CaptureHandAContact502D40(posed, source, projection, cameraEye, graphics.Callback);
        byte animation = frame.Arena.ReadByte(ArenaOffset(model + 0x20, 1));
        byte counter = frame.Arena.ReadByte(ArenaOffset(model + 0x26, 1));
        byte limit = frame.Arena.ReadByte(ArenaOffset(model + 0x27, 1));
        if (!NativeBattleCinematicPropContact.OwnsPairedTrack(contact, graphics.Callback, animation, counter, limit))
        {
            return frame;
        }
        uint table = frame.Arena.ReadUInt32(ArenaOffset(model + 0x14, 4));
        uint skeleton = frame.Arena.ReadUInt32(ArenaOffset(table, 4));
        int count = frame.Arena.ReadByte(ArenaOffset(skeleton, 1));
        ImmutableArray<NativeBattleActionMatrix> original = Enumerable.Range(0, count).Select(bone => NativeBattleArenaPose.ReadMatrix(frame.Arena, ArenaOffset(skeleton + 0x20u + (uint)(bone * 0x30), 32))).ToImmutableArray();
        ImmutableArray<NativeBattleActionMatrix> translated = NativeBattleCinematicPropContact.TranslateOriginal(original, contact!.WorldDelta, projection);
        NativeBattleEffectArena arena = frame.Arena;
        for (int bone = 0; bone < count; bone++)
        {
            arena = NativeBattleArenaPose.WriteMatrix(arena, ArenaOffset(skeleton + 0x20u + (uint)(bone * 0x30), 32), translated[bone], true);
        }
        return frame with { Arena = arena };
    }
}
