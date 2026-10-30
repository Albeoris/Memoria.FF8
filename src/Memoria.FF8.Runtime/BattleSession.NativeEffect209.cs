using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect209(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = (graphics.OriginalEffect ?? throw new NotSupportedException("Effect209 requires its selected effect.")) with { PresentationPackets = [] };
        NativeBattleEffect209Resources resources = effect.Binding.Effect209Resources ?? throw new NotSupportedException("Effect209 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect209 requires its current process memory.");
        NativeBattleEffect209Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect209 requires its CRT stream."), memory.Effect209 ?? throw new NotSupportedException("Effect209 requires its retained storage.")) { CapturePresentation = true, Staging = NativeFileStaging };
        NativeBattleEffect209Inputs? inputs = null;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect209 requires its arithmetic constants."), _ => graphics.Flags, current => new(current, (NativeEffectIoResult ?? throw new NotSupportedException("Effect209 requires its current I/O result.")) < 0 ? -1 : NativeEffectIoResult.Value), _ => nativeActionPackets, (_, actor) => graphics.Models[actor], _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect209 requires its current projection."), _ => Camera().Roll, _ => unchecked((short)(Camera().FocalLength ?? throw new NotSupportedException("Effect209 requires its scalar focal WORD."))), current => current.Memory.PrimitiveProcess?.Buffer80 ?? throw new NotSupportedException("Effect209 requires its current display side."), (_, address) => ReadColor(address), Anchor, Point, (_, handle) => (NativeVibrationProcess ?? throw new NotSupportedException("Effect209 requires its vibration process.")).IsPlaying(handle), (current, operation) => ExecuteNativeEffect209Operation(ref graphics, current, operation, resources, inputs!));
        frame = initialize ? NativeBattleEffect209Controller.Initialize(frame, resources, inputs) : NativeBattleEffect209Controller.Advance(frame, resources, inputs);
        PublishNativeEffect209Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect209 requires its current camera.");

        uint ReadColor(uint address)
        {
            NativeBattleStageColors colors = NativeStage?.ModelColors ?? throw new NotSupportedException("Effect209 requires its current global model colors.");
            return address switch
            {
                0xB8B7D8 => colors.Color40,
                0xB8B9A8 => colors.Color44,
                _ => throw new NotSupportedException("Effect209 selected an unsupported color owner.")
            };
        }

        NativeBattleEffect209PointStep Point(NativeBattleEffect209Frame current, int actor, byte selector, short offset, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained);
            }
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect209 requires its current actor skeleton.");
            NativeFileStaging = current.Staging;
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, offset));
            current = current with { Memory = observed, Staging = NativeFileStaging };
            for (int axis = 0; axis < 3; axis++)
            {
                int value = observed.ScriptedColorRegisters!.Mac[axis] ?? throw new NotSupportedException("Effect209 lost its current anchor MAC.");
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)value)));
            }
            return new(current, retained);
        }

        NativeBattleEffect209PointStep Anchor(NativeBattleEffect209Frame current, int actor, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect209 requires its current actor transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect209PointStep first = Point(current, actor, 0xF0, 0, retained);
            NativeBattleEffect209PointStep second = Point(first.Frame, actor, 0xF1, 0, first.Output8);
            for (int axis = 0; axis < 3; axis++)
            {
                int left = BinaryPrimitives.ReadInt16LittleEndian(first.Output8.Read(axis * 2, 2));
                int right = BinaryPrimitives.ReadInt16LittleEndian(second.Output8.Read(axis * 2, 2));
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((left + right) / 2))));
            }
            return new(second.Frame, retained.Write(6, BitConverter.GetBytes(transform.Angle36)));
        }
    }

    private void PublishNativeEffect209Frame(NativeBattleEffect209Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect209 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect209 lost its addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect209Frame ImportNativeEffect209Frame(NativeBattleEffect209Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Effect209 service lost its current memory.");
        return frame with { Memory = memory, State = memory.Effect209 ?? throw new InvalidDataException("Effect209 service lost its retained storage."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Effect209 service lost its CRT stream.") };
    }
}
