using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSalaryPresentationEffectsContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 91), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.PSHN_L, 127), (Jsm.Opcode.EFFECTPLAY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.SEVOL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 41), (Jsm.Opcode.SEVOLTRANS, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0));
        EventEngine Create(GameSession session)
        {
            EventEngine engine = new(session) { ResolveGlobalSoundResource = MutedGlobalSample };
            engine.LoadField(100, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        GameSession session = GameSession.CreateColdProcess();
        EventEngine live = Create(session);
        live.Step();
        Require(live.Scene.GlobalSoundRequests.Single() is { OriginalEffectId: 91, Voice: 26, RawPan: 128, EffectivePan: 64, Volume: 127 } && live.Scene.SoundVolume is { Voice: 27, Volume: 42 } && live.Scene.SoundVolumeTransition is { Voice: 28, Volume: 41 } && session.GlobalSoundProcess is { Rotation: 4 } process && process.OriginalEffectIds[26] == 91 && process.OriginalEffectIds[27] == 0 && process.OriginalEffectIds[28] == 0 && session.State.Variables[5000] == 77, "EFFECTPLAY/SEVOL/SEVOLTRANS did not share the actual automatic allocator, or volume allocation changed physical original IDs.");
        EventEngine restored = Create(GameSession.CreateColdProcess());
        restored.Restore(live.Capture());
        live.Step();
        restored.Step();
        Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Shared automatic pool did not survive independent VM restore.");
        NativeSalaryResult result = new(NativeSeeDState.CreateNewGame(), null, new NativeSalaryPayment(5, 4, 5000, 4000));
        NativeSalaryPresentation beforeUi = session.SalaryPresentation!;
        NativeGlobalSoundProcess beforeSound = session.GlobalSoundProcess!;
        NativeSalaryEffects prepared = live.PrepareSalaryEffects(result, beforeUi, beforeSound);
        Require(ReferenceEquals(beforeUi, session.SalaryPresentation) && ReferenceEquals(beforeSound, session.GlobalSoundProcess) && prepared.Presentation is { Phase: 1, Scale9A: 256 } && prepared.Requests.Select(request => request.OriginalEffectId).SequenceEqual(new uint[] { 91, 92, 93 }) && prepared.Requests.Select(request => request.Voice).SequenceEqual(new[] { 29, 24, 25 }), "Salary preparation wrote live owners or bypassed the existing automatic voice rotation.");
        session.SetNativeEventOwners(NativeWorldControlState.CreateNewGame(), seeD: result.SeeD, salaryPresentation: prepared.Presentation, globalSoundProcess: prepared.SoundProcess);
        live.PublishSalaryEffects(prepared);
        Require(session.SalaryPresentation is { Phase: 1, Scale9A: 256 } && session.GlobalSoundProcess!.OriginalEffectIds[29] == 91 && session.GlobalSoundProcess.OriginalEffectIds[24] == 92 && session.GlobalSoundProcess.OriginalEffectIds[25] == 93, "Atomic salary publication lost the native UI start or one of its three physical effect IDs.");
        NativeSalaryEffects closed = live.PrepareSalaryEffects(result with { Payment = null, CloseDisplay = true }, session.SalaryPresentation, session.GlobalSoundProcess);
        Require(closed.Presentation is { Phase: 7, Scale9A: 256 } && closed.Requests.Count == 0, "Countdown zero did not write native phase7 independently of payment.");
        int priorRequests = live.Scene.GlobalSoundRequests.Count;
        live.ResolveGlobalSoundResource = sample => sample == 92 ? null : MutedGlobalSample(sample);
        try
        {
            live.PrepareSalaryEffects(result, session.SalaryPresentation, session.GlobalSoundProcess);
            throw new InvalidOperationException("Salary silently omitted a missing original sound descriptor.");
        }
        catch (NotSupportedException)
        {
        }
        Require(priorRequests == live.Scene.GlobalSoundRequests.Count && session.GlobalSoundProcess == prepared.SoundProcess && session.SalaryPresentation == prepared.Presentation, "Failed salary descriptor preparation changed earlier sound/UI owners or published a partial notification.");

        RejectingSoundServices rejecting = new();
        EventEngine refused = new(rejecting, NativeEventMemory.FromDWords(new Dictionary<int, int>())) { ResolveGlobalSoundResource = MutedGlobalSample };
        refused.LoadField(100, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        try
        {
            refused.Step();
            throw new InvalidOperationException("Rejected automatic allocator publication advanced its instruction.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "EFFECTPLAY")
        {
        }
        JsonElement caller = refused.Capture().GetProperty("Fibers")[0];
        Require(caller.GetProperty("Position").GetInt32() == 5 && caller.GetProperty("Stack").GetArrayLength() == 4 && rejecting.GlobalSoundProcess!.IsColdProcess && refused.Scene.GlobalSoundRequests.Count == 0, "Rejected global process publication changed PC, operands, rotation or historical attempts.");
        JsonNode malformed = JsonNode.Parse(restored.Capture().GetRawText())!;
        malformed.AsObject().Remove("GlobalSoundProcess");
        JsonElement before = restored.Capture();
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Missing current global sound shape restored.");
        }
        catch (JsonException)
        {
        }
        Require(JsonElement.DeepEquals(before, restored.Capture()), "Missing sound process shape partially restored the VM.");
    }

    private sealed class RejectingSoundServices : IEventServices
    {
        public NativeGlobalSoundProcess? GlobalSoundProcess { get; } = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        public bool CanStoreGlobalSoundProcess => true;
        public bool IsMoviePlaying => false;
        public void SetGlobalSoundProcess(NativeGlobalSoundProcess state)
        {
            throw new NotSupportedException("Declared rejecting component publication.");
        }
        public void RequestMovie(int index)
        {
            throw new NotSupportedException();
        }
        public void RequestField(int fieldId, int entryPoint)
        {
            throw new NotSupportedException();
        }
        public void ShowDialog(string text)
        {
            throw new NotSupportedException();
        }
    }
}
