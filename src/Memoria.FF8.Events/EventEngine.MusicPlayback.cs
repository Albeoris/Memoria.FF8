using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Events;

public enum NativeMusicBufferProducer
{
    Field52CD10,
    WorldWmset5306E0,
    WorldFile543340
}

public sealed record NativeMusicBufferIdentity([property: JsonRequired] byte EncodedAkaoId, [property: JsonRequired] NativeMusicBufferProducer Producer)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Producer))
        {
            throw new InvalidDataException("Native music buffer lost its actual load producer.");
        }
    }
}

public sealed record NativeMusicPlaybackDecision([property: JsonRequired] int Channel, [property: JsonRequired] int? MusicId, [property: JsonRequired] bool PlayRequested, [property: JsonRequired] bool StopRequested, [property: JsonRequired] bool ConcertRequested, [property: JsonRequired] int? PlayGain, [property: JsonRequired] int? FinalGain)
{
    public void Validate()
    {
        if ((uint)Channel > 1 || PlayRequested && (MusicId is null || PlayGain is null || !StopRequested || ConcertRequested) || ConcertRequested && (Channel != 0 || MusicId != 43 || !StopRequested || PlayGain is not null || FinalGain is not null))
        {
            throw new InvalidDataException("Native music decision lost its physical channel, decoded ID or actual stop/play/concert branch.");
        }
    }
}

public sealed partial class EventEngine
{
    private NativeGlobalSoundProcess RequireMusicSoundProcess()
    {
        NativeGlobalSoundProcess process = services.GlobalSoundProcess ?? throw new NotSupportedException("Native music requires its retained global sound process owner.");
        process.Validate();
        return process;
    }

    private int RequireMusicChannel(int slot)
    {
        return ReadMusicChannel(slot)?.ChannelId ?? throw new NotSupportedException("Native music requires the actual retained event channel DWORD108/112 before routing its call.");
    }

    private ushort ReadMusicCurrentField()
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || FieldId < 72 || FieldId > ushort.MaxValue)
        {
            throw new NotSupportedException("Native music remap requires the authenticated current-field WORD.");
        }
        return (ushort)FieldId;
    }

    private int[] PeekMusicArguments(Fiber fiber, int count)
    {
        if (fiber.Stack.Count < count)
        {
            throw new NotSupportedException("Native music requires all original operands before publication.");
        }
        return fiber.Stack.Take(count).Reverse().ToArray();
    }

    private NativeGlobalSoundProcess? PrepareMusicPlayback(Fiber fiber, Jsm.Opcode opcode, out NativeMusicPlaybackDecision? decision)
    {
        decision = null;
        NativeMusicPlaybackState Apply(Func<NativeMusicPlaybackState, NativeMusicPlaybackState> update, out NativeGlobalSoundProcess process)
        {
            process = RequireMusicSoundProcess();
            return update(process.MusicPlayback ?? NativeMusicPlaybackState.Unknown());
        }
        if (opcode == Jsm.Opcode.INITSOUND)
        {
            int first = RequireMusicChannel(0);
            int second = RequireMusicChannel(1);
            if (first == -1 && second == -1)
            {
                return null;
            }
            NativeMusicPlaybackState playback = Apply(state =>
            {
                if (first != -1)
                {
                    state = state.SetGain46BB40(unchecked((uint)first), 127);
                }
                return second == -1 ? state : state.SetGain46BB40(unchecked((uint)second), 127);
            }, out NativeGlobalSoundProcess process);
            return process with { MusicPlayback = playback };
        }
        if (opcode is Jsm.Opcode.MUSICVOL or Jsm.Opcode.MUSICVOLTRANS or Jsm.Opcode.MUSICVOLFADE)
        {
            int[] args = PeekMusicArguments(fiber, opcode == Jsm.Opcode.MUSICVOLFADE ? 4 : opcode == Jsm.Opcode.MUSICVOLTRANS ? 3 : 2);
            uint channel = unchecked((uint)RequireMusicChannel(args[0] & 1));
            NativeMusicPlaybackState playback = Apply(state => opcode switch
            {
                Jsm.Opcode.MUSICVOL => state.FadeGain46BBC0(channel, 16, args[1]),
                Jsm.Opcode.MUSICVOLTRANS => state.FadeGain46BBC0(channel, unchecked((byte)(2 * args[1])), args[2]),
                _ => state.TransitionGain46BC50(channel, unchecked((byte)(2 * args[1])), args[2], args[3])
            }, out NativeGlobalSoundProcess process);
            return process with { MusicPlayback = playback };
        }
        if (opcode == Jsm.Opcode.MUSICSTOP)
        {
            int channel = RequireMusicChannel(PeekMusicArguments(fiber, 1)[0] & 1);
            if (channel == -1)
            {
                return null;
            }
            NativeMusicPlaybackState playback = Apply(state => state.Stop46B800(unchecked((uint)channel)), out NativeGlobalSoundProcess process);
            return process with { MusicPlayback = playback };
        }
        if (opcode == Jsm.Opcode.MUSICREPLAY)
        {
            NativeMusicProcessState buffers = Scene.MusicProcess ?? throw new NotSupportedException("MUSICREPLAY requires the retained native buffer owner.");
            byte bank = buffers.Bank201 ?? throw new NotSupportedException("MUSICREPLAY requires the retained BYTE201 buffer selector.");
            byte encoded = buffers.ReadBuffer(bank).EncodedAkaoId;
            byte stored = Scene.SoundMusicVolume197 ?? throw new NotSupportedException("MUSICREPLAY requires the actual saved BYTE197 after its plain-play call.");
            int? playGain = null;
            NativeMusicPlaybackState playback = Apply(state =>
            {
                playGain = state.ReadGain(0);
                return state.PlayEncoded46B530(0, encoded, playGain.Value, ReadMusicCurrentField).SetGain46BB40(0, stored);
            }, out NativeGlobalSoundProcess process);
            bool changed = process.MusicPlayback?.CurrentMusicId0 != playback.CurrentMusicId0;
            decision = new(0, playback.CurrentMusicId0, changed, changed, false, playGain, stored);
            return process with { MusicPlayback = playback };
        }
        if (opcode is not (Jsm.Opcode.MUSICCHANGE or Jsm.Opcode.MUSICSKIP or Jsm.Opcode.CROSSMUSIC or Jsm.Opcode.DUALMUSIC or Jsm.Opcode.CHOICEMUSIC))
        {
            return null;
        }
        bool pending = Scene.MusicProcess?.Pending ?? throw new NotSupportedException("Native music play requires its executed pending-load flag.");
        if (!pending)
        {
            return null;
        }
        int count = opcode is Jsm.Opcode.CROSSMUSIC or Jsm.Opcode.CHOICEMUSIC ? 2 : opcode is Jsm.Opcode.DUALMUSIC or Jsm.Opcode.MUSICSKIP ? 1 : 0;
        int[] arguments = PeekMusicArguments(fiber, count);
        if (opcode == Jsm.Opcode.CHOICEMUSIC)
        {
            NativeMusicPlaybackState playback = Apply(state => state.Concert46B990(unchecked((uint)arguments[0]) & 0x03FFFFFFu, ReadMusicCurrentField), out NativeGlobalSoundProcess process);
            bool performed = (unchecked((uint)arguments[0]) & 0x03FFFFFFu) != 4095 || ReadMusicCurrentField() == 634;
            decision = new(0, playback.CurrentMusicId0, false, performed, performed, null, null);
            return process with { MusicPlayback = playback };
        }
        NativeMusicProcessState pendingBuffers = Scene.MusicProcess!;
        byte priorBank = pendingBuffers.Bank201 ?? throw new NotSupportedException("Native music play requires the actual BYTE201 buffer selector.");
        byte selected = checked((byte)(priorBank ^ 1));
        byte encodedMusic = pendingBuffers.ReadBuffer(selected).EncodedAkaoId;
        int? requestedGain = null;
        int? finalGain = null;
        NativeMusicPlaybackState next = Apply(state =>
        {
            if (opcode == Jsm.Opcode.CROSSMUSIC)
            {
                requestedGain = arguments[0] & 127;
                return state.Stop46C060(0).PlayEncoded46B530(0, encodedMusic, requestedGain.Value, ReadMusicCurrentField);
            }
            if (opcode == Jsm.Opcode.DUALMUSIC)
            {
                requestedGain = arguments[0] & 127;
                return state.PlayEncoded46B530(1, encodedMusic, requestedGain.Value, ReadMusicCurrentField);
            }
            byte stored = Scene.SoundMusicVolume197 ?? throw new NotSupportedException("Native primary music play requires actual saved BYTE197 after its plain-play call.");
            requestedGain = state.ReadGain(0);
            finalGain = stored;
            return state.PlayEncoded46B530(0, encodedMusic, requestedGain.Value, ReadMusicCurrentField).SetGain46BB40(0, stored);
        }, out NativeGlobalSoundProcess owner);
        int physicalChannel = opcode == Jsm.Opcode.DUALMUSIC ? 1 : 0;
        int? id = physicalChannel == 0 ? next.CurrentMusicId0 : next.CurrentMusicId1;
        int? comparedId = opcode == Jsm.Opcode.CROSSMUSIC ? 0 : physicalChannel == 0 ? owner.MusicPlayback?.CurrentMusicId0 : owner.MusicPlayback?.CurrentMusicId1;
        bool playRequested = comparedId != id;
        decision = new(physicalChannel, id, playRequested, opcode == Jsm.Opcode.CROSSMUSIC || playRequested, false, requestedGain, finalGain);
        return owner with { MusicPlayback = next };
    }

    private string? UnsupportedMusicPlayback(Fiber fiber, Jsm.Opcode opcode)
    {
        try
        {
            _ = PrepareMusicPlayback(fiber, opcode, out _);
            if (opcode == Jsm.Opcode.MUSICLOAD && Scene.MusicChannel112?.ChannelId == -1 && Scene.MusicProcess?.Bank201 is not (0 or 1))
            {
                return "Native MUSICLOAD requires its actual destination bank selector before loading a buffer.";
            }
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private NativeMusicPlaybackDecision? CommitMusicPlayback(Fiber fiber, Jsm.Opcode opcode)
    {
        if (PrepareMusicPlayback(fiber, opcode, out NativeMusicPlaybackDecision? decision) is NativeGlobalSoundProcess process)
        {
            PublishGlobalSoundProcess(fiber, process);
        }
        return decision;
    }
}
