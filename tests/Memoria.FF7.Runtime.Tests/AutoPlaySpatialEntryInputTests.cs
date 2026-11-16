using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlaySpatialEntryInputTests
{
    internal static void Run()
    {
        FieldSpatialInputGuard Guard(FieldScriptInputKind kind, ushort mask, bool matched = true, bool mapped = true)
        {
            return new(new("authored", "program", 13, 7, 0x100, kind, mask, mapped, false), matched);
        }
        FieldInputState Project(ushort held, FieldInputState previous)
        {
            ushort pressed = (ushort)(held & ~previous.Held);
            return new(held, pressed, (ushort)(previous.Held & ~held), held, pressed, (ushort)(previous.RawHeld & ~held), PreviousHeld: previous.Held, PreviousRawHeld: previous.RawHeld);
        }
        FieldSpatialInputGuard[] either = [Guard(FieldScriptInputKind.Held, 0x220)];
        Ff7AutoplayInput ordinary = Ff7AutoPlayScriptedInput.SelectSpatialEntryInput(either, new());
        Check(ordinary.Held == 0x20 && !ordinary.Run && ordinary.Horizontal == 0 && ordinary.Vertical == 0, "A native ANY-mask gate must select one ordinary button through the established projection.");
        FieldSpatialInputGuard[] conjunction = [Guard(FieldScriptInputKind.Held, 0x60), Guard(FieldScriptInputKind.Held, 0x40), Guard(FieldScriptInputKind.Held, 0x20, false)];
        Ff7AutoplayInput combined = Ff7AutoPlayScriptedInput.SelectSpatialEntryInput(conjunction, new());
        Check(combined.Held == 0x40 && FieldMachine.AreSpatialInputGuardsSatisfied(conjunction, Project(combined.Held, new())), "AND predicates and unmatched native branches must retain their exact physical constraints.");
        foreach (FieldScriptInputKind kind in new[] { FieldScriptInputKind.Pressed, FieldScriptInputKind.Released })
        {
            FieldSpatialInputGuard[] edge = [Guard(kind, 0x20)];
            FieldInputState current = kind == FieldScriptInputKind.Pressed ? new(0x20, RawHeld: 0x20) : new();
            Ff7AutoplayInput prepare = Ff7AutoPlayScriptedInput.SelectSpatialEntryInput(edge, current);
            FieldInputState afterPreparation = Project(prepare.Held, current);
            Check(!FieldMachine.AreSpatialInputGuardsSatisfied(edge, afterPreparation), "An unavailable native edge must use a separate physical preparation callback.");
            Ff7AutoplayInput execute = Ff7AutoPlayScriptedInput.SelectSpatialEntryInput(edge, afterPreparation);
            Check(FieldMachine.AreSpatialInputGuardsSatisfied(edge, Project(execute.Held, afterPreparation)), "The next ordinary callback must produce the exact pressed or released edge.");
        }
        FieldSpatialInputGuard[] shared = [Guard(FieldScriptInputKind.Held, 0x20), Guard(FieldScriptInputKind.Held, 0x20, false, mapped: false)];
        bool rejected = false;
        try
        {
            _ = Ff7AutoPlayScriptedInput.SelectSpatialEntryInput(shared, new());
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }
        Check(rejected, "Mapped and raw predicates that contradict the shared physical button stream must fail explicitly.");
        Check(Ff7AutoPlayDiscovery.SpatialInputGuardSignature(conjunction) == Ff7AutoPlayDiscovery.SpatialInputGuardSignature(conjunction.Reverse().ToArray()) && Ff7AutoPlayDiscovery.SpatialInputGuardSignature(either) != Ff7AutoPlayDiscovery.SpatialInputGuardSignature([Guard(FieldScriptInputKind.Held, 0x220, false)]), "Spatial input contract identity must be stable across predicate order while preserving alternative branch outcomes.");
        string direct = "spatial:13:14AF:direct:triangle:51";
        Check(Ff7AutoPlayPolicy.TryReadDirectSpatialIdentity(direct, out int entity, out int pc, out int triangle) && entity == 13 && pc == 0x14AF && triangle == 51, "Existing direct spatial identity must retain its exact native source and sampled triangle.");
        Check(Ff7AutoPlayPolicy.TryReadDirectSpatialIdentity(direct + ":input:" + Ff7AutoPlayDiscovery.SpatialInputGuardSignature(either), out _, out _, out _) && !Ff7AutoPlayPolicy.TryReadDirectSpatialIdentity(direct + ":input:invalid", out _, out _, out _) && !Ff7AutoPlayPolicy.TryReadDirectSpatialIdentity("spatial:13:14AF:direct:triangle:invalid", out _, out _, out _), "Direct transition ownership must accept only a valid optional input-contract hash and native triangle identity.");
        OriginalSpatialEntry();
    }

    private static void OriginalSpatialEntry()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_SPATIAL_ENTRY_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original spatial entry: configure original resources and MEMORIA_FF7_SPATIAL_ENTRY_SAVE.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The original spatial-entry fixture has no FFVII state.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        using (Ff7Session cold = Ff7Session.Restore(installation, saved))
        {
            Check(JsonSerializer.Serialize(session.Capture() with { AutoPlay = null }) == JsonSerializer.Serialize(cold.Capture() with { AutoPlay = null }), "Independent original spatial-entry restores must preserve the exact native continuation.");
        }
        string before = JsonSerializer.Serialize(session.Capture() with { AutoPlay = null });
        Ff7AutoPlayTarget[] candidates = new Ff7AutoPlayDiscovery(session).Observe().Events.Where(target => target.Kind == Ff7AutoPlayTargetKind.AutonomousSpatial && target.SpatialInputGuards.Length > 0 && target.AnalysisComplete).ToArray();
        Check(candidates.Length > 0 && before == JsonSerializer.Serialize(session.Capture() with { AutoPlay = null }), "The original guarded spatial source must be discovered without changing native state.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("The original spatial-entry regression attempted an unrelated choice checkpoint."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayTarget? selected = null;
        int startZ = 0;
        bool admitted = false;
        int? firstLadderTargetZ = null;
        int projected = 0;
        session.NativeTickCompleted += observation =>
        {
            if (selected?.SpatialOwner is not { } owner)
            {
                return;
            }
            admitted |= (observation.ScriptActivity ?? []).Any(activity => activity.Dispatch is { Accepted: true } dispatch && dispatch.SourceEntity == selected.Entity && dispatch.SourcePriority == owner.SourcePriority && dispatch.SourceProgramCounter == owner.SourceProgramCounter && dispatch.TargetEntity == owner.TargetEntity && dispatch.TargetScript == owner.TargetScript && dispatch.TargetPriority == owner.TargetPriority && dispatch.RequestMode == owner.RequestMode);
        };
        Ff7AutoPlayController controller = new(session, driver =>
        {
            Ff7AutoplayInput action = policy.ChooseInput(driver);
            if (policy.State.Active is { Kind: Ff7AutoPlayTargetKind.AutonomousSpatial, Accepted: false } active)
            {
                Ff7AutoPlayTarget? target = new Ff7AutoPlayDiscovery(session).FindTarget(active.EventId);
                if (target is { SpatialInputGuards.Length: > 0 })
                {
                    Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                    selected ??= target;
                    if (projected == 0)
                    {
                        startZ = player.Z4096;
                    }
                    if (action.Held != 0)
                    {
                        Check(!target.TriangleOnly || player.Triangle == target.Triangle, "A selected original spatial branch received buttons before its native triangle predicate was ready.");
                        projected++;
                    }
                }
            }
            return action;
        }, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        controller.Start();
        bool descended = false;
        for (int callback = 0; callback < 512 && !descended; callback++)
        {
            controller.Advance(1);
            Check(controller.Error is null, controller.Error ?? "The original spatial-entry continuation failed.");
            Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
            if (admitted && player.Ladder is { } ladder)
            {
                firstLadderTargetZ ??= ladder.TargetZ;
            }
            descended = admitted && firstLadderTargetZ is int targetZ && player.Z4096 != startZ && player.Z4096 == targetZ && player.Ladder is not { Completed: false };
        }
        Ff7Actor final = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Check(projected > 0 && admitted && descended, "The original selected spatial input must admit its exact native request owner and complete the first native ladder within 512 ordinary callbacks.");
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "Original spatial-entry continuation changed the supplied standalone bytes.");
        Console.WriteLine($"PASS original guarded spatial entry {selected!.Id}, {controller.Steps} ordinary callbacks, {projected} physical input callbacks, first ladder targetZ={firstLadderTargetZ / 4096f}, final=({final.X4096 / 4096f},{final.Y4096 / 4096f},{final.Z4096 / 4096f}) triangle={final.Triangle}; source unchanged.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
