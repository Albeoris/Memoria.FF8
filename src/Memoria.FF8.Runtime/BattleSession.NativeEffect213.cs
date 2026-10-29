using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect213(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect213 requires its current effect.");
        NativeBattleEffect213Resources resources = effect.Binding.Effect213Resources ?? throw new NotSupportedException("Effect213 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect213 requires its current process memory.");
        NativeBattleEffect213Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect213 requires its current CRT stream."), memory.Effect213 ?? throw new NotSupportedException("Effect213 requires its retained state.")) { Staging = NativeFileStaging };
        NativeBattleEffect213Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect213 requires its color tables."), _ => graphics.Flags, _ => nativeActionPackets, (_, address) => CaptureNativeEffect210Model(graphics.Models[NativeEffect213Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect213 requires its current projection."), _ => Camera().Roll, Point, (current, address) => CountNativeEffect213Vertices(graphics, current, address), (current, address, ordinal, retained) => SelectNativeEffect213Vertex(ref graphics, current, address, ordinal, retained), (current, operation) => ExecuteNativeEffect213Operation(ref graphics, current, operation, resources)) { CapturePresentation = true };
        frame = initialize ? NativeBattleEffect213Controller.Initialize(frame, resources, inputs) : NativeBattleEffect213Controller.Advance(frame, resources, inputs);
        PublishNativeEffect213Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect213 requires its current camera.");

        NativeBattleEffect213PointStep Point(NativeBattleEffect213Frame current, uint address, short selector, short fraction, NativeBattleEffectArena retained)
        {
            int actor = NativeEffect213Actor(graphics, address);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained);
            }
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect213 point queries require their current addressed memory.");
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect213 point queries require their current skeleton.");
            NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, checked((byte)selector), fraction));
            current = current with { Memory = observed, Staging = NativeFileStaging };
            for (int axis = 0; axis < 3; axis++)
            {
                int value = observed.ScriptedColorRegisters!.Mac[axis] ?? throw new NotSupportedException("Effect213 point queries lost their current MAC.");
                retained = retained.Write(axis * 2, BitConverter.GetBytes(unchecked((short)value)));
            }
            return new(current, retained);
        }
    }

    private static int NativeEffect213Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect213 requires an actual current model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect213Frame(NativeBattleEffect213Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect213 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect213 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect213Frame ImportNativeEffect213Frame(NativeBattleEffect213Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect213 lost its process memory.");
        return frame with { Memory = memory, State = memory.Effect213 ?? throw new NotSupportedException("Effect213 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect213 lost its CRT stream.") };
    }
}
