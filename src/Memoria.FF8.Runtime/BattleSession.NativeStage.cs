namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleStageState? NativeStage { get; private set; }

    private void LoadNativeStage500EA0()
    {
        NativeBattleStageSource source = definition.NativeStageSource ?? throw new NotSupportedException("Native500EA0 requires its installed stage skeleton, animations and TIM; migrate the standalone definition explicitly.");
        NativeStage = (NativeStage ?? throw new NotSupportedException("Native500EA0 requires its retained process stage rows; migrate the standalone snapshot explicitly.")).Initialize500EA0(source);
        if (source.Callback?.Address == NativeBattleFinalStage142.CallbackAddress)
        {
            NativeBattleFinalStage142State specialized = NativeStage.FinalStage142 ?? throw new NotSupportedException("Native512500 requires its retained final-stage process owner.");
            specialized = NativeBattleFinalStage142.Initialize0(specialized);
            if (source.Texture.Data.Length < 0x14 + 8192)
            {
                throw new InvalidDataException("Native512500 requires its original full stage CLUT.");
            }
            NativeStage = NativeStage with { FinalStage142 = specialized with { ClutSource = new(source.Texture.Identity + ":CLUT", source.Texture.Data.Slice(0x14, 8192)), GenericCursor = 0x1941998 } };
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native500FB0 requires its retained original texture upload owner.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native500FB0 requires the retained505E30 queue and VRAM.");
        (textures, _) = textures.EnqueueTim505E30(source.Texture);
        NativeEffectMemory = memory with { Textures = textures };
    }

    private void RegisterNativeStage506DE0()
    {
        NativeBattleStageCallback callback = definition.NativeStageSource?.Callback ?? throw new NotSupportedException("Native506DE0 requires the installed stage initializer's original callback binding.");
        if (callback.UpdateTexture0 is null && callback.Address != NativeBattleFinalStage142.CallbackAddress)
        {
            throw new NotSupportedException($"Native stage callback{callback.Address:X} requires its original specialized stage service.");
        }
        NativeStage = (NativeStage ?? throw new NotSupportedException("Native506DE0 requires its retained stage rows.")) with { Task = new(callback, 0) };
        InitializeNativeStageCamera();
    }

    private void LoadNativeStagePalette506CF0()
    {
        NativeBattleStageState state = NativeStage ?? throw new NotSupportedException("Native506CF0 palette requires its initialized stage owner.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native506CF0 palette requires its original texture and packet owners.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native506CF0 palette requires its retained packet-bank process.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native506CF0 palette requires its original505DF0 queue.");
        (NativeBattlePrimitiveProcess next, NativeBattleStagePaletteAllocation allocation) = NativeBattleStagePalette.Write506CF0(process);
        textures = textures.EnqueueRectangle505DF0(new(0, 239, 256, 1), allocation.Source);
        NativeStage = state with { PaletteAllocation = allocation };
        NativeEffectMemory = memory with { PrimitiveProcess = next, Textures = textures };
    }

    private NativeBattleGraphicsState AdvanceNativeStage506C30(NativeBattleGraphicsState graphics)
    {
        NativeBattleStageState state = NativeStage ?? throw new NotSupportedException("Native506C30 requires its retained process stage callback owner; migrate the standalone snapshot explicitly.");
        if (state.Task is not { } task)
        {
            return graphics;
        }
        if (task.Callback.Address == NativeBattleFinalStage142.CallbackAddress)
        {
            return AdvanceNativeFinalStage142(graphics, state, task);
        }
        if (task.Phase == 0)
        {
            NativeStage = state with { Task = task with { Phase = 1 } };
            return graphics;
        }
        if (task.Phase != 1)
        {
            return graphics;
        }
        if (task.Callback.UpdateTexture0 == true && (state.Flags & 2) == 0)
        {
            NativeBattleStageTextureStep motion = NativeBattleStageTextureMotion.Advance50CB20(state.TextureAnimations, 0);
            state = state with { TextureAnimations = motion.Source };
            if (!motion.Copies.IsEmpty)
            {
                NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native stage texture motion requires its retained effect memory.");
                NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native stage texture motion requires the original505EB0 copy queue.");
                foreach (NativeBattleTextureCopy copy in motion.Copies)
                {
                    textures = textures.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
                }
                NativeEffectMemory = memory with { Textures = textures };
            }
        }
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native500FD0 requires its prior5033E0 camera matrix; a final-frame camera reconstruction is unsupported.");
        NativeStage = state.Step500FD0(projection.View ?? throw new NotSupportedException("Native500FD0 requires its actual prior5033E0 view matrix."));
        return SubmitNativeStagePackets(graphics, NativeStage);
    }
}
