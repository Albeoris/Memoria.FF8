namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void QueueNativeCommonTexture5084B0(ushort file, uint destination)
    {
        if (file != 0xA3)
        {
            throw new NotSupportedException("Native508660 requires its original A3 shared texture file.");
        }
        NativeBattleEffectInitializerAsset resource = definition.NativeCommonEffectTexture ?? throw new NotSupportedException("Native5084B0 A3 requires the original a8def.tim resource binding; explicitly migrate a named source if needed.");
        if (resource.Name != "a8def.tim" || resource.Data.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native A3 resource does not identify its original a8def.tim bytes.");
        }
        if (destination == 0x24224D4)
        {
            NativeBattleEffectMemory retained = NativeEffectMemory ?? throw new NotSupportedException("Native Ultra Waves texture restore requires its process memory.");
            NativeEffectIoResult = -1;
            NativeEffectMemory = NativeBattleUltraWaves154TextureRestore.Copy(retained, resource.Data);
            QueueNativeFileIo482610(resource.Data.Length, 0x508470);
            return;
        }
        int offset = CommonTextureOffset(destination);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native5084B0 requires its current destination memory.");
        NativeEffectIoResult = -1;
        NativeEffectMemory = memory with { Arena = (memory.Arena ?? throw new NotSupportedException("Native A3 destination requires its effect arena.")).Write(offset, resource.Data.AsSpan()) };
        QueueNativeFileIo482610(resource.Data.Length, 0x508470);
    }

    private void EnqueueNativeCommonTexture505E30(uint source)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native508660 requires its current mutable TIM memory.");
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native508660 requires its retained texture queue.");
        if (source == 0x24224D4)
        {
            (textures, _) = textures.EnqueueArenaTim505E30((memory.UltraWaves154 ?? throw new NotSupportedException("Native Ultra Waves texture restore lost its private TIM bytes.")).Packets, 0);
            NativeEffectMemory = memory with { Textures = textures };
            return;
        }
        (textures, _) = textures.EnqueueArenaTim505E30(memory.Arena ?? throw new NotSupportedException("Native508660 requires its actual A3 destination."), CommonTextureOffset(source));
        NativeEffectMemory = memory with { Textures = textures };
    }

    private bool TryWriteNativeTonberry90Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleTonberry90Controller.MainBank);
        if (delta >= 0xC8 || delta % 0x64 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x762360 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleTonberry90Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x762360 } current ? current : throw new NotSupportedException("Native Tonberry completion lost its physical root task.");
            updated = graphics with { OriginalEffect = NativeBattleTonberry90Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }

    private bool TryWriteNativeDoomtrain191Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint relative = unchecked(address - NativeBattleDoomtrain191Controller.ChildBank);
        if (relative >= 100 * 0x24 || relative % 0x24 != 14)
        {
            return false;
        }
        if (graphics.OriginalEffect is { Binding.Effect: 191 } effect)
        {
            updated = graphics with { OriginalEffect = NativeBattleDoomtrain191Finish.WriteCompletionByte(effect, address, value) };
        }
        else if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Effect: 191 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleDoomtrain191Finish.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            throw new NotSupportedException("Native Doomtrain completion requires its retained physical task bank.");
        }
        return true;
    }

    private void WriteNativeAuxiliaryCompletionByte(uint address, byte value)
    {
        if (address != 0x25217A8)
        {
            throw new NotSupportedException($"Native508660 completion BYTE0x{address:X8} has no retained owner.");
        }
        NativeEffectMemory = (NativeEffectMemory ?? throw new NotSupportedException("Native508660 requires its retained completion owner.")) with { QuezacotlCompletion17A8 = value };
    }

    private static int CommonTextureOffset(uint address)
    {
        if (address < 0x20DFAB8 || address >= 0x21DFAB8)
        {
            throw new NotSupportedException("Native508660 destination requires its actual owned mutable allocation.");
        }
        return checked((int)(address - 0x20DFAB8));
    }
}
