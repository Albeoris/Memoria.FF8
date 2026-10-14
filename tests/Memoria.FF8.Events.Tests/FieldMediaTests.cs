using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static byte[] FieldMediaProgram(params (Jsm.Opcode Opcode, int Parameter)[] commands)
    {
        byte[] bytes = SyntheticProgram([(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0), (Jsm.Opcode.LBL, 1), .. commands, (Jsm.Opcode.RET, 0)]);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), checked((ushort)(commands.Length + 4)));
        for (int index = 0; index < commands.Length; index++)
        {
            if (commands[index].Opcode == Jsm.Opcode.EFFECTPLAY2)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + 4 * (index + 3)), (int)Jsm.Opcode.EFFECTPLAY2 << 24 | commands[index].Parameter & 0xFFFFFF);
            }
        }
        return bytes;
    }

    private static EventEngine CreateFieldMedia(byte[] program, bool native = true, int fieldId = 101)
    {
        GameSession session = new(new GameState());
        EventEngine engine = new(session, session.State.Variables)
        {
            ResolveGlobalSoundResource = MutedGlobalSample,
            ResolveFieldSoundEffectTable = () => new NativeFieldSoundTable("generic.sfx", new string('A', 64), ImmutableArray.Create<uint>(10228, 10069))
        };
        engine.LoadField(fieldId, program, [], nativeRuntimeProfile: native ? NativeFieldRuntimeProfile.FF8En : null);
        engine.Step();
        return engine;
    }

    private static void NativeFieldMediaContract()
    {
        byte[] soundProgram = FieldMediaProgram((Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 384), (Jsm.Opcode.PSHN_L, 0x1000001), (Jsm.Opcode.EFFECTPLAY2, 0), (Jsm.Opcode.PSHN_L, -16), (Jsm.Opcode.PARTICLEON, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.PARTICLEOFF, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.SESTOP, 0));
        EventEngine sound = CreateFieldMedia(soundProgram);
        sound.Step();
        NativeFieldSoundRequest request = sound.Scene.NativeFieldSoundRequests.Single();
        Require(request.RawVolume == 255 && request.RawPan == 384 && request.RawChannelMask == 1 && request.Sound is { OriginalEffectId: 10228, Voice: 1, Volume: 127, RawPan: 128, EffectivePan: 64, Resource.SampleIndex: 2378 }, $"Field SFX narrowing, original-ID mapping or voice differs from native51FF00/46B2A0: {JsonSerializer.Serialize(request)}");
        Require(sound.Scene.SoundRequests.Count == 0 && sound.Scene.NativeParticleSlots.Count == 1 && sound.Scene.NativeParticleSlots[0] == 1 && sound.Scene.NativeParticleRequests.Single().RawSlot == -16, "Particle ON did not preserve masked original operand or invented unknown slot zeros.");
        EventEngine restored = CreateFieldMedia(soundProgram);
        restored.Restore(sound.Capture());
        for (int tick = 0; tick < 5; tick++)
        {
            sound.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(sound.Capture(), restored.Capture()), "Field SFX/particle complete VM differs after independent restoration.");
        }
        Require(sound.Scene.NativeParticleSlots[0] == 0 && sound.Scene.NativeParticleRequests.Count == 2 && sound.Scene.GlobalSoundRequests.Single().OriginalEffectId == 10228 && sound.Scene.GlobalSoundVoices[1].OriginalEffectId == 10228 && sound.Scene.SoundStop?.Voices.SequenceEqual(new[] { 1 }) == true, "Particle OFF or exact voice stop discarded original effect authority/history.");
        JsonElement mediaSaved = sound.Capture();
        foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
        {
            node => node["Scene"]!["NativeParticleSlots"]!["0"] = 1,
            node => node["Scene"]!["NativeFieldSoundRequests"]![0]!["RawPan"] = 385,
            node => node["Scene"]!["NativeMediaHistoryBoundary"]!["ParticleRequests"] = 999
        })
        {
            JsonNode malformed = JsonNode.Parse(mediaSaved.GetRawText())!;
            corrupt(malformed);
            try
            {
                sound.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Malformed native field media was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(mediaSaved, sound.Capture()), "Malformed media restore changed live VM.");
        }
        sound.LoadField(102, FieldMediaProgram(), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        Require(sound.Scene.NativeParticleSlots.Count == 0 && sound.Scene.NativeParticleRequests.Count == 2, "New field retained unjustified known particle flags or lost executed history.");
        sound.Step();
        sound.Restore(sound.Capture());

        foreach (int index in new[] { -1, 3 })
        {
            EventEngine rejected = CreateFieldMedia(FieldMediaProgram((Jsm.Opcode.PSHN_L, 127), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.EFFECTPLAY2, index), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 512)));
            rejected.ResolveGlobalSoundResource = null;
            rejected.Step();
            Require(rejected.Scene.NativeFieldSoundRequests.Single().Sound is null && rejected.Scene.GlobalSoundRequests.Count == 0 && rejected.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 9, "Native invalid field index did not consume and advance without a voice request.");
        }
        EventEngine adjacent = CreateFieldMedia(FieldMediaProgram((Jsm.Opcode.PSHN_L, 127), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.EFFECTPLAY2, 2)));
        try
        {
            adjacent.Step();
            throw new InvalidOperationException("Equal-count native field index invented adjacent memory.");
        }
        catch (EventScriptException) when (adjacent.LastFault?.Opcode == "EFFECTPLAY2")
        {
        }
        JsonElement held = adjacent.Capture().GetProperty("Fibers").EnumerateArray().Single(f => f.GetProperty("ScriptId").GetInt32() == 1);
        Require(held.GetProperty("Stack").GetArrayLength() == 3 && adjacent.Scene.NativeFieldSoundRequests.Count == 0, "Unknown adjacent SFX index consumed original operands.");

        NativeDisplayOffsetContract();
    }

    private static void NativeDisplayOffsetContract()
    {
        byte[] program = FieldMediaProgram((Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, -200), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.COFFSET, 0), (Jsm.Opcode.OFFSETSYNC, 0), (Jsm.Opcode.PSHN_L, 65539), (Jsm.Opcode.PSHN_L, 65540), (Jsm.Opcode.PSHN_L, 65541), (Jsm.Opcode.DOFFSET, 0), (Jsm.Opcode.PSHN_L, 400), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.COFFSET, 0), (Jsm.Opcode.OFFSETSYNC, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 512));
        EventEngine first = CreateFieldMedia(program, fieldId: 101);
        EventEngine second = CreateFieldMedia(program, fieldId: 220);
        first.Step();
        second.Step();
        Require(first.Actors[0].NativeDisplayOffset is { X: 0, Y: 0, Z: 0, Counter: 1, Mode: 2, Duration: 4 }, "COFFSET did not evaluate native counter0 after its issuing VM.");
        EventEngine replay = CreateFieldMedia(program);
        replay.Restore(first.Capture());
        Vector3[] golden = [new(14, -29, 43), new(50, -100, 150), new(85, -170, 256), new(100, -200, 300)];
        foreach (Vector3 expected in golden)
        {
            first.Step();
            second.Step();
            replay.Step();
            Require(first.Actors[0].DisplayOffset == expected && second.Actors[0].DisplayOffset == expected && JsonElement.DeepEquals(first.Capture(), replay.Capture()), "Native cosine golden, field-independent support or complete restored VM differs.");
            Require(!first.Capture().GetProperty("Variables").TryGetProperty("512", out _), "OFFSETSYNC continued before observing completed mode3 in VM.");
        }
        NativeDisplayOffsetState completed = first.Actors[0].NativeDisplayOffset!;
        Require(completed.Mode == 3 && completed.Counter == 4, "Cosine offset completion mode/counter differs.");
        NativeDisplayOffsetState? replaced = null;
        first.CommandExecuted += command =>
        {
            if (command.Name == "COFFSET")
            {
                replaced = first.Actors[0].NativeDisplayOffset;
            }
        };
        first.Step();
        replay.Step();
        Require(first.Actors[0].NativeLandingTriangle508 == 240 && first.Actors[0].NativeMovementCompletion542 == 0 && replaced is { X: 3, Y: 4, Z: 5, StartX: 100, StartY: -200, StartZ: 300, TargetX: 400, Counter: 0 } && first.Actors[0].NativeDisplayOffset is { X: 100, Y: -200, Z: 300, Counter: 1 }, "DOFFSET overwrote target words or COFFSET incorrectly started from current display XYZ.");
        Require(JsonElement.DeepEquals(first.Capture(), replay.Capture()), "Chained direct/cosine offsets changed restored VM.");
        for (int tick = 0; tick < 10; tick++)
        {
            first.Step();
            replay.Step();
            Require(JsonElement.DeepEquals(first.Capture(), replay.Capture()), "Chained offset completion failed independent restoration.");
        }
        Require(first.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 9, "Completed OFFSETSYNC failed to continue the original script.");
        JsonElement valid = first.Capture();
        foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
        {
            node => node["Actors"]![0]!["NativeDisplayOffset"]!["Counter"] = 9,
            node => node["Actors"]![0]!["NativeDisplayOffset"]!["TargetX"] = 401,
            node => node["Actors"]![0]!["NativeDisplayOffset"]!["Command"]!["ScriptId"] = 999,
            node => node["Actors"]![0]!["DisplayOffset"]!["X"] = 401
        })
        {
            JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
            corrupt(malformed);
            try
            {
                first.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Malformed native offset checkpoint was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(valid, first.Capture()), "Malformed offset restore changed live state.");
        }
        EventEngine unknown = CreateFieldMedia(program, native: false);
        try
        {
            unknown.Step();
            throw new InvalidOperationException("Legacy display offset inferred allocated target zeros.");
        }
        catch (EventScriptException) when (unknown.LastFault?.Opcode == "COFFSET")
        {
        }
        Require(unknown.Actors[0].NativeDisplayOffset is null && unknown.Actors[0].DisplayOffset == Vector3.Zero, "Unknown legacy target words changed on refused interpolation.");
        byte[] zeroProgram = FieldMediaProgram((Jsm.Opcode.PSHN_L, 12), (Jsm.Opcode.PSHN_L, -25), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.COFFSET, 0), (Jsm.Opcode.OFFSETSYNC, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.POPM_W, 512));
        EventEngine zero = CreateFieldMedia(zeroProgram);
        zero.Step();
        Require(zero.Actors[0].NativeDisplayOffset is { X: 12, Y: -25, Z: 3, Mode: 3, Counter: 0 }, "Native zero-duration cosine offset did not select its endpoint.");
        zero.Step();
        Require(zero.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 1, "Zero-duration OFFSETSYNC did not resume on the next VM update.");
        foreach (int duration in new[] { 65537, 0 })
        {
            byte[] linearProgram = FieldMediaProgram((Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 700), (Jsm.Opcode.PSHN_L, -932), (Jsm.Opcode.PSHN_L, duration), (Jsm.Opcode.LOFFSET, 0), (Jsm.Opcode.OFFSETSYNC, 0), (Jsm.Opcode.PSHN_L, 17), (Jsm.Opcode.POPM_W, 512));
            EventEngine linear = CreateFieldMedia(linearProgram);
            linear.Step();
            Require(linear.Actors[0].NativeDisplayOffset is { X: 0, Y: 0, Z: 0 } && linear.Actors[0].NativeDisplayOffset!.Mode == (duration == 0 ? 3 : 1), "Linear offset counter0 or native zero-duration start result differs.");
            EventEngine resumed = CreateFieldMedia(linearProgram);
            resumed.Restore(linear.Capture());
            linear.Step();
            resumed.Step();
            Require(linear.Actors[0].NativeDisplayOffset is { Mode: 3 } && linear.Actors[0].DisplayOffset == (duration == 0 ? Vector3.Zero : new Vector3(0, 700, -932)) && JsonElement.DeepEquals(linear.Capture(), resumed.Capture()), "Native linear duration1, lowWORD duration or independent restoration differs.");
            linear.Step();
            resumed.Step();
            Require(linear.Capture().GetProperty("Variables").GetProperty("512").GetInt32() == 17 && JsonElement.DeepEquals(linear.Capture(), resumed.Capture()), "Linear OFFSETSYNC failed next-VM continuation.");
        }
    }
}
