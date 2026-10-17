using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public sealed record NativeMusicLoadRequest(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int RawMusic, byte Music, int PriorSecondChannel, byte? PriorKeep207, bool Loaded, GlobalSoundBackendStatus BackendStatus);
public sealed record NativeMusicPlayRequest(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, string Opcode, bool ConsumedPending, byte? Music, byte? PriorBank, byte? Bank, bool? PriorKeepBit400, int? Volume, int? Duration, int? Channel, GlobalSoundBackendStatus BackendStatus, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SkipPosition = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] uint? ChoiceMask = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicPlaybackDecision? Playback { get; init; }
}

public sealed partial class EventEngine
{
    private void InitializeNewGameMusic(NativeFieldRuntimeProfile? profile, int fieldId)
    {
        if (program is not null || fieldId != 74 || profile is not { IsVerified: true } || services is not GameSession session || session.State.Scene != SceneKind.Field || session.State.FieldId != 74 || session.State.EventState is not null || session.State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            return;
        }
        if (EventMemory.Cells.Any(pair => pair.Key < 116 && (long)pair.Key + pair.Value.Width > 108 || pair.Key < 184 && (long)pair.Key + pair.Value.Width > 182 || pair.Key < 204 && (long)pair.Key + pair.Value.Width > 197 || pair.Key <= 207 && (long)pair.Key + pair.Value.Width > 207 || pair.Key < 215 && (long)pair.Key + pair.Value.Width > 213))
        {
            throw new NotSupportedException("Native New Game music reset intersects memory without an executed empty bootstrap.");
        }
        // 470560 executes52CA90(1): B201=0, B199/200/202=FF, DWORD108/112=-1;
        // the preceding248-byte clear proves B207/213/214 and bit400 zero.
        Scene.MusicProcess = new NativeMusicProcessState(1, null, 0, 0, 0, 255, new NativeMusicBootstrap(1, "ff8-en-music-new-game-470560-52ca90-v1", profile.ExecutableHash));
        Scene.MusicChannel108 = new NativeMusicChannelState(-1, 255);
        Scene.MusicChannel112 = new NativeMusicChannelState(-1, 255);
        Scene.NativeBattleMusic203 = 5;
        SetSoundVolumeByte(197, 127);
        SetSoundVolumeByte(198, 127);
    }

    private void CopyNativeMusicLoad(SceneEventState previous, NativeFieldRuntimeProfile? profile, bool restoringCheckpoint = false)
    {
        if (!restoringCheckpoint && profile is { IsVerified: true } && services.WorldControls is NativeWorldControlState controls)
        {
            // Normal52BC00 resets B207 and DWORD104 mask400 without resetting
            // pending, requested/completion bytes, buffers or channel identities.
            services.SetWorldControls(controls.WithNormalFieldLoad());
        }
        Scene.MusicProcess = previous.MusicProcess;
        Scene.NativeBattleMusicEntry = previous.NativeBattleMusicEntry;
        Scene.NativeBattleFieldReturn = previous.NativeBattleFieldReturn;
        Scene.NativeBattleMusic203 = previous.NativeBattleMusic203;
        Scene.MusicLoadRequest = previous.MusicLoadRequest;
        Scene.MusicPlayRequest = previous.MusicPlayRequest;
        Scene.MusicReplayRequest = previous.MusicReplayRequest;
        Scene.WorldMusicSelection = previous.WorldMusicSelection;
        Scene.WorldMusicLoad = previous.WorldMusicLoad;
    }

    private void ValidateNativeMusicFieldLoad(NativeFieldRuntimeProfile? profile)
    {
        if (profile is { IsVerified: true } && UnsupportedNativeMusicAliases(EventMemory) is string aliases)
        {
            throw new NotSupportedException(aliases);
        }
        if (profile is { IsVerified: true } && EventMemory.Cells.Any(pair => pair.Key <= 207 && (long)pair.Key + pair.Value.Width > 207))
        {
            throw new NotSupportedException("Native music field reset intersects a separate B207 memory alias; canonical event ownership is required.");
        }
    }

    private static string? UnsupportedNativeMusicAliases(NativeEventMemory memory)
    {
        // These are physical BYTEs owned by MusicProcess, not DWORD entries in Variables.
        // Readers project the owner directly; even equal scalar copies create a second owner.
        return memory.Cells.Any(pair => pair.Key <= 202 && (long)pair.Key + pair.Value.Width > 201 || pair.Key <= 214 && (long)pair.Key + pair.Value.Width > 213) ? "Native music process bytes overlap a second memory owner; migrate this save explicitly." : null;
    }

    private bool TryReadNativeMusicByte(int address, out int value)
    {
        byte? known = address switch
        {
            201 => Scene.MusicProcess?.Bank201,
            202 => Scene.MusicProcess?.Bank202,
            203 => Scene.NativeBattleMusic203,
            207 => services.WorldControls?.MusicKeep207,
            213 => Scene.MusicProcess?.Requested213,
            214 => Scene.MusicProcess?.Complete214,
            _ => null
        };
        value = known.GetValueOrDefault();
        return known is not null;
    }

    private string? UnsupportedMusicLoad(Fiber fiber, JsmOperation operation)
    {
        if (Scene.MusicProcess is not null && UnsupportedNativeMusicAliases(EventMemory) is string musicAliases)
        {
            return musicAliases;
        }
        if (UnsupportedWorldMusicSelection(fiber, operation) is string selection)
        {
            return selection;
        }
        if (operation.Opcode == Jsm.Opcode.EFFECTLOAD && (fiber.Stack.Count == 0 || !fiber.Stack.PeekValue().IsExact || LoadSoundResource is null || EventMemory.Cells.Any(pair => pair.Key <= 202 && (long)pair.Key + pair.Value.Width > 202)))
        {
            return "EFFECTLOAD requires its exact operand, explicit backend and unaliased physical BYTE202 owner";
        }
        if (operation.Opcode is Jsm.Opcode.MUSICLOAD or Jsm.Opcode.MUSICCHANGE or Jsm.Opcode.MUSICSKIP or Jsm.Opcode.CHOICEMUSIC or Jsm.Opcode.CROSSMUSIC or Jsm.Opcode.DUALMUSIC)
        {
            int count = operation.Opcode is Jsm.Opcode.MUSICLOAD or Jsm.Opcode.DUALMUSIC or Jsm.Opcode.MUSICSKIP ? 1 : operation.Opcode is Jsm.Opcode.CROSSMUSIC or Jsm.Opcode.CHOICEMUSIC ? 2 : 0;
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
            bool writesControls = operation.Opcode == Jsm.Opcode.MUSICLOAD ? Scene.MusicChannel112?.ChannelId == -1 : Scene.MusicProcess?.Pending == true;
            if (operation.Opcode != Jsm.Opcode.MUSICLOAD && Scene.MusicProcess is { Pending: true, Requested213: null })
            {
                return "Native pending music requires its executed requested BYTE213";
            }
            if (operation.Opcode == Jsm.Opcode.CHOICEMUSIC)
            {
                if (NativeRuntimeProfile is not { IsVerified: true })
                {
                    return "Native CHOICEMUSIC requires its authenticated original mask and pending-load contract";
                }
                if (Scene.MusicProcess?.Pending == true)
                {
                    try
                    {
                        NativeGlobalSoundProcess backend = services.GlobalSoundProcess ?? throw new NotSupportedException("Native CHOICEMUSIC requires the explicit current audio backend owner.");
                        backend.Validate();
                    }
                    catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
                    {
                        return exception.Message;
                    }
                }
            }
            if (writesControls)
            {
                if (services.WorldControls is not NativeWorldControlState controls)
                {
                    return "Native music keep writes require the current event DWORD104 and BYTE207 owner";
                }
                controls.Validate();
                if (UnsupportedWorldControlAliases() is string aliases)
                {
                    return aliases;
                }
            }
            foreach ((int address, NativeMemoryValue value) in EventMemory.Cells)
            {
                long end = (long)address + value.Width;
                bool channel = address < 116 && end > 108;
                bool bytes = address < 203 && end > 199 || address <= 207 && end > 207 || address < 215 && end > 213;
                bool flags = writesControls && address < 108 && end > 103;
                if (channel || bytes || flags)
                {
                    return "Native music load/play intersects an unproved overlapping memory alias";
                }
            }
            return null;
        }
        int width = NativeMemoryWidth(operation.Opcode);
        if (width == 0 || !new[] { 201, 202, 213, 214 }.Any(address => operation.Parameter <= address && (long)operation.Parameter + width > address))
        {
            return null;
        }
        return width == 1 && operation.Opcode is Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHSM_B && TryReadNativeMusicByte(operation.Parameter, out _) ? null : "Native music process memory requires known exact byte reads; overlapping access and unimplemented writers preserve operands";
    }

    private int[] ExecuteNativeMusicLoad(Fiber fiber)
    {
        byte music = unchecked((byte)fiber.Stack.Peek());
        int channel = Scene.MusicChannel112!.ChannelId;
        bool load = channel == -1;
        NativeMusicProcessState prior = Scene.MusicProcess ?? new NativeMusicProcessState(1, null, null, null, null, null);
        NativeMusicProcessState loaded = load ? prior.LoadBuffer(checked((byte)(prior.Bank201!.Value ^ 1)), new NativeMusicBufferIdentity(unchecked((byte)(music + 1)), NativeMusicBufferProducer.Field52CD10)) : prior;
        byte? priorKeep = services.WorldControls?.MusicKeep207;
        if (load)
        {
            NativeWorldControlState controls = services.WorldControls!;
            NativeWorldControlState next = controls with { EventFlags104 = controls.MusicKeep207 == 0 ? controls.EventFlags104 | 0x400u : controls.EventFlags104 & ~0x400u, MusicKeep207 = 1 };
            SetNativeWorldControls(fiber, next);
        }
        int[] args = Pop(fiber, 1);
        Scene.MusicProcess = loaded with { Pending = load, Requested213 = music, Complete214 = load ? (byte)1 : prior.Complete214 };
        Scene.LoadedMusic = music;
        Scene.MusicLoadRequest = new NativeMusicLoadRequest(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, args[0], music, channel, priorKeep, load, GlobalSoundBackendStatus.Muted);
        // Native52CD10 is synchronous in this executable and writes completion1.
        // MUSICLOAD never replaces either native channel DWORD or its music BYTE.
        return args;
    }

    private int[] ExecuteNativeMusicPlay(Fiber fiber, Jsm.Opcode opcode)
    {
        NativeGlobalSoundProcess? sound = PrepareMusicPlayback(fiber, opcode, out NativeMusicPlaybackDecision? playback);
        bool secondary = opcode == Jsm.Opcode.DUALMUSIC;
        bool skip = opcode == Jsm.Opcode.MUSICSKIP;
        bool choice = opcode == Jsm.Opcode.CHOICEMUSIC;
        int count = opcode == Jsm.Opcode.CROSSMUSIC || choice ? 2 : secondary || skip ? 1 : 0;
        int[] args = fiber.Stack.Take(count).Reverse().ToArray();
        NativeMusicProcessState prior = Scene.MusicProcess!;
        bool consumed = prior.Pending == true;
        byte? bank = consumed && !secondary && prior.Bank201 is byte knownBank ? unchecked((byte)(knownBank ^ 1)) : prior.Bank201;
        int? volume = choice ? null : opcode == Jsm.Opcode.CROSSMUSIC || secondary ? args[0] & 127 : Scene.SoundMusicVolume197;
        int? duration = opcode == Jsm.Opcode.CROSSMUSIC ? unchecked(2 * args[1]) : null;
        // 51F9A0 discards the top DWORD, masks the lower DWORD, and 46B990
        // always returns channel0, including4095's native early-return path.
        uint? choiceMask = choice ? unchecked((uint)args[0]) & 0x03FFFFFFu : null;
        bool? priorKeepBit = services.WorldControls is NativeWorldControlState knownControls ? (knownControls.EventFlags104 & 0x400) != 0 : null;
        if (consumed)
        {
            NativeWorldControlState controls = services.WorldControls!;
            NativeWorldControlState next = controls with { MusicKeep207 = (byte)((controls.EventFlags104 & 0x400) == 0 ? 1 : 0), EventFlags104 = controls.EventFlags104 & ~0x400u };
            PublishMusicPlaybackOwners(fiber, next, sound ?? throw new InvalidDataException("Consumed native music play lost its prepared global process."));
        }
        Pop(fiber, count);
        if (consumed)
        {
            Scene.WorldMusicLoad = null;
            byte music = prior.Requested213 ?? throw new InvalidDataException("Native pending music has no executed requested byte.");
            Scene.MusicProcess = prior with { Pending = false, Bank201 = bank, SelectionAuthority = NativeMusicSelectionAuthority.Field };
            if (secondary)
            {
                // 51FB00 selects the opposite buffer without52CCE0's bank toggle;
                // every46B870/46B530 path returns its requested channel1.
                Scene.MusicChannel112 = new NativeMusicChannelState(1, music);
            }
            else
            {
                Scene.MusicChannel108 = new NativeMusicChannelState(0, music, choiceMask);
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
        Scene.MusicPlayRequest = new NativeMusicPlayRequest(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, opcode.ToString(), consumed, consumed ? prior.Requested213 : null, prior.Bank201, bank, priorKeepBit, volume, duration, consumed ? secondary ? 1 : 0 : null, GlobalSoundBackendStatus.Muted, skip ? args[0] : null, choiceMask) { Playback = playback };
        AudioCommandExecuted?.Invoke(Scene.MusicPlayRequest);
        return args;
    }

    private void ValidateMusicLoadCheckpoint(Checkpoint saved)
    {
        ValidateWorldMusicCheckpoint(saved);
        NativeMusicProcessState? process = saved.Scene.MusicProcess;
        process?.Validate();
        saved.Scene.NativeBattleMusicEntry?.Validate();
        process?.Buffer0?.Validate();
        process?.Buffer1?.Validate();
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
        if (process is not null && UnsupportedNativeMusicAliases(saved.EventMemory) is string aliases)
        {
            throw new InvalidDataException(aliases);
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
            play.Playback?.Validate();
            if (!play.ConsumedPending && play.Playback is not null || play.Playback is { } decision && decision.Channel != play.Channel)
            {
                throw new InvalidDataException("Saved music decision disagrees with its actual pending/channel branch.");
            }
            if (play.Playback is { } selected && play.ChoiceMask is null && play.Music is byte source && selected.MusicId != (source == 255 ? -1 : source == 17 && play.FieldId != 339 ? 99 : source))
            {
                throw new InvalidDataException("Saved music decision differs from its source's decoded/remapped AKAO identity.");
            }
            bool secondary = play.Opcode == nameof(Jsm.Opcode.DUALMUSIC);
            if (play.Version != 1 || play.Opcode is not (nameof(Jsm.Opcode.MUSICCHANGE) or nameof(Jsm.Opcode.MUSICSKIP) or nameof(Jsm.Opcode.CHOICEMUSIC) or nameof(Jsm.Opcode.CROSSMUSIC) or nameof(Jsm.Opcode.DUALMUSIC)) || (play.Opcode == nameof(Jsm.Opcode.MUSICSKIP)) != (play.SkipPosition is not null) || (play.Opcode == nameof(Jsm.Opcode.CHOICEMUSIC)) != (play.ChoiceMask is not null) || play.ChoiceMask > 0x03FFFFFFu || play.ChoiceMask is not null && (play.Volume is not null || play.Duration is not null) || play.Channel != (play.ConsumedPending ? secondary ? 1 : 0 : null) || play.ConsumedPending && play.Music is null || play.Bank != (play.ConsumedPending && !secondary && play.PriorBank is byte bank ? unchecked((byte)(bank ^ 1)) : play.PriorBank) || process?.SelectionAuthority == NativeMusicSelectionAuthority.Field && process.Bank201 != play.Bank || secondary && (play.Volume is null or < 0 or > 127 || play.Duration is not null) || play.BackendStatus != GlobalSoundBackendStatus.Muted)
            {
                throw new InvalidDataException("Saved native music play lost its pending branch/channel/bank authority.");
            }
            if (play is { ConsumedPending: true, ChoiceMask: uint mask } && saved.Scene.WorldMusicLoad is null && saved.Scene.MusicChannel108 is { ChannelId: 0 } primary && primary.ChoiceMask != mask)
            {
                throw new InvalidDataException("Saved concert channel disagrees with its executed instrument selection.");
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
            if (script is null || !script.ContainsOperation(operation) || program.Operations[operation].Opcode != opcode)
            {
                throw new InvalidDataException("Saved native music request does not match its original instruction.");
            }
        }
    }
}
