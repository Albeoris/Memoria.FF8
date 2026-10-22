namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState ApplyNativeGraphicsStatus509CD0(NativeBattleGraphicsState graphics, int index, uint flags)
    {
        if (graphics.Models[index].ReadLinkedModelIndex140() is null)
        {
            return graphics with { Models = graphics.Models.SetItem(index, SetNativeGraphicsFlags(graphics.Models[index], flags, graphics)) };
        }
        (NativeBattleAuxiliaryState auxiliary, bool allocated) = graphics.Auxiliary.AppendModelRing(NativeBattleAuxiliaryKind.ModelStatus50A000, index, flags);
        return allocated ? graphics with { Auxiliary = auxiliary } : throw new InvalidOperationException("Native509CD0 cannot write its required50A000 payload after auxiliary allocation fails.");
    }

    private NativeBattleGraphicsState ScheduleNativeRingSequence505C00(NativeBattleGraphicsState graphics, byte index, int requested)
    {
        if (requested == 0)
        {
            return graphics;
        }
        NativeBattleGraphicsModel source = graphics.Models[index];
        if ((requested & 0x1000) == 0 && ((source.Flags8 ?? throw new InvalidDataException("Native505C00 requires its model DWORD8.")) & 0x18) != 0)
        {
            return graphics;
        }
        int selected = requested & ~0x1000;
        if (source.ReadLinkedModelIndex140() is null)
        {
            if (source.ModelId4 == 0x8F)
            {
                return graphics;
            }
            NativeBattleGraphicsModel selectedModel = NativeBattleSequenceSelections.SelectResource505BC0(source, NativeActionResource(graphics, index), selected);
            return graphics with { Models = graphics.Models.SetItem(index, selectedModel) };
        }
        (NativeBattleAuxiliaryState auxiliary, bool allocated) = graphics.Auxiliary.AppendModelRing(NativeBattleAuxiliaryKind.ModelSequence505C70, index, sequence: unchecked((byte)selected));
        if (!allocated)
        {
            return graphics;
        }
        foreach (int linked in NativeBattleModelRings.Walk(graphics.Models, index))
        {
            NativeBattleGraphicsModel model = graphics.Models[linked];
            if ((model.Flags0 & 2) != 0)
            {
                ushort flags = model.ResourceFlags44 ?? throw new NotSupportedException("Native505C00 requires the linked model resource WORD44.");
                graphics = graphics with { Models = graphics.Models.SetItem(linked, model with { ResourceFlags44 = (ushort)(flags & ~0x40) }) };
            }
        }
        return graphics with { Auxiliary = auxiliary };
    }

    private NativeBattleGraphicsState SelectNativeRingContactSequence(NativeBattleGraphicsState graphics, int index, byte sequence)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        ushort flags = model.ResourceFlags44 ?? throw new NotSupportedException("Native506620 requires its resource WORD44.");
        if (model.ModelId4 >= 16)
        {
            if (sequence != 0 && (flags & 0x140) != 0)
            {
                return SelectNativeHeaderSequence(graphics, checked((byte)index), sequence);
            }
            return (flags & 0x40) != 0 ? SelectNativeHeaderSequence(graphics, checked((byte)index), sequence) : graphics with { Models = graphics.Models.SetItem(index, model with { SequenceState = model.SequenceState! with { Pending3 = sequence } }) };
        }
        if (((model.Flags8 ?? throw new InvalidDataException("Native506620 requires its status DWORD8.")) & 0x300000) == 0 || sequence == 3)
        {
            if ((flags & 0x140) == 0 || sequence != 0)
            {
                graphics = graphics with { Models = graphics.Models.SetItem(index, model with { SequenceState = model.SequenceState! with { Default2 = 1 } }) };
            }
            if ((flags & 0x140) != 0 && sequence != 0)
            {
                return SelectNativeHeaderSequence(graphics, checked((byte)index), sequence);
            }
        }
        return graphics;
    }

    private NativeBattleGraphicsState ApplyNativeModelRingCallback(NativeBattleGraphicsState graphics, NativeBattleAuxiliaryKind kind, int source, uint argument)
    {
        if (kind == NativeBattleAuxiliaryKind.ModelResourceFlags50B080)
        {
            foreach (int linked in NativeBattleModelRings.Walk(graphics.Models, source))
            {
                NativeBattleGraphicsModel model = graphics.Models[linked];
                if ((model.Flags0 & 2) != 0)
                {
                    ushort flags = model.ResourceFlags44 ?? throw new NotSupportedException("Native50B080 requires its loaded resource WORD44.");
                    graphics = graphics with { Models = graphics.Models.SetItem(linked, model with { ResourceFlags44 = (ushort)(flags | argument) }) };
                }
            }
            return graphics;
        }
        if (kind == NativeBattleAuxiliaryKind.ModelSequence505C70)
        {
            foreach (int linked in NativeBattleModelRings.Walk(graphics.Models, source))
            {
                NativeBattleGraphicsModel model = graphics.Models[linked];
                if ((model.Flags0 & 2) != 0)
                {
                    model = NativeBattleSequenceSelections.SelectResource505BC0(model, NativeActionResource(graphics, checked((byte)linked)), unchecked((byte)argument));
                    graphics = graphics with { Models = graphics.Models.SetItem(linked, model) };
                }
            }
            return graphics;
        }
        graphics = graphics with { Models = graphics.Models.SetItem(source, SetNativeGraphicsFlags(graphics.Models[source], argument, graphics)) };
        NativeBattleGraphicsModel root = graphics.Models[source];
        ushort ownerFlags = root.Flags2 ?? throw new NotSupportedException("Native50A000 requires its source flags WORD2.");
        uint preserved = NativeBattleModelRings.PreservedStatusMask50A000(ownerFlags);
        uint propagated = argument & ~preserved;
        int current = root.ReadLinkedModelIndex140() ?? throw new InvalidDataException("Native50A000 dereferences a NULL first ring link.");
        HashSet<int> visited = [];
        do
        {
            if (!visited.Add(current))
            {
                throw new InvalidDataException("Native50A000 ring does not return to its source.");
            }
            NativeBattleGraphicsModel model = graphics.Models[current];
            if ((model.Flags0 & 2) != 0)
            {
                uint previous = model.Flags8 ?? throw new NotSupportedException("Native50A000 requires its linked model status DWORD8.");
                graphics = graphics with { Models = graphics.Models.SetItem(current, SetNativeGraphicsFlags(model, propagated | (preserved & previous), graphics)) };
            }
            int? next = graphics.Models[current].ReadLinkedModelIndex140();
            if (next is null || next == source)
            {
                break;
            }
            current = next.Value;
        }
        while (true);
        return graphics;
    }
}
