using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSwapContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        JsmProgram original = JsmProgram.Read(assets.LoadField(569).Jsm);
        Require(original.Operations[297].Opcode == Jsm.Opcode.SWAP && original.Operations[298].Opcode == Jsm.Opcode.PSHN_L && original.Operations[298].Parameter == 1 && original.Operations[299].Opcode == Jsm.Opcode.POPM_B && original.Operations[299].Parameter == 370, "Original569 party-switch branch changed.");

        (EventEngine Engine, GameSession Session) Create(byte[]? reserve = null)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration());
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1)];
            void WriteBackup(byte[] bytes)
            {
                for (int index = 0; index < bytes.Length; index++)
                {
                    main.AddRange([(Jsm.Opcode.PSHN_L, bytes[index]), (Jsm.Opcode.POPM_B, 188 + index)]);
                }
            }
            //Executed JUNCTION0 establishes independent field/combat order;
            //executed backup-bank writes then supply the other party.
            WriteBackup([0, 1, 5, 5, 0, 1]);
            main.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.JUNCTION, 0)]);
            WriteBackup(reserve ?? [2, 3, 4, 4, 2, 3]);
            main.AddRange([(Jsm.Opcode.PSHN_L, 314159), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 271828)]);
            main.AddRange(original.Operations.Skip(297).Take(3).Select(operation => (operation.Opcode, operation.Opcode == Jsm.Opcode.SWAP ? 0 : operation.Parameter)));
            main.AddRange([(Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6000), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 6008), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.SWAP, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(569, SyntheticObjects([1], [init, main.ToArray()]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (engine, session);
        }

        (EventEngine live, GameSession session) = Create();
        int swaps = 0;
        NativeCharacterVitals? before = null;
        NativePartyBindingMemory? binding = null;
        int[]? nativeBindings = null;
        long firstSwapFrame = -1;
        live.CommandExecuted += command =>
        {
            if (command.Name != nameof(Jsm.Opcode.SWAP))
            {
                return;
            }
            swaps++;
            byte[] expectedField = swaps == 1 ? [2, 3, 4] : [0, 1, 5];
            byte[] expectedCombat = swaps == 1 ? [4, 2, 3] : [5, 0, 1];
            byte[] expectedBackup = swaps == 1 ? [0, 1, 5, 5, 0, 1] : [2, 3, 4, 4, 2, 3];
            NativeCharacterVitals expected = (before!.DeepCopy() with { CombatParty = expectedCombat, JunctionPartyBackup = expectedBackup }).RebuildParty();
            Require(live.Scene.Party.SequenceEqual(expectedField.Select(identity => (int)identity)) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(session.CharacterVitals)), "SWAP lost independent field/combat bank order, backup exchange or495EF0 rebuild.");
            Require(live.Scene.PartyBindingMemory == binding && live.Scene.FieldPartyNativeActorIds.SequenceEqual(nativeBindings!), "SWAP refreshed the independently retained allocated actor bindings.");
            if (swaps == 1)
            {
                firstSwapFrame = live.Frame;
                before = session.CharacterVitals!.DeepCopy();
            }
        };
        for (int callback = 0; callback < 8 && !session.CharacterVitals!.JunctionPartyBackup.SequenceEqual(new byte[] { 2, 3, 4, 4, 2, 3 }); callback++)
        {
            live.Step();
        }
        Require(swaps == 0, "SWAP fixture skipped its before-action checkpoint.");
        before = session.CharacterVitals!.DeepCopy();
        binding = live.Scene.PartyBindingMemory;
        nativeBindings = live.Scene.FieldPartyNativeActorIds.ToArray();
        (EventEngine independent, GameSession _) = Create();
        independent.Restore(live.Capture());
        for (int callback = 0; callback < 8 && swaps < 2; callback++)
        {
            live.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "SWAP continuation diverged after independent restore.");
            if (swaps == 1)
            {
                Require(session.State.Variables[370] == 1 && session.State.Variables[6000] == 73 && live.Frame == firstSwapFrame, "Native569 SWAP return2 failed to execute its original location write and successor in the same callback.");
                independent.Restore(live.Capture());
            }
        }
        Require(swaps == 2 && session.State.Variables[6004] == 271828 && session.State.Variables[6008] == 314159, $"SWAP consumed a stack value, changedR0 or failed to exchange both parties twice: swaps={swaps}, stack={session.State.Variables.GetValueOrDefault(6004)}, r0={session.State.Variables.GetValueOrDefault(6008)}, fibers={live.Capture().GetProperty("Fibers")}.");

        foreach (byte[] reserve in new byte[][] { [2, 2, 4, 4, 2, 3], [2, 3, 4, 8, 2, 3] })
        {
            (EventEngine rejected, GameSession rejectedSession) = Create(reserve);
            NativeCharacterVitals? retained = null;
            int[]? retainedField = null;
            rejected.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.WAIT))
                {
                    retained = rejectedSession.CharacterVitals!.DeepCopy();
                    retainedField = rejected.Scene.Party.ToArray();
                }
            };
            try
            {
                for (int callback = 0; callback < 8; callback++)
                {
                    rejected.Step();
                }
                throw new InvalidOperationException("SWAP admitted unsupported duplicate field slots or an out-of-bank combat row.");
            }
            catch (EventScriptException) when (rejected.LastFault?.Opcode == nameof(Jsm.Opcode.SWAP))
            {
                JsonElement fiber = rejected.Capture().GetProperty("Fibers").EnumerateArray().Single(value => !value.GetProperty("Complete").GetBoolean());
                Require(fiber.GetProperty("Position").GetInt32() == rejected.LastFault.Operation && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([271828]) && rejected.Scene.Party.SequenceEqual(retainedField!) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(retained), JsonSerializer.SerializeToElement(rejectedSession.CharacterVitals)), "Rejected SWAP changed PC, stack or either canonical saved owner.");
            }
        }
        Console.WriteLine("SWAP: original569 branch, independent raw field/combat banks,188..193 exchange,495EF0 rebuild, no operands/R0/binding changes, return2, before/after restore and fail-before-mutation checks PASS.");
    }
}
