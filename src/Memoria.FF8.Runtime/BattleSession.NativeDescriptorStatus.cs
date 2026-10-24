namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState ApplyNativeDescriptorStatus502380(NativeBattleGraphicsState graphics, NativeBattleDescriptorPacket packet)
    {
        if (packet.Command is 108 or 109)
        {
            for (int index = 0; index < 3; index++)
            {
                NativeBattleGraphicsModel model = graphics.Models[index];
                if ((model.Flags0 & 2) == 0)
                {
                    model = model.WriteRetainedStatusMask(0x1000000, packet.Command == 108 ? 0x1000000u : 0);
                    graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
                }
                else
                {
                    uint status = model.Flags8 ?? throw new InvalidDataException("Native503190 requires its loaded model status DWORD8.");
                    graphics = SetNativeDescriptorStatusAndStanding(graphics, index, packet.Command == 108 ? status | 0x1000000 : status & ~0x1000000u);
                }
            }
            return graphics;
        }
        int target = RequireDescriptorModel(packet.Index);
        NativeBattleGraphicsModel selected = graphics.Models[target];
        if (packet.Command == 111)
        {
            return SelectNativeHeaderSequence(graphics, checked((byte)target), packet.Value);
        }
        uint previous = selected.Flags8 ?? throw new NotSupportedException("Native502380 status operation requires its actual retained graphics DWORD8.");
        uint flags;
        if (packet.Command is 110 or 114)
        {
            flags = packet.Command == 110 ? previous | 0x800000 : previous & ~0x800000u;
        }
        else
        {
            NativeBattleGraphicsRules rules = definition.NativeGraphics!.Rules;
            if (packet.Command == 117)
            {
                BattleCombatant source = combatants.SingleOrDefault(actor => actor.NativeIndex is >= 0 and < 7 && NativeParticipantAddress(actor.NativeIndex) == packet.Argument) ?? throw new NotSupportedException("Native50A070 requires its actual source participant pointer.");
                (ushort saved, uint battle) = NativeContactStatusMirror(source);
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

    private NativeBattleGraphicsState SetNativeDescriptorStatusAndStanding(NativeBattleGraphicsState graphics, int index, uint flags)
    {
        graphics = ApplyNativeGraphicsStatus509CD0(graphics, index, flags);
        NativeBattleGraphicsModel model = graphics.Models[index];
        byte identity = model.ModelId4 ?? throw new NotSupportedException("Native509C80 requires its retained model identity BYTE4.");
        return identity < 16 ? RefreshNativeGuardianStandingSequence(graphics, checked((byte)index)) : graphics;
    }
}
