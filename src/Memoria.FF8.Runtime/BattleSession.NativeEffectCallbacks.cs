using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeOriginalEffect(NativeBattleGraphicsState graphics)
    {
        if (graphics.OriginalEffect is not { ReturnedDescriptor: > 0 } effect)
        {
            return graphics;
        }
        if (effect.Binding.Entry == 0x8D6A00)
        {
            return AdvanceNativeCure8D6B30(graphics);
        }
        if (effect.Binding.Entry == 0x889D60)
        {
            return AdvanceNativeCura889E40(graphics);
        }
        if (effect.Binding.Entry == 0x87DB10)
        {
            return AdvanceNativeCuraga87DC50(graphics);
        }
        if (effect.Binding.Entry == 0x6C3550)
        {
            NativeBattleQuezacotlPreparation prepared = NativeBattleQuezacotl.Prepare6C3760(NativeEffectMemory ?? throw new NotSupportedException("Native Quezacotl callbacks require retained effect memory."), effect, NativeVibrationProcess, NativeCamera?.Projection?.View);
            NativeEffectMemory = prepared.Memory;
            NativeVibrationProcess = prepared.Vibration;
            graphics = graphics with { OriginalEffect = prepared.Effect };
            if (prepared.ChildBoundary)
            {
                NativeGraphics = graphics;
                NativeBattleEffectTaskPool childPool = prepared.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738);
                if (childPool.Slots[0].ReadWord(12) is >= 1 and <= 256)
                {
                    graphics = AdvanceNativeQuezacotlFirstWindow(graphics);
                    NativeBattleStageState childStage = NativeStage ?? throw new NotSupportedException("Native5713E0 requires its retained stage rows.");
                    NativeStage = childStage with { Groups = childStage.Groups.Select(group => group with { ColorDepth = 2400, ColorFlags = 0 }).ToImmutableArray() };
                    return graphics;
                }
                NativeBattleQuezacotlChildStep child = NativeBattleQuezacotlChild.AdvanceFirst6C3940(prepared.Memory, prepared.Effect);
                NativeEffectMemory = child.Memory;
                graphics = graphics with { OriginalEffect = child.Effect };
                if (child.FileRequest is { } request)
                {
                    QueueNativeQuezacotlFile(request, child.Effect);
                }
                if (child.Entered)
                {
                    graphics = SetNativeQuezacotlTargets6C5300(graphics, child.Effect, true);
                    uint sound = child.Effect.Binding.NativeQuezacotlResources?.InitialSound ?? throw new NotSupportedException("Native501330 requires the actual12AE4F0 Quezacotl sound DWORD.");
                    PublishNativeModuleSound(new(sound, 1, null) { Pan = 128 });
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native6C3940 requires its retained camera globals.")).WriteEffectGlobals(new(new(0, -3827, -1983), new(0, -5150, -723), 288, 0));
                }
            }
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native6C3760 requires its actual5713E0 stage context rows.");
            short intensity = checked((short)graphics.OriginalEffect!.Quezacotl!.Intensity17A4);
            NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = intensity, ColorFlags = 0 }).ToImmutableArray() };
            return graphics;
        }
        if (effect.Binding.Entry == 0x700C10)
        {
            NativeBattleEffect3Step module = NativeBattleEffect3.Step(effect, new(nativeActionPackets, index => ReadNativeGraphicsBone(graphics, index, 241, 0), index => graphics.Models[index].Transform ?? throw new NotSupportedException("Native effect3 requires its original model transform.")) { ApplyContact = ApplyContact });
            foreach (NativeBattleEffect3Sound sound in module.Sounds)
            {
                PublishNativeModuleSound(sound.Sound, sound.Flags, sound.Position);
            }
            return graphics with { OriginalEffect = module.Effect, ModuleDraws = graphics.ModuleDraws.AddRange(module.Draws) };
        }
        if (effect.Binding.Entry != 0x6298A0)
        {
            throw new NotSupportedException($"Native effect{effect.Binding.Effect} requires its original callback scheduler.");
        }
        NativeBattleEffectProgramResources resources = effect.Binding.ProgramResources ?? throw new NotSupportedException("Native Fire callbacks require their original executable resource bank.");
        NativeBattleFireStep step = NativeBattleFireEffect.Step(effect, new(graphics.Flags, nativeActionPackets, nativeCrtRandom ?? throw new NotSupportedException("Native Fire requires the retained55CBD2 CRT random owner."), index => ReadNativeModelAnchor(graphics, index), index => graphics.Models[index].Transform ?? throw new NotSupportedException("Native Fire requires its original model transform."), resources.Sprites.AddRange(definition.NativeModuleResources)) { ApplyContact = ApplyContact });
        nativeCrtRandom = step.CrtRandomAfter;
        foreach (NativeBattleFireSound sound in step.Sounds)
        {
            if (sound.DescriptorAddress != 0xDEE130 || sound.Volume != 256)
            {
                throw new InvalidDataException("Native Fire sound does not identify its original501330/5013A0 descriptor.");
            }
            if (sound.Position is { } position)
            {
                PublishNativeModuleSound(resources.Sound, 0x100, position);
            }
            else
            {
                byte pan = checked((byte)(sound.Pan ?? throw new InvalidDataException("Native501330 Fire sound lacks its original pan.")));
                PublishNativeModuleSound(new(resources.Sound, 0x100, null) { Pan = pan });
            }
        }
        return graphics with { OriginalEffect = step.Effect, ModuleDraws = graphics.ModuleDraws.AddRange(step.Draws) };

        void ApplyContact(int index)
        {
            ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
            graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(index)] });
            graphics = graphics with { PendingContacts = pending };
        }
    }
}
