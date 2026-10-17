using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMusicStopContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.MUSICSTOP, 0), (Jsm.Opcode.PSHM_L, 108), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine Create(byte[]? bytes = null)
        {
            GameSession session = CreateControlledMusicSession();
            EventEngine engine = new(session);
            engine.LoadField(100, bytes ?? content, []);
            SetControlledMusicChannels(engine);
            JsonNode saved = JsonNode.Parse(engine.Capture().GetRawText())!;
            saved["Scene"]!["LoadedMusic"] = 26;
            saved["Scene"]!["MusicPlaying"] = true;
            engine.Restore(JsonSerializer.SerializeToElement(saved));
            return engine;
        }
        EventEngine first = Create();
        first.Step();
        NativeMusicStop stopped = first.Scene.MusicStop ?? throw new InvalidOperationException("MUSICSTOP lost original stop provenance.");
        Require(stopped is { RawSlot: 2, Slot: 0, PriorChannelId: 0, StopRequested: true, BackendStatus: GlobalSoundBackendStatus.Muted } && first.Scene.MusicChannel108 is { ChannelId: -1, MusicId: 255 } && !first.Scene.MusicPlaying && first.Scene.LoadedMusic == 26 && first.Scene.MusicChannel112?.ChannelId == 1, "Unknown prior music channel fabricated a conditional stop/music byte or changed the other slot.");
        JsonElement begun = first.Capture();
        Require(begun.GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == -1 && CapturedVariables(begun).GetProperty("199").GetInt32() == 255, "Known post-stop DWORD was unreadable or unknown B199 was manufactured.");
        EventEngine resumed = Create();
        resumed.Restore(begun);
        for (int tick = 0; tick < 4; tick++)
        {
            first.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Music stop provenance/channel authority diverged after independent restore.");
        }
        byte[] repeat = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MUSICSTOP, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        first.LoadField(101, repeat, []);
        first.Step();
        Require(first.Scene.MusicStop is { PriorChannelId: -1, StopRequested: false } && first.Scene.MusicChannel108?.MusicId == 255, "Already-inactive channel claimed a backend stop or conditional music-byte write.");
        EventEngine across = Create(repeat);
        across.LoadField(101, repeat, []);
        across.Restore(first.Capture());
        Require(JsonElement.DeepEquals(first.Capture(), across.Capture()), "Cross-field music stop evidence failed VM-only independent restore.");

        byte[] unknownByte = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MUSICSTOP, 0), (Jsm.Opcode.PSHM_B, 199), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.RET, 0));
        EventEngine refused = Create(unknownByte);
        SetControlledMusicChannels(refused, -1, 1);
        try
        {
            refused.Step();
            throw new InvalidOperationException("MUSICSTOP supplied an unknown B199 value.");
        }
        catch (EventScriptException) when (refused.LastFault?.Opcode == "PSHM_B")
        {
        }
        Require(refused.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 3, "Unknown music-byte read advanced its issuing PC.");
        EventEngine ambiguous = Create();
        JsonNode alias = JsonNode.Parse(ambiguous.Capture().GetRawText())!;
        SetCapturedCell(alias, 107, 4, 65536);
        JsonElement beforeAlias = ambiguous.Capture();
        try
        {
            ambiguous.Restore(JsonSerializer.SerializeToElement(alias));
            throw new InvalidOperationException("MUSICSTOP restored an alias overlapping its canonical channel.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(beforeAlias, ambiguous.Capture()), "Rejected channel alias restore changed the controlled music-stop fixture.");

        foreach (Jsm.Opcode takeover in new[] { Jsm.Opcode.MUSICLOAD, Jsm.Opcode.MUSICCHANGE })
        {
            byte[] load = takeover == Jsm.Opcode.MUSICLOAD ? SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MUSICSTOP, 0), (Jsm.Opcode.PSHN_L, 26), (takeover, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)) : SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MUSICSTOP, 0), (takeover, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            EventEngine changed = Create(load);
            SetControlledMusicChannels(changed, 0, -1);
            try
            {
                changed.Step();
                throw new InvalidOperationException("Unproved generic music load/play selected a pending branch.");
            }
            catch (EventScriptException) when (changed.LastFault?.Opcode == takeover.ToString())
            {
            }
            Require(changed.Scene.MusicStop is not null && changed.Scene.MusicChannel108?.ChannelId == -1 && CapturedVariables(changed.Capture()).GetProperty("108").GetInt32() == -1, "Unproved music load/play erased the preceding actual stop authority.");
        }
        Console.WriteLine("Native MUSICSTOP: full slot operand masks &1, actual channel0 stop, repeated-1 no-call, retained gain and conditional music-byte writes; independent/cross-field restore, unknown aliases and unproved music load/play refusal verified.");
    }
}
