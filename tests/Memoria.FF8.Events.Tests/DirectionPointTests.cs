using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeDirectionPointContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        byte[] Content(int x = 70) => SyntheticObjects([1], [
            [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 39), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.MLIMIT, 0), (Jsm.Opcode.RET, 8)],
            [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0), (Jsm.Opcode.MOVESYNC, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, -136), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.DIRP, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create(int x = 70, bool acceptZero = true)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.Restore(session.State with { CharacterVitals = initial.DeepCopy(), WorldControls = NativeWorldControlState.CreateNewGame() });
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                CanUseNativeScriptMove = actor => actor.ObjectId == 0,
                ResolveNativeScriptDirection = query =>
                {
                    FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                    return result.Status == FieldNativeScriptDirectionStatus.Unsupported ? null : new NativeScriptDirectionResult((NativeScriptDirectionStatus)result.Status, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator);
                },
                // Declared component ground certificate isolates native direction and callback ownership.
                ResolveNativeScriptMovement = query =>
                {
                    if (query.Speed == 0 && !acceptZero)
                    {
                        return null;
                    }
                    int fixedX = query.Speed == 0 ? query.FixedX : 1000 * 4096;
                    int fixedY = query.Speed == 0 ? query.FixedY : 300 * 4096;
                    return new NativeScriptMovementResult(NativeScriptMovementStatus.Accepted, fixedX, fixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(fixedX, fixedY, query.Direction, [new NativeScriptGroundProbe(fixedX, fixedY, query.FixedZ >> 12), new NativeScriptGroundProbe(fixedX, fixedY, query.FixedZ >> 12), new NativeScriptGroundProbe(fixedX, fixedY, query.FixedZ >> 12)])]);
                }
            };
            engine.LoadField(600, Content(x), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        NativeScriptDirectionQuery? issued = null;
        Func<NativeScriptDirectionQuery, NativeScriptDirectionResult?> original = live.ResolveNativeScriptDirection!;
        live.ResolveNativeScriptDirection = query =>
        {
            if (query.Speed == 0)
            {
                issued ??= query;
            }
            return original(query);
        };
        while (live.Actors[0].NativeScriptMovement is not { Active: true, Speed: 0 } && live.Frame < 12)
        {
            live.Step();
        }
        ActorState actor = live.Actors[0];
        Require(issued is { Speed: 0, StopDistance: 0, TurnAccumulator: 5, DirectionLimit: 5, TargetX: 70 * 4096, TargetY: -136 * 4096 } && actor.Direction == 39 && actor.NativeTurnAccumulator474 == 0 && actor.MovementDirectionLimit == 5 && actor.NativeMovementCompletion542 == 1 && actor.X == 1000 && actor.Y == 300 && actor.MovementSpeed == 0, "DIRP used duration/direct facing, reset474/610 or bypassed zero-speed body geometry.");
        Require(!CapturedVariables(live.Capture()).TryGetProperty("5000", out _), "DIRP issue did not end the current entity budget.");
        JsonElement current = live.Capture();
        EventEngine restored = Create();
        restored.Restore(current);
        live.Step();
        restored.Step();
        Require(actor.NativeMovementCompletion542 == 2 && actor.MovementSpeed == actor.NativeMovementBaseSpeed && actor.NativeScriptMovement is { Active: false } && CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 77 && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "DIRP retry waited for542, popped more operands, changed physical speed restoration or differed after independent restore.");
        EventEngine wrapped = Create(0x100000 + 70);
        while (wrapped.Actors[0].NativeScriptMovement is not { Active: true, Speed: 0 } && wrapped.Frame < 12)
        {
            wrapped.Step();
        }
        Require(wrapped.Actors[0].NativeScriptMovement?.TargetX == 70 * 4096 && wrapped.Actors[0].Direction == 39, "DIRP did not wrap the fullDWORD shift before its bounded native coordinate admission.");
        EventEngine refused = Create(acceptZero: false);
        string? beforeActor = null;
        try
        {
            while (refused.Frame < 12)
            {
                beforeActor = JsonSerializer.Serialize(refused.Actors[0]);
                refused.Step();
            }
            throw new InvalidOperationException("DIRP bypassed a missing zero-speed geometry certificate.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == nameof(Jsm.Opcode.DIRP))
        {
        }
        JsonElement pending = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean());
        Require(pending.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([0, -136, 70]) && beforeActor == JsonSerializer.Serialize(refused.Actors[0]) && refused.Actors[0].MovementSpeed == refused.Actors[0].NativeMovementBaseSpeed && !CapturedVariables(refused.Capture()).TryGetProperty("5000", out _), "DIRP refusal consumed XYZ or changed actor words before admission.");
        Console.WriteLine("DIRP: fullDWORD XYZ wrapping, zero-speed mode1 body, preserved474/610, unconditional nextcallback512/542 writes, budget successor, pending restore and missing geometry atomic refusal passed.");
    }
}
