using Albeoris.Games.FF8.Jsm;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

/// <summary>The original TUTO or MENUTUTO request owning the suspended field VM. Browser requests have no tutorial ID.</summary>
public sealed record NativeTutorialRequest([property: JsonRequired] int Version, [property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] long Frame, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] long FiberSequence, [property: JsonRequired] int RawArgument, [property: JsonRequired] int MenuId, [property: JsonRequired] byte? TutorialId);

public sealed record NativeMenuTutorialRequest([property: JsonRequired] int Version, [property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramHash, [property: JsonRequired] long Frame, [property: JsonRequired] int TutorialId);

public sealed partial class EventEngine
{
    /// <summary>Opens an isolated lesson selected from the native tutorial browser.</summary>
    public void BeginMenuTutorial(int tutorialId)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)tutorialId, 9u);
        if (program is null || !defaultsStarted || Frame <= 0 || LastFault is not null || Scene.NativeMenuTutorial is not null || Scene.NativeTutorial is { TutorialId: not null } || partyMenu is not null && (partyMenu.Kind != NativeFieldMenuKind.Normal || partyMenu.Phase != NativePartyMenuPhase.Active) || PendingTripleTriad is not null || HasPendingBattle || transitionRequested || services.IsMoviePlaying)
        {
            throw new InvalidOperationException("The tutorial browser requires an initialized field and exclusive lesson ownership.");
        }
        Scene.NativeMenuTutorial = new NativeMenuTutorialRequest(1, FieldId, programHash, Frame, tutorialId);
        OperationCompleted?.Invoke();
    }

    /// <summary>Returns a browser-selected lesson to its retained browser without advancing the field.</summary>
    public void EndMenuTutorial()
    {
        if (Scene.NativeMenuTutorial is null)
        {
            throw new InvalidOperationException("No browser-selected tutorial is active.");
        }
        Scene.NativeMenuTutorial = null;
        Scene.NativeTutorialPlayback = null;
        OperationCompleted?.Invoke();
    }

    /// <summary>Baselines the continuously polled native input while a tutorial menu owns the field.</summary>
    public void ObserveNativeMenuButtons(ushort raw)
    {
        if (Scene.NativeTutorial is null && Scene.NativeMenuTutorial is null)
        {
            throw new InvalidOperationException("Native menu button observation requires an active tutorial owner.");
        }
        Memoria.FF8.Runtime.NativeInputConfiguration configuration = services.InputConfiguration ?? throw new NotSupportedException("Native menu input requires its actual saved input configuration.");
        Memoria.FF8.Runtime.NativeFieldButtonInput current = services.FieldButtonInput ?? throw new NotSupportedException("Native menu input requires its current sampler owner.");
        current.ValidateSample(FieldId, Frame, configuration);
        services.SetFieldButtonInput(current with { PhysicalHeld = raw, RawHeld = raw, RawPressed = 0, PendingPulse = 0, Held = configuration.Map(raw), Pressed = 0 });
        OperationCompleted?.Invoke();
    }

    /// <summary>Closes the native tutorial UI and resumes the next ordinary field instruction budget.</summary>
    public void CompleteNativeTutorial()
    {
        if (Scene.NativeMenuTutorial is not null)
        {
            throw new InvalidOperationException("Close the browser-selected lesson before returning its owning field menu.");
        }
        NativeTutorialRequest request = Scene.NativeTutorial ?? throw new InvalidOperationException("No native tutorial owns the suspended field VM.");
        Fiber fiber = fibers.SingleOrDefault(fiber => fiber.Sequence == request.FiberSequence && !fiber.Complete && fiber.WaitReason == WaitReason.NativeTutorial && fiber.ObjectId == request.ObjectId && fiber.Script.Id == request.ScriptId && fiber.Position == request.Operation + 1) ?? throw new InvalidDataException("Native tutorial lost its suspended issuing fiber.");
        Scene.NativeTutorial = null;
        Scene.NativeTutorialPlayback = null;
        fiber.WaitReason = WaitReason.None;
        StepCore();
        OperationCompleted?.Invoke();
    }

    private void RequestNativeTutorial(Fiber fiber, Jsm.Opcode opcode, int argument)
    {
        bool browser = opcode == Jsm.Opcode.MENUTUTO;
        Scene.NativeTutorial = new NativeTutorialRequest(1, FieldId, programHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, fiber.Sequence, argument, browser ? 26 : 29, browser ? null : unchecked((byte)argument));
        // Native return3 has consumed its operand and advanced its PC. The menu owns
        // the field until explicit close; elapsed field callbacks cannot satisfy it.
        fiber.WaitUntil = 0;
        fiber.WaitReason = WaitReason.NativeTutorial;
    }

    private void ValidateNativeTutorialCheckpoint(Checkpoint saved, IReadOnlyDictionary<long, Fiber> bySequence)
    {
        NativeTutorialRequest? request = saved.Scene.NativeTutorial;
        if (saved.Scene.NativeTutorialPlayback is System.Text.Json.JsonElement playback && (request is null && saved.Scene.NativeMenuTutorial is null || playback.ValueKind != System.Text.Json.JsonValueKind.Object))
        {
            throw new InvalidDataException("Saved tutorial playback must be an object owned by a pending native tutorial request.");
        }
        NativeMenuTutorialRequest? menu = saved.Scene.NativeMenuTutorial;
        if (menu is not null && (menu.Version != 1 || menu.FieldId != saved.FieldId || menu.ProgramHash != saved.ProgramHash || menu.Frame != saved.Frame || menu.Frame <= 0 || !saved.DefaultsStarted || menu.TutorialId is < 0 or > 8 || request is { TutorialId: not null } || saved.PartyMenu is not null && (saved.PartyMenu.Kind != NativeFieldMenuKind.Normal || saved.PartyMenu.Phase != NativePartyMenuPhase.Active) || saved.Scene.TripleTriadRequest is not null || saved.Scene.NativeBattleWaits?.Count > 0 || saved.TransitionRequested || services.IsMoviePlaying))
        {
            throw new InvalidDataException("Saved browser-selected tutorial changed its initialized field, exact resource request or exclusive scene ownership.");
        }
        Fiber[] waiting = bySequence.Values.Where(fiber => fiber.WaitReason == WaitReason.NativeTutorial).ToArray();
        if (request is null)
        {
            if (waiting.Length != 0)
            {
                throw new InvalidDataException("Saved native tutorial wait has no original request.");
            }
            return;
        }
        if (request.Version != 1 || request.FieldId != saved.FieldId || request.ProgramHash != saved.ProgramHash || request.Frame != saved.Frame || request.Frame <= 0 || !saved.DefaultsStarted || request.MenuId is not (26 or 29) || request.MenuId == 26 && (request.RawArgument != 0 || request.TutorialId is not null) || request.MenuId == 29 && request.TutorialId != unchecked((byte)request.RawArgument) || waiting.Length != 1 || waiting[0].Sequence != request.FiberSequence || waiting[0].Complete || waiting[0].Initialization || !waiting[0].Started || waiting[0].ObjectId != request.ObjectId || waiting[0].Script.Id != request.ScriptId || waiting[0].Position != request.Operation + 1 || waiting[0].WaitUntil != 0 || !waiting[0].Script.ContainsOperation(request.Operation) || (uint)request.Operation >= program!.Operations.Count || program.Operations[request.Operation].Opcode != (request.MenuId == 26 ? Jsm.Opcode.MENUTUTO : Jsm.Opcode.TUTO) || saved.Scene.TripleTriadRequest is not null || saved.Scene.NativeBattleWaits?.Count > 0 || saved.TransitionRequested || saved.PartyMenu is not null || services.IsMoviePlaying)
        {
            throw new InvalidDataException("Saved native tutorial changed its original request, advanced PC, issuing fiber or exclusive scene ownership.");
        }
    }
}
