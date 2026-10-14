using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public enum MinimalMenuKind
{
    Tutorial,
    GuardianForceName,
    TutorialBrowser
}

/// <summary>Original native request completed without presentation under the authorized minimal UI policy. Names and story variables are unchanged.</summary>
public sealed record MinimalMenuCompletion(int Version, MinimalMenuKind Kind, int FieldId, string ProgramHash, long Frame, int ObjectId, int ScriptId, int Operation, int RawArgument, int MenuId, byte Selection, int? GuardianForceId);

public sealed partial class EventEngine
{
    private string? UnsupportedMinimalMenu(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode == Jsm.Opcode.MENUTUTO)
        {
            return null;
        }
        if (operation.Opcode is not (Jsm.Opcode.TUTO or Jsm.Opcode.MENUNAME))
        {
            return null;
        }
        if (fiber.Stack.Count == 0)
        {
            return "Native menu request requires its original operand";
        }
        if (!fiber.Stack.PeekValue().IsExact)
        {
            return "Native menu request requires its exact original DWORD operand";
        }
        if (operation.Opcode == Jsm.Opcode.MENUNAME && fiber.Stack.Peek() is not (0 or 1 or 2) && !TryNativeGuardianForceName(fiber.Stack.Peek(), out _, out _))
        {
            return "MENUNAME argument requires its original character0..2 or Guardian Force3..18 naming request; other menu subsystems remain unsupported";
        }
        if (operation.Opcode == Jsm.Opcode.MENUNAME && TryNativeGuardianForceName(fiber.Stack.Peek(), out _, out int id))
        {
            if (services.CharacterVitals is not Memoria.FF8.Runtime.NativeCharacterVitals state)
            {
                return "MENUNAME GF acquisition requires its authentic saved GF flags and inputs; no GF record is constructed from a naming receipt";
            }
            _ = state.AcquireGuardianForce(id);
        }
        return null;
    }

    private void CompleteMinimalMenu(Fiber fiber, Jsm.Opcode opcode, int argument)
    {
        // TODO(TD-011): replace automatic menu completion with the original menu UI
        bool tutorial = opcode is Jsm.Opcode.TUTO or Jsm.Opcode.MENUTUTO;
        bool browser = opcode == Jsm.Opcode.MENUTUTO;
        int menuId = browser ? 26 : 29;
        int? guardianForce = null;
        if (!tutorial)
        {
            if (!TryNativeGuardianForceName(argument, out menuId, out int guardianForceId))
            {
                throw new InvalidOperationException("Character naming must use its existing name prompt rather than a GF completion.");
            }
            guardianForce = guardianForceId;
        }
        MinimalMenuCompletion completion = new(1, browser ? MinimalMenuKind.TutorialBrowser : tutorial ? MinimalMenuKind.Tutorial : MinimalMenuKind.GuardianForceName, FieldId, programHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, argument, menuId, browser ? (byte)1 : tutorial ? (byte)argument : (byte)1, guardianForce);
        if (guardianForce is int id)
        {
            // Native47E480 ORs mask1 only, preserving every other saved GF input
            // and its last produced maximum. Acquisition does not rebuild stats.
            CommitSavedCharacters(fiber, services.CharacterVitals!.AcquireGuardianForce(id), opcode);
        }
        Scene.MinimalMenuWaits.Add(fiber.Sequence, Scene.MinimalMenuCompletions.Count);
        Scene.MinimalMenuCompletions.Add(completion);
        // Native return3 advances the PC and ends this instruction budget.
        // Automatic completion resumes at the next ordinary update.
        fiber.WaitUntil = Frame + 1;
        fiber.WaitReason = WaitReason.MinimalMenu;
    }

    private void ValidateMinimalMenuCheckpoint(Checkpoint saved, IReadOnlyDictionary<long, Fiber> bySequence)
    {
        foreach (MinimalMenuCompletion completion in saved.Scene.MinimalMenuCompletions)
        {
            if (completion is null || completion.Version != 1 || !Enum.IsDefined(completion.Kind) || completion.FieldId is < 0 or > ushort.MaxValue || completion.ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || completion.Frame < 0 || completion.ObjectId < 0 || completion.ScriptId < 0 || completion.Operation < 0)
            {
                throw new InvalidDataException("Saved minimal menu completion lacks valid original request provenance.");
            }
            bool tutorial = completion.Kind == MinimalMenuKind.Tutorial;
            if (completion.Kind == MinimalMenuKind.TutorialBrowser)
            {
                if (completion.RawArgument != 0 || completion.MenuId != 26 || completion.Selection != 1 || completion.GuardianForceId is not null)
                {
                    throw new InvalidDataException("Saved tutorial browser changed its zero-operand native menu26 request.");
                }
                continue;
            }
            if (tutorial ? completion.MenuId != 29 || completion.Selection != (byte)completion.RawArgument || completion.GuardianForceId is not null : !TryNativeGuardianForceName(completion.RawArgument, out int menuId, out int guardianForceId) || completion.MenuId != menuId || completion.Selection != 1 || completion.GuardianForceId != guardianForceId || saved.CharacterVitals is null || (saved.CharacterVitals.GuardianForces[guardianForceId].Flags & 1) == 0)
            {
                throw new InvalidDataException("Saved minimal menu completion changed its original tutorial byte or GF acquisition request.");
            }
        }
        foreach ((long sequence, int index) in saved.Scene.MinimalMenuWaits)
        {
            if ((uint)index >= saved.Scene.MinimalMenuCompletions.Count || !bySequence.TryGetValue(sequence, out Fiber? owner) || owner.Complete || owner.WaitReason != WaitReason.MinimalMenu)
            {
                throw new InvalidDataException("Saved minimal menu yield lacks its completed request or live original owner.");
            }
            MinimalMenuCompletion completion = saved.Scene.MinimalMenuCompletions[index];
            Jsm.Opcode opcode = completion.Kind switch
            {
                MinimalMenuKind.Tutorial => Jsm.Opcode.TUTO,
                MinimalMenuKind.TutorialBrowser => Jsm.Opcode.MENUTUTO,
                _ => Jsm.Opcode.MENUNAME
            };
            if (completion.FieldId != saved.FieldId || completion.ProgramHash != saved.ProgramHash || completion.Frame > saved.Frame || owner.ObjectId != completion.ObjectId || owner.Script.Id != completion.ScriptId || owner.Position != completion.Operation + 1 || owner.WaitUntil != completion.Frame + 1 || (uint)completion.Operation >= program!.Operations.Count || program.Operations[completion.Operation].Opcode != opcode || saved.Scene.MinimalMenuWaits.Values.Count(value => value == index) != 1)
            {
                throw new InvalidDataException("Saved minimal menu yield changed its issuing operation, advanced PC or next-update resume time.");
            }
        }
        if (bySequence.Values.Any(fiber => fiber.WaitReason == WaitReason.MinimalMenu && !saved.Scene.MinimalMenuWaits.ContainsKey(fiber.Sequence)))
        {
            throw new InvalidDataException("Saved minimal menu yield has no completed original request.");
        }
    }

    private static bool TryNativeGuardianForceName(int argument, out int menuId, out int guardianForceId)
    {
        guardianForceId = argument switch
        {
            3 => 0,
            4 => 1,
            5 => 2,
            6 => 3,
            7 => 4,
            8 => 6,
            9 => 5,
            10 => 7,
            11 => 8,
            12 => 9,
            13 => 10,
            14 => 12,
            15 => 11,
            16 => 13,
            17 => 14,
            18 => 15,
            _ => -1
        };
        menuId = guardianForceId < 0 ? -1 : guardianForceId + 5;
        return guardianForceId >= 0;
    }
}
