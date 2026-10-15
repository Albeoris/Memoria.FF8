using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

/// <summary>Executed no-eligible522770 request; actor WORD516 remains the sole phase word.</summary>
public sealed record NativeDrawPointInteraction([property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] long Frame, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] long OwnerSequence, [property: JsonRequired] int RawArgument, [property: JsonRequired] byte PointIndex, [property: JsonRequired] byte PointByte, [property: JsonRequired] byte DepletionAtIssue, [property: JsonRequired] uint RandomStateBefore, [property: JsonRequired] uint RandomStateAfter, [property: JsonRequired] short Quantity, [property: JsonRequired] string CommonMessageHash);

public sealed partial class EventEngine
{
    public Func<NativeDrawPointCatalog>? ReadNativeDrawPointCatalog { get; set; }

    private (NativeDrawPointCatalog Catalog, NativeCharacterVitals Vitals) PrepareNoEligibleDrawPoint()
    {
        NativeDrawPointCatalog catalog = ReadNativeDrawPointCatalog?.Invoke() ?? throw new NotSupportedException("DRAWPOINT requires its authenticated point table and original common message provider.");
        catalog.Validate();
        NativeCharacterVitals vitals = services.CharacterVitals ?? throw new NotSupportedException("DRAWPOINT requires its actual saved character and GF inputs.");
        if (catalog.KernelHash != vitals.KernelSha256)
        {
            throw new NotSupportedException("DRAWPOINT and saved character command definitions have different kernel provenance.");
        }
        NativeWorldControlState controls = services.WorldControls ?? throw new NotSupportedException("DRAWPOINT requires actual eventDWORD104 command restrictions.");
        byte disabled = 0;
        if ((controls.EventFlags104 & 0x800) != 0)
        {
            if (EventMemory.Cells.TryGetValue(243, out NativeMemoryValue? source) != true || source is null || source.Width != 1 || !source.Value.IsExact)
            {
                throw new NotSupportedException("DRAWPOINT's restricted command branch requires its actual source BYTE243.");
            }
            disabled = checked((byte)source.Value.RequireExact());
        }
        NativeCharacterVitals rebuilt = vitals.RebuildParty();
        if ((disabled & 8) == 0 && rebuilt.CombatParty.Any(row => row != 255 && catalog.HasDrawCommand(rebuilt.Records[row])))
        {
            throw new NotSupportedException("DRAWPOINT has an eligible Draw command; its explicit character choice, particle completion and magic acquisition stages are not yet implemented.");
        }
        return (catalog, rebuilt);
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
            if (current.OwnerSequence != fiber.Sequence || current.Operation != fiber.Position || current.RawArgument != fiber.Stack.Peek() || actors[fiber.ObjectId].NativeMovementAccelerationTarget != 1)
            {
                return "DRAWPOINT requires its single live original request and shared WORD516 phase";
            }
            if (dialogues.ContainsKey(6))
            {
                return null;
            }
            if (ReadDrawPointDepletion(current.PointIndex) is 2 or 3)
            {
                return null;
            }
        }
        else if (dialogues.ContainsKey(6) || actors[fiber.ObjectId].NativeScriptMovement is { Active: true, Suspended: false, Accelerated: true })
        {
            return "DRAWPOINT requires its actual free channel6 and a proved WORD516 consumer boundary";
        }
        try
        {
            _ = PrepareNoEligibleDrawPoint();
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
        if (Scene.DrawPointInteraction is null)
        {
            (NativeDrawPointCatalog catalog, NativeCharacterVitals vitals) = PrepareNoEligibleDrawPoint();
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
            try
            {
                services.SetNativeEventOwners(services.WorldControls!, characterVitals: vitals, eventRandom: next);
            }
            catch (NotSupportedException exception)
            {
                throw NativeOwnerPublicationFault(fiber, exception);
            }
            Scene.DrawPointIndex = index;
            Scene.DrawPointInteraction = new NativeDrawPointInteraction(FieldId, programHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, raw, index, point, depletion, before.State, next.State, quantity, catalog.CommonMessageHash);
            actor.NativeMovementAccelerationTarget = 1;
            // Common message7 is not a field MSD index. Its native source is retained above.
            // Position0/0 belongs to the existing minimal host panel, not claimed native window geometry.
            ShowDialogue(fiber, new DialogueState(6, -1, catalog.NoEligibleMessage, 0, 0), false);
        }
        else if (!dialogues.ContainsKey(6))
        {
            if (ReadDrawPointDepletion(Scene.DrawPointInteraction.PointIndex) is not (2 or 3))
            {
                (_, NativeCharacterVitals vitals) = PrepareNoEligibleDrawPoint();
                try
                {
                    services.SetNativeEventOwners(services.WorldControls!, characterVitals: vitals);
                }
                catch (NotSupportedException exception)
                {
                    throw NativeOwnerPublicationFault(fiber, exception);
                }
            }
            fiber.Results[0] = NativeScalarValue.Exact(0);
            Scene.DrawPointInteraction = null;
            return Pop(fiber, 1);
        }
        // Native return1 retains the instruction/operand until the next actual VM callback.
        fiber.Position--;
        WaitFrames(fiber, 1);
        return [];
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
        if (current.FieldId != saved.FieldId || current.ProgramHash != saved.ProgramHash || current.Frame < 0 || current.Frame > saved.Frame || (uint)current.ObjectId >= saved.Actors.Length || owner is null || owner.Complete || owner.ObjectId != current.ObjectId || owner.ScriptId != current.ScriptId || owner.Position != current.Operation || owner.WaitReason != WaitReason.Frames || owner.Stack.Length == 0 || !owner.Stack[0].IsExact || owner.Stack[0].RequireExact() != current.RawArgument || (uint)current.Operation >= program!.Operations.Count || program.Operations[current.Operation].Opcode != Jsm.Opcode.DRAWPOINT || saved.Actors[current.ObjectId].NativeMovementAccelerationTarget != 1 || saved.Scene.DrawPointIndex != index || current.PointIndex != index || current.PointByte != catalog.PointBytes[index] || current.CommonMessageHash != catalog.CommonMessageHash || current.DepletionAtIssue > 3 || current.RandomStateAfter != next || current.Quantity != quantity || saved.EventRandom is null || saved.CharacterVitals?.KernelSha256 != catalog.KernelHash || dialogue is not null && (dialogue.MessageId != -1 || dialogue.Text != catalog.NoEligibleMessage || !dialogue.CanConfirm || dialogue.FirstChoice is not null || dialogue.IsNamePrompt || dialogue.ObjectId != current.ObjectId || dialogue.ScriptId != current.ScriptId || dialogue.Operation != current.Operation))
        {
            throw new InvalidDataException("Saved DRAWPOINT lost its issued operand, sampled random/depletion quantity, common message or shared phase.");
        }
    }
}
