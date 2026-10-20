using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSoundPositionContract()
    {
        foreach ((bool Transition, uint Mask, int Duration, int Pan) sample in new (bool, uint, int, int)[] { (true, 0, 20, 255), (true, 0xFF000000, 2, -1), (true, 0xFF800005, 3, 128), (true, 0x800000, 128, 256), (true, 4, -1, int.MinValue), (true, 5, int.MaxValue, int.MaxValue), (false, 0, 0, -1), (false, 0xFFFFFF, 0, 128), (false, 0x800000, 0, 256) })
        {
            List<(Jsm.Opcode, int)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.PSHM_L, 3000)];
            if (sample.Transition)
            {
                operations.Add((Jsm.Opcode.PSHM_L, 3004));
            }
            operations.AddRange([(Jsm.Opcode.PSHM_L, 3008), (sample.Transition ? Jsm.Opcode.SEPOSTRANS : Jsm.Opcode.SEPOS, 0), (Jsm.Opcode.POPM_B, 1000), (Jsm.Opcode.PSHN_L, 71), (Jsm.Opcode.POPM_B, 1001), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
            byte[] content = SyntheticProgram(operations.ToArray());
            (GameSession Session, EventEngine Engine) Create()
            {
                GameSession session = CreateControlledMusicSession();
                session.SetEventMemory(session.State.EventMemory.Write(3000, 4, NativeScalarValue.Exact(unchecked((int)sample.Mask))));
                session.SetEventMemory(session.State.EventMemory.Write(3004, 4, NativeScalarValue.Exact(sample.Duration)));
                session.SetEventMemory(session.State.EventMemory.Write(3008, 4, NativeScalarValue.Exact(sample.Pan)));
                EventEngine engine = new(session);
                engine.LoadField(100, content, []);
                return (session, engine);
            }
            (GameSession session, EventEngine first) = Create();
            int notifications = 0;
            first.AudioCommandExecuted += command =>
            {
                if (command is NativeSoundPosition)
                {
                    notifications++;
                }
            };
            first.Step();
            NativeSoundPosition request = first.Scene.SoundPosition ?? throw new InvalidOperationException("Native position command produced no request.");
            uint mask = sample.Mask & 0xFFFFFF;
            int voice = mask == 0 ? 26 : System.Numerics.BitOperations.TrailingZeroCount(mask) + 1;
            byte duration = sample.Transition ? unchecked((byte)(2 * sample.Duration)) : (byte)0;
            int milliseconds = duration * 10 < 50 ? 0 : duration * 10;
            Require(request.RawMask == sample.Mask && request.RawDuration == (sample.Transition ? sample.Duration : null) && request.RawPan == sample.Pan && request.Mask == mask && request.Voice == voice && request.Pan == unchecked((byte)sample.Pan) >> 1 && request.DurationByte == duration && request.DurationMilliseconds == milliseconds && request.BackendStatus == GlobalSoundBackendStatus.Muted, "Sound position lost original DWORD order, low24 voice selection, unsigned BYTE pan or wrapped doubled duration.");
            Require(notifications == 1 && session.State.Variables[1000] == 73 && session.State.Variables[1001] == 71 && session.GlobalSoundProcess!.Rotation == (mask == 0 ? 2 : 1) && session.GlobalSoundProcess.OriginalEffectIds.All(id => id == 0), "Sound position waited, consumed an unrelated operand, changed effect IDs or bypassed current automatic voice allocation.");
            JsonElement checkpoint = first.Capture();
            (GameSession _, EventEngine restored) = Create();
            restored.Restore(checkpoint);
            first.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(first.Capture(), restored.Capture()), "Sound position exact-frame/next-callback restoration diverged.");
            foreach (string property in new[] { "Voice", "Pan", "DurationMilliseconds", "LoadEpoch", "Operation" })
            {
                JsonNode malformed = JsonNode.Parse(checkpoint.GetRawText())!;
                malformed["Scene"]!["SoundPosition"]![property] = property == "Pan" ? 255 : -1;
                JsonElement before = restored.Capture();
                try
                {
                    restored.Restore(JsonSerializer.SerializeToElement(malformed));
                    throw new InvalidOperationException("Malformed sound position receipt passed restore.");
                }
                catch (InvalidDataException)
                {
                }
                Require(JsonElement.DeepEquals(before, restored.Capture()), "Invalid sound position restore changed VM or current audio owners.");
            }
            byte[] other = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8));
            first.LoadField(101, other, []);
            restored.LoadField(101, other, []);
            restored.Restore(first.Capture());
            Require(first.Scene.SoundPosition == request && JsonElement.DeepEquals(first.Capture(), restored.Capture()), "Field allocation erased process sound position history or compared it with the reset local clock.");
            first.LoadField(100, content, []);
            restored.LoadField(100, content, []);
            restored.Restore(first.Capture());
            Require(first.Frame == 0 && first.Scene.AudioLoadEpoch > request.LoadEpoch && JsonElement.DeepEquals(first.Capture(), restored.Capture()), "Same-field revisit lost the executed request's load identity.");
        }
        byte[] rejectedProgram = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SEPOSTRANS, 0), (Jsm.Opcode.RET, 8));
        foreach (bool missing in new[] { false, true })
        {
            RejectingSoundPositionServices services = new(missing ? null : NativeGlobalSoundProcess.FromBufferlessColdProcess());
            EventEngine rejected = new(services, NativeEventMemory.FromDWords(new Dictionary<int, int>()));
            rejected.LoadField(100, rejectedProgram, []);
            JsonElement owner = JsonSerializer.SerializeToElement(services.GlobalSoundProcess);
            try
            {
                rejected.Step();
                throw new InvalidOperationException("Sound position accepted a missing or rejecting persistent owner.");
            }
            catch (EventScriptException) when (rejected.LastFault?.Opcode == "SEPOSTRANS")
            {
            }
            JsonElement fiber = rejected.Capture().GetProperty("Fibers")[0];
            Require(services.Attempts == (missing ? 0 : 1) && rejected.Scene.SoundPosition is null && fiber.GetProperty("Position").GetInt32() == 4 && fiber.GetProperty("Stack").EnumerateArray().Select(item => item.GetInt32()).SequenceEqual([255, 20, 0]) && JsonElement.DeepEquals(owner, JsonSerializer.SerializeToElement(services.GlobalSoundProcess)), "Refused sound position advanced the PC, consumed operands or changed current audio ownership.");
        }
    }

    private sealed class RejectingSoundPositionServices(NativeGlobalSoundProcess? process) : IEventServices
    {
        public NativeGlobalSoundProcess? GlobalSoundProcess => process;
        public bool CanStoreGlobalSoundProcess => true;
        public bool IsMoviePlaying => false;
        public int Attempts { get; private set; }
        public void SetGlobalSoundProcess(NativeGlobalSoundProcess state)
        {
            Attempts++;
            throw new NotSupportedException("Component rejects global allocator publication.");
        }
        public void RequestMovie(int index)
        {
            throw new NotSupportedException();
        }
        public void RequestField(int fieldId, int entryPoint)
        {
            throw new NotSupportedException();
        }
        public void ShowDialog(string text)
        {
            throw new NotSupportedException();
        }
    }
}
