using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect301(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect301 requires its current effect.");
        effect = effect with { PresentationPackets = [] };
        NativeBattleEffect301Resources resources = effect.Binding.Effect301Resources ?? throw new NotSupportedException("Effect301 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect301 requires its current process memory.");
        NativeBattleEffect301Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect301 requires its current CRT stream."), memory.Effect301 ?? throw new NotSupportedException("Effect301 requires its retained state.")) { Staging = NativeFileStaging, CapturePresentation = true };
        NativeBattleEffect301Inputs? inputs = null;
        inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect301 requires its color tables."), (_, address) => CaptureNativeEffect301Model(graphics.Models[NativeEffect301Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect301 requires its current projection."), _ => Camera().Roll, Anchor, (current, operation) => ExecuteNativeEffect301Operation(ref graphics, current, operation, resources, inputs!));
        frame = initialize ? NativeBattleEffect301Controller.Initialize(frame, resources, inputs) : NativeBattleEffect301Controller.Advance(frame, resources, inputs);
        PublishNativeEffect301Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect301 requires its current camera.");

        NativeBattleEffect301PointStep Point(NativeBattleEffect301Frame current, uint address, byte selector, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect301Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect301 point queries require their current addressed memory.");
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect301 requires its current skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, selector, 0);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }

        NativeBattleEffect301PointStep Anchor(NativeBattleEffect301Frame current, uint address, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[NativeEffect301Actor(graphics, address)];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect301 requires its current anchor transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect301PointStep first = Point(current, address, 0xF0, retained);
            NativeBattleEffect301PointStep second = Point(first.Frame, address, 0xF1, first.Output8);
            for (int axis = 0; axis < 3; axis++)
            {
                int left = BinaryPrimitives.ReadInt16LittleEndian(first.Output8.Read(axis * 2, 2));
                int right = BinaryPrimitives.ReadInt16LittleEndian(second.Output8.Read(axis * 2, 2));
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((left + right) / 2))));
            }
            return new(second.Frame, retained.Write(6, BitConverter.GetBytes(transform.Angle36)));
        }
    }

    private void PublishNativeEffect301Frame(NativeBattleEffect301Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect301 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect301 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect301Frame ImportNativeEffect301Frame(NativeBattleEffect301Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect301 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect301 ?? throw new NotSupportedException("Effect301 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect301 lost its CRT stream.") };
    }
}
