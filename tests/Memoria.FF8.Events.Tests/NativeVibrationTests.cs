using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeVibrationContract()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeVibrationBank bank = assets.ReadNativeVibrationBank();
        byte[] Content(int pattern = 0, int priority = 256, bool missingOperand = false)
        {
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, pattern)];
            if (!missingOperand)
            {
                main.Add((Jsm.Opcode.PSHN_L, priority));
            }
            main.AddRange([(Jsm.Opcode.SETVIBRATE, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
            return SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)], main.ToArray()]);
        }
        GameSession Session()
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.SetCharacterVitals(assets.ReadNewGameCharacterVitals());
            return session;
        }
        EventEngine Engine(GameSession session, int pattern = 0, int priority = 256, bool missing = false, bool native = true)
        {
            EventEngine engine = new(session, session.State.Variables) { ReadNativeVibrationBank = () => bank };
            engine.LoadField(600, Content(pattern, priority, missing), [], nativeRuntimeProfile: native ? NativeFieldRuntimeProfile.FF8En : null);
            return engine;
        }
        GameSession liveSession = Session();
        EventEngine live = Engine(liveSession);
        byte? issuedPriority = null;
        live.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.SETVIBRATE))
            {
                issuedPriority = liveSession.VibrationProcess!.Slots[0].Priority;
            }
        };
        live.Step();
        live.Step();
        Require(liveSession.State.Variables[5000] == 9 && live.Scene.VibrationRequest is { RawPattern: 0, RawPriority: 256, EffectivePriority: 257, PatternOffset: 256, NativeReturn: 32 } && liveSession.VibrationProcess!.LastSetReturn == 32 && issuedPriority == 1 && liveSession.VibrationProcess!.Slots[0].Priority == 0, "SETVIBRATE failed full DWORD issue/low BYTE storage, exact handle, same-budget return2.");
        Require(liveSession.VibrationProcess!.Slots[0].A.Counter == 0 && liveSession.VibrationProcess.Ports[0].RequestedA == 0, "Native field callback did not sample its queue before StopAll/controller-request clearing.");
        JsonElement snapshot = live.Capture();
        GameSession restoredSession = Session();
        EventEngine restored = Engine(restoredSession);
        restored.Restore(snapshot);
        for (int callback = 0; callback < 6; callback++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Actual queue/channel/quarter-clock state diverged after independent restore.");
        }
        NativeVibrationRequest receipt = live.Scene.VibrationRequest!;
        ushort generation = liveSession.VibrationProcess!.Slots[0].Generation;
        live.LoadField(601, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        live.Step();
        Require(live.Scene.VibrationRequest == receipt && liveSession.VibrationProcess!.LastSetReturn == 32 && liveSession.VibrationProcess!.Slots[0].Generation == generation && liveSession.VibrationProcess!.Slots.All(slot => slot.Priority == 0), "Native field entry fabricated/reset a generation or lost global1D9CDE8 instead of stopping priorities only.");
        foreach ((int pattern, int priority) in new[] { (7, -65536), (32, 256), (33, -2), (63, 0) })
        {
            GameSession session = Session();
            EventEngine engine = Engine(session, pattern, priority);
            engine.Step();
            engine.Step();
            Require(engine.Scene.VibrationRequest is { NativeReturn: -1, PatternOffset: 0 } && session.VibrationProcess!.LastSetReturn == -1 && session.State.Variables[5000] == 9, "Original missing pattern request selected a guessed handle/domain instead of exact -1 from the actual available queue.");
        }
        foreach ((int pattern, bool missing, bool native, bool owner) in new[] { (-1, false, true, true), (64, false, true, true), (65536, false, true, true), (0, true, true, true), (0, false, true, false) })
        {
            GameSession session = Session();
            if (!owner)
            {
                session = new GameSession(session.State with { VibrationProcess = null });
            }
            EventEngine refused = Engine(session, pattern, 1, missing, native);
            refused.Step();
            try
            {
                refused.Step();
                throw new InvalidOperationException("SETVIBRATE accepted missing operands/process/original authority or aliased its full pattern DWORD.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement fiber = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean());
            Require(fiber.GetProperty("Stack").GetArrayLength() == (missing ? 1 : 2) && refused.Scene.VibrationRequest is null && !session.State.Variables.ContainsKey(5000), "Refused vibration issue consumed its operands or wrote a request/successor.");
        }
        JsonNode malformed = JsonNode.Parse(snapshot.GetRawText())!;
        malformed["Scene"]![nameof(SceneEventState.VibrationRequest)]![nameof(NativeVibrationRequest.EffectivePriority)] = 1;
        EventEngine invalid = Engine(Session());
        JsonElement before = invalid.Capture();
        try
        {
            invalid.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Vibration restore accepted forged issue priority.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, invalid.Capture()), "Malformed vibration receipt mutated VM/process before restore refusal.");
        GameSession rejectedSession = Session();
        RejectingVibrationServices rejectedService = new(rejectedSession);
        EventEngine rejected = new(rejectedService, rejectedSession.State.Variables) { ReadNativeVibrationBank = () => bank };
        rejected.LoadField(600, Content(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        rejected.Step();
        string previousQueue = JsonSerializer.Serialize(rejectedSession.VibrationProcess);
        try
        {
            rejected.Step();
            throw new InvalidOperationException("SETVIBRATE ignored a rejecting native owner service.");
        }
        catch (EventScriptException) when (rejected.LastFault?.Opcode == nameof(Jsm.Opcode.SETVIBRATE))
        {
        }
        JsonElement rejectedFiber = rejected.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean());
        Require(rejectedFiber.GetProperty("Position").GetInt32() == rejected.LastFault!.Operation && rejectedFiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([256, 0]) && previousQueue == JsonSerializer.Serialize(rejectedSession.VibrationProcess) && rejected.Scene.VibrationRequest is null, "Rejected queue publication consumed PC/operands or partially changed the process.");
        Console.WriteLine("SETVIBRATE: authentic defined/absent banks, full DWORD issue, exact process handle, real queue output/callbacks, same-budget successor, independent restore, field-stop retention and atomic unsupported request/restore/setter checks passed.");
    }

    private sealed class RejectingVibrationServices(GameSession session) : IEventServices
    {
        public NativeVibrationProcessState? VibrationProcess => session.VibrationProcess;
        public bool CanStoreVibrationProcess => true;
        public NativeEventRandomState? EventRandom => session.EventRandom;
        public bool CanStoreEventRandom => true;
        public bool CanStoreNativeEventOwners => true;
        public NativeWorldControlState? WorldControls => session.WorldControls;
        public bool IsMoviePlaying => false;

        public void SetWorldControls(NativeWorldControlState controls) => session.SetWorldControls(controls);

        public void SetVibrationProcess(NativeVibrationProcessState vibration)
        {
            if (vibration.LastSetReturn == 32)
            {
                throw new NotSupportedException("Declared SETVIBRATE owner-publication rejection.");
            }
            session.SetVibrationProcess(vibration);
        }

        public void SetNativeEventOwners(NativeWorldControlState controls, NativeSeeDState? seeD = null, NativeExamTimerState? timer = null, NativeCharacterVitals? characterVitals = null, NativeEconomy? economy = null, NativeGlobalSoundProcess? globalSoundProcess = null, NativeSalaryPresentation? salaryPresentation = null, NativeEventRandomState? eventRandom = null, NativeVibrationProcessState? vibrationProcess = null, NativeFieldFadeState? fieldFade = null, Dictionary<int, NativeMemoryValue>? nativeVariableValues = null)
        {
            session.SetNativeEventOwners(controls, seeD, timer, characterVitals, economy, globalSoundProcess, salaryPresentation, eventRandom, vibrationProcess, fieldFade, nativeVariableValues);
        }

        public void RequestMovie(int index)
        {
        }

        public void RequestField(int fieldId, int entryPoint)
        {
        }

        public void ShowDialog(string text)
        {
        }
    }
}
