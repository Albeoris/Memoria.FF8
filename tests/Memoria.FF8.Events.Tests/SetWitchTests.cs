using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSetWitchContract()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeSavedBattleFlagFieldWrite previous = new(600, new string('A', 64), 0, 1, 5, 1, 1);
        foreach (int raw in new[] { 0, 1, 256, -65536, -1 })
        {
            foreach (NativeSavedBattleFlags? initial in new NativeSavedBattleFlags?[] { null, new(NativeInputConfiguration.OriginalInitHash, 0x10, 0x11, previous), new(NativeInputConfiguration.OriginalInitHash, 0x9E) })
            {
                byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 12345), (Jsm.Opcode.PSHN_L, raw), (Jsm.Opcode.SETWITCH, 0), (Jsm.Opcode.POPM_L, 4000), (Jsm.Opcode.PSHI_L, 1), (Jsm.Opcode.POPM_L, 4004), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
                (GameSession Session, EventEngine Engine) Create(GameState? state = null)
                {
                    GameSession session = state is null ? CreateDirectedLinearTurnSession(vitals, economy) : new(state);
                    if (state is null && initial is not null)
                    {
                        session.Restore(session.State with { SavedBattleFlags = initial });
                    }
                    EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4 };
                    engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                    return (session, engine);
                }
                var live = Create();
                List<int[]> consumed = [];
                live.Engine.CommandExecuted += command =>
                {
                    if (command.Name == nameof(Jsm.Opcode.SETWITCH))
                    {
                        consumed.Add(command.Arguments.ToArray());
                    }
                };
                live.Engine.Step();
                live.Engine.Step();
                NativeSavedBattleFlags flags = live.Session.State.SavedBattleFlags!;
                byte before = initial?.Value ?? 0;
                byte expected = (byte)(raw != 0 ? before | 0x20 : before & ~0x20);
                Require(flags.Value == expected && flags.EffectiveKnownMask == (initial is null ? 0x21 : initial.KnownMask is null ? 255 : 0x31) && flags.WitchFieldWrite is { FieldId: 600, ObjectId: 0, ScriptId: 1 } write && write.RawOperand == raw && flags.FieldWrite == initial?.FieldWrite && live.Session.State.CharacterVitals!.DreamEnabled == vitals.DreamEnabled && live.Engine.EventMemory.Read(4000, 4).RequireExact() == 12345 && live.Engine.EventMemory.Read(4004, 4).RequireExact() == 901 && consumed.Count == 1 && consumed[0].SequenceEqual(new[] { raw }), "SETWITCH lost full-DWORD zero/nonzero, changed unrelated known flags/results/stack, fabricated unknown bits or delayed its successor.");
                GameState cold = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(live.Session.State))!;
                var independent = Create(cold);
                independent.Engine.Restore(live.Engine.Capture());
                for (int callback = 0; callback < 2; callback++)
                {
                    live.Engine.Step();
                    independent.Engine.Step();
                    Require(live.Session.State.SavedBattleFlags == independent.Session.State.SavedBattleFlags && JsonElement.DeepEquals(live.Engine.Capture(), independent.Engine.Capture()), "SETWITCH saved owner or next Wait continuation diverged after full-state cold restoration.");
                }
                Require(consumed.Count == 1, "SETWITCH popped or published again after completion.");
            }
        }
        NativeSavedBattleFlags witchOnly = new(NativeInputConfiguration.OriginalInitHash, 0x20, 0x21, WitchFieldWrite: previous);
        NativeSavedBattleFlags combined = witchOnly.WithFieldBit4(previous);
        Require(combined.KnownMask == 0x31 && combined.Value == 0x30 && combined.WithWitch(previous with { RawOperand = 0 }).Value == 0x10 && combined.WithDreamEnabled(true).Value == 0x31, "SETWITCH and existing bit4/dream producers did not compose within one saved BYTE owner.");
        NativeSavedBattleFlags legacy = JsonSerializer.Deserialize<NativeSavedBattleFlags>("{\"InitOutSha256\":\"" + NativeInputConfiguration.OriginalInitHash + "\",\"Value\":239}")!;
        legacy.Validate();
        Require(legacy.EffectiveKnownMask == 255 && legacy.WitchFieldWrite is null && !JsonSerializer.Serialize(legacy).Contains("WitchFieldWrite"), "Absent SETWITCH provenance changed a legacy full known BYTE or expanded its serialized shape.");
        GameSession retainedOwner = new(new GameState { SavedBattleFlags = legacy });
        retainedOwner.SetNativeWitch(previous with { RawOperand = 0 });
        Require(retainedOwner.State.SavedBattleFlags!.Value == (legacy.Value & ~0x20) && retainedOwner.State.CharacterVitals is null, "SETWITCH failed to preserve an already known canonical flag BYTE when the separate character alias was absent.");
        foreach (NativeSavedBattleFlags malformed in new[] { witchOnly with { KnownMask = 0x31 }, witchOnly with { Value = 0 }, witchOnly with { WitchFieldWrite = null }, combined with { KnownMask = 255 } })
        {
            try
            {
                malformed.Validate();
                throw new InvalidOperationException("Saved SETWITCH owner accepted missing/mismatched provenance or fabricated known bits.");
            }
            catch (InvalidDataException)
            {
            }
        }
        foreach (bool missingOperand in new[] { false, true })
        {
            (Jsm.Opcode, int)[] script = missingOperand ? [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.SETWITCH, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 256), (Jsm.Opcode.SETWITCH, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], script]);
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine refused = new(session) { AnimationFrameCount = (_, _) => 4 };
            refused.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            refused.Step();
            if (!missingOperand)
            {
                // Isolate the missing dream alias from the unrelated salary sampler in this synthetic refusal fixture.
                typeof(GameSession).GetProperty(nameof(GameSession.State))!.SetValue(session, session.State with { CharacterVitals = null, SeeD = null });
            }
            try
            {
                refused.Step();
                throw new InvalidOperationException("SETWITCH admitted missing operand or canonical dream owner.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == nameof(Jsm.Opcode.SETWITCH))
            {
            }
            JsonElement fiber = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(session.State.SavedBattleFlags is null && fiber.GetProperty("Position").GetInt32() == JsmProgram.Read(content).Operations.Single(operation => operation.Opcode == Jsm.Opcode.SETWITCH).Index && fiber.GetProperty("Stack").GetArrayLength() == (missingOperand ? 0 : 1) && (missingOperand || fiber.GetProperty("Stack")[0].GetInt32() == 256), "SETWITCH refusal changed owner, issuing PC or original operand.");
        }
        Console.WriteLine("Native SETWITCH: full-DWORD zero/nonzero, saved bit5-only update, unknown-mask expansion and legacy preservation, bit4/dream composition, immediate successor, cold owner/VM next-Wait restore and malformed/missing-owner/operand refusals pass.");
    }
}
