using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

/// <summary>Executed522770 request and its process selection globals; actor WORD516 is the sole phase word.</summary>
public sealed record NativeDrawPointInteraction([property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] long Frame, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] long OwnerSequence, [property: JsonRequired] int RawArgument, [property: JsonRequired] byte PointIndex, [property: JsonRequired] byte PointByte, [property: JsonRequired] byte DepletionAtIssue, [property: JsonRequired] uint RandomStateBefore, [property: JsonRequired] uint RandomStateAfter, [property: JsonRequired] short Quantity, [property: JsonRequired] string CommonMessageHash)
{
    public byte EligibleCombatSlots { get; init; }
    public int Message { get; init; } = 7;
    public int? Selection { get; init; }
    public int? CombatSlot { get; init; }
    public int? Acquired { get; init; }
}

public sealed partial class EventEngine
{
    public Func<NativeDrawPointCatalog>? ReadNativeDrawPointCatalog { get; set; }

    private (NativeDrawPointCatalog Catalog, NativeCharacterVitals Vitals, byte Eligible) PrepareDrawPointParty()
    {
        NativeDrawPointCatalog catalog = ReadNativeDrawPointCatalog?.Invoke() ?? throw new NotSupportedException("DRAWPOINT requires its authenticated point table and original common message provider.");
        catalog.Validate();
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("DRAWPOINT requires its actual saved character and GF inputs.");
        if (catalog.KernelHash != vitals.KernelSha256)
        {
            throw new NotSupportedException("DRAWPOINT and saved character command definitions have different kernel provenance.");
        }
        NativeInputConfiguration input = services.InputConfiguration ?? throw new NotSupportedException("DRAWPOINT requires the current native command restriction process alias.");
        input.Validate();
        byte disabled = input.CommandRestrictions;
        NativeCharacterVitals rebuilt = vitals.RebuildParty();
        byte eligible = 0;
        for (int slot = 0; slot < 3; slot++)
        {
            if ((disabled & 8) == 0 && rebuilt.CombatParty[slot] != 255 && catalog.HasDrawCommand(rebuilt.Records[rebuilt.CombatParty[slot]]))
            {
                eligible |= (byte)(1 << slot);
            }
        }
        return (catalog, rebuilt, eligible);
    }

    private string? UnsupportedDrawPointInteraction(Fiber fiber)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || !services.CanStoreNativeEventOwners || services.EventRandom is not { DrawPoints: not null } || fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact || UnsupportedEventRandomAliases() is not null || UnsupportedDrawPointAliases(false) is not null)
        {
            return "DRAWPOINT requires its exact original operand, executed native allocation and canonical random/depletion/character publication";
        }
        if (EventMemory.Overlaps(211, 1))
        {
            return "DRAWPOINT's channel6 completion bit overlaps an unowned scalar alias";
        }
        if (Scene.DrawPointInteraction is NativeDrawPointInteraction current)
        {
            if (current.OwnerSequence != fiber.Sequence || current.Operation != fiber.Position || current.RawArgument != fiber.Stack.Peek() || actors[fiber.ObjectId].NativeMovementAccelerationTarget is null or < 1 or > 7)
            {
                return "DRAWPOINT requires its single live original request and shared WORD516 phase";
            }
            if (dialogues.ContainsKey(6))
            {
                return null;
            }
            if (actors[fiber.ObjectId].NativeMovementAccelerationTarget == 1 && ReadDrawPointDepletion(current.PointIndex) is 2 or 3)
            {
                return null;
            }
        }
        else if (dialogues.ContainsKey(6) || actors[fiber.ObjectId].NativeScriptMovement is { Active: true, Suspended: false, Accelerated: true })
        {
            return "DRAWPOINT requires its actual free channel6 and a proved WORD516 consumer boundary";
        }
        if (Scene.DrawPointInteraction is null && HiddenDrawPointUnavailable)
        {
            return null;
        }
        try
        {
            NativeDrawPointInteraction? pending = Scene.DrawPointInteraction;
            if (pending is null)
            {
                (NativeDrawPointCatalog catalog, _, byte eligible) = PrepareDrawPointParty();
                byte index = unchecked((byte)(fiber.Stack.Peek() - 1));
                if (eligible != 0 && ReadDrawPointDepletion(index) is not (2 or 3))
                {
                    _ = catalog.FormatMessage(2, catalog.PointBytes[index] & 63);
                    _ = PrepareDrawPointSound(66);
                }
            }
            else if (actors[fiber.ObjectId].NativeMovementAccelerationTarget == 1)
            {
                _ = PrepareDrawPointParty();
            }
            else if (actors[fiber.ObjectId].NativeMovementAccelerationTarget == 2)
            {
                _ = ReadNativeDrawPointCatalog!().FormatSelection(Scene.Party.ToArray(), services.CharacterVitals!, pending.PointByte & 63, pending.EligibleCombatSlots);
            }
            else if (actors[fiber.ObjectId].NativeMovementAccelerationTarget == 3 && (pending.Selection is null or < 0 || pending.Selection > Scene.Party.TakeWhile(identity => identity != 255).Count()))
            {
                return "DRAWPOINT selection closed without its actual channel6 result";
            }
            else if (actors[fiber.ObjectId].NativeMovementAccelerationTarget == 4)
            {
                int slot = FindDrawPointCombatSlot(Scene.Party[pending.Selection!.Value]);
                if ((pending.EligibleCombatSlots & (1 << slot)) != 0)
                {
                    ActorState receiver = ResolveNativeIndexedPartyBody(pending.Selection.Value, out _, out _) ?? throw new NotSupportedException("DRAWPOINT receiver has no retained native body.");
                    _ = PrepareNativeDrawPointAcquisition(receiver);
                    _ = PrepareDrawPointSound(67);
                }
            }
            else if (actors[fiber.ObjectId].NativeMovementAccelerationTarget == 6 && !services.CanStoreMessageVariables)
            {
                return "DRAWPOINT requires the canonical message-variable bank before acquiring magic";
            }
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteDrawPointInteraction(Fiber fiber)
    {
        ActorState actor = actors[fiber.ObjectId];
        if (Scene.DrawPointInteraction is null && HiddenDrawPointUnavailable)
        {
            return CompleteDrawPointInteraction(fiber, true);
        }
        if (Scene.DrawPointInteraction is null)
        {
            (NativeDrawPointCatalog catalog, NativeCharacterVitals vitals, byte eligible) = PrepareDrawPointParty();
            int raw = fiber.Stack.Peek();
            byte index = unchecked((byte)(raw - 1));
            byte point = catalog.PointBytes[index];
            NativeEventRandomState before = services.EventRandom!;
            byte depletion = ReadDrawPointDepletion(index);
            int basis = unchecked((sbyte)point) < 0 ? 20 : 10;
            if (depletion == 1)
            {
                basis /= 2;
            }
            NativeEventRandomState next = before.Draw(out int draw);
            short quantity = (short)(basis * ((draw & 255) + 128) / 512 + 1);
            int message = eligible == 0 ? 7 : depletion is 2 or 3 ? 3 : 2;
            PreparedGlobalSoundEmission? sound = message == 2 ? PrepareDrawPointSound(66) : null;
            try
            {
                services.SetNativeEventOwners(services.WorldControls!, characterVitals: vitals, eventRandom: next, globalSoundProcess: sound?.Process);
            }
            catch (NotSupportedException exception)
            {
                throw NativeOwnerPublicationFault(fiber, exception);
            }
            Scene.DrawPointIndex = index;
            Scene.DrawPointInteraction = new NativeDrawPointInteraction(FieldId, programHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, raw, index, point, depletion, before.State, next.State, quantity, catalog.CommonMessageHash) { EligibleCombatSlots = eligible, Message = message };
            actor.NativeMovementAccelerationTarget = 1;
            PublishDrawPointSound(sound);
            ShowDrawPointMessage(fiber, catalog, message);
        }
        else
        {
            NativeDrawPointInteraction current = Scene.DrawPointInteraction;
            switch (actor.NativeMovementAccelerationTarget)
            {
                case 1:
                    if (dialogues.ContainsKey(6))
                    {
                        break;
                    }
                    if (ReadDrawPointDepletion(current.PointIndex) is 2 or 3)
                    {
                        return CompleteDrawPointInteraction(fiber, true);
                    }
                    (_, NativeCharacterVitals vitals, byte eligible) = PrepareDrawPointParty();
                    services.SetNativeEventOwners(services.WorldControls!, characterVitals: vitals);
                    if (eligible == 0)
                    {
                        return CompleteDrawPointInteraction(fiber, true);
                    }
                    Scene.DrawPointInteraction = current with { EligibleCombatSlots = eligible, Selection = null, CombatSlot = null, Acquired = null };
                    actor.NativeMovementAccelerationTarget = 2;
                    break;
                case 2:
                    ShowDrawPointSelection(fiber);
                    actor.NativeMovementAccelerationTarget = 3;
                    break;
                case 3:
                    if (dialogues.ContainsKey(6))
                    {
                        break;
                    }
                    if (current.Selection is not int selection)
                    {
                        throw Fault(fiber, "DRAWPOINT selection closed without its actual channel6 result.");
                    }
                    if (selection == 0)
                    {
                        return CompleteDrawPointInteraction(fiber, true);
                    }
                    Scene.DrawPointInteraction = current with { Selection = selection - 1 };
                    actor.NativeMovementAccelerationTarget = 4;
                    break;
                case 4:
                    int combatSlot = FindDrawPointCombatSlot(Scene.Party[current.Selection!.Value]);
                    if ((current.EligibleCombatSlots & (1 << combatSlot)) != 0)
                    {
                        ActorState receiver = ResolveNativeIndexedPartyBody(current.Selection.Value, out _, out _) ?? throw Fault(fiber, "DRAWPOINT receiver has no retained native body.");
                        PreparedGlobalSoundEmission sound = PrepareDrawPointSound(67);
                        StartNativeDrawPointAcquisition(receiver);
                        services.SetNativeEventOwners(services.WorldControls!, globalSoundProcess: sound.Process);
                        PublishDrawPointSound(sound);
                        actor.NativeMovementAccelerationTarget = 5;
                    }
                    else
                    {
                        actor.NativeMovementAccelerationTarget = 6;
                    }
                    Scene.DrawPointInteraction = current with { CombatSlot = combatSlot };
                    break;
                case 5:
                    if (!NativeDrawPointAcquisitionActive)
                    {
                        actor.NativeMovementAccelerationTarget = 6;
                    }
                    break;
                case 6:
                    CompleteDrawPointAcquisition(fiber);
                    break;
                case 7:
                    if (!dialogues.ContainsKey(6))
                    {
                        NativeEventRandomState random = services.EventRandom!;
                        NativeDrawPointMemory bank = random.DrawPoints!;
                        int address = 116 + current.PointIndex / 4;
                        int shift = 2 * (current.PointIndex & 3);
                        int value = (bank.Read(address, 1) & ~(3 << shift)) | (((current.PointByte & 0x40) != 0 ? 2 : 3) << shift);
                        services.SetNativeEventOwners(services.WorldControls!, eventRandom: random with { DrawPoints = bank.Write(address, 1, value) });
                        return CompleteDrawPointInteraction(fiber, false);
                    }
                    break;
            }
        }
        // Native return1 retains the instruction/operand until the next actual VM callback.
        fiber.Position--;
        WaitFrames(fiber, 1);
        return [];
    }

    private int[] CompleteDrawPointInteraction(Fiber fiber, bool zeroResult)
    {
        if (zeroResult)
        {
            fiber.Results[0] = NativeScalarValue.Exact(0);
        }
        Scene.DrawPointInteraction = null;
        return Pop(fiber, 1);
    }

    private PreparedGlobalSoundEmission PrepareDrawPointSound(uint effect)
    {
        return PrepareGlobalSound(effect, 0, 128, 127, services.GlobalSoundProcess ?? throw new NotSupportedException("DRAWPOINT requires the current global sound allocator."));
    }

    private void PublishDrawPointSound(PreparedGlobalSoundEmission? sound)
    {
        if (sound is not null)
        {
            Scene.GlobalSoundRequests.Add(sound.Request);
            AudioCommandExecuted?.Invoke(sound.Request);
        }
    }

    private int FindDrawPointCombatSlot(int identity)
    {
        NativeCharacterVitals vitals = services.CharacterVitals!;
        for (int slot = 0; slot < 3; slot++)
        {
            byte row = vitals.CombatParty[slot];
            if (row != 255 && vitals.Records[row].CharacterId == identity)
            {
                return slot;
            }
        }
        throw new NotSupportedException("DRAWPOINT field identity has no current native combat slot.");
    }

    private void ShowDrawPointMessage(Fiber fiber, NativeDrawPointCatalog catalog, int message)
    {
        NativeDrawPointInteraction current = Scene.DrawPointInteraction!;
        string text = catalog.FormatMessage(message, current.PointByte & 63, current.Selection is int selected ? Scene.Party[selected] : 0);
        Scene.DrawPointInteraction = current with { Message = message };
        ShowDialogue(fiber, new DialogueState(6, -1, text, 0, 0), false);
    }

    private void ShowDrawPointSelection(Fiber fiber)
    {
        NativeDrawPointCatalog catalog = ReadNativeDrawPointCatalog!();
        NativeDrawPointInteraction current = Scene.DrawPointInteraction!;
        int count = Scene.Party.TakeWhile(identity => identity != 255).Count();
        if (count == 0)
        {
            throw Fault(fiber, "DRAWPOINT has no field-party selection entries.");
        }
        string text = catalog.FormatSelection(Scene.Party.ToArray(), services.CharacterVitals!, current.PointByte & 63, current.EligibleCombatSlots);
        Scene.DrawPointInteraction = current with { Message = 4, Selection = null };
        // Native selection result0 cancels;1..count are retained field-party entries.
        ShowDialogue(fiber, new DialogueState(6, -1, text, 0, 0, FirstChoice: 1, LastChoice: count + 1, InitialChoice: 2, CancelChoice: 1), false);
    }

    private void CompleteDrawPointAcquisition(Fiber fiber)
    {
        NativeDrawPointInteraction current = Scene.DrawPointInteraction!;
        NativeCharacterVitals vitals = services.CharacterVitals!;
        int acquired = 0;
        if ((current.EligibleCombatSlots & (1 << current.CombatSlot!.Value)) != 0)
        {
            byte row = vitals.CombatParty[current.CombatSlot.Value];
            for (; acquired < current.Quantity; acquired++)
            {
                NativeCharacterVitals next = vitals.AddMagicUnit(row, current.PointByte & 63, out int result);
                if (result != 0)
                {
                    break;
                }
                vitals = next;
            }
            services.SetNativeEventOwners(services.WorldControls!, characterVitals: vitals);
            services.SetMessageVariables((services.MessageVariables ?? NativeMessageVariables.Unknown()).WithWrite(7, acquired));
        }
        Scene.DrawPointInteraction = current with { Acquired = acquired };
        ShowDrawPointMessage(fiber, ReadNativeDrawPointCatalog!(), acquired > 0 ? 5 : 6);
        actors[fiber.ObjectId].NativeMovementAccelerationTarget = acquired > 0 ? (short)7 : (short)1;
    }

    private void RecordDrawPointChoice(DialogueState dialogue, int? choice)
    {
        if (choice is int selected && dialogue.Channel == 6 && Scene.DrawPointInteraction is { Message: 4 } current && actors[current.ObjectId].NativeMovementAccelerationTarget == 3)
        {
            Scene.DrawPointInteraction = current with { Selection = selected - dialogue.FirstChoice!.Value };
        }
    }

    private byte ReadDrawPointDepletion(byte index)
    {
        NativeDrawPointMemory bank = services.EventRandom!.DrawPoints!;
        return (byte)((bank.Depletion[index / 4] >> (2 * (index & 3))) & 3);
    }

    private void ValidateDrawPointInteractionCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.DrawPointInteraction is not NativeDrawPointInteraction current)
        {
            return;
        }
        NativeDrawPointCatalog catalog = ReadNativeDrawPointCatalog?.Invoke() ?? throw new InvalidDataException("Saved DRAWPOINT requires its original resource provider.");
        catalog.Validate();
        FiberCheckpoint? owner = saved.Fibers.FirstOrDefault(fiber => fiber.Sequence == current.OwnerSequence);
        byte index = unchecked((byte)(current.RawArgument - 1));
        uint next = unchecked(current.RandomStateBefore * 1103515245u + 12345u);
        int basis = unchecked((sbyte)current.PointByte) < 0 ? 20 : 10;
        if (current.DepletionAtIssue == 1)
        {
            basis /= 2;
        }
        int quantity = basis * ((int)((next >> 16) & 255) + 128) / 512 + 1;
        DialogueState? dialogue = saved.Dialogues.FirstOrDefault(dialogue => dialogue.Channel == 6);
        if (current.FieldId != saved.FieldId || current.ProgramHash != saved.ProgramHash || current.Frame < 0 || current.Frame > saved.Frame || (uint)current.ObjectId >= saved.Actors.Length || owner is null || owner.Complete || owner.ObjectId != current.ObjectId || owner.ScriptId != current.ScriptId || owner.Position != current.Operation || owner.WaitReason != WaitReason.Frames || owner.Stack.Length == 0 || !owner.Stack[0].IsExact || owner.Stack[0].RequireExact() != current.RawArgument || (uint)current.Operation >= program!.Operations.Count || program.Operations[current.Operation].Opcode != Jsm.Opcode.DRAWPOINT || saved.Actors[current.ObjectId].NativeMovementAccelerationTarget is null or < 1 or > 7 || saved.Scene.DrawPointIndex != index || current.PointIndex != index || current.PointByte != catalog.PointBytes[index] || current.CommonMessageHash != catalog.CommonMessageHash || current.DepletionAtIssue > 3 || current.RandomStateAfter != next || current.Quantity != quantity || saved.EventRandom is null || saved.CharacterVitals?.KernelSha256 != catalog.KernelHash || current.EligibleCombatSlots > 7 || current.Message is < 2 or > 7)
        {
            throw new InvalidDataException("Saved DRAWPOINT lost its issued operand, sampled random/depletion quantity, common message or shared phase.");
        }
        short phase = saved.Actors[current.ObjectId].NativeMovementAccelerationTarget!.Value;
        int count = saved.Scene.Party.TakeWhile(identity => identity != 255).Count();
        if (current.Selection is < 0 || phase == 3 && current.Selection > count || phase >= 4 && (current.Selection is null || current.Selection >= count) || phase is 5 or 6 or 7 && (current.CombatSlot is null or < 0 or > 2) || phase == 7 && (current.Message != 5 || current.Acquired is null or <= 0 || current.Acquired > current.Quantity) || phase == 3 && current.Message != 4 || phase == 2 && dialogue is not null || current.Acquired is < 0 || current.Acquired > current.Quantity)
        {
            throw new InvalidDataException("Saved DRAWPOINT lost its native selection, combat slot or acquired quantity.");
        }
        if (dialogue is not null)
        {
            string expected = current.Message == 4 ? catalog.FormatSelection(saved.Scene.Party.ToArray(), saved.CharacterVitals!, current.PointByte & 63, current.EligibleCombatSlots) : catalog.FormatMessage(current.Message, current.PointByte & 63, current.Selection is int selected ? saved.Scene.Party[selected] : 0);
            if (dialogue.MessageId != -1 || dialogue.Text != expected || !dialogue.CanConfirm || dialogue.IsNamePrompt || dialogue.ObjectId != current.ObjectId || dialogue.ScriptId != current.ScriptId || dialogue.Operation != current.Operation || current.Message == 4 && (dialogue.FirstChoice != 1 || dialogue.LastChoice != count + 1 || dialogue.InitialChoice != 2 || dialogue.CancelChoice != 1 || phase != 3 || current.Selection is not null) || current.Message != 4 && (dialogue.FirstChoice is not null || phase is not (1 or 7)))
            {
                throw new InvalidDataException("Saved DRAWPOINT lost its authenticated common text or channel6 choice metadata.");
            }
        }
    }
}
