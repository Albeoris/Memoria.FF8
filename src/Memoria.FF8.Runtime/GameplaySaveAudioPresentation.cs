using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Optional device presentation hints. Resource or decoder incompatibility discards a position without invalidating gameplay.</summary>
public sealed record GameplaySaveAudioPosition(string Backend, string Fingerprint, string Unit, long Position, int SampleRate, int Channels, long LoopStart, long LoopEnd, int StartPoint = 0, double InitialSystemGain = 1, bool Paused = false);
public sealed record GameplaySaveAudioTrack(int Slot, int MusicId, GameplaySaveAudioPosition[] Positions);
public sealed record GameplaySaveAudioVoice(int Voice, uint EffectId, GameplaySaveAudioPosition Position);
public sealed record GameplaySaveAudioPresentation(int Version, GameplaySaveAudioTrack[] Music, GameplaySaveAudioVoice[] Ambient);
