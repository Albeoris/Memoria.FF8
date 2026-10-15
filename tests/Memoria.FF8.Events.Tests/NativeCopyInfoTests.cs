using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCopyInfoContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        EventEngine Create(int sourceId = 2)
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 9000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 7), (Jsm.Opcode.PSHN_L, 88), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] copy = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 123456), (Jsm.Opcode.PSHN_L, sourceId), (Jsm.Opcode.COPYINFO, 0), (Jsm.Opcode.POPM_L, 5004), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 5000), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 5008), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETINFO, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 5010), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_L, 5014), (Jsm.Opcode.PSHI_L, 6), (Jsm.Opcode.POPM_L, 5018), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] source = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -19), (Jsm.Opcode.PSHN_L, 22), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 3), (Jsm.Opcode.PSHN_L, 2000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] moving = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.PSHN_L, -100), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session, session.EventMemory)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = actor => actor.ObjectId == 2,
                ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 231, 0),
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX - 1377;
                    int y = query.FixedY + 733;
                    NativeScriptGroundProbe[] fan = [new(x, y, 0), new(x, y, 0), new(x, y, 0)];
                    return new(NativeScriptMovementStatus.Accepted, x, y, 0, query.Triangle, query.Direction, [new(x, y, query.Direction, fan)]);
                }
            };
            engine.LoadField(74, SyntheticObjects([1, 16 << 7, (32 << 7) | 1], [caller, copy, unused, source, moving]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        NativeScriptMovementState? sampled = null;
        NativeMotionAuthority? authority = null;
        ushort? completion = null;
        short? counter = null;
        int retainedModel = -1;
        live.CommandExecuted += command =>
        {
            if (command.Name == "COPYINFO")
            {
                sampled = live.Actors[2].NativeScriptMovement;
            }
        };
        while (live.Actors[0].NativeCopiedPosition is null && live.Frame < 16)
        {
            authority = live.Actors[0].NativeMotionAuthority;
            completion = live.Actors[0].NativeMovementCompletion542;
            counter = live.Actors[0].NativeTurnAccumulator474;
            retainedModel = live.Actors[0].ModelId;
            live.Step();
        }
        NativeCopiedPosition copied = live.Actors[0].NativeCopiedPosition ?? throw new InvalidOperationException("COPYINFO did not issue its exact raw placement.");
        ActorState destination = live.Actors[0];
        Require(sampled is not null && copied.SourceNativeObjectId == 2 && live.Scene.NativeActorObjectIds.SequenceEqual([0, 2]) && copied.FixedX == sampled.FixedX && copied.FixedY == sampled.FixedY && copied.FixedZ == sampled.FixedZ && (copied.FixedX & 4095) != 0 && (copied.FixedY & 4095) != 0 && destination.WalkmeshTriangle == 3 && destination.Direction == 231 && destination.MovementSpeed == 2000, "COPYINFO lost the full native-object table owner, fractional raw XYZ, BYTE facing or WORD triangle/current speed.");
        JsonElement issued = live.Capture();
        Require(destination.ModelId == retainedModel && destination.ModelId != live.Actors[2].ModelId && destination.NativeMovementBaseSpeed == 9000, $"COPYINFO changed caller model/base speed: {retainedModel}->{destination.ModelId}/{destination.NativeMovementBaseSpeed}.");
        Require(destination.NativeMotionAuthority == authority && destination.NativeMovementCompletion542 == completion && destination.NativeTurnAccumulator474 == counter && destination.NativeScriptMovement is null, $"COPYINFO changed caller physical words: authority{authority}->{destination.NativeMotionAuthority}, completion{completion}->{destination.NativeMovementCompletion542}, counter{counter}->{destination.NativeTurnAccumulator474}, movement{destination.NativeScriptMovement}.");
        Require(issued.GetProperty("Variables").GetProperty("5004").GetInt32() == 123456 && issued.GetProperty("Variables").GetProperty("5000").GetInt32() == 901 && issued.GetProperty("Variables").GetProperty("5008").GetInt32() == 73, "COPYINFO changed unrelated operands/R0 or failed same-callback return2 continuation.");
        EventEngine independent = Create();
        independent.Restore(issued);
        for (int callback = 0; callback < 4; callback++)
        {
            live.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()) && live.Actors[0].NativeCopiedPosition == copied, "Copied raw position diverged after restore or followed subsequent source movement.");
        }
        JsonElement values = live.Capture().GetProperty("Variables");
        Require(values.GetProperty("5010").GetInt32() == copied.FixedX / 4096 && values.GetProperty("5014").GetInt32() == copied.FixedY / 4096 && values.GetProperty("5018").GetInt32() == 2000 && live.Actors[2].NativeScriptMovement!.FixedX != copied.FixedX, "GETINFO did not consume the copied raw owner with native signed division/current speed.");
        EventEngine refused = Create(258);
        try
        {
            for (int callback = 0; callback < 16; callback++)
            {
                refused.Step();
            }
            throw new InvalidOperationException("COPYINFO truncated an out-of-table full DWORD source index.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "COPYINFO")
        {
            JsonElement owner = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
            Require(owner.GetProperty("Position").GetInt32() == refused.LastFault.Operation && owner.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([258, 123456]) && refused.Actors[0] is { X: 10, Y: 20, MovementSpeed: 9000, NativeCopiedPosition: null }, "COPYINFO refusal changed PC, full operands or destination body.");
        }
        Console.WriteLine("COPYINFO native-object/raw source: fractional one-time XYZ, facing/triangle/current speed, preserved caller model/base/flags/R0, return2, independent restore/GETINFO and atomic full-DWORD source refusal PASS (declared geometry).");
    }
}
