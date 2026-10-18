namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState BeginNativeOriginalEffect(NativeBattleGraphicsState graphics, ushort effect, bool destination21DFEC0 = false)
    {
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effect, definition.NativeEffectCatalog);
        selected = NativeBattleEffectResourceResolver.MaterializeSelected(selected, nativeEffectResourceResolver);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50AF20 requires its retained571870 allocator owner; migrate the standalone snapshot explicitly.");
        memory = memory.RetainReturnedTaskOnModuleLoad(graphics.OriginalEffect);
        (memory, selected) = NativeBattleOriginalEffects.Initialize(memory, selected);
        NativeEffectMemory = memory.PublishEntry50AF20(selected.Binding, destination21DFEC0);
        return graphics with { OriginalEffect = selected };
    }

    private NativeBattleGraphicsState PrepareNativeOriginalEffectParameters(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte parameterFlags = 0)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native module invocation requires the retained50AF20 entry owner.");
        graphics = PrepareNativeHeaderGeometry(graphics, context);
        NativeBattleEffectParameters parameters = CreateNativeOriginalEffectParameters(context, parameterFlags);
        return graphics with { OriginalEffect = effect with { Parameters = parameters } };
    }

    private NativeBattleEffectParameters CreateNativeOriginalEffectParameters(NativeBattleActionGraphicsState context, byte parameterFlags = 0)
    {
        return new(context.ActorIndex, (byte)(context.HitFlags | parameterFlags), context.TargetMask, context.HeaderIndex, 0, 0) { Parameter80 = NativeEffectMemory?.Parameter80, Parameter82 = NativeEffectMemory?.Parameter82 };
    }

    private NativeBattleGraphicsState InvokeNativeOriginalEffect(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte parameterFlags = 0)
    {
        graphics = PrepareNativeOriginalEffectParameters(graphics, context, parameterFlags);
        graphics = PrepareNativeOriginalEffectActor(graphics, context.ActorIndex);
        return InvokeNativeOriginalEffect(graphics);
    }

    private static NativeBattleGraphicsState PrepareNativeOriginalEffectActor(NativeBattleGraphicsState graphics, byte actorIndex)
    {
        NativeBattleGraphicsModel actor = graphics.Models[actorIndex];
        return graphics with { Models = graphics.Models.SetItem(actorIndex, actor with { Rotation12 = 0, Rotation14 = actorIndex >= 3 ? (short)2048 : (short)0, Rotation16 = 0 }) };
    }

    private NativeBattleGraphicsState InvokeNativeOriginalEffect(NativeBattleGraphicsState graphics, bool assignReturnedTask = true)
    {
        NativeBattleGraphicsState entered = InvokeNativeOriginalEffectEntry(graphics);
        if (assignReturnedTask)
        {
            NativeEffectMemory = (NativeEffectMemory ?? throw new NotSupportedException("Native assigned entry requires its process task pointer owner.")).AssignReturnedTask(entered.OriginalEffect ?? throw new InvalidDataException("Native entry lost its returned pool owner."));
        }
        return entered;
    }

    private NativeBattleGraphicsState InvokeNativeOriginalEffectEntry(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native effect invocation requires its actual loaded module entry.");
        _ = effect.Parameters ?? throw new InvalidDataException("Native effect entry lacks its50AFC0 parameter block.");
        if (effect.Binding.Entry == 0x755130)
        {
            return EnterNativeSearch755130(graphics);
        }
        if (effect.Binding.Entry == 0x6652C0)
        {
            return EnterNativeEffect311(graphics);
        }
        if (effect.Binding.Entry == 0x6D1310)
        {
            return EnterNativeEffect107(graphics);
        }
        if (effect.Binding.Entry == 0x6C0C80)
        {
            return EnterNativeAero6C0CB0(graphics);
        }
        if (effect.Binding.Entry == 0x6D6310)
        {
            return EnterNativeBlizzaga6D6320(graphics);
        }
        if (effect.Binding.Entry == 0x6DA300)
        {
            return EnterNativeBlizzara6DA310(graphics);
        }
        if (effect.Binding.Entry == 0x6DC2D0)
        {
            return EnterNativeThundara6DC2E0(graphics);
        }
        if (effect.Binding.Entry == 0x61BFE0)
        {
            return EnterNativeFiraga61BFE0(graphics);
        }
        if (effect.Binding.Entry == 0x61CE10)
        {
            return EnterNativeFira61CE10(graphics);
        }
        if (effect.Binding.Entry == 0x61ABA0)
        {
            return EnterNativeBlizzard61ABA0(graphics);
        }
        if (effect.Binding.Entry == 0x8D6A00)
        {
            return EnterNativeCure8D6A00(graphics);
        }
        if (effect.Binding.Entry is 0x8649C0 or 0x8D4450)
        {
            return EnterNativeLife(graphics);
        }
        if (effect.Binding.Entry == 0x8D5080)
        {
            return EnterNativeDouble8D5080(graphics);
        }
        if (effect.Binding.Entry == 0x89DEF0)
        {
            return EnterNativeTriple89DEF0(graphics);
        }
        if (effect.Binding.Entry == 0x889D60)
        {
            return EnterNativeCura889D60(graphics);
        }
        if (effect.Binding.Entry == 0x87DB10)
        {
            return EnterNativeCuraga87DB10(graphics);
        }
        if (effect.Binding.Entry is 0x88CF70 or 0x883110 or 0x7FD150 or 0x7DEA20)
        {
            return EnterNativeRestoration(graphics);
        }
        if (effect.Binding.Entry is 0x811F70 or 0x7F7CB0 or 0x7F2810)
        {
            return EnterNativeRecovery(graphics);
        }
        if (effect.Binding.Entry == 0x6C3550)
        {
            NativeBattleQuezacotlEntry entered = NativeBattleQuezacotl.Enter6C3640(NativeEffectMemory ?? throw new NotSupportedException("Native Quezacotl requires its retained effect memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Quezacotl requires its retained55CBD2 random owner."), nativeActionPackets, index => (graphics.Models[index].Transform ?? throw new NotSupportedException("Native6C3640 requires the actual graphical model WORD32 position.")).Position28.Z);
            NativeEffectMemory = entered.Memory;
            nativeCrtRandom = entered.Random;
            return graphics with { OriginalEffect = entered.Effect };
        }
        if (effect.Binding.Entry == 0x6298A0)
        {
            if ((effect.Parameters.Flags79 & 1) == 0)
            {
                NativeBattleEffectProgramResources resources = effect.Binding.ProgramResources ?? throw new NotSupportedException("Native Fire6298A0 requires its original DED5FC camera descriptor and sprite resources.");
                NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Native Fire lost its original571870 arena owner.");
                NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native Fire requires the retained505E30 upload slots and VRAM; migrate the named standalone snapshot explicitly.");
                textures = textures.Register5099A0(resources.Animation);
                NativeBattleEffectInitializerAsset asset = effect.Binding.InitializerAsset ?? throw new InvalidDataException("Native Fire lost its loaded mag001.tim resource.");
                (textures, _) = textures.EnqueueTim505E30(new(asset.Name, asset.Data));
                NativeEffectMemory = memory with { Textures = textures };
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Fire camera registration requires its actual camera owner.")).RegisterControlResource(textures.Animation);
            }
            return graphics with { OriginalEffect = NativeBattleFireEffect.InitializePools(effect, nativeActionPackets) };
        }
        if (effect.Binding.Entry == 0x700C10)
        {
            if ((effect.Parameters.Flags79 & 1) == 0)
            {
                NativeBattleEffect3Resources resources = effect.Binding.Effect3Resources ?? throw new NotSupportedException("Native effect3 requires its original embedded TIM and camera resource.");
                NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new InvalidDataException("Native effect3 lost its571870 arena owner.");
                NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native effect3 requires the retained505E30 upload slots and VRAM.");
                textures = textures.Register5099A0(resources.Animation);
                (textures, _) = textures.EnqueueTim505E30(resources.Texture);
                NativeEffectMemory = memory with { Textures = textures };
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native effect3 requires its camera owner.")).RegisterControlResource(textures.Animation);
            }
            return graphics with { OriginalEffect = NativeBattleEffect3.InitializePools(effect) };
        }
        throw new NotSupportedException($"Native effect{effect.Binding.Effect} entry{effect.Binding.Entry:X} requires its original module callback and graphics-resource contract.");
    }

    private bool NativeOriginalSoundBusy()
    {
        return (NativeEffectMemory ?? throw new NotSupportedException("Native501190 requires its published501230 sound flags.")).SoundPublishedFlags != 0;
    }

    private bool NativeOriginalEffectBusy(NativeBattleGraphicsState graphics)
    {
        //A replacement DWORD100 can reference an effect resource or a row in the current shared file staging.
        if (graphics.Models.Any(model => (model.Flags0 & 2) != 0 && (model.ResourceMagicId is not null || model.ActiveStagedResourceRow100 is not null)))
        {
            return true;
        }
        if ((NativeEffectMemory ?? throw new NotSupportedException("Native50AE80 requires its retained task pointer owner.")).ReadReturnedTask5044B0() != 0)
        {
            return true;
        }
        return NativeCameraTaskMask != 0 || NativeOriginalSoundBusy();
    }

    private bool NativeOriginalEffectDescriptorReleased(NativeBattleGraphicsState graphics)
    {
        return (NativeEffectMemory ?? throw new NotSupportedException("Native effect completion requires its canonical task pointer owner.")).ReadReturnedTask5044B0() == 0;
    }

    private NativeBattleGraphicsState PublishNativeOriginalEffectTitle(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context)
    {
        if (context.PendingData is null)
        {
            return graphics;
        }
        string text = context.PendingData.FormattedText ?? throw new NotSupportedException("Native50A750 requires the actual header title string.");
        byte duration = unchecked((byte)(8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native50A750 requires the saved message-speed BYTE.")) + 8));
        if (duration != 0)
        {
            events.Add(new(Frame, "NativeText", null, null, duration, text) { NativeMessageMode = 1 });
        }
        return graphics with { ActionGraphics = context with { PendingData = null } };
    }

    private (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) PollNativeOriginalGraphics(NativeBattleGraphicsState graphics, NativeBattleActionGraphicsState context, byte phase)
    {
        if (phase == 0)
        {
            if (ModelsBusy(graphics.Models))
            {
                return (graphics, phase, false);
            }
            phase = 1;
        }
        if (phase == 1)
        {
            graphics = BeginNativeOriginalEffect(graphics, NativeBattleActionGraphics.Word(nativeActionPackets.Headers[context.HeaderIndex], 6));
            return (graphics, 2, false);
        }
        if (phase == 2)
        {
            int io = NativeEffectIoResult ?? throw new NotSupportedException("Native50B190 requires the original signed effect IO result.");
            return (graphics, io < 0 || graphics.ModuleEffects.PublishedCount != 0 ? phase : (byte)3, false);
        }
        if (phase == 3)
        {
            graphics = PublishNativeOriginalEffectTitle(graphics, context);
            graphics = PrepareNativeOriginalEffectParameters(graphics, context);
            graphics = PrepareNativeOriginalEffectActor(graphics, context.ActorIndex);
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native50B190 requires the actual camera selector owner.")).SetActionSelector(65535);
            graphics = InvokeNativeOriginalEffect(graphics);
            return (graphics with { Flags = graphics.Flags | 0x10 }, 4, false);
        }
        if (phase == 4)
        {
            if (NativeOriginalEffectBusy(graphics))
            {
                return (graphics, phase, false);
            }
            graphics = CompleteNativeHeaderGraphics(graphics);
            NativeEffectMemory = (NativeEffectMemory ?? throw new InvalidDataException("Native50B190 lost its effect memory owner.")) with { Active = 0 };
            return (graphics with { Flags = graphics.Flags & ~0x10u }, phase, true);
        }
        return (graphics, phase, false);
    }
}
