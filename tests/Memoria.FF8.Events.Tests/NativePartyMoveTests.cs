using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyMoveContract(string gameDirectory, bool asynchronous = false)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        EventEngine Create(int slot = 0)
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] move = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 123456), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.PSHN_L, 65536), (asynchronous ? Jsm.Opcode.RPMOVEA : Jsm.Opcode.PMOVEA, asynchronous ? 122 : 64), (Jsm.Opcode.POPM_L, 5000), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 5004), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 5008), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] targetMove = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, -200), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.DOFFSET, 0), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session, session.EventMemory)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = actor => actor.ObjectId is 0 or 2,
                ResolveNativeScriptDirection = query => Math.Abs((long)query.TargetX - query.FixedX) <= 8192 ? new(NativeScriptDirectionStatus.Snapped, query.TargetX, query.TargetY, query.MovementDirection, query.TurnAccumulator) : new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX + (query.ObjectId == 0 ? 8192 : 1377);
                    NativeScriptGroundProbe[] fans = [new(x, query.FixedY, 3), new(x, query.FixedY, 3), new(x, query.FixedY, 3)];
                    return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, fans)]);
                }
            };
            engine.LoadField(74, SyntheticObjects([1, 16 << 7, (32 << 7) | 1], [caller, move, unused, target, targetMove]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }

        EventEngine live = Create();
        while (live.Actors[0].NativeScriptMovement is null && live.Frame < 16)
        {
            live.Step();
        }
        NativeScriptMovementState first = live.Actors[0].NativeScriptMovement ?? throw new InvalidOperationException("PMOVEA did not issue its shared native mode1 body.");
        Require(first.StopDistance == 0 && first.TargetNativeObjectId == 2 && live.Scene.NativeActorObjectIds.SequenceEqual([0, 2]) && live.Scene.PartyBindingMemory is { First: 1 } && live.Scene.FieldPartyNativeActorIds.SequenceEqual([255, 255, 255]) && (first.TargetX & 4095) != 0, "PMOVEA lost low-WORD stop distance, compact saved binding, fractional raw target or FOLLOWOFF independence.");
        JsonElement issued = live.Capture();
        JsonElement held = issued.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        if (asynchronous)
        {
            Require(first.Asynchronous && held.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([123456]) && !held.TryGetProperty("NativeMoveOperation", out _) && !held.TryGetProperty("NativeMoveTarget", out _) && held.GetProperty("Position").GetInt32() == first.Operation + 1 && !CapturedVariables(issued).TryGetProperty("5008", out _), "RPMOVEA did not consume both operands, advance PC and yield without a blocking target receipt.");
            EventEngine restoredAsync = Create();
            restoredAsync.Restore(issued);
            live.Step();
            restoredAsync.Step();
            JsonElement successor = CapturedVariables(live.Capture());
            Require(successor.GetProperty("5000").GetInt32() == 123456 && successor.GetProperty("5004").GetInt32() == 901 && successor.GetProperty("5008").GetInt32() == 73 && live.Actors[0].NativeScriptMovement is { Active: true } && live.Actors[2].NativeScriptMovement!.FixedX != first.TargetX, "RPMOVEA failed its return3 next-callback continuation or waited for the physical move to finish.");
            for (int callback = 0; callback < 24; callback++)
            {
                NativeScriptMovementState current = live.Actors[0].NativeScriptMovement!;
                Require(current.TargetX == first.TargetX && current.TargetY == first.TargetY && current.TargetZ == first.TargetZ && JsonElement.DeepEquals(live.Capture(), restoredAsync.Capture()), "RPMOVEA retargeted its moving party body or diverged after independent asynchronous restore.");
                if (!current.Active)
                {
                    break;
                }
                live.Step();
                restoredAsync.Step();
            }
            Require(live.Actors[0].NativeScriptMovement is { Active: false, Asynchronous: true, AnimationFlag40000: false } && live.Actors[0].NativeMotionAuthority?.FacingHold == 0, "RPMOVEA did not drain the existing post-VM asynchronous completion and animation flags.");
            Console.WriteLine("RPMOVEA shared saved-party fixture: fractional target captured once after FOLLOWOFF/DOFFSET, low-WORD stop, consumed slot, return3 yield, next-callback stack/R0 continuation, independent restore and asynchronous completion PASS.");
            return;
        }
        Require(held.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([0, 123456]) && held.GetProperty("NativeMoveTarget").GetInt32() == 0 && held.GetProperty("Position").GetInt32() == first.Operation, "PMOVEA consumed its full party slot early or confused it with the last resolved native object.");
        EventEngine independent = Create();
        independent.Restore(issued);
        bool retargeted = false;
        for (int callback = 0; callback < 24 && !CapturedVariables(live.Capture()).TryGetProperty("5008", out _); callback++)
        {
            int previousTargetX = live.Actors[2].NativeScriptMovement!.FixedX;
            bool polling = live.Actors[0].NativeMovementCompletion542 != 2;
            live.Step();
            independent.Step();
            NativeScriptMovementState current = live.Actors[0].NativeScriptMovement!;
            if (polling)
            {
                Require(current.TargetX == previousTargetX, "PMOVEA did not sample the party body's raw target on this incomplete VM poll.");
                retargeted |= current.TargetX != first.TargetX;
            }
            Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "PMOVEA diverged after an independent held-slot/raw-target restore.");
            independent.Restore(live.Capture());
        }
        JsonElement variables = CapturedVariables(live.Capture());
        Require(retargeted && variables.GetProperty("5000").GetInt32() == 123456 && variables.GetProperty("5004").GetInt32() == 901 && variables.GetProperty("5008").GetInt32() == 73 && live.Actors[0].NativeScriptMovement is { Active: false, AnimationFlag40000: false } && live.Actors[0].NativeMotionAuthority?.FacingHold == 0, "PMOVEA failed its retargeted completion, held-slot consumption, unchanged R0/stack or native return2 successor.");

        EventEngine rejected = Create(256);
        try
        {
            while (rejected.Frame < 16)
            {
                rejected.Step();
            }
            throw new InvalidOperationException("PMOVEA truncated the full DWORD party slot256.");
        }
        catch (EventScriptException) when (rejected.LastFault?.Opcode == "PMOVEA")
        {
            JsonElement rejectedOwner = rejected.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
            Require(rejectedOwner.GetProperty("Position").GetInt32() == rejected.LastFault.Operation && rejectedOwner.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([65536, 256, 123456]) && rejected.Actors[0].NativeScriptMovement is null && rejected.Actors[0].X == 0, "PMOVEA refusal changed its PC, operands or physical mode1 body.");
        }
        Console.WriteLine("PMOVEA saved compact binding after FOLLOWOFF: low-WORD stop, full-DWORD held slot, fractional moving-target polls, R0/stack, return2, independent restore and atomic out-of-bank refusal PASS.");
    }
}
