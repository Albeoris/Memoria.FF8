using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    /// <summary>Native ISMEMBER queries for identities0..7; REST eligibility separately uses physical row flags.</summary>
    public IReadOnlySet<int>? NormalMembers => services.CharacterVitals is NativeCharacterVitals state ? Enumerable.Range(0, 8).Where(character => state.Records.FirstOrDefault(row => row.CharacterId == character) is { } row && (row.MemberFlags & 1) != 0).ToHashSet() : null;
    public IReadOnlySet<int>? AcquiredGuardianForces => services.CharacterVitals is NativeCharacterVitals state ? state.GuardianForces.Select((row, index) => (row, index)).Where(item => (item.row.Flags & 1) != 0).Select(item => item.index).ToHashSet() : null;

    private string? UnsupportedSavedCharacterCommand(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.REST or Jsm.Opcode.ADDMEMBER or Jsm.Opcode.SUBMEMBER or Jsm.Opcode.ISMEMBER))
        {
            return null;
        }
        if (operation.Opcode != Jsm.Opcode.REST && (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact))
        {
            return "Native membership requires its exact original full-DWORD operand";
        }
        int raw = operation.Opcode == Jsm.Opcode.REST ? 0 : fiber.Stack.Peek();
        bool guestAdd = operation.Opcode == Jsm.Opcode.ADDMEMBER && raw is 8 or 9 or 10;
        bool guestQuery = operation.Opcode == Jsm.Opcode.ISMEMBER && raw >= 8;
        NativeCharacterVitals? state = services.CharacterVitals;
        if (!guestAdd && !guestQuery && state is null)
        {
            return "Native saved-character command requires authentic character/GF rows and kernel definitions; no membership or health defaults are inferred";
        }
        try
        {
            if (!guestAdd && !guestQuery)
            {
                state!.Validate();
            }
            if (operation.Opcode == Jsm.Opcode.REST)
            {
                _ = state!.Rest();
            }
            else if (operation.Opcode != Jsm.Opcode.ISMEMBER && !guestAdd)
            {
                _ = state!.WithMemberFlags(raw, operation.Opcode == Jsm.Opcode.ADDMEMBER);
            }
            if (operation.Opcode != Jsm.Opcode.REST && (operation.Opcode == Jsm.Opcode.ISMEMBER ? fiber.Stack.Peek() >= 8 : fiber.Stack.Peek() is 8 or 9 or 10))
            {
                if (services.WorldControls is not NativeWorldControlState controls)
                {
                    return "Native guest membership requires the authentic event DWORD104 owner";
                }
                controls.Validate();
                if (operation.Opcode == Jsm.Opcode.SUBMEMBER && !services.CanStoreNativeEventOwners)
                {
                    return "SUBMEMBER guest reset and event flags require atomic saved-owner publication";
                }
            }
            return null;
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private void CommitSavedCharacters(Fiber fiber, NativeCharacterVitals next, Jsm.Opcode opcode)
    {
        try
        {
            services.SetCharacterVitals(next);
        }
        catch (NotSupportedException exception)
        {
            fiber.Position--;
            string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native saved-character service rejected {opcode}; PC and operands preserved.";
            LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, opcode.ToString(), message);
            throw new EventScriptException(message, exception);
        }
    }

    private void ExecuteRest(Fiber fiber)
    {
        NativeCharacterVitals next = services.CharacterVitals!.Rest();
        CommitSavedCharacters(fiber, next, Jsm.Opcode.REST);
    }

    private int[] ExecuteMembership(Fiber fiber, Jsm.Opcode opcode)
    {
        int raw = fiber.Stack.Peek();
        if (opcode == Jsm.Opcode.ISMEMBER)
        {
            int value;
            if (raw < 8)
            {
                NativeCharacterVital? row = services.CharacterVitals!.Records.FirstOrDefault(candidate => candidate.CharacterId == unchecked((byte)raw));
                value = row is null ? 0 : row.MemberFlags & 1;
            }
            else
            {
                uint mask = 1u << unchecked(raw - 8);
                value = unchecked((int)(services.WorldControls!.EventFlags104 & mask));
            }
            fiber.Results[0] = value;
        }
        else
        {
            NativeCharacterVitals? next = opcode == Jsm.Opcode.ADDMEMBER && raw is 8 or 9 or 10 ? null : services.CharacterVitals!.WithMemberFlags(raw, opcode == Jsm.Opcode.ADDMEMBER);
            if (raw is 8 or 9 or 10)
            {
                NativeWorldControlState controls = services.WorldControls!;
                NativeWorldControlState nextControls = controls.WithGuestMember(raw, opcode == Jsm.Opcode.ADDMEMBER);
                try
                {
                    if (opcode == Jsm.Opcode.ADDMEMBER)
                    {
                        services.SetWorldControls(nextControls);
                    }
                    else
                    {
                        services.SetNativeEventOwners(nextControls, characterVitals: next);
                    }
                }
                catch (NotSupportedException exception)
                {
                    fiber.Position--;
                    string message = $"Field {FieldId}, object {fiber.ObjectId}, script {fiber.Script.Id}, operation {fiber.Position}: native guest membership service rejected {opcode}; PC and operands preserved.";
                    LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, opcode.ToString(), message);
                    throw new EventScriptException(message, exception);
                }
            }
            else
            {
                CommitSavedCharacters(fiber, next!, opcode);
            }
        }
        return Pop(fiber, 1);
    }
}
