namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState ScheduleNativeTextureAnimation(NativeBattleGraphicsState graphics, int index, NativeBattleAuxiliaryProgram program, byte sequence)
    {
        return graphics with { Auxiliary = graphics.Auxiliary.AppendTextureAnimation(index, sequence, program) };
    }

    private void ApplyNativeTextureMotion(NativeBattleGraphicsState graphics, int index, byte selector, int variant)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        if ((model.Flags0 & 2) == 0)
        {
            return;
        }
        NativeBattleGraphicsResource resource = (FindNativeGraphicsBinding(graphics.ActorIds[index] ?? throw new InvalidDataException("Native50C780 lost its graphical allocation identity.")) ?? throw new InvalidDataException("Native50C780 lost the original body resource DWORD132.")).Resource;
        NativeBattleTextureCopy? copy = NativeBattleTextureMotion.Plan50C780(resource.TextureAnimations, model.TextureAllocationMask, selector, variant);
        if (copy is null)
        {
            return;
        }
        if (model.BodyTextureSlots is null)
        {
            throw new NotSupportedException("Native50C780 requires its original model TIMs already uploaded by507400; migrate the named standalone snapshot explicitly.");
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50C780 requires its retained process texture owner.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native50C780 requires its explicit505EB0 queue and VRAM owner.");
        NativeEffectMemory = memory with { Textures = textures.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY) };
    }
}
