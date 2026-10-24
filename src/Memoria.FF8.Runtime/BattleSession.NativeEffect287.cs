using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect287(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect287 requires its current effect.");
        NativeBattleEffect287Resources resources = effect.Binding.Effect287Resources ?? throw new NotSupportedException("Effect287 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect287 requires its current process memory.");
        NativeBattleEffect287Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect287 requires its current CRT stream."), memory.Effect287 ?? throw new NotSupportedException("Effect287 requires its retained state.")) { Staging = NativeFileStaging };
        NativeBattleEffect287Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect287 requires its color tables."), _ => nativeActionPackets, (current, address) => CaptureNativeEffect287Model(graphics, current, address), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect287 requires its current projection."), _ => Camera().Roll, (current, directory, bone) => ReadNativeEffect287BoneMatrix(graphics, current, directory, bone), (current, point, retained) => NativeEffect287PointMac(graphics, current, point, retained), (current, model, selector, retained) => NativeEffect287LocalPoint(graphics, current, model, selector, retained), (current, model, retained) => NativeEffect287Anchor(graphics, current, model, retained), SelectIo, Random, (current, operation) => ExecuteNativeEffect287Operation(ref graphics, current, operation, resources));
        frame = initialize ? NativeBattleEffect287Controller.Initialize(frame, resources, inputs) : NativeBattleEffect287Controller.Advance(frame, resources, inputs);
        PublishNativeEffect287Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect287 requires its current camera.");

        NativeBattleEffect287PollStep SelectIo(NativeBattleEffect287Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueNativeEffect287File(current, resources, request) : current, !poll.Busy);
        }

        NativeBattleEffect287RandomStep Random(NativeBattleEffect287Frame current)
        {
            NativeBattleEffectMemory drawn = current.Memory.DrawSharedEffectRandom7059E0(out ushort value);
            return new(current with { Memory = drawn }, value);
        }
    }

    private NativeBattleEffect287Frame QueueNativeEffect287File(NativeBattleEffect287Frame frame, NativeBattleEffect287Resources resources, NativeBattleEffectFileRequest request)
    {
        NativeBattleEffectFileAsset file = resources.ReadFile(request.FileId);
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect287 requires its current file destination.");
        frame = frame with { Memory = frame.Memory with { Arena = arena.Write(offset, file.Data.AsSpan()) } };
        QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
        return frame;
    }

    private void PublishNativeEffect287Frame(NativeBattleEffect287Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect287 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect287 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect287Frame ImportNativeEffect287Frame(NativeBattleEffect287Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect287 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect287 ?? throw new NotSupportedException("Effect287 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect287 lost its CRT stream.") };
    }
}
