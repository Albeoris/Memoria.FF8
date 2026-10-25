namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleStageState? NativeStage { get; private set; }

    private void LoadNativeStage500EA0()
    {
        NativeBattleStageSource source = definition.NativeStageSource ?? throw new NotSupportedException("Native500EA0 requires its installed stage skeleton, animations and TIM; migrate the standalone definition explicitly.");
        NativeBattleStagePhysicalSource physical = source.PhysicalSource ?? throw new NotSupportedException("Native500EA0 requires its authenticated shared resource prefix.");
        (NativeBattleFileStaging staging, NativeBattleModelCpuResources cpu) = physical.Publish500EA0(NativeFileStaging ?? throw new NotSupportedException("Native500EA0 requires its current shared resource memory."), NativeModelCpuResources ?? throw new NotSupportedException("Native500EA0 requires its current model heap cursor and vertex capacity."));
        NativeFileStaging = staging;
        NativeModelCpuResources = cpu;
        NativeStage = (NativeStage ?? throw new NotSupportedException("Native500EA0 requires its retained process stage rows; migrate the standalone snapshot explicitly.")).Initialize500EA0(source, NativeGraphics?.Callback);
        if (source.Callback?.Address == NativeBattleFinalStage142.CallbackAddress)
        {
            NativeBattleFinalStage142State specialized = NativeStage.FinalStage142 ?? throw new NotSupportedException("Native512500 requires its retained final-stage process owner.");
            specialized = NativeBattleFinalStage142.Initialize0(specialized);
            if (source.Texture.Data.Length < 0x14 + 8192)
            {
                throw new InvalidDataException("Native512500 requires its original full stage CLUT.");
            }
            (NativeBattleModelCpuResources reservedCpu, uint genericCursor) = physical.Reserve512500(NativeModelCpuResources);
            NativeModelCpuResources = reservedCpu;
            NativeStage = NativeStage with { FinalStage142 = specialized with { ClutSource = new(source.Texture.Identity + ":CLUT", source.Texture.Data.Slice(0x14, 8192)), GenericCursor = genericCursor } };
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native500FB0 requires its retained original texture upload owner.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native500FB0 requires the retained505E30 queue and VRAM.");
        (textures, _) = textures.EnqueueTim505E30(source.Texture);
        NativeEffectMemory = memory with { Textures = textures };
        if (source.Callback?.Address == NativeBattleStage37.CallbackAddress)
        {
            InitializeNativeStage37(source);
        }
        if (source.Callback?.Address == NativeBattleStage137.CallbackAddress)
        {
            NativeModelCpuResources = physical.Reserve50DF10(NativeModelCpuResources);
            NativeBattleStageExtendedState extended = NativeStage.Extended ?? throw new NotSupportedException("Native50DF10 requires its retained stage process.");
            NativeStage = NativeStage with { Extended = extended with { Stage137 = NativeBattleStage137.Initialize50DF10(extended.Stage137, NativeStage.SourceAddress1D9A820 ?? throw new NotSupportedException("Native50DF10 requires its actual stage directory address.")) } };
        }
    }

    private NativeBattleGraphicsState RegisterNativeStage506DE0(NativeBattleGraphicsState graphics)
    {
        NativeBattleStageCallback callback = definition.NativeStageSource?.Callback ?? throw new NotSupportedException("Native506DE0 requires the installed stage initializer's original callback binding.");
        if (callback.Address == NativeBattleStage147.CallbackAddress)
        {
            int encounter = definition.EncounterId ?? throw new NotSupportedException("Native511DB0 requires its original encounter identity to select the stage callback.");
            if (encounter is not (1000 or 136))
            {
                callback = new(0x511EB0, null);
            }
        }
        if (callback.UpdateTexture0 is null && callback.Address != NativeBattleFinalStage142.CallbackAddress && !NativeBattleStageSpecialized.Supports(callback.Address) && callback.Address is not (0x518F50 or 0x50DFF0 or 0x511EF0 or 0x511EB0))
        {
            throw new NotSupportedException($"Native stage callback{callback.Address:X} requires its original specialized stage service.");
        }
        NativeStage = (NativeStage ?? throw new NotSupportedException("Native506DE0 requires its retained stage rows.")) with { Task = new(callback, 0) };
        if (callback.Address is NativeBattleStage147.CallbackAddress or 0x511EB0)
        {
            NativeBattleStageExtendedState extended = NativeStage.Extended ?? throw new NotSupportedException("Native511DB0 requires its retained stage process.");
            NativeStage = NativeStage with { Extended = extended with { Stage147 = new() } };
        }
        InitializeNativeStageCamera();
        return callback.Address == NativeBattleStage147.CallbackAddress ? InitializeNativeStage147(graphics) : graphics;
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
        if (NativeBattleStageSpecialized.Supports(task.Callback.Address))
        {
            return AdvanceNativeSpecializedStage(graphics, state, task);
        }
        if (task.Callback.Address == NativeBattleStage37.CallbackAddress)
        {
            return AdvanceNativeStage37(graphics, state, task);
        }
        if (task.Callback.Address == NativeBattleStage137.CallbackAddress)
        {
            return AdvanceNativeStage137(graphics, state, task);
        }
        if (task.Callback.Address is NativeBattleStage147.CallbackAddress or 0x511EB0)
        {
            return AdvanceNativeStage147(graphics, state, task);
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
            AdvanceNativeStageTexture(0);
        }
        return DrawNativeStandardStage(graphics);
    }

    private void AdvanceNativeStageTexture(ushort selector)
    {
        NativeBattleStageState state = NativeStage ?? throw new NotSupportedException("Native50CB20 requires its stage owner.");
        NativeBattleStageTextureStep motion = NativeBattleStageTextureMotion.Advance50CB20(state.TextureAnimations, selector);
        NativeStage = state with { TextureAnimations = motion.Source };
        if (motion.Copies.IsEmpty)
        {
            return;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native stage texture motion requires its retained effect memory.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native stage texture motion requires the original505EB0 copy queue.");
        foreach (NativeBattleTextureCopy copy in motion.Copies)
        {
            textures = textures.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
        }
        NativeEffectMemory = memory with { Textures = textures };
    }

    private NativeBattleGraphicsState DrawNativeStandardStage(NativeBattleGraphicsState graphics)
    {
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native500FD0 requires its prior5033E0 camera matrix; a final-frame camera reconstruction is unsupported.");
        NativeStage = NativeStage!.Step500FD0(projection.View ?? throw new NotSupportedException("Native500FD0 requires its actual prior5033E0 view matrix."));
        return SubmitNativeStagePackets(graphics, NativeStage);
    }

    private NativeBattleGraphicsState DispatchNativeStageControl(NativeBattleGraphicsState graphics, byte command)
    {
        if (command is < 100 or > 103)
        {
            throw new NotSupportedException("Native83 stage control requires its original transition command.");
        }
        uint address = definition.NativeStageSource?.Callback?.Address ?? throw new NotSupportedException("Native50E510 requires its original stage initializer binding.");
        if (address == NativeBattleStage37.CallbackAddress)
        {
            return DispatchNativeStage37(graphics, command);
        }
        if (address == NativeBattleFinalStage142.CallbackAddress)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native512500 requires its loaded stage.");
            NativeBattleStageTask task = stage.Task ?? throw new NotSupportedException("Native512500 requires its registered task.");
            NativeBattleFinalStage142State state = stage.FinalStage142 ?? throw new NotSupportedException("Native512500 requires its retained process owner.");
            NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native512500 requires its camera owner.");
            short shake = camera.ScreenOffsetScratchY ?? (camera.ProjectionOffsetsCleared ? (short)0 : throw new NotSupportedException("Native512500 requires its screen-offset scratch WORD."));
            NativeBattleFinalStage142Step step = NativeBattleFinalStage142.Control(state, stage, task.Phase, shake, command);
            NativeStage = step.Stage with { FinalStage142 = step.State, Task = task with { Phase = step.Phase } };
            foreach (NativeBattleFinalStage142Command draw in step.Commands)
            {
                if (draw.Kind != NativeBattleFinalStage142CommandKind.Overlay)
                {
                    throw new InvalidDataException("Native512500 transition control produced an unexpected stage command.");
                }
                graphics = SubmitNativeFinalStage142Overlay(graphics, draw, NativeEffectMemory!.PrimitiveProcess!.Buffer80!.Value);
            }
        }
        return graphics;
    }
}
