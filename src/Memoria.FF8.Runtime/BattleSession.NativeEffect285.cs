using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect285(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect285 requires its current effect.");
        NativeBattleEffect285Resources resources = effect.Binding.Effect285Resources ?? throw new NotSupportedException("Effect285 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect285 requires its current process memory.");
        NativeBattleEffect285Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect285 requires its current CRT stream."), memory.Effect285 ?? throw new NotSupportedException("Effect285 requires its retained state.")) { Staging = NativeFileStaging };
        NativeBattleEffect285Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect285 requires its color tables."), _ => nativeActionPackets, (_, address) => NativeBattleRaldo19ModelMemory.Capture(graphics.Models[NativeEffect285Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect285 requires its current projection."), _ => CaptureNativeEffect285Camera(Camera()), Point, SelectIo, (current, operation) => ExecuteNativeEffect285Operation(ref graphics, current, operation, resources));
        frame = initialize ? NativeBattleEffect285Controller.Initialize(frame, resources, inputs) : NativeBattleEffect285Controller.Advance(frame, resources, inputs);
        PublishNativeEffect285Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect285 requires its current camera.");

        NativeBattleEffect285PointStep Point(NativeBattleEffect285Frame current, uint address, NativeBattleEffectArena local, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect285Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect285 requires its current primary skeleton.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
            int selector = BinaryPrimitives.ReadInt16LittleEndian(local.Read(6, 2));
            if ((uint)selector >= (uint)bones.Length)
            {
                throw new NotSupportedException("Effect285 bone point leaves its current physical rig.");
            }
            NativeBattleEffect285BonePointResult point = NativeBattleEffect285BonePoint.Transform(current.Memory, model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect285 requires its current world matrix."), bones[selector], local, retained);
            return new(current with { Memory = point.Memory }, point.Output8);
        }

        NativeBattleEffect285IoCompletionStep SelectIo(NativeBattleEffect285Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueNativeEffect285File(current, resources, request) : current, !poll.Busy);
        }
    }

    private NativeBattleEffect285Frame QueueNativeEffect285File(NativeBattleEffect285Frame current, NativeBattleEffect285Resources resources, NativeBattleEffectFileRequest request)
    {
        NativeBattleEffectFileAsset file = resources.ReadFile(request.FileId);
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect285 requires its current file destination.");
        current = current with { Memory = current.Memory with { Arena = arena.Write(offset, file.Data.AsSpan()) } };
        QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
        return current;
    }

    private static int NativeEffect285Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect285 requires an actual current model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect285Frame(NativeBattleEffect285Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect285 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect285 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect285Frame ImportNativeEffect285Frame(NativeBattleEffect285Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect285 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect285 ?? throw new NotSupportedException("Effect285 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect285 lost its CRT stream.") };
    }
}
