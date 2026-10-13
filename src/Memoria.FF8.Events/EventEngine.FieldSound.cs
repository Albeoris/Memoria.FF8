using System.Collections.Immutable;

namespace Memoria.FF8.Events;

public sealed record NativeFieldSoundTable(string MemberName, string Sha256, ImmutableArray<uint> EffectIds);
public sealed record NativeFieldSoundRequest(int Version, int FieldId, string ProgramHash, int ObjectId, int ScriptId, int Operation, long Frame, int EffectIndex, string TableMember, string TableHash, int TableCount, uint RawVolume, uint RawPan, uint RawChannelMask, GlobalSoundRequest? Sound);
public sealed record NativeMediaHistoryBoundary(int FieldSounds, int ParticleRequests);

public sealed partial class EventEngine
{
    private readonly Dictionary<long, NativeFieldSoundRequest> preparedFieldSounds = [];

    public Func<NativeFieldSoundTable?>? ResolveFieldSoundEffectTable { get; set; }

    private static bool IsNativeHash(string? value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }

    private string? UnsupportedFieldSound(Fiber fiber, int effectIndex)
    {
        if (fiber.Stack.Count < 3 || ResolveFieldSoundEffectTable is null)
        {
            return "EFFECTPLAY2 requires three original operands and the current field SFX table";
        }
        NativeFieldSoundTable? table;
        try
        {
            table = ResolveFieldSoundEffectTable();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or ArithmeticException or NotSupportedException or InvalidOperationException)
        {
            return $"EFFECTPLAY2 field table: {exception.Message}";
        }
        if (table is null || table.EffectIds.IsDefault || !IsNativeHash(table.Sha256) || string.IsNullOrWhiteSpace(table.MemberName) || !table.MemberName.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase))
        {
            return "EFFECTPLAY2 requires original field SFX metadata";
        }
        // Native51FF00 checks index>count, rather than >=count. Its equal-count
        // access reads adjacent loader memory, whose identity is not established.
        if (effectIndex == table.EffectIds.Length)
        {
            return "EFFECTPLAY2 equal-count index requires the original adjacent loader DWORD";
        }
        int[] arguments = fiber.Stack.Take(3).Reverse().ToArray();
        uint volume = unchecked((uint)arguments[0]);
        uint pan = unchecked((uint)arguments[1]);
        uint rawMask = unchecked((uint)arguments[2]);
        GlobalSoundRequest? sound = null;
        if (effectIndex >= 0 && effectIndex < table.EffectIds.Length)
        {
            uint mask = rawMask & 0xFFFFFF;
            if (mask == 0 || ResolveGlobalSoundResource is null)
            {
                return "EFFECTPLAY2 requires an explicit global backend and proved voice allocation; rotating automatic voices remain unknown";
            }
            uint effectId = table.EffectIds[effectIndex];
            int sample = MapGlobalSoundSample(effectId);
            GlobalSoundResource? resource;
            try
            {
                resource = ResolveGlobalSoundResource(sample);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or ArithmeticException or NotSupportedException)
            {
                return $"EFFECTPLAY2 global descriptor: {exception.Message}";
            }
            if (resource is null || !ValidGlobalSoundResource(resource, sample))
            {
                return "EFFECTPLAY2 requires a valid original global sound descriptor";
            }
            uint nativePan = unchecked((byte)pan);
            sound = new GlobalSoundRequest(effectId, rawMask, mask, System.Numerics.BitOperations.TrailingZeroCount(mask) + 1, nativePan, nativePan >> 1, volume & 127, 100, resource);
        }
        preparedFieldSounds[fiber.Sequence] = new NativeFieldSoundRequest(1, FieldId, programHash, fiber.ObjectId, fiber.Script.Id, fiber.Position, Frame, effectIndex, table.MemberName, table.Sha256, table.EffectIds.Length, volume, pan, rawMask, sound);
        return null;
    }

    private int[] ExecuteFieldSound(Fiber fiber, int effectIndex)
    {
        NativeFieldSoundRequest request = preparedFieldSounds[fiber.Sequence];
        preparedFieldSounds.Remove(fiber.Sequence);
        int[] arguments = Pop(fiber, 3);
        EnsureNativeMediaHistoryBoundary();
        Scene.NativeFieldSoundRequests.Add(request);
        if (request.Sound is GlobalSoundRequest sound)
        {
            Scene.GlobalSoundRequests.Add(sound);
            Scene.GlobalSoundVoices[sound.Voice] = sound;
        }
        return [effectIndex, .. arguments];
    }

    private void ValidateFieldSoundCheckpoint(Checkpoint saved)
    {
        ValidateNativeMediaHistoryBoundary(saved.Scene);
        foreach ((NativeFieldSoundRequest request, int index) in saved.Scene.NativeFieldSoundRequests.Select((request, index) => (request, index)))
        {
            bool rejected = request.EffectIndex < 0 || request.EffectIndex > request.TableCount;
            if (request.Version != 1 || (uint)request.FieldId > ushort.MaxValue || !IsNativeHash(request.ProgramHash) || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 0 || request.TableCount < 0 || !IsNativeHash(request.TableHash) || string.IsNullOrWhiteSpace(request.TableMember) || !request.TableMember.EndsWith(".sfx", StringComparison.OrdinalIgnoreCase) || request.EffectIndex == request.TableCount || rejected != (request.Sound is null))
            {
                throw new InvalidDataException("Saved field SFX request has inconsistent table, operand or native range provenance.");
            }
            if (request.Sound is GlobalSoundRequest sound && (sound.RawChannelMask != request.RawChannelMask || sound.RawPan != unchecked((byte)request.RawPan) || sound.Volume != (request.RawVolume & 127) || !saved.Scene.GlobalSoundRequests.Contains(sound)))
            {
                throw new InvalidDataException("Saved field SFX request differs from its resolved global voice attempt.");
            }
            if (index >= saved.Scene.NativeMediaHistoryBoundary!.FieldSounds && (request.FieldId != saved.FieldId || request.ProgramHash != saved.ProgramHash || request.Frame > saved.Frame || !MatchesNativeMediaInstruction(request.ObjectId, request.ScriptId, request.Operation) || program!.Operations[request.Operation].Opcode != Albeoris.Games.FF8.Jsm.Jsm.Opcode.EFFECTPLAY2 || program.Operations[request.Operation].Parameter != request.EffectIndex))
            {
                throw new InvalidDataException("Saved field SFX request does not identify its original issuing instruction.");
            }
        }
    }

    private void CopyNativeMediaHistory(SceneEventState previous)
    {
        Scene.NativeFieldSoundRequests.AddRange(previous.NativeFieldSoundRequests);
        Scene.NativeParticleRequests.AddRange(previous.NativeParticleRequests);
        Scene.NativeMediaHistoryBoundary = new NativeMediaHistoryBoundary(Scene.NativeFieldSoundRequests.Count, Scene.NativeParticleRequests.Count);
        preparedFieldSounds.Clear();
    }

    private void EnsureNativeMediaHistoryBoundary()
    {
        Scene.NativeMediaHistoryBoundary ??= new NativeMediaHistoryBoundary(Scene.NativeFieldSoundRequests.Count, Scene.NativeParticleRequests.Count);
    }

    private static void ValidateNativeMediaHistoryBoundary(SceneEventState scene)
    {
        if (scene.NativeMediaHistoryBoundary is not NativeMediaHistoryBoundary boundary)
        {
            if (scene.NativeFieldSoundRequests.Count != 0 || scene.NativeParticleRequests.Count != 0 || scene.NativeParticleSlots.Count != 0)
            {
                throw new InvalidDataException("Saved native media history has no current field boundary.");
            }
            return;
        }
        if (boundary.FieldSounds < 0 || boundary.FieldSounds > scene.NativeFieldSoundRequests.Count || boundary.ParticleRequests < 0 || boundary.ParticleRequests > scene.NativeParticleRequests.Count)
        {
            throw new InvalidDataException("Saved native media history boundary exceeds its retained requests.");
        }
    }

    private bool MatchesNativeMediaInstruction(int objectId, int scriptId, int operation)
    {
        return program is not null && (uint)objectId < program.Objects.Count && program.Objects[objectId].Scripts.Any(script => script.Id == scriptId && operation >= script.Start && operation < script.End);
    }
}
