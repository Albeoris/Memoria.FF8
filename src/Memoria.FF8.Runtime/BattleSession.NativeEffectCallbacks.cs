using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState AdvanceNativeOriginalEffect(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native500900 requires its retained returned-task pointer.");
        if (memory.ReadReturnedTask5044B0() == 0)
        {
            return graphics;
        }
        NativeBattleOriginalEffectState selected = graphics.OriginalEffect ?? throw new InvalidDataException("Native returned task has no current module selection.");
        NativeBattleOriginalEffectState task = memory.DetachedReturnedTask ?? selected;
        if (task.ReturnedDescriptor != memory.ReturnedTaskPointer1D96AAC)
        {
            throw new InvalidDataException("Native500900 returned-task pointer disagrees with its actual pool owner.");
        }
        NativeBattleGraphicsState advanced = AdvanceNativeOriginalEffectTask(graphics with { OriginalEffect = task });
        NativeBattleOriginalEffectState current = advanced.OriginalEffect ?? throw new InvalidDataException("Native500900 lost its polled returned-task owner.");
        memory = NativeEffectMemory!;
        if (current.ReturnedDescriptor == 0)
        {
            NativeEffectMemory = memory with { ReturnedTaskPointer1D96AAC = 0, DetachedReturnedTask = null };
        }
        else if (memory.DetachedReturnedTask is not null)
        {
            NativeEffectMemory = memory with { DetachedReturnedTask = current };
        }
        return memory.DetachedReturnedTask is null ? advanced : advanced with { OriginalEffect = selected };
    }

    private NativeBattleGraphicsState AdvanceNativeOriginalEffectTask(NativeBattleGraphicsState graphics)
    {
        if (graphics.OriginalEffect is not { ReturnedDescriptor: > 0 } effect)
        {
            return graphics;
        }
        if (effect.Binding.Entry == 0x5D1AD0)
        {
            return RunNativeHoly175(graphics, false);
        }
        if (effect.Binding.Entry == 0x6D3DB0)
        {
            return RunNativeThundaga105(graphics, false);
        }
        if (effect.Binding.Entry == 0x5A8750)
        {
            return RunNativeCactuar199(graphics, false);
        }
        if (effect.Binding.Entry == 0x731DE0)
        {
            return AdvanceNativeMoogle731F70(graphics);
        }
        if (effect.Binding.Entry == 0x759370)
        {
            return AdvanceNativeRush759510(graphics);
        }
        if (effect.Binding.Entry is 0x74E390 or 0x7475F0)
        {
            return AdvanceNativeAngeloRestoration(graphics);
        }
        if (effect.Binding.Entry == 0x755130)
        {
            return AdvanceNativeSearch7552B0(graphics);
        }
        if (effect.Binding.Entry == 0x6652C0)
        {
            return AdvanceNativeEffect311(graphics);
        }
        if (effect.Binding.Entry == 0x6D1310)
        {
            return AdvanceNativeEffect107(graphics);
        }
        if (effect.Binding.Entry == 0x6C0C80)
        {
            return AdvanceNativeAero6C0D20(graphics);
        }
        if (effect.Binding.Entry == 0x6D6310)
        {
            return AdvanceNativeBlizzaga6D6400(graphics);
        }
        if (effect.Binding.Entry == 0x6DA300)
        {
            return AdvanceNativeBlizzara6DA3A0(graphics);
        }
        if (effect.Binding.Entry == 0x6DC2D0)
        {
            return AdvanceNativeThundara6DC390(graphics);
        }
        if (effect.Binding.Entry == 0x61BFE0)
        {
            return AdvanceNativeFiraga61CDA0(graphics);
        }
        if (effect.Binding.Entry == 0x61CE10)
        {
            return AdvanceNativeFira61DD90(graphics);
        }
        if (effect.Binding.Entry == 0x61ABA0)
        {
            return AdvanceNativeBlizzard61BF70(graphics);
        }
        if (effect.Binding.Entry == 0x8D6A00)
        {
            return AdvanceNativeCure8D6B30(graphics);
        }
        if (effect.Binding.Entry is 0x8649C0 or 0x8D4450)
        {
            return AdvanceNativeLife(graphics);
        }
        if (effect.Binding.Entry == 0x8D5080)
        {
            return AdvanceNativeDouble8D5180(graphics);
        }
        if (effect.Binding.Entry == 0x89DEF0)
        {
            return AdvanceNativeTriple89DFF0(graphics);
        }
        if (effect.Binding.Entry == 0x889D60)
        {
            return AdvanceNativeCura889E40(graphics);
        }
        if (effect.Binding.Entry == 0x87DB10)
        {
            return AdvanceNativeCuraga87DC50(graphics);
        }
        if (effect.Binding.Entry is 0x88CF70 or 0x883110 or 0x7FD150 or 0x7DEA20)
        {
            return AdvanceNativeRestoration(graphics);
        }
        if (effect.Binding.Entry is 0x811F70 or 0x7F7CB0 or 0x7F2810)
        {
            return AdvanceNativeRecovery(graphics);
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
                if (childPool.Head is null)
                {
                    return AdvanceNativeQuezacotlAfterChild6C3760(graphics);
                }
                if (childPool.Slots[0].ReadWord(12) is >= 1 and <= 355)
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
