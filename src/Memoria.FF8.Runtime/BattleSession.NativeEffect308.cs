using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect308(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect308 requires its current effect.");
        NativeBattleEffect308Resources resources = effect.Binding.Effect308Resources ?? throw new NotSupportedException("Effect308 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect308 requires current process memory.");
        NativeBattleEffect308State state = memory.Effect308 ?? throw new NotSupportedException("Effect308 requires its retained state.");
        NativeBattleEffect308Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect308 requires its current CRT stream."), state) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect308 requires its current addressed memory.") };
        NativeBattleEffect308Inputs? inputs = null;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect308 requires its color tables."), (_, address) => CaptureNativeEffect301Model(graphics.Models[NativeEffect308Actor(graphics, address)]), (_, address) => graphics.Models[NativeEffect308Actor(graphics, address)], ReadBody, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect308 requires its current projection."), _ => Camera().Roll, Point, _ => nativeActionPackets, SelectIo, Random, (current, operation) => ExecuteNativeEffect308Operation(ref graphics, current, operation, resources, inputs!));
        frame = initialize ? NativeBattleEffect308Controller.Initialize(frame, resources, inputs) : NativeBattleEffect308Controller.Advance(frame, resources, inputs);
        PublishNativeEffect308Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect308 requires its current camera.");

        NativeBattleEffect308BodySource ReadBody(NativeBattleEffect308Frame current, uint address)
        {
            int actor = NativeEffect308Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect308 requires its current physical rig.");
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(model.SequenceState ?? throw new NotSupportedException("Effect308 requires its current decoded pose."), pose);
            System.Collections.Immutable.ImmutableArray<NativeBattlePackedMatrix32> bones = ReadNativeEffect308PackedBones(graphics, current, actor, pose);
            (uint geometry, Func<int, int, byte[]> read) = ReadNativeEffect308Geometry(graphics, current, actor);
            int length = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect308 requires its current CPU scratch capacity.")) * 8);
            return new(pose, decoded, bones, geometry, Read) { VertexScratchAddress = checked(0x196B018u - (uint)length), VertexScratchLength = length };

            NativeBattleEffectArena Read(uint selected, int count)
            {
                long offset = (long)selected - geometry;
                if (offset < 0 || offset > int.MaxValue)
                {
                    throw new InvalidDataException("Effect308 geometry leaves its current addressed owner.");
                }
                return NativeBattleEffectArena.Clear().Write(0, read(checked((int)offset), count));
            }
        }

        NativeBattleEffect308PointStep Point(NativeBattleEffect308Frame current, uint address, int selector, int fraction, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect308Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect308 point queries require their current addressed memory.");
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect308 requires its current skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            retained = retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained);
        }

        NativeBattleEffect308PollStep SelectIo(NativeBattleEffect308Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueNativeEffect308File(current, resources, request) : current, poll.Busy);
        }

        NativeBattleEffect308RandomStep Random(NativeBattleEffect308Frame current)
        {
            NativeBattleEffectMemory drawn = current.Memory.DrawSharedEffectRandom7059E0(out ushort value);
            return new(current with { Memory = drawn }, value);
        }
    }

    private NativeBattleEffect308Frame QueueNativeEffect308File(NativeBattleEffect308Frame frame, NativeBattleEffect308Resources resources, NativeBattleEffectFileRequest request)
    {
        NativeBattleEffectFileAsset file = resources.Files.TryGetValue(request.FileId, out NativeBattleEffectFileAsset? selected) ? selected : throw new NotSupportedException("Effect308 selected an unsupported original completion file.");
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect308 requires its current file destination.");
        frame = frame with { Memory = frame.Memory with { Arena = arena.Write(offset, file.Data.AsSpan()) } };
        QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
        return frame;
    }

    private void PublishNativeEffect308Frame(NativeBattleEffect308Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect308 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect308 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect308Frame ImportNativeEffect308Frame(NativeBattleEffect308Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect308 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect308 ?? throw new NotSupportedException("Effect308 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect308 lost its CRT stream.") };
    }

    private static int NativeEffect308Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect308 requires an actual physical model address.");
        }
        return checked((int)(offset / 156));
    }
}
