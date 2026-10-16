using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSealedOffContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        JsmProgram original = JsmProgram.Read(assets.LoadField(98).Jsm);
        Require(original.Operations[269].Opcode == Jsm.Opcode.SEALEDOFF && original.Operations[268].Opcode == Jsm.Opcode.PSHN_L && original.Operations[268].Parameter == 8, "Original98 Draw release no longer supplies mask8 to SEALEDOFF.");
        (EventEngine Engine, GameSession Session) Create(int? argument, bool fieldFlag, bool uncertainLow = false)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.SetInputConfiguration(assets.ReadNewGameInputConfiguration() with { Options = 0xAD020001u });
            session.SetWorldControls(session.WorldControls! with { EventFlags104 = 0x400018u | (fieldFlag ? 0x800u : 0) });
            if (argument is null)
            {
                session.SetExamTimer(session.ExamTimer! with { TimerMemoryActivated = true, Alternatives = [new NativeTimerAlternative(0, 0, 8), new NativeTimerAlternative(0, 0, uncertainLow ? 265 : 264)] });
            }
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.RET, 8)];
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 173), (Jsm.Opcode.POPM_B, 243), (Jsm.Opcode.PSHN_L, 314159), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 271828)];
            if (argument is int raw)
            {
                main.Add((Jsm.Opcode.PSHN_L, raw));
            }
            else
            {
                main.AddRange([(Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0)]);
            }
            main.AddRange([(Jsm.Opcode.SEALEDOFF, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6000), (Jsm.Opcode.POPM_L, 6004), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 6008), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
            engine.LoadField(98, SyntheticObjects([1], [init, main.ToArray()]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (engine, session);
        }
        foreach (bool enabled in new[] { true, false })
        {
            foreach (int? argument in new int?[] { 0, 8, 264, -248, 256, 255, -1, null })
            {
                (EventEngine engine, GameSession session) = Create(argument, enabled);
                ScriptCommand? receipt = null;
                engine.CommandExecuted += command =>
                {
                    if (command.Name == nameof(Jsm.Opcode.SEALEDOFF))
                    {
                        receipt = command;
                    }
                };
                engine.Step();
                JsonElement before = engine.Capture();
                NativeCharacterVitals prior = session.CharacterVitals!.DeepCopy();
                uint priorFlags = session.WorldControls!.EventFlags104;
                for (int callback = 0; callback < 8 && !session.EventMemory.KnownValues.ContainsKey(6000); callback++)
                {
                    engine.Step();
                }
                byte expected = unchecked((byte)(173 & ~(byte)(argument ?? 8)));
                Require(receipt is not null && session.EventMemory.Read(243, 1).RequireExact() == expected && session.InputConfiguration!.CommandRestrictions == expected && (session.InputConfiguration.Options & 0x00FFFFFFu) == 0x00020001u, $"SEALEDOFF failed lowBYTE publication: raw={argument}, flag={enabled}, receipt={JsonSerializer.Serialize(receipt)}, byte243={session.EventMemory.Read(243, 1)}, options={session.InputConfiguration!.Options:X8}, expected={expected}.");
                Require(session.WorldControls!.EventFlags104 == priorFlags && engine.Scene.Party.SequenceEqual([0, 1, 5]) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(prior.RebuildParty()), JsonSerializer.SerializeToElement(session.CharacterVitals)), "SEALEDOFF changed flags, field/combat parties or reserve bank beyond495EF0 rebuild.");
                Require(session.EventMemory.Read(6000, 1).RequireExact() == 73 && session.EventMemory.Read(6004, 4).RequireExact() == 271828 && (argument is null ? session.EventMemory.Read(6008, 4).Values.SequenceEqual([8, 264]) : session.EventMemory.Read(6008, 4).RequireExact() == 314159), "SEALEDOFF changed R0, consumed another slot or yielded before its successor.");
                if (argument is null)
                {
                    Require(receipt!.Arguments.Count == 0 && receipt.ScalarArguments is { Count: 1 } && receipt.ScalarArguments[0].Values.SequenceEqual([8, 264]), "Known lowBYTE SEALEDOFF selected a fabricated fullDWORD representative.");
                }
                (EventEngine restored, _) = Create(argument, enabled);
                restored.Restore(before);
                while (restored.Frame < engine.Frame)
                {
                    restored.Step();
                }
                Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "SEALEDOFF preinstruction restoration changed its outcome.");
                (EventEngine resumed, _) = Create(argument, enabled);
                resumed.Restore(engine.Capture());
                engine.Step();
                resumed.Step();
                Require(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()), "SEALEDOFF postinstruction restoration changed the next callback.");
            }
        }
        (EventEngine rejected, GameSession rejectedSession) = Create(null, true, uncertainLow: true);
        rejected.Step();
        try
        {
            for (int callback = 0; callback < 8; callback++)
            {
                rejected.Step();
            }
            throw new InvalidOperationException("SEALEDOFF admitted an unknown consumed lowBYTE.");
        }
        catch (EventScriptException) when (rejected.LastFault?.Opcode == nameof(Jsm.Opcode.SEALEDOFF))
        {
            JsonElement fiber = rejected.Capture().GetProperty("Fibers").EnumerateArray().Single(value => !value.GetProperty("Complete").GetBoolean());
            Require(fiber.GetProperty("Position").GetInt32() == rejected.LastFault.Operation && fiber.GetProperty("Stack").GetArrayLength() == 2 && rejectedSession.EventMemory.Read(243, 1).RequireExact() == 173 && rejectedSession.InputConfiguration!.CommandRestrictions == 173, "Rejected SEALEDOFF changed PC, stack or either restriction alias.");
        }
        Console.WriteLine("SEALEDOFF original98 mask8, lowBYTE/fullslot, exact mask aliases, unchanged flags/parties/backup/R0, return2, known-low uncertain-high trace, before/after restore and unknown-low refusal PASS.");
    }
}
