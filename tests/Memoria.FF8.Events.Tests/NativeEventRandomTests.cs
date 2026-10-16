using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeEventRandomContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        byte[] CallbackProgram(params (Jsm.Opcode Opcode, int Parameter)[] operations)
        {
            operations[0] = (Jsm.Opcode.LBL, 1);
            return SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], operations]);
        }
        (GameSession Session, EventEngine Engine) Create(byte[] content)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            EventEngine engine = new(session);
            engine.LoadField(600, content, [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
            engine.Step();
            return (session, engine);
        }
        byte[] code = CallbackProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.RND, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.POPM_L, 1104), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RND, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1108), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0));
        var live = Create(code);
        live.Engine.Step();
        Require(live.Session.State.Variables[1100] == 198 && live.Session.State.Variables[1104] == 42 && live.Session.EventRandom!.State == 0x41C67EA6, "Native RND did not preserve deeper operands, exact lowBYTE or same-callback successor.");
        JsonElement saved = live.Engine.Capture();
        Require(!saved.TryGetProperty("RandomState", out _) && saved.GetProperty("EventRandom").GetProperty("State").GetUInt32() == 0x41C67EA6, "A second managed VM random authority survived.");
        var restored = Create(code);
        restored.Engine.Restore(saved);
        live.Engine.Step();
        restored.Engine.Step();
        Require(live.Session.State.Variables[1108] == 126 && JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Native process generator did not preserve independent restore and next callback.");
        live.Engine.LoadField(601, CallbackProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RND, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1112), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
        live.Engine.Step();
        live.Engine.Step();
        Require(live.Session.State.Variables[1112] == 129, "Native field load reset the shared process generator.");

        // Real native POPM producers prepare a pending aging bank; no saved-state padding.
        byte[] memory = CallbackProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0xC9), (Jsm.Opcode.POPM_B, 116), (Jsm.Opcode.PSHN_L, 0x2800), (Jsm.Opcode.POPM_W, 180), (Jsm.Opcode.PSHM_B, 116), (Jsm.Opcode.POPM_L, 1120), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RND, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 1124), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0));
        var pending = Create(memory);
        pending.Engine.Step();
        Require(pending.Session.State.Variables[1120] == 0xC9 && pending.Session.EventRandom!.State == 1 && pending.Session.EventRandom.DrawPoints!.RegenerationCounter180 == 0x2800 && !pending.Session.State.Variables.ContainsKey(116) && !pending.Session.State.Variables.ContainsKey(180), "Same-budget bank projection consumed RNG early or retained a dictionary mirror.");
        JsonElement beforeAging = pending.Engine.Capture();
        var pendingRestore = Create(memory);
        pendingRestore.Engine.Restore(beforeAging);
        pending.Engine.Step();
        pendingRestore.Engine.Step();
        Require(pending.Session.State.Variables[1124] == 129 && pending.Session.EventRandom!.DrawPoints!.RegenerationCounter180 == 0 && JsonElement.DeepEquals(pending.Engine.Capture(), pendingRestore.Engine.Capture()), "PreVM aging did not draw before script RND or restore the pending WORD exactly.");
        var signed = Create(CallbackProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.POPM_W, 116), (Jsm.Opcode.PSHSM_B, 116), (Jsm.Opcode.POPM_L, 1130), (Jsm.Opcode.PSHSM_W, 116), (Jsm.Opcode.POPM_L, 1134), (Jsm.Opcode.PSHM_W, 116), (Jsm.Opcode.POPM_L, 1138), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 0)));
        signed.Engine.Step();
        Require(signed.Session.State.Variables[1130] == -1 && signed.Session.State.Variables[1134] == -1 && signed.Session.State.Variables[1138] == 65535 && signed.Session.EventRandom!.State == 1, "Packed-bank signed BYTE/WORD reads lost native width or consumed RNG.");
        foreach (string missing in new[] { "EventRandom", "bank", "null-bank" })
        {
            JsonNode invalid = JsonNode.Parse(beforeAging.GetRawText())!;
            if (missing == "bank")
            {
                invalid["EventRandom"]!["DrawPoints"]!.AsObject().Remove("Depletion");
            }
            else if (missing == "null-bank")
            {
                invalid["EventRandom"]!["DrawPoints"] = null;
            }
            else
            {
                invalid.AsObject().Remove("EventRandom");
            }
            JsonElement unchanged = pendingRestore.Engine.Capture();
            try
            {
                pendingRestore.Engine.Restore(JsonSerializer.SerializeToElement(invalid));
                throw new InvalidOperationException("Missing current RNG/bank shape was inferred during restore.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
            }
            Require(JsonElement.DeepEquals(unchanged, pendingRestore.Engine.Capture()), "Malformed native RNG restore partially changed VM or process owners.");
        }

        RejectingRandomServices rejecting = new();
        EventEngine refused = new(rejecting, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
        refused.LoadField(600, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 99), (Jsm.Opcode.RND, 0), (Jsm.Opcode.RET, 0)), []);
        try
        {
            refused.Step();
            throw new InvalidOperationException("RND ignored a rejected current-owner setter.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement caller = refused.Capture().GetProperty("Fibers")[0];
        Require(refused.LastFault?.Opcode == "RND" && caller.GetProperty("Position").GetInt32() == 2 && caller.GetProperty("Stack").GetArrayLength() == 1 && !caller.GetProperty("Results").TryGetProperty("0", out _) && rejecting.EventRandom!.State == 1, "Rejected RND publication changed PC, operand, result or process.");

        GameSession unknown = new(new GameState());
        EventEngine blocked = new(unknown);
        blocked.LoadField(600, code, [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
        blocked.Step();
        JsonElement beforeCallback = blocked.Capture();
        try
        {
            blocked.Step();
            throw new InvalidOperationException("Native callback invented an unknown RNG/depletion origin.");
        }
        catch (NotSupportedException)
        {
        }
        Require(blocked.Frame == 1 && JsonElement.DeepEquals(beforeCallback, blocked.Capture()) && unknown.EventRandom is null, "Unknown native callback mutated the VM before rejecting its missing owner.");

        var suspended = Create(code);
        suspended.Session.SetEventRandom(suspended.Session.EventRandom! with { FieldCallbackFlagsCF10 = 1 });
        JsonElement beforeSuspended = suspended.Engine.Capture();
        try
        {
            suspended.Engine.Step();
            throw new InvalidOperationException("Unproduced CF10 suspension silently advanced the ordinary VM.");
        }
        catch (NotSupportedException)
        {
        }
        Require(suspended.Engine.Frame == 1 && JsonElement.DeepEquals(beforeSuspended, suspended.Engine.Capture()), "Unsupported suspended529FF0 branch mutated current owners or VM.");
    }

    private sealed class RejectingRandomServices : IEventServices
    {
        public NativeEventRandomState? EventRandom { get; } = NativeEventRandomState.FromColdProcess();
        public bool CanStoreEventRandom => true;
        public bool IsMoviePlaying => false;
        public void SetEventRandom(NativeEventRandomState random) => throw new NotSupportedException("Declared rejected owner publication.");
        public void RequestMovie(int index) => throw new NotSupportedException();
        public void RequestField(int fieldId, int entryPoint) => throw new NotSupportedException();
        public void ShowDialog(string text) => throw new NotSupportedException();
    }
}
