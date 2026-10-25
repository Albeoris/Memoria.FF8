using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeEncounterControlContract()
    {
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.BATTLEOFF, Jsm.Opcode.BATTLEON })
        {
            foreach (bool? pending in new bool?[] { null, false, true })
            {
                foreach (bool disabled20 in new[] { false, true })
                {
                    foreach (byte keep in new byte[] { 0, 2, 255 })
                    {
                        List<(Jsm.Opcode Opcode, int Parameter)> operations = [(Jsm.Opcode.LBL, 1)];
                        if (pending is not null)
                        {
                            operations.AddRange([(Jsm.Opcode.PSHN_L, 26), (Jsm.Opcode.MUSICLOAD, 0)]);
                            if (pending == false)
                            {
                                operations.Add((Jsm.Opcode.MUSICCHANGE, 0));
                            }
                        }
                        operations.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (opcode, 0), (Jsm.Opcode.PSHM_L, 104), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHM_B, 207), (Jsm.Opcode.POPI_L, 2), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
                        byte[] code = BattleModeCallbackProgram(operations.ToArray());
                        GameSession session = CreateNativeMusicSession();
                        EventEngine engine = new(session);
                        engine.LoadField(74, code, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                        bool waiting = false;
                        engine.CommandExecuted += command => waiting |= command.Name == "WAIT";
                        for (int frame = 0; frame < 8 && !waiting; frame++)
                        {
                            engine.Step();
                        }
                        Require(waiting && engine.Scene.MusicProcess?.Pending == pending, "Encounter fixture did not reach its executed pending-music boundary.");

                        uint flags = 0xABCDF618u | (disabled20 ? 0x20u : 0u);
                        if (keep == 0)
                        {
                            flags &= ~0x400u;
                        }
                        NativeWorldControlState before = session.WorldControls! with { EventFlags103 = 0xD7, EventFlags104 = flags, MusicKeep207 = keep, Menu209 = 0xE1 };
                        session.SetWorldControls(before);
                        NativeMusicProcessState? beforeMusic = engine.Scene.MusicProcess;
                        JsonElement snapshot = engine.Capture();
                        GameSession independent = CloneMusicSession(session.State);
                        EventEngine restored = new(independent);
                        restored.LoadField(74, code, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                        using JsonDocument document = JsonDocument.Parse(snapshot.GetRawText());
                        restored.Restore(document.RootElement);
                        Require(JsonElement.DeepEquals(snapshot, restored.Capture()), "Encounter controls did not survive independent JSON restore at the opcode boundary.");

                        bool unsupported = opcode == Jsm.Opcode.BATTLEON && !disabled20 && pending is null;
                        if (unsupported)
                        {
                            foreach (EventEngine candidate in new[] { engine, restored })
                            {
                                try
                                {
                                    candidate.Step();
                                    throw new InvalidOperationException("BATTLEON invented an unknown pending-music branch.");
                                }
                                catch (EventScriptException) when (candidate.LastFault?.Opcode == "BATTLEON")
                                {
                                }
                                JsonElement fiber = candidate.Capture().GetProperty("Fibers").EnumerateArray().Single(value => value.GetProperty("ScriptId").GetInt32() == 1);
                                Require(fiber.GetProperty("Position").GetInt32() == candidate.LastFault!.Operation && fiber.GetProperty("Stack").GetArrayLength() == 0 && candidate.Scene.MusicProcess == beforeMusic, "Unsupported BATTLEON advanced its PC, changed its stack or mutated pending music.");
                            }
                            Require(session.WorldControls == before && independent.WorldControls == before && JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Unsupported BATTLEON changed canonical controls or resumed differently after JSON restore.");
                            continue;
                        }

                        NativeWorldControlState expected = opcode == Jsm.Opcode.BATTLEOFF
                            ? before with { EventFlags104 = flags & ~0x400u, MusicKeep207 = 1 }
                            : disabled20 ? before
                            : pending == true ? before with { EventFlags104 = flags | 0x400u }
                            : before with { MusicKeep207 = 0 };
                        engine.Step();
                        restored.Step();
                        JsonElement results = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(value => value.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Results");
                        Require(session.WorldControls == expected && independent.WorldControls == expected && engine.Scene.MusicProcess == beforeMusic, "Encounter opcode changed an unrelated control byte/bit or selected the wrong pending-music branch.");
                        Require(results.GetProperty("1").GetInt32() == unchecked((int)expected.EventFlags104) && results.GetProperty("2").GetInt32() == expected.MusicKeep207 && !engine.EventMemory.Cells.ContainsKey(104) && !engine.EventMemory.Cells.ContainsKey(207), "Encounter opcode did not publish DWORD104/BYTE207 through their shared canonical memory owner.");
                        Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Encounter opcode continuation differed after independent JSON restore.");
                        Require(!engine.Capture().GetProperty("Scene").TryGetProperty("RandomBattlesEnabled", out _), "Encounter state retained a duplicate random-battle enable flag.");

                        engine.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                        Require(session.WorldControls == expected with { EventFlags104 = expected.EventFlags104 & ~0x400u, MusicKeep207 = 0, Menu209 = (byte)(expected.Menu209 & 0xFC) } && engine.Scene.MusicProcess == beforeMusic, "Normal field entry failed to clear encounter keep/menu state while retaining unrelated controls and pending music.");
                    }
                }
            }
        }
        foreach (int raw in new[] { -1, 0x1234, 256 })
        {
            byte[] code = BattleModeCallbackProgram((Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, raw), (Jsm.Opcode.POPM_B, 207), (Jsm.Opcode.PSHM_B, 207), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHSM_B, 207), (Jsm.Opcode.POPI_L, 2), (Jsm.Opcode.BATTLECUT, 0), (Jsm.Opcode.BATTLEON, 0), (Jsm.Opcode.PSHM_L, 104), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHM_B, 207), (Jsm.Opcode.POPI_L, 4), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8));
            GameSession session = CreateNativeMusicSession();
            EventEngine engine = new(session);
            engine.LoadField(74, code, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            bool waiting = false;
            engine.CommandExecuted += command => waiting |= command.Name == "WAIT";
            for (int frame = 0; frame < 4 && !waiting; frame++)
            {
                engine.Step();
            }
            Require(waiting && engine.Scene.MusicProcess?.Pending is null, "Direct encounter-memory fixture did not reach its initialized unknown-pending boundary.");
            NativeWorldControlState before = session.WorldControls! with { EventFlags103 = 0xD7, EventFlags104 = 0xABCDF618u, MusicKeep207 = 2, Menu209 = 0xE1 };
            session.SetWorldControls(before);
            bool completed = false;
            engine.CommandExecuted += command => completed |= command.Name == "BATTLEON";
            for (int frame = 0; frame < 4 && !completed; frame++)
            {
                engine.Step();
            }
            Require(completed, "Direct encounter-memory fixture did not complete its ordinary callback.");
            NativeWorldControlState expected = before with { EventFlags104 = before.EventFlags104 | 0x20u, MusicKeep207 = 1 };
            JsonElement results = engine.Capture().GetProperty("Fibers").EnumerateArray().Single(value => value.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Results");
            Require(results.GetProperty("1").GetInt32() == unchecked((byte)raw) && results.GetProperty("2").GetInt32() == unchecked((sbyte)raw), "Direct BYTE207 write lost low-byte narrowing or signed/unsigned canonical reads.");
            Require(session.WorldControls == expected && results.GetProperty("3").GetInt32() == unchecked((int)expected.EventFlags104) && results.GetProperty("4").GetInt32() == 1 && engine.Scene.MusicProcess?.Pending is null && !engine.EventMemory.Cells.ContainsKey(207), "BATTLECUT failed to publish byte207/mask20, preserve mask400/unrelated controls or bypass unknown pending music at subsequent BATTLEON.");
            GameSession independent = CloneMusicSession(session.State);
            EventEngine restored = new(independent);
            restored.LoadField(74, code, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            using JsonDocument document = JsonDocument.Parse(engine.Capture().GetRawText());
            restored.Restore(document.RootElement);
            Require(independent.WorldControls == expected && JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "BATTLECUT and direct BYTE207 ownership failed independent JSON restore.");
        }
        Console.WriteLine("Encounter controls: canonical DWORD104/BYTE207 writes, bit20 precedence, pending/unknown music branches, shared reads, independent JSON restore and normal field reset.");
    }
}
