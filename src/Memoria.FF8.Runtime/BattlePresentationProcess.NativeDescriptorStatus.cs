namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGraphicsState ApplyNativeDescriptorStatus502380(NativeBattleGraphicsState graphics, NativeBattleDescriptorPacket packet)
    {
        if (packet.Command is 108 or 109)
        {
            uint mask = packet.Command == 108 ? 0x1000000u : 0;
            graphics = graphics with { NativeBindings = graphics.NativeBindings.WriteUnboundPartyStatusMask(0x1000000, mask) };
            foreach (BattleModelEntry<BattlePartyModelOwner> entry in graphics.ActorModels.Party.Entries)
            {
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(entry.Owner);
                if ((model.Flags0 & 2) == 0)
                {
                    model = model.WriteRetainedStatusMask(0x1000000, mask);
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(entry.Owner, model) };
                }
                else
                {
                    uint status = model.Flags8 ?? throw new InvalidDataException("Native503190 requires its loaded model status DWORD8.");
                    graphics = SetNativeDescriptorStatusAndStanding(graphics, entry.Owner, (status & ~0x1000000u) | mask);
                }
            }
            return graphics;
        }
        BattleModelOwner target = RequireDescriptorModelOwner(graphics, packet.ModelSubject);
        NativeBattleGraphicsModel selected = graphics.ActorModels.Get(target);
        if (packet.Command == 111)
        {
            return SelectNativeHeaderSequence(graphics, target, packet.Value);
        }
        uint previous = selected.Flags8 ?? throw new NotSupportedException("Native502380 status operation requires its actual retained graphics DWORD8.");
        uint flags;
        if (packet.Command is 110 or 114)
        {
            flags = packet.Command == 110 ? previous | 0x800000 : previous & ~0x800000u;
        }
        else
        {
            NativeBattleGraphicsRules rules = resources.NativeGraphics!.Rules;
            if (packet.Command == 117)
            {
                int source = Enumerable.Range(0, NativeBattleSlots.Count).SingleOrDefault(slot => NativeParticipantAddress(slot) == packet.Argument, -1);
                NativeBattleModelLifecycleServices frame = hostedFrameServices ?? throw new InvalidOperationException("Native descriptor status requires the current frame facts.");
                BattleModelOwner owner = source < 0 ? throw new NotSupportedException("Native50A070 requires its actual source participant pointer.") : graphics.NativeBindings.DecodeLiteral(checked((byte)source), graphics.ActorModels);
                NativeBattleModelActorFacts facts = frame.Facts[owner];
                ushort saved = facts.PresentationSavedStatus ?? throw new NotSupportedException("Native descriptor status requires the current WORD B92 mirror.");
                uint battle = facts.PresentationBattleStatus ?? throw new NotSupportedException("Native descriptor status requires the current DWORD B1C mirror.");
                flags = (previous & 0x1800000) | rules.Project(saved, battle);
            }
            else
            {
                uint operand = 1u << (packet.Value & 255);
                uint mask = packet.Command == 118 ? rules.Project(unchecked((ushort)operand), 0) : rules.Project(0, operand);
                flags = (previous & ~mask) | ((packet.Value >> 8) != 0 ? mask : 0);
            }
        }
        return SetNativeDescriptorStatusAndStanding(graphics, target, flags);
    }

    private NativeBattleGraphicsState SetNativeDescriptorStatusAndStanding(NativeBattleGraphicsState graphics, BattleModelOwner index, uint flags)
    {
        graphics = ApplyNativeGraphicsStatus509CD0(graphics, index, flags);
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
        byte identity = model.ModelId4 ?? throw new NotSupportedException("Native509C80 requires its retained model identity BYTE4.");
        return identity < 16 ? RefreshNativeGuardianStandingSequence(graphics, index) : graphics;
    }
}
