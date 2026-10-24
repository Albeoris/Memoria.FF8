using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect290(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect290 requires its current effect.");
        NativeBattleEffect290Resources resources = effect.Binding.Effect290Resources ?? throw new NotSupportedException("Effect290 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect290 requires current process memory.");
        NativeBattleEffect290State state = memory.Effect290 ?? throw new NotSupportedException("Effect290 requires its retained state.");
        (NativeBattleEffect290State boundState, NativeBattleFileStaging staging) = state.BindSources(NativeFileStaging ?? throw new NotSupportedException("Effect290 requires its current addressed memory."), resources);
        NativeBattleEffect290Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect290 requires its current CRT stream."), boundState) { Staging = staging };
        NativeBattleEffect290Inputs? inputs = null;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect290 requires its color tables."), (_, address) => CaptureNativeEffect301Model(graphics.Models[NativeEffect290Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect290 requires its current projection."), _ => Camera().Roll, Point, SelectIo, Random, (current, operation) => ExecuteNativeEffect290Operation(ref graphics, current, operation, resources, inputs!))
        {
            AdvanceLower = NativeBattleEffect290Lower.Advance,
            AdvanceProgramme = NativeBattleEffect290Programme.Advance
        };
        frame = initialize ? NativeBattleEffect290Controller.Initialize(frame, resources, inputs) : NativeBattleEffect290Controller.Advance(frame, resources, inputs);
        PublishNativeEffect290Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect290 requires its current camera.");

        NativeBattleEffect290PointStep Point(NativeBattleEffect290Frame current, uint address, int selector, int fraction, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect290Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect290 point queries require their current addressed memory.");
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect290 requires its current skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            retained = retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained);
        }

        NativeBattleEffect290PollStep SelectIo(NativeBattleEffect290Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueNativeEffect290File(current, resources, request) : current, !poll.Busy);
        }

        NativeBattleEffect290RandomStep Random(NativeBattleEffect290Frame current)
        {
            NativeBattleEffectMemory drawn = current.Memory.DrawSharedEffectRandom7059E0(out ushort value);
            return new(current with { Memory = drawn }, value);
        }
    }

    private NativeBattleEffect290Frame QueueNativeEffect290File(NativeBattleEffect290Frame frame, NativeBattleEffect290Resources resources, NativeBattleEffectFileRequest request)
    {
        NativeBattleEffectFileAsset file = resources.ReadFile(request.FileId);
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect290 requires its current file destination.");
        frame = frame with { Memory = frame.Memory with { Arena = arena.Write(offset, file.Data.AsSpan()) } };
        QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
        return frame;
    }

    private void PublishNativeEffect290Frame(NativeBattleEffect290Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect290 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect290 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect290Frame ImportNativeEffect290Frame(NativeBattleEffect290Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect290 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect290 ?? throw new NotSupportedException("Effect290 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect290 lost its CRT stream.") };
    }

    private static int NativeEffect290Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect290 requires an actual physical model address.");
        }
        return checked((int)(offset / 156));
    }
}
