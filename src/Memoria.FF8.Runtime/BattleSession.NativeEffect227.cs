using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect227(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = (graphics.OriginalEffect ?? throw new NotSupportedException("Effect227 requires its current effect.")) with { PresentationPackets = [] };
        NativeBattleEffect227Resources resources = effect.Binding.Effect227Resources ?? throw new NotSupportedException("Effect227 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect227 requires current process memory.");
        NativeBattleEffect227Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect227 requires its current CRT stream."), memory.Effect227 ?? throw new NotSupportedException("Effect227 requires its retained state.")) { CapturePresentation = true, Staging = NativeFileStaging };
        NativeBattleEffect227Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect227 requires its color tables."), (_, address) => CaptureNativeEffect210Model(graphics.Models[NativeEffect227Actor(graphics, address)]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect227 requires its current projection."), _ => Camera().Roll, Point, (current, operation) => ExecuteNativeEffect227Operation(ref graphics, current, operation, resources));
        frame = initialize ? NativeBattleEffect227Controller.Initialize(frame, resources, inputs) : NativeBattleEffect227Controller.Advance(frame, resources, inputs);
        PublishNativeEffect227Frame(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect227 requires its current camera.");

        NativeBattleEffect227PointStep Point(NativeBattleEffect227Frame current, uint address, NativeBattleEffectArena local8, NativeBattleEffectArena retained8)
        {
            int actor = NativeEffect227Actor(graphics, address);
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect227 requires its current caster skeleton.");
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Effect227 requires its current addressed memory.");
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
            NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actor], source, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
            staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
            short selector = BinaryPrimitives.ReadInt16LittleEndian(local8.Read(6, 2));
            var matrices = NativeBattleBodyBoneMatrices.Require(prepared.Model, source);
            if ((uint)selector >= (uint)matrices.Length)
            {
                throw new NotSupportedException("Effect227 selected a bone outside its current skeleton.");
            }
            NativeBattleActionMatrix world = prepared.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect227 requires its current world matrix.");
            NativeBattleEffect227BonePointStep selected = NativeBattleEffect227BonePoint.Transform693990(current.Memory, world, matrices[selector], local8, retained8);
            graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
            NativeFileStaging = staging;
            return new(current with { Memory = selected.Memory, Staging = staging }, selected.Output8);
        }
    }

    private static int NativeEffect227Actor(NativeBattleGraphicsState graphics, uint address)
    {
        long offset = (long)address - 0x1D972C0;
        if (offset < 0 || offset % 156 != 0 || offset / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Effect227 requires an actual current model address.");
        }
        return checked((int)(offset / 156));
    }

    private void PublishNativeEffect227Frame(NativeBattleEffect227Frame frame)
    {
        NativeEffectMemory = frame.Memory with { Effect227 = frame.State };
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect227 lost its current addressed memory.");
        nativeCrtRandom = frame.Random;
    }

    private NativeBattleEffect227Frame ImportNativeEffect227Frame(NativeBattleEffect227Frame frame)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect227 lost its current process memory.");
        return frame with { Memory = memory, State = memory.Effect227 ?? throw new NotSupportedException("Effect227 lost its retained state."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new NotSupportedException("Effect227 lost its CRT stream.") };
    }
}
