namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGraphicsState ApplyNativeGraphicsStatus509CD0(NativeBattleGraphicsState graphics, BattleModelOwner index, uint flags)
    {
        if (graphics.ActorModels.Get(index).ReadLinkedModel() is null)
        {
            return graphics with { ActorModels = graphics.ActorModels.Set(index, SetNativeGraphicsFlags(index, graphics.ActorModels.Get(index), flags, graphics)) };
        }
        (NativeBattleAuxiliaryState auxiliary, bool allocated) = graphics.Auxiliary.AppendModelRing(NativeBattleAuxiliaryKind.ModelStatus50A000, index, graphics.NativeBindings.CaptureAliases(graphics.ActorModels), flags);
        return allocated ? graphics with { Auxiliary = auxiliary } : throw new InvalidOperationException("Native509CD0 cannot write its required50A000 payload after auxiliary allocation fails.");
    }

    private NativeBattleGraphicsState ScheduleNativeRingSequence505C00(NativeBattleGraphicsState graphics, BattleModelOwner index, int requested)
    {
        if (requested == 0)
        {
            return graphics;
        }
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true && foreign.NativeResource is null)
        {
            if ((requested & ~0x1000) is 1 or 2 && foreign.CanReact && foreign.AtStandingPose is not null)
            {
                presentationSequences.Add(new(index, NativeBattleForeignSequenceKind.Standing));
                return graphics;
            }
            if ((requested & ~0x1000) is >= 4 and <= 6 && foreign.CanReact)
            {
                presentationSequences.Add(new(index, NativeBattleForeignSequenceKind.Hit));
                return graphics;
            }
            throw new NotSupportedException($"Host actor slot {index} requires a host presentation service for native sequence {requested & ~0x1000}.");
        }
        NativeBattleGraphicsModel source = graphics.ActorModels.Get(index);
        if ((requested & 0x1000) == 0 && ((source.Flags8 ?? throw new InvalidDataException("Native505C00 requires its model DWORD8.")) & 0x18) != 0)
        {
            return graphics;
        }
        int selected = requested & ~0x1000;
        if (source.ReadLinkedModel() is null)
        {
            if (source.ModelId4 == 0x8F)
            {
                return graphics;
            }
            NativeBattleGraphicsModel selectedModel = NativeBattleSequenceSelections.SelectResource505BC0(source, NativeActionResource(graphics, index), selected);
            return graphics with { ActorModels = graphics.ActorModels.Set(index, selectedModel) };
        }
        (NativeBattleAuxiliaryState auxiliary, bool allocated) = graphics.Auxiliary.AppendModelRing(NativeBattleAuxiliaryKind.ModelSequence505C70, index, graphics.NativeBindings.CaptureAliases(graphics.ActorModels), sequence: unchecked((byte)selected));
        if (!allocated)
        {
            return graphics;
        }
        foreach (BattleModelOwner linked in NativeBattleModelRings.Walk(graphics.ActorModels, index))
        {
            NativeBattleGraphicsModel model = graphics.ActorModels.Get(linked);
            if ((model.Flags0 & 2) != 0)
            {
                ushort flags = model.ResourceFlags44 ?? throw new NotSupportedException("Native505C00 requires the linked model resource WORD44.");
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(linked, model with { ResourceFlags44 = (ushort)(flags & ~0x40) }) };
            }
        }
        return graphics with { Auxiliary = auxiliary };
    }

    private NativeBattleGraphicsState SelectNativeRingContactSequence(NativeBattleGraphicsState graphics, BattleModelOwner index, byte sequence)
    {
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
        ushort flags = model.ResourceFlags44 ?? throw new NotSupportedException("Native506620 requires its resource WORD44.");
        if (model.ModelId4 >= 16)
        {
            if (sequence != 0 && (flags & 0x140) != 0)
            {
                return SelectNativeHeaderSequence(graphics, index, sequence);
            }
            return (flags & 0x40) != 0 ? SelectNativeHeaderSequence(graphics, index, sequence) : graphics with { ActorModels = graphics.ActorModels.Set(index, model with { SequenceState = model.SequenceState! with { Pending3 = sequence } }) };
        }
        if (((model.Flags8 ?? throw new InvalidDataException("Native506620 requires its status DWORD8.")) & 0x300000) == 0 || sequence == 3)
        {
            if ((flags & 0x140) == 0 || sequence != 0)
            {
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, model with { SequenceState = model.SequenceState! with { Default2 = 1 } }) };
            }
            if ((flags & 0x140) != 0 && sequence != 0)
            {
                return SelectNativeHeaderSequence(graphics, index, sequence);
            }
        }
        return graphics;
    }

    private NativeBattleGraphicsState ApplyNativeModelRingCallback(NativeBattleGraphicsState graphics, NativeBattleAuxiliaryKind kind, BattleModelOwner source, uint argument)
    {
        if (kind == NativeBattleAuxiliaryKind.ModelResourceFlags50B080)
        {
            foreach (BattleModelOwner linked in NativeBattleModelRings.Walk(graphics.ActorModels, source))
            {
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(linked);
                if ((model.Flags0 & 2) != 0)
                {
                    ushort flags = model.ResourceFlags44 ?? throw new NotSupportedException("Native50B080 requires its loaded resource WORD44.");
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(linked, model with { ResourceFlags44 = (ushort)(flags | argument) }) };
                }
            }
            return graphics;
        }
        if (kind == NativeBattleAuxiliaryKind.ModelSequence505C70)
        {
            foreach (BattleModelOwner linked in NativeBattleModelRings.Walk(graphics.ActorModels, source))
            {
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(linked);
                if ((model.Flags0 & 2) != 0)
                {
                    model = NativeBattleSequenceSelections.SelectResource505BC0(model, NativeActionResource(graphics, linked), unchecked((byte)argument));
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(linked, model) };
                }
            }
            return graphics;
        }
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(source, SetNativeGraphicsFlags(source, graphics.ActorModels.Get(source), argument, graphics)) };
        NativeBattleGraphicsModel root = graphics.ActorModels.Get(source);
        ushort ownerFlags = root.Flags2 ?? throw new NotSupportedException("Native50A000 requires its source flags WORD2.");
        uint preserved = NativeBattleModelRings.PreservedStatusMask50A000(ownerFlags);
        uint propagated = argument & ~preserved;
        BattleModelOwner current = root.ReadLinkedModel() ?? throw new InvalidDataException("Native50A000 dereferences a NULL first ring link.");
        HashSet<BattleModelOwner> visited = [];
        do
        {
            if (!visited.Add(current))
            {
                throw new InvalidDataException("Native50A000 ring does not return to its source.");
            }
            NativeBattleGraphicsModel model = graphics.ActorModels.Get(current);
            if ((model.Flags0 & 2) != 0)
            {
                uint previous = model.Flags8 ?? throw new NotSupportedException("Native50A000 requires its linked model status DWORD8.");
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(current, SetNativeGraphicsFlags(current, model, propagated | (preserved & previous), graphics)) };
            }
            BattleModelOwner? next = graphics.ActorModels.Get(current).ReadLinkedModel();
            if (next is null || next == source)
            {
                break;
            }
            current = next;
        }
        while (true);
        return graphics;
    }
}
