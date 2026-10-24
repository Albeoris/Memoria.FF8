using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect324Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect324(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect324 requires its current effect.");
        NativeBattleEffect324Resources resources = effect.Binding.Effect324Resources ?? throw new NotSupportedException("Effect324 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect324 requires current process memory.");
        NativeBattleEffect324Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect324 requires its current CRT stream."), memory.Effect324 ?? throw new NotSupportedException("Effect324 requires its retained state.")) { Staging = NativeFileStaging };
        NativeBattleEffect324Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect324 requires its color tables."), _ => graphics.Flags, _ => nativeActionPackets, (_, address) => CaptureNativeEffect324Model(graphics.Models[NativeEffect324Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect324 requires its current projection."), _ => Camera().Roll, Point, ReadBones, ReadColor, (current, handle) => new(current, (NativeVibrationProcess ?? throw new NotSupportedException("Effect324 requires its current vibration process.")).IsPlaying(handle)), (current, operation) => ExecuteNativeEffect324Operation(ref graphics, current, operation, resources));
        frame = initialize ? NativeBattleEffect324Controller.Initialize(frame, resources, inputs) : NativeBattleEffect324Controller.Advance(frame, resources, inputs);
        PublishNativeEffect324Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect324 requires its current camera.");

        NativeBattleEffect324PointStep Point(NativeBattleEffect324Frame current, uint address, short selector, short fraction, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect324Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect324 requires its current addressed memory.");
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect324 requires its current skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), fraction);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            retained = retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained);
        }

        ImmutableArray<NativeBattlePackedMatrix32> ReadBones(NativeBattleEffect324Frame current, uint address)
        {
            int actor = NativeEffect324Actor(graphics, address);
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect324 requires its current primary skeleton.");
            return NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], source).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
        }

        uint ReadColor(NativeBattleEffect324Frame current, uint address)
        {
            return address == 0xB8B7D8 ? (NativeStage?.ModelColors ?? throw new NotSupportedException("Effect324 requires its current stage color.")).Color40 : throw new NotSupportedException("Effect324 selected an unsupported stage color address.");
        }
    }

    private static int NativeEffect324Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect324 requires an actual current model address.");
        }
        return checked((int)(offset / 156));
    }

    private static NativeBattleEffectArena CaptureNativeEffect324Model(NativeBattleGraphicsModel model)
    {
        NativeBattleEffectArena bytes = CaptureNativeEffect210Model(model);
        if (model.Color40 is uint color)
        {
            bytes = bytes.Write(0x28, BitConverter.GetBytes(color));
        }
        if (model.Alpha7 is byte alpha)
        {
            bytes = bytes.Write(7, new byte[] { alpha });
        }
        if (model.SequenceState is { } sequence)
        {
            bytes = bytes.Write(0x7C, BitConverter.GetBytes(sequence.MeshMask124));
        }
        return bytes;
    }

    internal static NativeBattleGraphicsModel ApplyNativeEffect324ModelBytes(NativeBattleGraphicsModel model, NativeBattleEffect324WriteModelBytes write)
    {
        return (write.Offset, write.Length) switch
        {
            (0, 2) => model with { Flags0 = BinaryPrimitives.ReadUInt16LittleEndian(write.Data.Read(0, 2)) },
            (7, 1) => model with { Alpha7 = write.Data.Read(0, 1)[0] },
            (0x28, 4) => model with { Color40 = BinaryPrimitives.ReadUInt32LittleEndian(write.Data.Read(0, 4)) },
            (0x40, 32) => model with { GeometryWorldMatrix = ReadNativeEffect324Matrix(write.Data) },
            (0x7C, 4) => model with { SequenceState = (model.SequenceState ?? throw new NotSupportedException("Effect324 requires its current mesh mask.")) with { MeshMask124 = BinaryPrimitives.ReadUInt32LittleEndian(write.Data.Read(0, 4)) } },
            _ => throw new NotSupportedException("Effect324 model stores require their actual field widths.")
        };
    }

    private static NativeBattleActionMatrix ReadNativeEffect324Matrix(NativeBattleEffectArena data)
    {
        (byte[] bytes, bool[] known) = data.ReadSnapshot(0, 32);
        return new NativeBattlePackedMatrix32(bytes.ToImmutableArray(), known.ToImmutableArray()).ReadMatrix();
    }

    private void PublishNativeEffect324Frame(NativeBattleEffect324Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect324 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect324 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect324Frame ImportNativeEffect324Frame(NativeBattleEffect324Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect324 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect324 ?? throw new NotSupportedException("Effect324 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect324 lost its CRT stream.") };
    }
}
