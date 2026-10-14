using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeMusicBootstrap(int Version, string Rule, string ExecutableHash);
public sealed record NativeMusicProcessState(int Version, bool? Pending, byte? Requested213, byte? Complete214, byte? Bank201, byte? Keep207, bool? KeepBit400, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NativeMusicBootstrap? Bootstrap = null);
public sealed record NativeMusicLoadRequest(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int RawMusic, byte Music, int PriorSecondChannel, byte? PriorKeep207, bool Loaded, GlobalSoundBackendStatus BackendStatus);
public sealed record NativeMusicPlayRequest(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, string Opcode, bool ConsumedPending, byte? Music, byte? PriorBank, byte? Bank, bool? PriorKeepBit400, int? Volume, int? Duration, int? Channel, GlobalSoundBackendStatus BackendStatus, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SkipPosition = null);

public sealed partial class EventEngine
{
    private void InitializeNewGameMusic(NativeFieldRuntimeProfile? profile, int fieldId)
    {
        if (program is not null || fieldId != 74 || profile is not { IsVerified: true } || services is not GameSession session || session.State.Scene != SceneKind.Field || session.State.FieldId != 74 || session.State.EventState is not null || session.State.ReplayJournal is not { EntryCount: 0, IsComplete: true } || session.State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            return;
        }
        if (variables.Keys.Any(address => address is >= 101 and <= 115 or >= 194 and <= 214))
        {
            throw new NotSupportedException("Native New Game music reset intersects memory without an executed empty bootstrap.");
        }
        // 470560 executes52CA90(1): B201=0, B199/200=FF, DWORD108/112=-1;
        // the preceding248-byte clear proves B207/213/214 and bit400 zero.
        Scene.MusicProcess = new NativeMusicProcessState(1, null, 0, 0, 0, 0, false, new NativeMusicBootstrap(1, "ff8-en-music-new-game-470560-52ca90-v1", profile.ExecutableHash));
        Scene.MusicChannel108 = new NativeMusicChannelState(-1, 255);
        Scene.MusicChannel112 = new NativeMusicChannelState(-1, 255);
        variables[108] = -1;
        variables[112] = -1;
        variables[199] = 255;
        variables[200] = 255;
        SetSoundVolumeByte(197, 127);
        SetSoundVolumeByte(198, 127);
    }

    private void CopyNativeMusicLoad(SceneEventState previous, NativeFieldRuntimeProfile? profile)
    {
        NativeMusicProcessState? process = previous.MusicProcess;
        if (profile is { IsVerified: true })
        {
            // Native52BC00's normal field branch resets only these field controls.
            process = (process ?? new NativeMusicProcessState(1, null, null, null, null, null, null)) with { Keep207 = 0, KeepBit400 = false };
            if (variables.ContainsKey(207))
            {
                variables[207] = 0;
            }
        }
        Scene.MusicProcess = process;
        Scene.MusicLoadRequest = previous.MusicLoadRequest;
        Scene.MusicPlayRequest = previous.MusicPlayRequest;
    }

    private void ValidateNativeMusicFieldLoad(NativeFieldRuntimeProfile? profile)
    {
        if (profile is { IsVerified: true } && variables.TryGetValue(207, out int alias) && Scene.MusicProcess?.Keep207 != alias)
        {
            throw new NotSupportedException("Native music field reset intersects an unproved B207 memory width.");
        }
    }

    private bool TryReadNativeMusicByte(int address, out int value)
    {
        byte? known = address switch
        {
            201 => Scene.MusicProcess?.Bank201,
            207 => Scene.MusicProcess?.Keep207,
            213 => Scene.MusicProcess?.Requested213,
            214 => Scene.MusicProcess?.Complete214,
            _ => null
        };
        value = known.GetValueOrDefault();
        return known is not null;
    }

    private string? UnsupportedMusicLoad(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is Jsm.Opcode.MUSICLOAD or Jsm.Opcode.MUSICCHANGE or Jsm.Opcode.MUSICSKIP or Jsm.Opcode.CROSSMUSIC or Jsm.Opcode.DUALMUSIC)
        {
            int count = operation.Opcode is Jsm.Opcode.MUSICLOAD or Jsm.Opcode.DUALMUSIC or Jsm.Opcode.MUSICSKIP ? 1 : operation.Opcode == Jsm.Opcode.CROSSMUSIC ? 2 : 0;
            if (FieldId < 72 || fiber.Stack.Count < count)
            {
                return "Native music load/play requires its original operands in an ordinary field";
            }
            if (operation.Opcode == Jsm.Opcode.MUSICLOAD && Scene.MusicChannel112 is null)
            {
                return "Native MUSICLOAD needs executed second-channel authority for its pending-load branch";
            }
            if (operation.Opcode != Jsm.Opcode.MUSICLOAD && Scene.MusicProcess?.Pending is null)
            {
                return "Native music play requires its executed pending-load flag";
            }
            if (services.NativeVariableValues?.Any(pair => new[] { 108, 109, 110, 111, 112, 113, 114, 115, 199, 200, 201, 207, 213, 214 }.Any(address => pair.Key <= address && pair.Key + pair.Value.Width > address)) == true)
            {
                return "Native music load/play overlaps uncertain memory without an exact owned-byte projection";
            }
            foreach ((int address, int value) in variables)
            {
                bool channel = address >= 109 && address <= 115 || operation.Opcode != Jsm.Opcode.MUSICLOAD && address >= 105 && address <= 111;
                bool bytes = address >= 196 && address <= 201 || address >= 204 && address <= 207 || address >= 210 && address <= 214;
                bool flags = address is >= 101 and <= 107 && (operation.Opcode == Jsm.Opcode.MUSICLOAD ? Scene.MusicChannel112?.ChannelId == -1 : Scene.MusicProcess?.Pending == true);
                bool knownChannel = address == 108 && Scene.MusicChannel108?.ChannelId == value || address == 112 && Scene.MusicChannel112?.ChannelId == value;
                if ((channel || bytes || flags) && !knownChannel && !IsKnownNativeByteAlias(address, value))
                {
                    return "Native music load/play intersects an unproved overlapping memory alias";
                }
            }
            return null;
        }
        int width = NativeMemoryWidth(operation.Opcode);
        if (width == 0 || !new[] { 201, 207, 213, 214 }.Any(address => operation.Parameter <= address && (long)operation.Parameter + width > address))
        {
            return null;
        }
        return width == 1 && operation.Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B && TryReadNativeMusicByte(operation.Parameter, out _) ? null : "Native music process memory requires known exact byte reads; overlapping access and unimplemented writers preserve operands";
    }

    private int[] ExecuteNativeMusicLoad(Fiber fiber)
    {
        int[] args = Pop(fiber, 1);
        byte music = unchecked((byte)args[0]);
        int channel = Scene.MusicChannel112!.ChannelId;
        bool load = channel == -1;
        NativeMusicProcessState prior = Scene.MusicProcess ?? new NativeMusicProcessState(1, null, null, null, null, null, null);
        Scene.MusicProcess = prior with { Pending = load, Requested213 = music, Complete214 = load ? (byte)1 : prior.Complete214, Keep207 = load ? (byte)1 : prior.Keep207, KeepBit400 = load ? prior.Keep207 is byte keep ? keep == 0 : null : prior.KeepBit400 };
        variables[213] = music;
        Scene.LoadedMusic = music;
        Scene.MusicLoadRequest = new NativeMusicLoadRequest(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, args[0], music, channel, prior.Keep207, load, GlobalSoundBackendStatus.Muted);
        // Native52CD10 is synchronous in this executable and writes completion1.
        // MUSICLOAD never replaces either native channel DWORD or its music BYTE.
        return args;
    }

    private int[] ExecuteNativeMusicPlay(Fiber fiber, Jsm.Opcode opcode)
    {
        bool secondary = opcode == Jsm.Opcode.DUALMUSIC;
        bool skip = opcode == Jsm.Opcode.MUSICSKIP;
        int[] args = opcode == Jsm.Opcode.CROSSMUSIC ? Pop(fiber, 2) : secondary || skip ? Pop(fiber, 1) : [];
        NativeMusicProcessState prior = Scene.MusicProcess!;
        bool consumed = prior.Pending == true;
        byte? bank = consumed && !secondary && prior.Bank201 is byte knownBank ? unchecked((byte)(knownBank ^ 1)) : prior.Bank201;
        int? volume = opcode == Jsm.Opcode.CROSSMUSIC || secondary ? args[0] & 127 : Scene.SoundMusicVolume197;
        int? duration = opcode == Jsm.Opcode.CROSSMUSIC ? unchecked(2 * args[1]) : null;
        if (consumed)
        {
            byte music = prior.Requested213 ?? throw new InvalidDataException("Native pending music has no executed requested byte.");
            // Music owns only DWORD104 mask400. The separate world-control
            // ledger retains mask200, mask20 and B209; no full DWORD is inferred.
            Scene.MusicProcess = prior with { Pending = false, Bank201 = bank, Keep207 = prior.KeepBit400 is bool keepBit ? (byte)(keepBit ? 0 : 1) : null, KeepBit400 = false };
            if (secondary)
            {
                // 51FB00 selects the opposite buffer without52CCE0's bank toggle;
                // every46B870/46B530 path returns its requested channel1.
                Scene.MusicChannel112 = new NativeMusicChannelState(1, music);
                variables[112] = 1;
                variables[200] = music;
            }
            else
            {
                Scene.MusicChannel108 = new NativeMusicChannelState(0, music);
                variables[108] = 0;
                variables[199] = music;
                Scene.MusicPlaying = true;
            }
            if (opcode == Jsm.Opcode.CROSSMUSIC)
            {
                // Installed46B850 ignores its duration; there is no invented audio ramp.
                Scene.MusicCrossfadeFrames = 0;
            }
            WaitFrames(fiber, 1);
        }
        // Native51F900 consumes a full DWORD even without pending music. With
        // pending music46B890 retains that position and returns primary channel0.
        Scene.MusicPlayRequest = new NativeMusicPlayRequest(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, opcode.ToString(), consumed, consumed ? prior.Requested213 : null, prior.Bank201, bank, prior.KeepBit400, volume, duration, consumed ? secondary ? 1 : 0 : null, GlobalSoundBackendStatus.Muted, skip ? args[0] : null);
        AudioCommandExecuted?.Invoke(Scene.MusicPlayRequest);
        return args;
    }

    private void ValidateMusicLoadCheckpoint(Checkpoint saved)
    {
        NativeMusicProcessState? process = saved.Scene.MusicProcess;
        if (process is not null && (process.Version != 1 || process.Pending == true && (process.Requested213 is null || process.Complete214 != 1)))
        {
            throw new InvalidDataException("Saved native music process lacks its pending load/completion authority.");
        }
        if (process?.Bootstrap is NativeMusicBootstrap bootstrap && (bootstrap.Version != 1 || bootstrap.Rule != "ff8-en-music-new-game-470560-52ca90-v1" || bootstrap.ExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash))
        {
            throw new InvalidDataException("Saved native music bootstrap is not the authenticated New Game reset.");
        }
        if (process?.Bank201 is byte knownBank && (process.Bootstrap is null || knownBank > 1) || process?.Pending is not null && saved.Scene.MusicLoadRequest is null || saved.Scene.MusicLoadRequest is NativeMusicLoadRequest currentLoad && (process is null || process.Requested213 != currentLoad.Music || !currentLoad.Loaded && process.Pending != false))
        {
            throw new InvalidDataException("Saved native music process disagrees with its executed bank/load authority.");
        }
        foreach (int address in new[] { 201, 207, 213, 214 })
        {
            byte? known = address == 201 ? process?.Bank201 : address == 207 ? process?.Keep207 : address == 213 ? process?.Requested213 : process?.Complete214;
            if (saved.Variables.TryGetValue(address, out int alias) && known is byte value && alias != value)
            {
                throw new InvalidDataException("Saved native music byte disagrees with its exact alias.");
            }
        }
        if (saved.Scene.MusicLoadRequest is NativeMusicLoadRequest load)
        {
            if (load.Version != 1 || load.Music != unchecked((byte)load.RawMusic) || load.Loaded != (load.PriorSecondChannel == -1) || load.BackendStatus != GlobalSoundBackendStatus.Muted)
            {
                throw new InvalidDataException("Saved native MUSICLOAD lost its original operand/channel branch.");
            }
            ValidateMusicRequestSite(saved, load.FieldId, load.ObjectId, load.ScriptId, load.Operation, load.Frame, Jsm.Opcode.MUSICLOAD);
        }
        if (saved.Scene.MusicPlayRequest is NativeMusicPlayRequest play)
        {
            bool secondary = play.Opcode == nameof(Jsm.Opcode.DUALMUSIC);
            if (play.Version != 1 || play.Opcode is not (nameof(Jsm.Opcode.MUSICCHANGE) or nameof(Jsm.Opcode.MUSICSKIP) or nameof(Jsm.Opcode.CROSSMUSIC) or nameof(Jsm.Opcode.DUALMUSIC)) || (play.Opcode == nameof(Jsm.Opcode.MUSICSKIP)) != (play.SkipPosition is not null) || play.Channel != (play.ConsumedPending ? secondary ? 1 : 0 : null) || play.ConsumedPending && play.Music is null || play.Bank != (play.ConsumedPending && !secondary && play.PriorBank is byte bank ? unchecked((byte)(bank ^ 1)) : play.PriorBank) || process?.Bank201 != play.Bank || secondary && (play.Volume is null or < 0 or > 127 || play.Duration is not null) || play.BackendStatus != GlobalSoundBackendStatus.Muted)
            {
                throw new InvalidDataException("Saved native music play lost its pending branch/channel/bank authority.");
            }
            ValidateMusicRequestSite(saved, play.FieldId, play.ObjectId, play.ScriptId, play.Operation, play.Frame, Enum.Parse<Jsm.Opcode>(play.Opcode));
        }
    }

    private void ValidateMusicRequestSite(Checkpoint saved, int fieldId, int objectId, int scriptId, int operation, long frame, Jsm.Opcode opcode)
    {
        if (fieldId < 72 || objectId < 0 || scriptId < 0 || operation < 0 || frame < 0)
        {
            throw new InvalidDataException("Invalid native music request source.");
        }
        if (fieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(actor => actor.Index == objectId)?.Scripts.FirstOrDefault(script => script.Id == scriptId);
            if (script is null || operation < script.Start || operation >= script.End || program.Operations[operation].Opcode != opcode)
            {
                throw new InvalidDataException("Saved native music request does not match its original instruction.");
            }
        }
    }
}
