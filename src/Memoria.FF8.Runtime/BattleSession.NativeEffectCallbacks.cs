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
                events.Add(new BattleEvent(Frame, "NativeModuleSound", null, null, 0, string.Empty) { NativeModuleSound = new(resources.Sound, 0x100, null) { Pan = pan } });
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
