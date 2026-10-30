using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSalaryFieldMovementContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        (GameSession Session, EventEngine Engine, List<int> Speeds) Create(bool forcedWalk = false, bool blocked = false)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(initial.DeepCopy());
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.SetNativeEventOwners(session.WorldControls!.WithSalaryEnabled(true), seeD: session.SeeD!.WithSalaryUiEnabled(true));
            List<int> speeds = [];
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolveWalkmesh = (x, y) => new(new(x, y, 0), 0),
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                CanUseNativePlayerMovement = _ => true,
                ResolveGlobalSoundResource = sample =>
                {
                    GlobalAudioSampleDescriptor descriptor = GlobalAudioFormat.Read(gameDirectory, sample);
                    return new GlobalSoundResource(sample, GlobalSoundBackendStatus.Muted, new GlobalSoundLoopPolicy(descriptor.PlaybackFlags, descriptor.LoopStartPcmByte, descriptor.LoopEndPcmByte, descriptor.WaveFormat.SampleRate, 16, descriptor.WaveFormat.Channels, null));
                }
            };
            engine.ResolveNativePlayerMovement = query =>
            {
                speeds.Add(query.Speed);
                int x = query.FixedX + 4096;
                NativePlayerGroundProbe[] fans = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                return new NativePlayerMovementResult(blocked ? query.FixedX : x, query.FixedY, query.FixedZ, 0, query.Direction, [new(x, query.FixedY, query.Direction)], [fans], !blocked);
            };
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.FADEIN, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (forcedWalk ? Jsm.Opcode.RUNDISABLE : Jsm.Opcode.RUNENABLE, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]]);
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            void Set(string property, object value) => typeof(SceneEventState).GetProperty(property)!.SetValue(engine.Scene, value);
            // Explicit component trace, not a patched research checkpoint.
            PartyTraceSample sample = new(0, 0, 0, 0, 0);
            Set(nameof(SceneEventState.PartyTrace), new PartyTraceState(0, 0, 0, Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray(), Enumerable.Repeat<PartyTraceSample?>(sample, 64).ToArray()));
            Set(nameof(SceneEventState.NativePartyTraceVersion), 1);
            engine.Scene.FieldPartyNativeActorIds.Clear();
            engine.Scene.FieldPartyNativeActorIds.AddRange([0, 255, 255]);
            Set(nameof(SceneEventState.NativePlayerPosition), new FieldFixedPosition(0, 0, 0));
            Set(nameof(SceneEventState.NativePlayerEarlyRetryState), (byte)0);
            typeof(ActorState).GetProperty(nameof(ActorState.WalkmeshTriangle))!.SetValue(engine.Actors[0], 0);
            return (session, engine, speeds);
        }
        PlayerAction move = new(PlayerActionKind.Move, Horizontal: 1);
        var walk = Create(true);
        Require(walk.Engine.Scene.RunEnabled, "Synthetic walk flag was changed before its native VM turn.");
        PlayerActionResult firstWalk = walk.Engine.Apply(move);
        Require(firstWalk.Accepted, $"Declared native walk was refused: {firstWalk}; fault={walk.Engine.LastFault}; fade={walk.Session.FieldFade?.Physical.Mode}; scene={walk.Engine.Scene.NativeFieldInitialization}.");
        Require(walk.Speeds.SequenceEqual(new[] { 2696 }) && walk.Session.SeeD!.SalaryMovementCounter == 0 && walk.Engine.Scene.NativeSalaryMovementUnits == 3, $"Current VM forced walk did not precede the certified motor, or salary consumed current movement instead of the previous result: speeds={string.Join(',', walk.Speeds)}, counter={walk.Session.SeeD!.SalaryMovementCounter}, scalar={walk.Engine.Scene.NativeSalaryMovementUnits}, control={walk.Engine.IsUserControlEnabled}, run={walk.Engine.Scene.RunEnabled}.");
        var restored = Create(true);
        restored.Engine.Restore(walk.Engine.Capture());
        walk.Engine.Step();
        restored.Engine.Step();
        Require(walk.Session.EventMemory.Read(4, 4).RequireExact() == 3 && walk.Session.EventMemory.Read(12, 4).RequireExact() == 3 && walk.Session.EventMemory.Read(244, 4).RequireExact() == 3, "Field progress did not consume the previous accepted walk before VM.");
        Require(walk.Session.SeeD!.SalaryMovementCounter == 3 && walk.Engine.Scene.NativeSalaryMovementUnits == 0 && JsonElement.DeepEquals(walk.Engine.Capture(), restored.Engine.Capture()), "Restored pending walk scalar was lost, consumed twice or failed to clear on idle.");
        NativeLimitProgress learning = assets.ReadNewGameLimitProgress();
        walk.Session.SetLimitProgress(learning with { AngeloFlags = 8 });
        restored.Session.SetLimitProgress(learning);
        restored.Engine.Restore(walk.Engine.Capture());
        Require(restored.Session.LimitProgress!.AngeloFlags == 8 && JsonElement.DeepEquals(walk.Engine.Capture(), restored.Engine.Capture()), "Independent VM restore lost its current Angelo learning owner.");
        var run = Create();
        run.Engine.Apply(move);
        Require(run.Speeds.Single() == 7929 && run.Engine.Scene.NativeSalaryMovementUnits == 5 && run.Session.SeeD!.SalaryMovementCounter == 0, "Run did not publish native5/run7929 for the next callback.");
        run.Engine.Apply(new PlayerAction(PlayerActionKind.FieldButtons, Value: 0x10));
        run.Engine.Apply(move);
        Require(run.Speeds.Last() == 2696 && run.Engine.Scene.NativeSalaryMovementUnits == 3 && run.Session.SeeD!.SalaryMovementCounter == 5, "Held0x10 was ignored by speed, movement scalar or prior-run accounting.");
        Require(run.Session.EventMemory.Read(4, 4).RequireExact() == 5 && run.Session.EventMemory.Read(12, 4).RequireExact() == 5 && run.Session.EventMemory.Read(244, 4).RequireExact() == 5, "Run progress did not use native5 before the current walk callback.");
        var rejected = Create(blocked: true);
        rejected.Engine.Apply(move);
        rejected.Engine.Step();
        Require(rejected.Session.SeeD!.SalaryMovementCounter == 0 && rejected.Engine.Scene.NativeSalaryMovementUnits == 0 && rejected.Engine.Actors[0].X == 0, "Rejected479C60 attempt was counted as successful movement.");
        var payout = Create();
        payout.Engine.Apply(move);
        payout.Session.SetSeeD(payout.Session.SeeD! with { SalaryMovementCounter = 0x5FFF });
        JsonElement before = payout.Engine.Capture();
        payout.Engine.Apply(move);
        Require(payout.Engine.Frame == before.GetProperty("Frame").GetInt64() + 1 && payout.Session.SeeD is { RankPoints: 490, SalaryMovementCounter: 0, PreviousRank186: 500, SalaryDisplayCountdown184: 149 } && payout.Session.Economy is { Gil: 7000, EventBankGil72: 7000 } && payout.Engine.Scene.NativeSalaryMovementUnits == 5 && payout.Speeds.Count == 2 && payout.Engine.Scene.GlobalSoundRequests.TakeLast(3).Select(request => request.OriginalEffectId).SequenceEqual(new uint[] { 91, 92, 93 }), "Field payout did not consume the genuine previous run, publish post-payout bank gil or preserve the current motor/sound order.");
        var unknownPayout = Create();
        unknownPayout.Engine.Apply(move);
        unknownPayout.Session.SetSeeD(unknownPayout.Session.SeeD! with { SalaryMovementCounter = 0x5FFF, SalaryTable = null });
        JsonElement unknownPaymentBefore = unknownPayout.Engine.Capture();
        try
        {
            unknownPayout.Engine.Apply(move);
            throw new InvalidOperationException("Unproved native salary table was guessed at payment.");
        }
        catch (NotSupportedException)
        {
        }
        Require(JsonElement.DeepEquals(unknownPaymentBefore, unknownPayout.Engine.Capture()) && unknownPayout.Speeds.Count == 1, "Missing-table payout refusal advanced the clock, VM or physical motor before its preflight.");
        foreach (bool missing in new[] { false, true })
        {
            JsonNode malformed = JsonNode.Parse(walk.Engine.Capture().GetRawText())!;
            if (missing)
            {
                malformed["Scene"]!.AsObject().Remove(nameof(SceneEventState.NativeSalaryMovementUnits));
            }
            else
            {
                malformed["Scene"]![nameof(SceneEventState.NativeSalaryMovementUnits)] = 4;
            }
            JsonElement original = restored.Engine.Capture();
            try
            {
                restored.Engine.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Missing or malformed salary scalar was admitted.");
            }
            catch (Exception error) when (error is JsonException or InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(original, restored.Engine.Capture()), "Salary scalar restore refusal mutated VM or services.");
        }
        GameSession unknownSession = new(new GameState());
        int obsoleteResolverCalls = 0;
        EventEngine unknown = new(unknownSession)
        {
            ResolveWalkmesh = (x, y) =>
            {
                obsoleteResolverCalls++;
                return new(new(x, y, 0), 0);
            }
        };
        unknown.LoadField(74, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 0)), []);
        unknown.Step();
        JsonElement unknownBefore = unknown.Capture();
        try
        {
            Require(!unknown.Apply(move).Accepted, "Unknown native movement admitted the obsolete walkmesh controller.");
        }
        catch (NotSupportedException)
        {
        }
        Require(unknown.Scene.NativeSalaryMovementUnits is null && obsoleteResolverCalls == 0 && JsonElement.DeepEquals(unknownBefore, unknown.Capture()), "Unknown field inferred salary0, invoked a fallback motor or advanced before native admission.");
        Console.WriteLine("Field salary: previous-callback3/5/0, VM-before-body walk, held walk, rejected movement, independent restore, actual payout/new bank gil and atomic missing-input/metadata refusal passed.");
    }
}
