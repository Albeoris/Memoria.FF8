using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSpuContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeSpuAudioAttempt failed = new("EyesOnMe", new string('a', 64), 4096, 44100, 2, false);
        NativeSpuAudioAttempt opened = failed with { PlaybackStarted = true };
        (GameSession Session, EventEngine Engine) Create(byte[] content, NativeSpuAudioAttempt? outcome = null)
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                PrepareSpuAudio = () => outcome ?? failed,
                StopSpuAudio = () =>
                {
                },
                HasLiveSpuAudio = () => outcome?.PlaybackStarted == true
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SPUREADY, 0), (Jsm.Opcode.PSHN_L, 13), (Jsm.Opcode.POPM_B, 4000), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SPUSYNC, 0), (Jsm.Opcode.PSHN_L, 14), (Jsm.Opcode.POPM_B, 4001), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SPUSYNC, 0), (Jsm.Opcode.PSHN_L, -1), (Jsm.Opcode.SPUSYNC, 0), (Jsm.Opcode.POPM_B, 4002), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_L, 4004), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        var live = Create(content);
        int readyCalls = 0;
        live.Engine.PrepareSpuAudio = () =>
        {
            readyCalls++;
            return failed;
        };
        live.Engine.Step();
        live.Engine.Step();
        NativeSpuProcess state = live.Session.State.GlobalSoundProcess!.Spu!;
        Require(readyCalls == 1 && state.Audio == failed && !state.AudioActive && state.Counter == 1 && state.CallbackCount == 1 && state.LastSyncCallback is null && live.Engine.EventMemory.Read(4000, 1).RequireExact() == 0 && !live.Session.IsMoviePlaying && live.Engine.Scene.MovieActors is null && live.Engine.Scene.NativeMovieMask!.Value == 1, "SPUREADY lost actual resource outcome, reset/post-VM counter, return3 yield or fabricated an AVI/CAM movie.");
        live.Engine.Step();
        int syncOperation = JsmProgram.Read(content).Operations.First(operation => operation.Opcode == Jsm.Opcode.SPUSYNC).Index;
        JsonElement held = live.Engine.Capture();
        JsonElement heldFiber = held.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(live.Engine.EventMemory.Read(4000, 1).RequireExact() == 13 && live.Engine.EventMemory.Read(4001, 1).RequireExact() == 0 && heldFiber.GetProperty("Position").GetInt32() == syncOperation && heldFiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 2, 77 }) && live.Session.State.GlobalSoundProcess!.Spu is { Counter: 2, LastSyncCounter: 1 }, "SPUSYNC did not retain its full DWORD/issuing PC while the strict unsigned counter bound was unmet.");
        var independent = Create(content);
        independent.Engine.Restore(held);
        for (int callback = 0; callback < 3; callback++)
        {
            live.Engine.Step();
            independent.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), independent.Engine.Capture()), "SPU muted resource process or retained timed poll diverged after independent cold restoration.");
        }
        Require(live.Engine.EventMemory.Read(4001, 1).RequireExact() == 14 && live.Engine.EventMemory.Read(4002, 1).RequireExact() == 77 && live.Engine.EventMemory.Read(4004, 4).RequireExact() == 901 && readyCalls == 1 && live.Session.State.GlobalSoundProcess!.Spu is { Counter: 5, LastSyncCounter: 3 }, "SPUSYNC failed strict-equality waiting, nonpositive immediate polls or changed unrelated operands/results.");
        NativeSpuProcess wrapped = state with { CallbackCount = uint.MaxValue, Counter = uint.MaxValue };
        Require(!wrapped.SyncWaits(int.MaxValue) && wrapped.AdvanceCallback().Counter == 0 && wrapped.AdvanceCallback().SyncWaits(1), "SPUSYNC changed unsigned DWORD counter comparison or wrap behavior.");
        JsonNode malformed = JsonNode.Parse(held.GetRawText())!;
        malformed["GlobalSoundProcess"]!["Spu"]!["Counter"] = 99;
        JsonElement before = independent.Engine.Capture();
        try
        {
            independent.Engine.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("SPU restore accepted a counter unrelated to executed callback count.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(before, independent.Engine.Capture()), "Rejected SPU restore changed the current process or VM.");
        foreach (int selector in new[] { -1, 1, 256 })
        {
            byte[] invalid = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, selector), (Jsm.Opcode.SPUREADY, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            var refused = Create(invalid);
            int portCalls = 0;
            refused.Engine.PrepareSpuAudio = () =>
            {
                portCalls++;
                return failed;
            };
            refused.Engine.Step();
            try
            {
                refused.Engine.Step();
                throw new InvalidOperationException("Unproved SPUREADY selector was narrowed or used to address a fabricated packet.");
            }
            catch (EventScriptException) when (refused.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.SPUREADY))
            {
            }
            JsonElement fiber = refused.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(portCalls == 0 && refused.Session.State.GlobalSoundProcess!.Spu is null && fiber.GetProperty("Position").GetInt32() == JsmProgram.Read(invalid).Operations.Single(operation => operation.Opcode == Jsm.Opcode.SPUREADY).Index && fiber.GetProperty("Stack")[0].GetInt32() == selector, "Selector refusal touched the resource port, native owner, PC or full operand.");
        }
        var success = Create(content, opened);
        success.Engine.Step();
        success.Engine.Step();
        var missingHandle = Create(content);
        try
        {
            missingHandle.Engine.Restore(success.Engine.Capture());
            throw new InvalidOperationException("Active-audio cold restore invented a playback resource.");
        }
        catch (NotSupportedException)
        {
        }
        int stopped = 0;
        success.Engine.StopSpuAudio = () =>
        {
            stopped++;

        };
        success.Engine.LoadField(601, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(stopped == 1 && success.Session.State.GlobalSoundProcess!.Spu is { AudioActive: false, Stopped: true }, "Actual field departure did not stop the live audio resource and retain the callback process.");
        foreach (NativeSpuAudioAttempt invalid in new[] { failed with { ResourceId = "Other" }, failed with { SourceSha256 = "bad" }, failed with { PcmBytes = 0 }, failed with { SampleRate = 0 }, failed with { Channels = 0 } })
        {
            try
            {
                invalid.Validate();
                throw new InvalidOperationException("SPU audio receipt accepted an invalid resource identity or PCM format.");
            }
            catch (InvalidDataException)
            {
            }
        }
        Console.WriteLine("Native SPUREADY86/SPUSYNC356: authenticated resource outcome, no AVI, return3 yield, strict unsigned callback timing and retained stack, <=0 polls, counter wrap, muted cold restore, atomic selector/receipt rejection and live-audio departure/active cold-playback refusal pass.");
    }
}
