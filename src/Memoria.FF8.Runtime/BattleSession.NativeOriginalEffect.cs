namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState BeginNativeOriginalEffect(NativeBattleGraphicsState graphics, ushort effect)
    {
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(effect, definition.NativeEffectCatalog);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50AF20 requires its retained571870 allocator owner; migrate the standalone snapshot explicitly.");
        (memory, selected) = NativeBattleOriginalEffects.Initialize(memory, selected);
        NativeEffectMemory = memory;
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
        return new(context.ActorIndex, (byte)(context.HitFlags | parameterFlags), context.TargetMask, context.HeaderIndex, 0, 0) { Parameter80 = NativeEffectMemory?.Parameter80 };
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

    private NativeBattleGraphicsState InvokeNativeOriginalEffect(NativeBattleGraphicsState graphics)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native effect invocation requires its actual loaded module entry.");
        _ = effect.Parameters ?? throw new InvalidDataException("Native effect entry lacks its50AFC0 parameter block.");
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
                NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Fire camera registration requires its actual camera owner.")) with { ControlResource = textures.Animation, ControlCursor = 0 };
            }
            return graphics with { OriginalEffect = NativeBattleFireEffect.InitializePools(effect, nativeActionPackets) };
        }
        throw new NotSupportedException($"Native effect{effect.Binding.Effect} entry{effect.Binding.Entry:X} requires its original module callback and graphics-resource contract.");
    }

    private bool NativeOriginalSoundBusy()
    {
        return (NativeEffectMemory ?? throw new NotSupportedException("Native501190 requires its published501230 sound flags.")).SoundPublishedFlags != 0;
    }

    private bool NativeOriginalEffectBusy(NativeBattleGraphicsState graphics)
    {
        //ResourceMagicId is the logical owner of a replacement DWORD100; null denotes the loaded original DWORD104.
        if (graphics.Models.Any(model => (model.Flags0 & 2) != 0 && model.ResourceMagicId is not null))
        {
            return true;
        }
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native50AE80 lost its module entry owner.");
        if ((effect.ReturnedDescriptor ?? throw new InvalidDataException("Native50AE80 reached an effect whose entry has not returned.")) != 0)
        {
            return true;
        }
        return NativeCameraTaskMask != 0 || NativeOriginalSoundBusy();
    }

    private static bool NativeOriginalEffectDescriptorReleased(NativeBattleGraphicsState graphics)
    {
        return ((graphics.OriginalEffect ?? throw new NotSupportedException("Native effect completion requires its retained module owner.")).ReturnedDescriptor ?? throw new InvalidDataException("Native effect completion reached an entry that has not returned.")) == 0;
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
