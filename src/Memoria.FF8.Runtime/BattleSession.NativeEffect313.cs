using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect313Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect313(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect313 requires its current effect.");
        NativeBattleEffect313Resources resources = effect.Binding.Effect313Resources ?? throw new NotSupportedException("Effect313 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect313 requires current process memory.");
        NativeBattleEffect313Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect313 requires its current CRT stream."), memory.Effect313 ?? throw new NotSupportedException("Effect313 requires its retained state.")) { Staging = NativeFileStaging };
        NativeBattleEffect313Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect313 requires its color tables."), _ => graphics.Flags, _ => nativeActionPackets, (_, address) => CaptureNativeEffect210Model(graphics.Models[NativeEffect313Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect313 requires its current projection."), _ => Camera().Roll, _ => CameraEye(), Point, Anchor, TargetCenter, (_, address) => graphics.Models[NativeEffect313Actor(graphics, address)].Sequence ?? throw new NotSupportedException("Effect313 requires its current sequence header BYTE0."), (current, operation) => ExecuteNativeEffect313Operation(ref graphics, current, operation, resources)) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect313Controller.Initialize(frame, resources, inputs) : NativeBattleEffect313Controller.Advance(frame, resources, inputs);
        PublishNativeEffect313Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect313 requires its current camera.");

        NativeBattleEffect313PointStep Point(NativeBattleEffect313Frame current, uint address, int selector, int fraction, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect313Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect313 requires its current addressed memory.");
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect313 requires its current skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            retained = retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained);
        }

        NativeBattleEffect313PointStep Anchor(NativeBattleEffect313Frame current, uint address, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[NativeEffect313Actor(graphics, address)];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect313 requires its current target transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect313PointStep first = Point(current, address, 0xF0, 0, retained);
            NativeBattleEffect313PointStep second = Point(first.Frame, address, 0xF1, 0, first.Output8);
            for (int axis = 0; axis < 3; axis++)
            {
                int left = BinaryPrimitives.ReadInt16LittleEndian(first.Output8.Read(axis * 2, 2));
                int right = BinaryPrimitives.ReadInt16LittleEndian(second.Output8.Read(axis * 2, 2));
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((left + right) / 2))));
            }
            return new(second.Frame, retained.Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        NativeBattleEffect313PointStep TargetCenter(NativeBattleEffect313Frame current, ushort mask, NativeBattleEffectArena retained)
        {
            NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Effect313 requires its current selected physical target positions.")).ToArray());
            return new(current, retained.Write(0, BitConverter.GetBytes(center.X)).Write(2, BitConverter.GetBytes(center.Y)).Write(4, BitConverter.GetBytes(center.Z)));
        }

        NativeBattleEffectArena CameraEye()
        {
            NativeBattleCameraState camera = Camera();
            NativeBattleCameraPoint eye = (camera.CurrentPose ?? throw new NotSupportedException("Effect313 requires its current camera eye.")).Eye;
            NativeBattleEffectArena packed = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(eye.X)).Write(2, BitConverter.GetBytes(eye.Y)).Write(4, BitConverter.GetBytes(eye.Z));
            return camera.EyePaddingF6 is short padding ? packed.Write(6, BitConverter.GetBytes(padding)) : packed;
        }
    }

    private static int NativeEffect313Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect313 requires an actual current model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect313Frame(NativeBattleEffect313Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect313 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect313 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect313Frame ImportNativeEffect313Frame(NativeBattleEffect313Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect313 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect313 ?? throw new NotSupportedException("Effect313 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect313 lost its CRT stream.") };
    }
}
