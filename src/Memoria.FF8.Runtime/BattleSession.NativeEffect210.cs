using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect210(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect210 requires its selected effect.");
        NativeBattleEffect210Resources resources = effect.Binding.Effect210Resources ?? throw new NotSupportedException("Effect210 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect210 requires its current process memory.");
        NativeBattleEffect210Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect210 requires its CRT stream."), memory.Effect210 ?? throw new NotSupportedException("Effect210 requires its retained storage.")) { Staging = NativeFileStaging };
        NativeBattleEffect210Inputs? inputs = null;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect210 requires its arithmetic constants."), _ => graphics.Flags, _ => nativeActionPackets, (_, actor) => graphics.Models[actor], (_, actor) => CaptureNativeEffect210Model(graphics.Models[actor]), ReadBones, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect210 requires its current projection."), _ => Camera().Roll, Point, Anchor, PollIo, SelectIo, (current, operation) => ExecuteNativeEffect210Operation(ref graphics, current, operation, resources, inputs!))
        {
            ReadCommonSprite5710B0 = (_, id) => definition.NativeModuleResources.Single(resource => resource.Id == id)
        };

        frame = initialize ? NativeBattleEffect210Controller.Initialize(frame, resources, inputs) : NativeBattleEffect210Controller.Advance(frame, resources, inputs);
        PublishNativeEffect210Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect210 requires its current camera.");

        ImmutableArray<NativeBattlePackedMatrix32> ReadBones(NativeBattleEffect210Frame current, int actor)
        {
            NativeBattleBodyBoneMatrices bones = graphics.Models[actor].CurrentBodyBoneMatrices ?? throw new NotSupportedException("Effect210 requires its current primary bone publication.");
            return bones.Matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
        }

        NativeBattleEffect210IoStep PollIo(NativeBattleEffect210Frame current)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(current.Memory);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueNativeEffect210File(current, resources, request) : current, poll.Busy);
        }

        NativeBattleEffect210IoCompletionStep SelectIo(NativeBattleEffect210Frame current, int index)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Select5342C0(current.Memory, index);
            current = current with { Memory = poll.Memory };
            return new(poll.Request is { } request ? QueueNativeEffect210File(current, resources, request) : current, !poll.Busy);
        }

        NativeBattleEffect210PointStep Point(NativeBattleEffect210Frame current, int actor, byte selector, short offset, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained);
            }
            NativeFileStaging = current.Staging;
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect210 requires its current actor skeleton.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, offset));
            current = current with { Memory = observed, Staging = NativeFileStaging };
            for (int axis = 0; axis < 3; axis++)
            {
                int value = observed.ScriptedColorRegisters!.Mac[axis] ?? throw new NotSupportedException("Effect210 lost its current anchor MAC.");
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)value)));
            }
            return new(current, retained);
        }

        NativeBattleEffect210PointStep Anchor(NativeBattleEffect210Frame current, int actor, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect210 requires its current actor transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect210PointStep first = Point(current, actor, 0xF0, 0, retained);
            NativeBattleEffect210PointStep second = Point(first.Frame, actor, 0xF1, 0, first.Output8);
            for (int axis = 0; axis < 3; axis++)
            {
                int left = BinaryPrimitives.ReadInt16LittleEndian(first.Output8.Read(axis * 2, 2));
                int right = BinaryPrimitives.ReadInt16LittleEndian(second.Output8.Read(axis * 2, 2));
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((left + right) / 2))));
            }
            return new(second.Frame, retained.Write(6, BitConverter.GetBytes(transform.Angle36)));
        }
    }

    private NativeBattleEffect210Frame QueueNativeEffect210File(NativeBattleEffect210Frame current, NativeBattleEffect210Resources resources, NativeBattleEffectFileRequest request)
    {
        NativeBattleEffectFileAsset file = resources.ReadFile(request.FileId);
        int offset = NativeBattleWaterExecutionFields.ArenaOffset(request.DestinationAddress, file.Data.Length);
        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect210 file loading requires its current destination arena.");
        current = current with { Memory = current.Memory with { Arena = arena.Write(offset, file.Data.AsSpan()) } };
        QueueNativeFileIo482610(file.Data.Length, request.CompletionCallback);
        return current;
    }


    private static NativeBattleEffectArena CaptureNativeEffect210Model(NativeBattleGraphicsModel model)
    {
        NativeBattleEffectArena bytes = NativeBattleRaldo19ModelMemory.Capture(model);
        if (model.GeometryWorldMatrix is { } world)
        {
            NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(world, null);
            for (int index = 0; index < matrix.Bytes.Length; index++)
            {
                bytes = matrix.Known[index] ? bytes.Write(0x40 + index, matrix.Bytes.AsSpan().Slice(index, 1)) : bytes.MarkUnknown(0x40 + index, 1);
            }
        }
        if (model.Transform is { } transform)
        {
            bytes = bytes.Write(0x26, BitConverter.GetBytes(transform.Scale38));
        }
        return bytes;
    }

    private void PublishNativeEffect210Frame(NativeBattleEffect210Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect210 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect210 lost its addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect210Frame ImportNativeEffect210Frame(NativeBattleEffect210Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Effect210 service lost its current memory.");
        return frame with { Memory = memory, State = memory.Effect210 ?? throw new InvalidDataException("Effect210 service lost its retained storage."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Effect210 service lost its CRT stream.") };
    }
}
