using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect321Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect321(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect321 requires its current effect.");
        NativeBattleEffect321Resources resources = effect.Binding.Effect321Resources ?? throw new NotSupportedException("Effect321 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect321 requires its current process memory.");
        NativeBattleEffect321Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect321 requires its current CRT stream."), memory.Effect321 ?? throw new NotSupportedException("Effect321 requires its retained state.")) { Staging = NativeFileStaging };
        NativeBattleEffect321Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect321 requires its color tables."), _ => graphics.Flags, _ => nativeActionPackets, (_, address) => CaptureNativeEffect210Model(graphics.Models[NativeEffect321Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect321 requires its current projection."), _ => Camera().Roll, Anchor, Center, (current, handle) => new(current, (NativeVibrationProcess ?? throw new NotSupportedException("Effect321 requires its current vibration process.")).IsPlaying(handle)), (current, operation) => ExecuteNativeEffect321Operation(ref graphics, current, operation, resources)) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect321Controller.Initialize(frame, resources, inputs) : NativeBattleEffect321Controller.Advance(frame, resources, inputs);
        PublishNativeEffect321Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect321 requires its current camera.");

        NativeBattleEffect321PointStep Point(NativeBattleEffect321Frame current, uint address, byte selector, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect321Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect321 point queries require their current addressed memory.");
            NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect321 requires its current skeleton.");
            NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, selector, 0);
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, producer);
            NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
            return new(current with { Memory = observed, Staging = NativeFileStaging }, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }

        NativeBattleEffect321PointStep Anchor(NativeBattleEffect321Frame current, uint address, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[NativeEffect321Actor(graphics, address)];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect321 requires its current anchor transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattleEffect321PointStep first = Point(current, address, 0xF0, retained);
            NativeBattleEffect321PointStep second = Point(first.Frame, address, 0xF1, first.Output8);
            for (int axis = 0; axis < 3; axis++)
            {
                int left = BinaryPrimitives.ReadInt16LittleEndian(first.Output8.Read(axis * 2, 2));
                int right = BinaryPrimitives.ReadInt16LittleEndian(second.Output8.Read(axis * 2, 2));
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((left + right) / 2))));
            }
            return new(second.Frame, retained.Write(6, BitConverter.GetBytes(transform.Angle36)));
        }

        NativeBattleEffect321PointStep Center(NativeBattleEffect321Frame current, ushort mask, NativeBattleEffectArena retained)
        {
            NativeBattleVector point = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Effect321 requires actual current target positions.")).ToArray());
            return new(current, retained.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z)));
        }
    }

    private static int NativeEffect321Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect321 requires an actual current model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect321Frame(NativeBattleEffect321Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect321 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect321 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect321Frame ImportNativeEffect321Frame(NativeBattleEffect321Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect321 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect321 ?? throw new NotSupportedException("Effect321 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect321 lost its CRT stream.") };
    }

    private bool TryWriteNativeEffect321Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: Entry } effect || address is not (RootBank + 14 or RootBank + 30))
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect321 completion requires its current process memory.");
        NativeBattleEffect321Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect321 completion requires its retained CRT stream."), memory.Effect321 ?? throw new NotSupportedException("Effect321 completion requires its physical state.")) { Staging = NativeFileStaging };
        frame = NativeBattleEffect321Controller.WriteCompletionByte(frame, address, value);
        PublishNativeEffect321Frame(frame);
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
