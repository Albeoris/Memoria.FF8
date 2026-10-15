using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeEventMemoryOwnershipContract()
    {
        NativeEventMemoryRegion[] regions = [new(8, 4, NativeEventMemoryOwner.SeeD), new(201, 2, NativeEventMemoryOwner.MusicProcess), new(204, 1, NativeEventMemoryOwner.Disc)];
        Require(NativeEventMemoryOwnership.Resolve(12, 1, regions) == NativeEventMemoryOwner.Generic && NativeEventMemoryOwnership.Resolve(203, 1, regions) == NativeEventMemoryOwner.Generic, "Owner resolution extended a declared span into its adjacent byte.");
        Require(NativeEventMemoryOwnership.Resolve(8, 4, regions) == NativeEventMemoryOwner.SeeD && NativeEventMemoryOwnership.Resolve(201, 2, regions) == NativeEventMemoryOwner.MusicProcess && NativeEventMemoryOwnership.Resolve(204, 1, regions) == NativeEventMemoryOwner.Disc, "Whole typed spans did not resolve to their canonical owners.");
        foreach ((int address, int width) in new[] { (7, 2), (11, 2), (200, 2), (201, 4), (203, 2), (204, 2) })
        {
            ExpectMemoryOwnershipFailure<NotSupportedException>(() => NativeEventMemoryOwnership.Resolve(address, width, regions), "A read/write span crossed registered owners or surrounding generic memory.");
        }
        NativeEventMemory adjacent = NativeEventMemory.Empty.Write(201, 1, 0).Write(12, 1, 9);
        NativeEventMemoryRegion[] discAndSeeD = [new(8, 4, NativeEventMemoryOwner.SeeD), new(204, 1, NativeEventMemoryOwner.Disc)];
        NativeEventMemoryOwnership.Validate(adjacent, discAndSeeD);
        foreach ((int address, int width) in new[] { (9, 1), (201, 4), (204, 1) })
        {
            NativeEventMemory duplicate = NativeEventMemory.Empty.Write(address, width, 0);
            ExpectMemoryOwnershipFailure<InvalidDataException>(() => NativeEventMemoryOwnership.Validate(duplicate, discAndSeeD), "An equal-valued generic cell duplicated canonical owner bytes.");
        }
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => NativeEventMemoryOwnership.Validate(NativeEventMemory.Empty, [new(204, 1, NativeEventMemoryOwner.Disc), new(204, 1, NativeEventMemoryOwner.Disc)]), "Duplicate canonical registrations were accepted.");
        NativeExamTimerState inactiveTimer = NativeExamTimerState.FromColdProcess() with { Event182Mask4 = false };
        GameSession inactiveTimerSession = new(new GameState { ExamTimer = inactiveTimer });
        IReadOnlyList<NativeEventMemoryRegion> inactiveTimerRegions = NativeEventMemoryOwnership.RootRegions(inactiveTimerSession.State);
        Require(NativeEventMemoryOwnership.Resolve(182, 2, inactiveTimerRegions) == NativeEventMemoryOwner.Timer && NativeEventMemoryOwnership.Resolve(205, 2, inactiveTimerRegions) == NativeEventMemoryOwner.Generic && inactiveTimerRegions.SequenceEqual(NativeEventMemoryOwnership.RootRegions(inactiveTimerSession)), "Known false timer flags failed to reserve WORD182 independently of missing inactive timer WORD205.");
        inactiveTimerSession.SetEventMemory(inactiveTimerSession.EventMemory.Write(205, 2, 7));
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => inactiveTimerSession.SetEventMemory(inactiveTimerSession.EventMemory.Write(182, 1, 0)), "An inactive timer's known false flag accepted a duplicate generic BYTE182.");
        Require(inactiveTimerSession.EventMemory.Read(205, 2).RequireExact() == 7 && !inactiveTimerSession.EventMemory.Overlaps(182, 2), "Timer flag alias refusal changed the independently generic inactive WORD205.");

        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0x123456), (Jsm.Opcode.POPM_L, 512), (Jsm.Opcode.PSHN_L, 0xAB), (Jsm.Opcode.POPM_B, 513), (Jsm.Opcode.PSHN_L, 0xCDEF), (Jsm.Opcode.POPM_W, 514), (Jsm.Opcode.PSHM_L, 512), (Jsm.Opcode.POPM_L, 1000), (Jsm.Opcode.PSHM_B, 513), (Jsm.Opcode.POPM_B, 1004), (Jsm.Opcode.PSHSM_W, 514), (Jsm.Opcode.POPM_L, 1008), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHM_W, 514), (Jsm.Opcode.POPM_W, 1012), (Jsm.Opcode.RET, 0));
        EventEngine component = new(new EventMemoryComponentServices());
        component.LoadField(100, content, []);
        component.Step();
        Require(component.EventMemory.Read(512, 4).RequireExact() == unchecked((int)0xCDEFAB56) && component.EventMemory.Read(1000, 4).RequireExact() == unchecked((int)0xCDEFAB56), "POPM_B/POPM_W partial writes or PSHM_L lost little-endian neighboring bytes.");
        Require(component.EventMemory.Cells[1004].Width == 1 && component.EventMemory.Read(1004, 1).RequireExact() == 0xAB && component.EventMemory.Read(1008, 4).RequireExact() == unchecked((short)0xCDEF), "Opcode memory lost its declared BYTE width or signed WORD read.");
        JsonElement held = component.Capture();
        Require(held.GetProperty("EventMemory").GetProperty("Version").GetInt32() == 1 && !held.TryGetProperty("Variables", out _) && !held.TryGetProperty("NativeVariableValues", out _), "A checkpoint persisted scalar aliases beside its authoritative typed memory.");
        ExpectMemoryOwnershipFailure<NotSupportedException>(() => ((IDictionary<int, int>)component.Variables)[512] = 99, "The public diagnostic variable view allowed mutation.");
        Require(component.EventMemory.Read(512, 4).RequireExact() == unchecked((int)0xCDEFAB56), "Attempting to mutate a diagnostic view changed the memory owner.");

        EventEngine resumed = new(new EventMemoryComponentServices());
        resumed.LoadField(100, content, []);
        resumed.Restore(held);
        Require(JsonElement.DeepEquals(held, resumed.Capture()) && resumed.EventMemory.Cells[1004].Width == 1, "Independent component restore changed the typed BYTE cell or suspended VM.");
        JsonNode duplicateCell = JsonNode.Parse(held.GetRawText())!;
        duplicateCell["EventMemory"]!["Cells"]!["513"] = new JsonObject { ["Width"] = 1, ["Value"] = 0xAB };
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => resumed.Restore(JsonSerializer.SerializeToElement(duplicateCell)), "An overlapping exact snapshot alias restored despite equal byte values.");
        Require(JsonElement.DeepEquals(held, resumed.Capture()), "Overlapping snapshot refusal changed the live VM.");
        JsonNode missingMemory = JsonNode.Parse(held.GetRawText())!;
        missingMemory.AsObject().Remove("EventMemory");
        ExpectMemoryOwnershipFailure<JsonException>(() => resumed.Restore(JsonSerializer.SerializeToElement(missingMemory)), "A snapshot without explicit typed memory acquired an inferred memory bank.");
        Require(JsonElement.DeepEquals(held, resumed.Capture()), "Missing-memory refusal changed the live VM.");
        component.Step();
        resumed.Step();
        Require(JsonElement.DeepEquals(component.Capture(), resumed.Capture()) && resumed.EventMemory.Cells[1012].Width == 2 && resumed.EventMemory.Read(1012, 2).RequireExact() == 0xCDEF, "Independent continuation lost the requested WORD width or changed the following opcode.");
        EventEngine repeated = new(new EventMemoryComponentServices());
        repeated.LoadField(100, content, []);
        repeated.Step();
        repeated.Step();
        Require(JsonElement.DeepEquals(component.Capture(), repeated.Capture()), "Repeating the synthetic source commands changed typed memory or VM state.");

        byte[] uncertainContent = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_W, 432), (Jsm.Opcode.PSHN_L, 777), (Jsm.Opcode.PSHM_B, 433), (Jsm.Opcode.RET, 0));
        GameSession timerSession = new(new GameState { ExamTimer = NativeExamTimerState.FromReachedSet(0) });
        EventEngine uncertain = new(timerSession);
        uncertain.LoadField(100, uncertainContent, []);
        uncertain.Step();
        ExpectMemoryOwnershipFailure<EventScriptException>(() => uncertain.Step(), "A partial read selected a byte from uncertain timer alternatives.");
        JsonElement refused = uncertain.Capture();
        JsonElement refusedFiber = refused.GetProperty("Fibers")[0];
        Require(uncertain.LastFault is { Opcode: "PSHM_B", Operation: 9 } && refusedFiber.GetProperty("Position").GetInt32() == 9 && refusedFiber.GetProperty("Stack").GetArrayLength() == 1 && refusedFiber.GetProperty("Stack")[0].GetInt32() == 777 && uncertain.EventMemory.Read(432, 2).Values.SequenceEqual([599, 600]), "Uncertain partial-read refusal consumed the stack, moved PC or changed memory alternatives.");
        JsonNode forgedSource = JsonNode.Parse(refused.GetRawText())!;
        forgedSource["EventMemory"]!["Cells"]!["432"]!["Value"]!["Origin"]!["Operation"] = 0;
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => uncertain.Restore(JsonSerializer.SerializeToElement(forgedSource)), "A non-GETTIMER opcode supplied accepted memory provenance.");
        Require(JsonElement.DeepEquals(refused, uncertain.Capture()), "Forged provenance refusal changed PC, stack or memory.");

        GameSession native = GameSession.CreateColdProcess();
        native.StartNewGame();
        native.SetEventMemory(native.EventMemory.Write(201, 1, 0).Write(12, 1, 7));
        NativeEventMemory nativeBefore = native.EventMemory;
        Require(native.EventMemory.Cells[201].Width == 1 && native.EventMemory.Read(12, 1).RequireExact() == 7, "GameSession treated neighboring BYTE201/BYTE12 cells as DWORD aliases of Disc/SeeD owners.");
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => native.SetEventMemory(native.EventMemory.Write(9, 1, 0)), "GameSession accepted an equal-valued copy of canonical SeeD memory.");
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => native.SetEventMemory(native.EventMemory.Write(204, 1, native.DiscProcess!.SavedDisc204)), "GameSession accepted an equal-valued copy of canonical disc memory.");
        Require(ReferenceEquals(nativeBefore, native.EventMemory), "Rejected root-owner memory publication changed authoritative event memory.");
        EventEngine nativeEngine = new(native);
        SceneEventState sceneBefore = nativeEngine.Scene;
        NativeFieldFadeState? fadeBefore = native.FieldFade;
        ExpectMemoryOwnershipFailure<NotSupportedException>(() => nativeEngine.LoadField(74, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En), "New Game music initialization adopted an unproved generic BYTE201 alias.");
        Require(nativeEngine.FieldId == 0 && nativeEngine.Frame == 0 && nativeEngine.NativeRuntimeProfile is null && nativeEngine.Actors.Count == 0 && ReferenceEquals(nativeEngine.Scene, sceneBefore) && ReferenceEquals(nativeBefore, native.EventMemory) && native.FieldFade == fadeBefore, "Music ownership refusal changed loaded-field metadata, scene, fade or memory before rejection.");
        GameSession musicSession = CreateNativeMusicSession();
        EventEngine musicEngine = new(musicSession);
        musicEngine.LoadField(74, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        JsonElement musicBefore = musicEngine.Capture();
        NativeEventMemory musicMemoryBefore = musicSession.EventMemory;
        Require(musicEngine.Scene.MusicProcess is { Bank201: 0 }, "The scene-ownership fixture lacks its executed New Game music owner.");
        GameSession prospectiveScene = new(new GameState { EventMemory = NativeEventMemory.Empty.Write(201, 1, 0) });
        GameState prospectiveBefore = prospectiveScene.State;
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => prospectiveScene.SetEventState(musicBefore), "Publishing a typed Scene claim accepted a preexisting generic music byte.");
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => prospectiveScene.SetDepartingFieldState(musicBefore), "Publishing a departing typed Scene claim accepted a preexisting generic music byte.");
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => new GameSession(prospectiveBefore with { EventState = held, DepartingFieldState = musicBefore }), "An unrelated current VM hid a conflicting departing Scene memory owner.");
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => new GameSession(prospectiveBefore with { EventState = musicBefore, DepartingFieldState = held }), "An unrelated departing VM hid a conflicting current Scene memory owner.");
        Require(ReferenceEquals(prospectiveBefore, prospectiveScene.State) && prospectiveScene.State.EventState is null, "Conflicting Scene publication changed the original root checkpoint.");
        NativeEventSceneMemoryOwners prospectiveOwners = NativeEventSceneMemoryOwners.None;
        prospectiveScene.BindEventMemorySceneOwners(() => prospectiveOwners);
        prospectiveOwners = NativeEventSceneMemoryOwners.MusicProcess;
        string refusedOwnershipSave = Path.Combine(Path.GetTempPath(), $"ff8-event-memory-refused-{Guid.NewGuid():N}.json");
        try
        {
            ExpectMemoryOwnershipFailure<InvalidDataException>(() => prospectiveScene.Save(refusedOwnershipSave, registerJournalReference: false), "Save published a generic byte claimed by its live canonical Scene.");
            Require(!File.Exists(refusedOwnershipSave) && ReferenceEquals(prospectiveBefore, prospectiveScene.State), "Refused Scene ownership validation published a save or changed its root checkpoint.");
        }
        finally
        {
            File.Delete(refusedOwnershipSave);
        }
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => musicSession.SetEventMemory(musicSession.EventMemory.Write(201, 1, 0)), "The public memory setter accepted a duplicate active Scene music byte before capture.");
        Require(ReferenceEquals(musicMemoryBefore, musicSession.EventMemory) && JsonElement.DeepEquals(musicBefore, musicEngine.Capture()), "Live Scene alias refusal changed memory or captured metadata.");
        JsonNode forgedCapturedMemory = JsonNode.Parse(musicBefore.GetRawText())!;
        forgedCapturedMemory["EventMemory"]!["Cells"]!["201"] = new JsonObject { ["Width"] = 1, ["Value"] = 0 };
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => musicSession.SetEventState(JsonSerializer.SerializeToElement(forgedCapturedMemory)), "Publishing a VM accepted an alias inside its own captured event memory bank.");
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => musicSession.SetDepartingFieldState(JsonSerializer.SerializeToElement(forgedCapturedMemory)), "Publishing a departing VM accepted an alias inside its own captured event memory bank.");
        Require(musicSession.State.EventState is null && musicSession.State.DepartingFieldState is null && JsonElement.DeepEquals(musicBefore, musicEngine.Capture()), "Rejected captured-memory aliases changed root snapshot metadata or the active VM.");
        musicSession.SetEventState(musicBefore);
        NativeEventMemory forgedMusicMemory = musicSession.EventMemory.Write(201, 1, 0);
        ExpectMemoryOwnershipFailure<InvalidDataException>(() => new GameSession(musicSession.State with { EventMemory = forgedMusicMemory }), "A root checkpoint accepted memory aliasing its captured canonical Scene.");
        string ownershipSave = Path.Combine(Path.GetTempPath(), $"ff8-event-memory-scene-{Guid.NewGuid():N}.json");
        try
        {
            musicSession.Save(ownershipSave, registerJournalReference: false);
            GameState savedMusic = GameSession.Load(ownershipSave);
            Require(savedMusic.EventMemory.Cells.Count == musicMemoryBefore.Cells.Count, "Saving and loading a canonical Scene changed its generic memory bank.");
            JsonNode forgedMusicSave = JsonNode.Parse(File.ReadAllText(ownershipSave))!;
            JsonNode alias = new JsonObject { ["Width"] = 1, ["Value"] = 0 };
            forgedMusicSave["EventMemory"]!["Cells"]!["201"] = alias.DeepClone();
            forgedMusicSave["EventState"]!["EventMemory"]!["Cells"]!["201"] = alias.DeepClone();
            File.WriteAllText(ownershipSave, forgedMusicSave.ToJsonString());
            ExpectMemoryOwnershipFailure<InvalidDataException>(() => GameSession.Load(ownershipSave), "Matching forged root/VM memory banks bypassed captured Scene ownership during file loading.");
            Require(JsonElement.DeepEquals(musicBefore, musicEngine.Capture()) && ReferenceEquals(musicMemoryBefore, musicSession.EventMemory), "A forged saved-memory load changed the active game or VM.");
        }
        finally
        {
            File.Delete(ownershipSave);
        }
        Console.WriteLine("Native event memory ownership: resolver spans, equal-value alias refusal, typed opcode overlap, independent restore/continuation, read-only views, timer refusal/provenance and root-owner/New Game preflight verified against synthetic execution baselines.");
    }

    private static void ExpectMemoryOwnershipFailure<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private sealed class EventMemoryComponentServices : IEventServices
    {
        public bool IsMoviePlaying => false;

        public void RequestMovie(int index)
        {
            throw new InvalidOperationException("Unexpected movie in typed event memory component.");
        }

        public void RequestField(int fieldId, int entryPoint)
        {
            throw new InvalidOperationException("Unexpected departure in typed event memory component.");
        }

        public void ShowDialog(string text)
        {
            throw new InvalidOperationException("Unexpected dialogue in typed event memory component.");
        }
    }
}
