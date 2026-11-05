using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GameplayScriptMenuContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        foreach (bool save in new[] { false, true })
        {
            foreach (GameplaySaveScriptMenuPhase phase in Enum.GetValues<GameplaySaveScriptMenuPhase>())
            {
                GameplayScriptMenuCase(assets, save, false, phase);
                if (!save)
                {
                    GameplayScriptMenuCase(assets, false, true, phase);
                }
            }
        }
        GameplayScriptMenuCase(assets, true, false, GameplaySaveScriptMenuPhase.Closing, 5);
        GameplayScriptMenuCase(assets, true, false, GameplaySaveScriptMenuPhase.Closing, 10);
    }

    private static void GameplayScriptMenuCase(GameAssets assets, bool save, bool switched, GameplaySaveScriptMenuPhase phase, int closingCallbacks = 0)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.ADDMEMBER, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_L, 500), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (save ? Jsm.Opcode.MENUSAVE : Jsm.Opcode.MENUNORMAL, 0), (Jsm.Opcode.PSHM_L, 1100), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([1, (8 << 7) | 1, (16 << 7) | 1], [initialize, main, [(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        JsmOperation calculation = JsmProgram.Read(content).Operations.Single(operation => operation.Opcode == Jsm.Opcode.CAL);
        BinaryPrimitives.WriteInt32LittleEndian(content.AsSpan(BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6)) + calculation.Index * 4), (int)Jsm.Opcode.CAL << 24);
        (GameSession Session, EventEngine Engine) Create(GameState? state = null)
        {
            GameSession session;
            if (state is null)
            {
                session = GameSession.CreateColdProcess();
                session.StartNewGame();
                session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
                session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
                session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
                session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
                session.SetEventMemory(session.EventMemory.Write(1100, 4, NativeScalarValue.Exact(0)));
            }
            else
            {
                session = new(state);
            }
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4, ResolveNativeTriangleHeight = (_, _, _) => 0, ResolvePartyTraceTriangle = (_, _, _) => 0 };
            engine.LoadField(74, content, [], arrival: state is null ? new(74, 0, 0, 0, 0) : null, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En, fieldLoadReason: 0);
            if (state is null)
            {
                session.SetWorldControls(session.WorldControls! with { Menu209 = (byte)(session.WorldControls!.Menu209 | 2) });
            }
            return (session, engine);
        }
        var source = Create();
        for (int tick = 0; source.Engine.PartyMenu?.Phase != NativePartyMenuPhase.Active && tick < 30; tick++)
        {
            source.Engine.Step();
            if (source.Engine.PartyMenu?.Phase is NativePartyMenuPhase.Requested or NativePartyMenuPhase.Entering)
            {
                Require(!source.Engine.HasSupportedGameplayScriptMenu, "Unsettled menu opening was admitted as a settled panel.");
            }
        }
        Require(source.Engine.PartyMenu?.Phase == NativePartyMenuPhase.Active, "Declared menu fixture did not reach its active panel.");
        source.Session.SetEconomy(source.Session.Economy! with { Gil = source.Session.Economy!.Gil + 123 });
        NativeCharacterVitals edits = source.Session.CharacterVitals!.DeepCopy();
        edits.Records[0] = edits.Records[0] with { Experience = edits.Records[0].Experience + 17 };
        source.Session.SetCharacterVitals(edits);
        source.Session.SetEventMemory(source.Session.EventMemory.Write(500, 4, NativeScalarValue.Exact(99)));
        if (switched)
        {
            PlayerActionResult switchedResult = source.Engine.Apply(new(PlayerActionKind.SwitchPartyMembers, 0, Quantity: 1));
            Require(switchedResult.Accepted && source.Engine.PartyMenu!.NativeReturnResult == 1, $"Declared menu fixture did not commit its proved normal-menu party result: {switchedResult}; party={string.Join(',', source.Session.CharacterVitals!.CombatParty)} flags={string.Join(',', source.Session.CharacterVitals.Records.Take(3).Select(record => record.MemberFlags))} entry={source.Engine.PartyMenu!.NativeEntrySelection}.");
        }
        if (phase != GameplaySaveScriptMenuPhase.Active)
        {
            Require(source.Engine.Apply(new(PlayerActionKind.Cancel)).Accepted, "Declared menu fixture could not close its actual panel.");
            for (int tick = 0; source.Engine.PartyMenu!.Phase.ToString() != phase.ToString() && tick < 30; tick++)
            {
                source.Engine.Step();
            }
        }
        for (int tick = 0; tick < closingCallbacks; tick++)
        {
            source.Engine.Step();
        }
        Require(source.Engine.HasSupportedGameplayScriptMenu, "Supported logical script menu was not admitted.");
        string vitalsBefore = JsonSerializer.Serialize(source.Session.CharacterVitals);
        string economyBefore = JsonSerializer.Serialize(source.Session.Economy);
        GameplaySaveFieldContinuation captured = source.Engine.CaptureGameplaySaveFieldContinuation().Detach();
        string encoded = JsonSerializer.Serialize(captured);
        string sourceBefore = source.Engine.Capture().GetRawText();
        Require(captured.ScriptMenu?.Phase == phase && captured.ScriptMenu.PendingPartyRefresh == (switched && phase != GameplaySaveScriptMenuPhase.FieldResumeInitialization), "Logical menu phase or pending party refresh was lost.");
        GameplaySaveSceneFacts facts = new(GameplaySaveSceneFactsOrigin.Field, 74, source.Engine.Scene.Names.ToDictionary(), source.Engine.Scene.UnlockedTutorials.ToArray(), source.Engine.Scene.Party.Select(value => (byte)value).ToArray(), source.Engine.Scene.PartyPower, source.Engine.Scene.MenuEnabled, source.Engine.Scene.RunEnabled, source.Engine.Scene.MapJumpDisabled, source.Engine.Scene.PlaceId, source.Engine.Scene.NativeSalaryMovementUnits, null, null) { PersistentBattleMode = 0, ActiveBattleMode = 0 };
        captured.ValidateResume(new(1, GameplaySaveResumeKind.Field, 74, 74, captured.GetLeaderPosition(), null), facts);
        if (switched && phase == GameplaySaveScriptMenuPhase.Active)
        {
            GameplaySaveScriptMenu menu = captured.ScriptMenu!;
            NativeFieldLocationSlot[] wrongPose = menu.EntryLocation.ToArray();
            wrongPose[0] = wrongPose[0] with { Pose = wrongPose[0].Pose! with { X = checked((short)(wrongPose[0].Pose!.X + 1)) } };
            RejectGameplayMenu(() => (captured with { ScriptMenu = menu with { EntryLocation = wrongPose } }).Validate());
            RejectGameplayMenu(() => (captured with { ScriptMenu = menu with { OwnerSequence = menu.OwnerSequence + 1000 } }).Validate());
            RejectGameplayMenu(() => (captured with { ScriptMenu = null }).Validate());
            RejectGameplayMenu(() => (captured with { ScriptMenu = menu with { Kind = (GameplaySaveScriptMenuKind)2 } }).Validate());
            var invalid = Create(source.Session.State with { FieldLoadContext = NativeFieldLoadContext.FromGameplayContinuation(74), EventState = null });
            RejectGameplayMenu(() => invalid.Engine.RestoreGameplaySaveFieldContinuation(captured, facts with { FieldParty = [2, 1, 0] }));
            Require(invalid.Session.EventMemory.Read(1100, 4).RequireExact() == 0, "Invalid pending menu context granted its post-menu reward.");
        }
        if (save && phase == GameplaySaveScriptMenuPhase.Closing)
        {
            RejectGameplayMenu(() => (captured with { ScriptMenu = captured.ScriptMenu! with { SaveFade = checked((short)(captured.ScriptMenu.SaveFade!.Value + 1)) } }).Validate());
        }
        var target = Create(source.Session.State with { FieldLoadContext = NativeFieldLoadContext.FromGameplayContinuation(74), EventState = null });
        NativePlaytimeState? playtime = target.Session.State.Playtime;
        long timer = target.Session.ExamTimer!.ClockCalls;
        target.Engine.RestoreGameplaySaveFieldContinuation(captured, facts);
        Require(target.Engine.PartyMenu is not null && target.Engine.PartyMenu.Phase.ToString() == phase.ToString() && target.Engine.PartyMenu.NativeReturnResult == source.Engine.PartyMenu!.NativeReturnResult, "Restore discarded the script menu or invented a choice/result.");
        Require(target.Session.State.Playtime == playtime && target.Session.ExamTimer!.ClockCalls == timer && target.Session.NativeFieldLocation!.Origin == NativeFieldLocationOrigin.GameplayContinuation, "Menu reconstruction advanced time or invented an executed location capture.");
        Require(vitalsBefore == JsonSerializer.Serialize(target.Session.CharacterVitals) && economyBefore == JsonSerializer.Serialize(target.Session.Economy), "Menu reconstruction lost committed canonical edits.");
        Require(vitalsBefore == JsonSerializer.Serialize(source.Session.CharacterVitals) && economyBefore == JsonSerializer.Serialize(source.Session.Economy) && encoded == JsonSerializer.Serialize(captured), "Menu reconstruction mutated its source.");
        Require(sourceBefore == source.Engine.Capture().GetRawText(), "Menu reconstruction changed the original live VM or its menu owners.");
        var exact = Create(target.Session.State with { EventState = null });
        exact.Engine.Restore(target.Engine.Capture());
        Require(exact.Engine.PartyMenu?.NativeReturnResult == target.Engine.PartyMenu!.NativeReturnResult && exact.Session.NativeFieldLocation?.Origin == NativeFieldLocationOrigin.GameplayContinuation, "Existing exact reader lost the reconstructed menu context.");
        var original = Create(source.Session.State with { EventState = null });
        original.Engine.Restore(source.Engine.Capture());
        Require(original.Session.NativeFieldLocation?.Origin == NativeFieldLocationOrigin.ExecutedMenuCapture, "Existing exact menu snapshot changed its default executed location origin.");
        if (phase == GameplaySaveScriptMenuPhase.Active)
        {
            Require(source.Engine.Apply(new(PlayerActionKind.Cancel)).Accepted && target.Engine.Apply(new(PlayerActionKind.Cancel)).Accepted, "Restored active menu could not take the real close action.");
        }
        for (int tick = 0; tick < 30; tick++)
        {
            source.Engine.Step();
            target.Engine.Step();
            Require(source.Engine.Frame == target.Engine.Frame && source.Engine.PartyMenu?.Phase == target.Engine.PartyMenu?.Phase && source.Session.ExamTimer!.ClockCalls == target.Session.ExamTimer!.ClockCalls && source.Session.EventMemory.Read(1100, 4).RequireExact() == target.Session.EventMemory.Read(1100, 4).RequireExact(), "Menu close/reentry timing differs from uninterrupted continuation.");
            Require(target.Session.EventMemory.Read(1100, 4).RequireExact() is 0 or 1, "Menu continuation duplicated its post-menu reward.");
        }
        Require(target.Engine.PartyMenu is null && target.Session.EventMemory.Read(1100, 4).RequireExact() == 1 && target.Session.EventMemory.Read(500, 4).RequireExact() == 99, "Menu continuation replayed INIT or failed its one-time reward.");
        Require(source.Engine.Scene.FieldPartyNativeActorIds.SequenceEqual(target.Engine.Scene.FieldPartyNativeActorIds) && source.Engine.Scene.Party.SequenceEqual(target.Engine.Scene.Party), "Real kind6 return failed the committed party refresh.");
    }

    private static void RejectGameplayMenu(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, "Malformed logical menu context was admitted.");
    }
}
