using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect318Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect318(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect318 requires its current effect.");
        NativeBattleEffect318Resources resources = effect.Binding.Effect318Resources ?? throw new NotSupportedException("Effect318 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect318 requires current process memory.");
        NativeBattleEffect318Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect318 requires its current CRT stream."), memory.Effect318 ?? throw new NotSupportedException("Effect318 requires its retained state.")) { Staging = NativeFileStaging };
        NativeBattleEffect318Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect318 requires its color tables."), _ => nativeActionPackets, (_, address) => CaptureNativeEffect210Model(graphics.Models[NativeEffect318Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect318 requires its current projection."), _ => Camera().Roll, Point, Anchor, ResolveBoneRecord, SelectIo, (current, operation) => ExecuteNativeEffect318Operation(ref graphics, current, operation, resources));
        frame = initialize ? NativeBattleEffect318Controller.Initialize(frame, resources, inputs) : NativeBattleEffect318Controller.Advance(frame, resources, inputs);
        PublishNativeEffect318Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect318 requires its current camera.");

        NativeBattleEffect318PointStep Point(NativeBattleEffect318Frame current, uint address, int selector, int fraction, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect318Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect318 requires its current addressed memory.");
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect318 requires its current skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            retained = retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained);
        }

        NativeBattleEffect318PointStep Anchor(NativeBattleEffect318Frame current, uint address, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[NativeEffect318Actor(graphics, address)];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect318 requires its current target transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect318PointStep first = Point(current, address, 0xF0, 0, retained);
            NativeBattleEffect318PointStep second = Point(first.Frame, address, 0xF1, 0, first.Output8);
            for (int axis = 0; axis < 3; axis++)
            {
                int left = BinaryPrimitives.ReadInt16LittleEndian(first.Output8.Read(axis * 2, 2));
                int right = BinaryPrimitives.ReadInt16LittleEndian(second.Output8.Read(axis * 2, 2));
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((left + right) / 2))));
            }
            return new(second.Frame, retained.Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        uint ResolveBoneRecord(NativeBattleEffect318Frame current, uint address, int bone)
        {
            int actor = NativeEffect318Actor(graphics, address);
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect318 requires its current caster skeleton.");
            _ = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], source);
            return NativeBattleEffect319BoneCache.ResolveRecord(NativeEffect318SkeletonAddress(graphics, current, actor), source.BoneCount, bone);
        }

        NativeBattleEffect318PollStep SelectIo(NativeBattleEffect318Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueNativeEffect318File(current, resources, request) : current, !poll.Busy);
        }
    }

    private NativeBattleEffect318Frame QueueNativeEffect318File(NativeBattleEffect318Frame current, NativeBattleEffect318Resources resources, NativeBattleEffectFileRequest request)
    {
        NativeBattleEffectFileAsset file = resources.ReadFile(request.FileId);
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect318 requires its current file destination arena.");
        current = current with { Memory = current.Memory with { Arena = arena.Write(offset, file.Data.AsSpan()) } };
        QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
        return current;
    }

    private static int NativeEffect318Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect318 requires an actual current model address.");
        }
        return checked((int)(offset / 156));
    }

    private uint NativeEffect318SkeletonAddress(NativeBattleGraphicsState graphics, NativeBattleEffect318Frame frame, int actor)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            NativeBattleResourcePointer? pointer;
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, staged, out pointer))
            {
                pointer = IsNativeEffectArenaAddress(staged) ? frame.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(staged, 4)) : (frame.Staging ?? throw new NotSupportedException("Effect318 requires its staged resource row.")).ReadResourcePointer(staged);
            }
            return pointer?.Cpu?.PhysicalAddress ?? pointer?.StagingAddress ?? throw new NotSupportedException("Effect318 requires its current addressed skeleton pointer.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect318 requires its current CPU resource owner.");
        return cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Effect318 requires its current primary resource row.")].Require(12).PhysicalAddress;
    }

    private void PublishNativeEffect318Frame(NativeBattleEffect318Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect318 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect318 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect318Frame ImportNativeEffect318Frame(NativeBattleEffect318Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect318 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect318 ?? throw new NotSupportedException("Effect318 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect318 lost its CRT stream.") };
    }
}
