using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GameplayAnimationWaitContract()
    {
        foreach (bool sync in new[] { false, true })
        {
            foreach (bool completed in new[] { false, true })
            {
                foreach (int frames in new[] { 6, 10, 1 })
                {
                    GameplayAnimationWaitCase(sync, completed, frames);
                }
            }
        }
        GameplayAnimationWaitCase(false, false, 6, generic: true);
    }

    private static void GameplayAnimationWaitCase(bool sync, bool completed, int currentFrames, bool generic = false)
    {
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_L, 500), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode Opcode, int Parameter)> ordinary = [(Jsm.Opcode.LBL, 1), (sync ? Jsm.Opcode.RANIMEKEEP : Jsm.Opcode.ANIMEKEEP, 1)];
        if (sync)
        {
            ordinary.Add((Jsm.Opcode.ANIMESYNC, 0));
        }
        ordinary.AddRange([(Jsm.Opcode.PSHM_L, 1100), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.CAL, 0), (Jsm.Opcode.POPM_L, 1100), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        List<(Jsm.Opcode Opcode, int Parameter)[]> scripts = [initialize, ordinary.ToArray()];
        ushort[] groups = [1];
        if (completed)
        {
            scripts.Add([(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
            scripts.Add([(Jsm.Opcode.LBL, 8), (Jsm.Opcode.SETMODEL, 2), (Jsm.Opcode.RET, 8)]);
            scripts.Add([(Jsm.Opcode.LBL, 9), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQEW, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            groups = [2, (8 << 7) | 1];
        }
        byte[] content = SyntheticObjects(groups, scripts);
        int operationsOffset = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6));
        foreach (JsmOperation operation in JsmProgram.Read(content).Operations.Where(operation => operation.Opcode is Jsm.Opcode.CAL or Jsm.Opcode.REQEW))
        {
            BinaryPrimitives.WriteInt32LittleEndian(content.AsSpan(operationsOffset + operation.Index * 4), (int)operation.Opcode << 24);
        }
        (GameSession Session, EventEngine Engine) Create(int clipFrames, GameState? state = null)
        {
            GameSession session = state is null ? GameSession.CreateColdProcess() : new(state);
            if (state is null)
            {
                session.SetWorldControls(NativeWorldControlState.CreateNewGame());
                session.SetEventRandom(session.EventRandom!.ClearSavedStateForNewGame());
                session.Restore(session.State with { Scene = SceneKind.Field, FieldId = 600 });
                session.SetEventMemory(session.EventMemory.Write(1100, 4, NativeScalarValue.Exact(0)));
            }
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, animation) => animation == 1 ? clipFrames : 4,
                ResolveNativeTriangleHeight = (_, _, _) => 0,
                ResolvePartyTraceTriangle = (_, _, _) => 0
            };
            engine.LoadField(600, content, [], arrival: state is null ? new(600, 0, 0, 0, 0) : null, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En, fieldLoadReason: 0);
            return (session, engine);
        }
        var source = Create(6);
        int snapshotFrame = completed ? 8 : 4;
        while (source.Engine.Frame < snapshotFrame)
        {
            source.Engine.Step();
        }
        source.Session.SetEventMemory(source.Session.EventMemory.Write(500, 4, NativeScalarValue.Exact(99)));
        GameplaySaveFieldContinuation saved = source.Engine.CaptureGameplaySaveFieldContinuation().Detach();
        if (generic)
        {
            saved = saved with { Scripts = saved.Scripts with { Fibers = saved.Scripts.Fibers.Select(fiber => fiber.Wait == GameplaySaveScriptWait.NativeAnimationBlocking ? fiber with { Wait = GameplaySaveScriptWait.Animation } : fiber).ToArray() } };
        }
        GameplaySaveScriptWait expected = generic ? GameplaySaveScriptWait.Animation : sync ? GameplaySaveScriptWait.NativeAnimationSync : GameplaySaveScriptWait.NativeAnimationBlocking;
        Require(saved.Scripts.Fibers.Any(fiber => fiber.Wait == expected), "Animation continuation lost its distinct blocking/sync wait.");
        Require(saved.Actors.Actors[0].Animation.UseResourceLastPose == completed, "Completed full-clip pose was confused with an authored explicit range.");
        Require(((saved.Actors.Actors[0].Animation.Flags!.Value & GameplaySaveActorAnimationFlags.Complete) != 0) == completed, "Capture invented or lost the actual completion flag.");
        GameplaySaveSceneFacts facts = new(GameplaySaveSceneFactsOrigin.Field, 600, source.Engine.Scene.Names.ToDictionary(), source.Engine.Scene.UnlockedTutorials.ToArray(), source.Engine.Scene.Party.Select(value => (byte)value).ToArray(), source.Engine.Scene.PartyPower, source.Engine.Scene.MenuEnabled, source.Engine.Scene.RunEnabled, source.Engine.Scene.MapJumpDisabled, source.Engine.Scene.PlaceId, source.Engine.Scene.NativeSalaryMovementUnits, null, null) { PersistentBattleMode = 0, ActiveBattleMode = 0 };
        GameState root = source.Session.State with { FieldLoadContext = NativeFieldLoadContext.FromGameplayContinuation(600) };
        if (!sync && !completed && currentFrames == 6)
        {
            GameplaySaveFieldContinuation badPosition = saved with
            {
                Scripts = saved.Scripts with
                {
                    Fibers = saved.Scripts.Fibers.Select(fiber => fiber.Wait == expected ? fiber with { Position = fiber.Position - 1 } : fiber).ToArray()
                }
            };
            var invalid = Create(currentFrames, root);
            bool rejected = false;
            try
            {
                invalid.Engine.RestoreGameplaySaveFieldContinuation(badPosition, facts);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Require(rejected && invalid.Session.EventMemory.Read(1100, 4).RequireExact() == 0, "Animation wait with an unrelated consumed opcode was admitted or granted a reward.");
        }
        int uninterruptedRewardFrame = -1;
        if (completed)
        {
            for (int tick = 0; tick < 20; tick++)
            {
                source.Engine.Step();
                if (source.Session.EventMemory.Read(1100, 4).RequireExact() == 1 && uninterruptedRewardFrame < 0)
                {
                    uninterruptedRewardFrame = checked((int)source.Engine.Frame);
                }
                Require(source.Session.EventMemory.Read(1100, 4).RequireExact() is 0 or 1, "Uninterrupted request fixture repeated its reward.");
            }
            Require(uninterruptedRewardFrame > snapshotFrame, "Uninterrupted preempted animation never reached its reward.");
        }
        if (completed && !sync && currentFrames == 1)
        {
            GameplaySaveFieldContinuation explicitRange = saved with
            {
                Actors = saved.Actors with
                {
                    Actors = saved.Actors.Actors.Select(actor => actor.ObjectId == 0 ? actor with { Animation = actor.Animation with { UseResourceLastPose = false, End = source.Engine.Actors[0].NativeAnimationHeader!.Terminal } } : actor).ToArray()
                }
            };
            var oldRange = Create(currentFrames, root);
            bool rejected = false;
            try
            {
                oldRange.Engine.RestoreGameplaySaveFieldContinuation(explicitRange, facts);
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
            Require(rejected, "Selected shorter clip silently rewrote an older explicit animation endpoint.");
            var unchangedRange = Create(6, root);
            unchangedRange.Engine.RestoreGameplaySaveFieldContinuation(explicitRange, facts);
            Require(unchangedRange.Engine.Actors[0].NativeAnimationHeader!.Terminal == explicitRange.Actors.Actors[0].Animation.End, "Older explicit endpoint changed with compatible selected resources.");
        }
        var target = Create(currentFrames, root);
        int issued = 0;
        target.Engine.CommandExecuted += command =>
        {
            if (command.Name is nameof(Jsm.Opcode.ANIMEKEEP) or nameof(Jsm.Opcode.RANIMEKEEP))
            {
                issued++;
            }
        };
        target.Engine.RestoreGameplaySaveFieldContinuation(saved, facts);
        Require(target.Session.EventMemory.Read(500, 4).RequireExact() == 99 && issued == 0, "Restore replayed INIT or reissued the animation command.");
        NativeActorAnimationHeader restored = target.Engine.Actors[0].NativeAnimationHeader!;
        Require(((restored.Flags!.Value & 0x0800) != 0) == completed, "Changed current clip fabricated a completion flag.");
        if (completed)
        {
            Require(restored.Frame == (currentFrames - 1) * 16 && restored.Terminal == restored.Frame, "Known completed final pose did not follow the selected current resource.");
        }
        int rewardFrame = -1;
        for (int tick = 0; tick < 20; tick++)
        {
            target.Engine.Step();
            if (target.Session.EventMemory.Read(1100, 4).RequireExact() == 1 && rewardFrame < 0)
            {
                rewardFrame = checked((int)target.Engine.Frame);
            }
            Require(target.Session.EventMemory.Read(1100, 4).RequireExact() is 0 or 1, $"Animation reward counter {target.Session.EventMemory.Read(1100, 4).RequireExact()} at frame{target.Engine.Frame}; sync={sync}, completed={completed}, clip={currentFrames}.");
        }
        int expectedReward = completed ? uninterruptedRewardFrame : snapshotFrame + Math.Max(1, currentFrames - 2) + (sync || generic ? 0 : 1);
        Require(rewardFrame == expectedReward && issued == 0 && target.Session.EventMemory.Read(500, 4).RequireExact() == 99, $"Reward frame{rewardFrame}, expected{expectedReward}; issues={issued}, sentinel={target.Session.EventMemory.Read(500, 4).RequireExact()}, sync={sync}, complete={completed}, clip={currentFrames}, savedPhase={saved.Actors.Actors[0].Animation.Frame}.");
        GameplaySaveFieldContinuation afterReward = target.Engine.CaptureGameplaySaveFieldContinuation().Detach();
        var second = Create(currentFrames, target.Session.State with { FieldLoadContext = NativeFieldLoadContext.FromGameplayContinuation(600) });
        second.Engine.RestoreGameplaySaveFieldContinuation(afterReward, facts);
        second.Engine.Step();
        second.Engine.Step();
        Require(second.Session.EventMemory.Read(1100, 4).RequireExact() == 1, "Loading the post-reward continuation granted the reward again.");
        Require(!JsonSerializer.Serialize(saved).Contains("FileStaging", StringComparison.Ordinal), "Animation continuation persisted native render memory.");
    }
}
