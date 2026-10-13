using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeVibrationContract()
    {
        byte[] executable = File.ReadAllBytes(Path.Combine("ws", "original", "FF8_EN.exe"));
        Require(Convert.ToHexString(SHA256.HashData(executable)) == NativeFieldRuntimeProfile.FF8EnExecutableHash, "Vibration metadata requires the original executable identity.");
        int pe = BinaryPrimitives.ReadInt32LittleEndian(executable.AsSpan(60));
        int sections = BinaryPrimitives.ReadUInt16LittleEndian(executable.AsSpan(pe + 6));
        int optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(executable.AsSpan(pe + 20));
        int rva = NativeVibrationPatternTable.Address - 0x400000;
        int tableOffset = -1;
        for (int index = 0; index < sections; index++)
        {
            int section = pe + 24 + optionalSize + 40 * index;
            int start = BinaryPrimitives.ReadInt32LittleEndian(executable.AsSpan(section + 12));
            int size = Math.Max(BinaryPrimitives.ReadInt32LittleEndian(executable.AsSpan(section + 8)), BinaryPrimitives.ReadInt32LittleEndian(executable.AsSpan(section + 16)));
            if (rva >= start && rva < start + size)
            {
                tableOffset = BinaryPrimitives.ReadInt32LittleEndian(executable.AsSpan(section + 20)) + rva - start;
                break;
            }
        }
        Require(tableOffset >= 0 && Convert.ToHexString(SHA256.HashData(executable.AsSpan(tableOffset, 4 * NativeVibrationPatternTable.EntryCount))) == NativeVibrationPatternTable.Hash, "Original vibration pointer-table identity changed.");
        for (int index = 0; index < NativeVibrationPatternTable.EntryCount; index++)
        {
            Require(NativeVibrationPatternTable.Offset(index) == BinaryPrimitives.ReadInt32LittleEndian(executable.AsSpan(tableOffset + 4 * index)), "Shared vibration lookup does not match the original table entry.");
        }
        byte[] Content(int pattern = 32, int priority = 256, bool missingOperand = false)
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)];
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, pattern)];
            if (!missingOperand)
            {
                main.Add((Jsm.Opcode.PSHN_L, priority));
            }
            main.AddRange([(Jsm.Opcode.SETVIBRATE, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 33), (Jsm.Opcode.PSHN_L, -2), (Jsm.Opcode.SETVIBRATE, 0), (Jsm.Opcode.PSHN_L, 34), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETVIBRATE, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 0)]);
            return SyntheticObjects([1], [init, main.ToArray()]);
        }
        (GameSession Session, EventEngine Engine) Create(int pattern = 32, int priority = 256, bool missingOperand = false, bool native = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(600, Content(pattern, priority, missingOperand), [], nativeRuntimeProfile: native ? NativeFieldRuntimeProfile.FF8En : null);
            return (session, engine);
        }
        var live = Create();
        List<NativeVibrationRequest> issued = [];
        live.Engine.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.SETVIBRATE))
            {
                issued.Add(live.Engine.Scene.VibrationRequest!);
            }
        };
        live.Engine.Step();
        Require(live.Engine.Scene.VibrationRequest is null, "Fresh field allocation fabricated a native vibration return or request.");
        live.Engine.Step();
        Require(live.Session.State.Variables[5000] == 9 && live.Engine.Scene.VibrationRequest is { RawPattern: 32, RawPriority: 256, EffectivePriority: 257, PatternOffset: 0, ReturnDomain: NativeVibrationReturnDomain.MissingPatternOrNoQueue, BackendStatus: NativeVibrationBackendStatus.Muted } && live.Engine.Scene.VibrationRequest.PossibleNativeReturns.SequenceEqual([-1, 0]), "SETVIBRATE dropped operands, narrowed full priority, invented a handle or yielded instead of native return2.");
        JsonElement saved = live.Engine.Capture();
        var restored = Create();
        restored.Engine.Restore(saved);
        for (int callback = 0; callback < 2; callback++)
        {
            live.Engine.Step();
            restored.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "Vibration request native return domain diverged after independent restore.");
        }
        Require(issued.Select(request => (request.RawPattern, request.RawPriority, request.EffectivePriority)).SequenceEqual([(32, 256, 257), (33, -2, -1), (34, 0, 1)]) && live.Session.State.Variables[5001] == 7, "Successive original missing-pattern requests did not retain complete DWORD issue state and immediate continuation.");
        JsonNode invalid = JsonNode.Parse(saved.GetRawText())!;
        invalid["Scene"]![nameof(SceneEventState.VibrationRequest)]![nameof(NativeVibrationRequest.EffectivePriority)] = 1;
        try
        {
            Create().Engine.Restore(JsonSerializer.SerializeToElement(invalid));
            throw new InvalidOperationException("Saved vibration request accepted a forged priority narrowing.");
        }
        catch (InvalidDataException)
        {
        }
        foreach (int pattern in new[] { 7, 31, 63 })
        {
            var other = Create(pattern, -65536);
            other.Engine.Step();
            other.Engine.Step();
            Require(other.Engine.Scene.VibrationRequest is { RawPriority: -65536, EffectivePriority: -65535 } request && request.RawPattern == pattern, "Vibration zero-entry admission used the reached32/33/34 cases or truncated signed priority.");
        }
        foreach ((int pattern, bool missing, bool native) in new (int, bool, bool)[] { (0, false, true), (6, false, true), (-1, false, true), (64, false, true), (65568, false, true), (32, false, false) })
        {
            var refused = Create(pattern, 1, missing, native);
            refused.Engine.Step();
            try
            {
                refused.Engine.Step();
                throw new InvalidOperationException("SETVIBRATE accepted a defined/unproved pattern, raw index alias, missing operand or native history.");
            }
            catch (EventScriptException) when (refused.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.SETVIBRATE))
            {
            }
            JsonElement stack = refused.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean()).GetProperty("Stack");
            Require(stack.GetArrayLength() == (missing ? 1 : 2) && stack[stack.GetArrayLength() - 1].GetInt32() == pattern && refused.Engine.Scene.VibrationRequest is null && !refused.Session.State.Variables.ContainsKey(5000), "Vibration refusal consumed operands or mutated request/result state.");
        }
        NativeVibrationRequest last = live.Engine.Scene.VibrationRequest!;
        live.Engine.LoadField(601, SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.RET, 0)]]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(live.Engine.Scene.VibrationRequest == last, "Ordinary field load reset native1D9CDE8 request/return authority.");
        Console.WriteLine("SETVIBRATE: original64-entry table/hash, missing-pattern native{-1,0} return domain, full DWORD priority|1, same-budget return2, restored/repeated/global carry and atomic defined-pattern/index/history refusals passed.");
    }
}
