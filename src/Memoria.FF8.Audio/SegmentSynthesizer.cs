namespace Memoria.FF8.Audio;

public sealed record SegmentInstrumentCoverage(int Notes, int MatchedRegions, IReadOnlyList<string> UnmappedNotes, IReadOnlyList<string> MissingInstruments);

/// <summary>Sample-clock sequencer and DLS level 1 wavetable synthesizer shared by every host.</summary>
public sealed class SegmentSynthesizer(DlsBank bank, int sampleRate = 44100) : IAudioSource
{
    private readonly object gate = new();
    private readonly ChannelState[] channels = Enumerable.Range(0, 256).Select(index => new ChannelState(index % 16 == 9)).ToArray();
    private readonly List<Note> notes = [];
    private readonly MeltySynth.Reverb reverb = new(sampleRate);
    private MusicSegment? segment;
    private double seconds;
    private int eventIndex;
    private uint repeats;
    private bool playing;
    private bool paused;
    private double gain = 1;
    private double systemGain = 1;
    private double[] eventSeconds = [];
    private double loopEndSeconds;
    private double loopStartSeconds;
    private double lengthSeconds;
    public int SampleRate { get; } = sampleRate;

    public bool IsPlaying
    {
        get
        {
            lock (gate)
            {
                return playing && !paused;
            }
        }
    }

    public double PositionSeconds
    {
        get
        {
            lock (gate)
            {
                return seconds;
            }
        }
    }

    public void Play(MusicSegment music, int hundredthDecibels = 0, int startPoint = 0)
    {
        if (startPoint < 0 || startPoint >= music.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startPoint));
        }
        SegmentInstrumentCoverage coverage = InspectInstrumentCoverage(music);
        if (coverage.MissingInstruments.Count != 0)
        {
            throw new NotSupportedException($"The segment contains unavailable DLS instruments: {string.Join("; ", coverage.MissingInstruments.Take(8))}");
        }
        lock (gate)
        {
            notes.Clear();
            foreach (ChannelState channel in channels)
            {
                channel.Reset();
            }
            segment = music;
            eventSeconds = music.Events.Select(e => music.SecondsAt(e.Tick)).ToArray();
            loopEndSeconds = music.SecondsAt(music.LoopEnd);
            loopStartSeconds = music.SecondsAt(music.LoopStart);
            lengthSeconds = music.SecondsAt(music.Length);
            repeats = music.Repeats;
            seconds = music.SecondsAt(startPoint);
            eventIndex = 0;
            gain = Gain(hundredthDecibels);
            paused = false;
            playing = true;
            while (eventIndex < music.Events.Count && music.Events[eventIndex].Tick < startPoint)
            {
                SegmentEvent item = music.Events[eventIndex++];
                if (item.Status != 0x90)
                {
                    Dispatch(item);
                }
            }
            ValidateInstruments(music);
        }
    }

    public SegmentInstrumentCoverage InspectInstrumentCoverage(MusicSegment music)
    {
        uint[] banks = new uint[256];
        ResetBanks(banks);
        uint[] programs = new uint[256];
        int[] transpositions = new int[256];
        int count = 0;
        int regions = 0;
        List<string> unmapped = [];
        List<string> missingInstruments = [];
        foreach (SegmentEvent item in music.Events)
        {
            if ((uint)item.Channel >= banks.Length)
            {
                throw new NotSupportedException($"Unsupported performance channel {item.Channel}.");
            }
            if (item.Status == 0xf0 && item.Data1 == 1)
            {
                Array.Clear(banks);
                ResetBanks(banks);
                Array.Clear(programs);
                Array.Clear(transpositions);
            }
            else if (item.Status == 0xc0)
            {
                if ((item.Flags & 1) != 0)
                {
                    programs[item.Channel] = item.Patch & 127;
                }
                if ((item.Flags & 2) != 0)
                {
                    banks[item.Channel] = (item.Patch & 0x80000000) | ((item.Patch >> 8) & 127) | (((item.Patch >> 16) & 127) << 8);
                }
                if ((item.Flags & 0x80) != 0)
                {
                    transpositions[item.Channel] = item.Transpose;
                }
            }
            else if (item.Status == 0x90 && item.Data2 != 0)
            {
                count++;
                uint selectedBank = banks[item.Channel];
                uint program = programs[item.Channel];
                int key = Math.Clamp(item.Data1 + transpositions[item.Channel], 0, 127);
                DlsInstrument? instrument = bank.Instruments.FirstOrDefault(i => i.Bank == selectedBank && i.Program == program);
                if (instrument is null)
                {
                    missingInstruments.Add($"tick={item.Tick} channel={item.Channel} bank={selectedBank:X8} patch={program}");
                }
                int matched = instrument?.Regions.Count(r => key >= r.KeyLow && key <= r.KeyHigh && item.Data2 >= r.VelocityLow && item.Data2 <= r.VelocityHigh) ?? 0;
                regions += matched;
                if (matched == 0)
                {
                    unmapped.Add($"tick={item.Tick} channel={item.Channel} bank={selectedBank:X8} patch={program} key={key} velocity={item.Data2}");
                }
            }
        }
        return new SegmentInstrumentCoverage(count, regions, unmapped, missingInstruments);
    }

    private static void ResetBanks(uint[] banks)
    {
        for (int channel = 0; channel < banks.Length; channel++)
        {
            banks[channel] = channel % 16 == 9 ? 0x80000000u : 0;
        }
    }

    private void ValidateInstruments(MusicSegment music)
    {
        foreach (SegmentEvent item in music.Events.Where(e => e.Status == 0xc0 && (e.Flags & 1) != 0))
        {
            uint patchBank = (item.Flags & 2) != 0 ? item.Patch & 0x807f7f00u : 0;
            uint bankNumber = (patchBank & 0x80000000) | ((patchBank >> 8) & 0x7f) | ((patchBank >> 16) & 0x7f) << 8;
            uint program = item.Patch & 127;
            if (!bank.Instruments.Any(i => i.Program == program && i.Bank == bankNumber))
            {
                throw new NotSupportedException($"DLS instrument bank {bankNumber:X8}, program {program} is unavailable.");
            }
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            playing = false;
            paused = false;
            notes.Clear();
        }
    }

    public void Pause()
    {
        lock (gate)
        {
            paused = playing;
        }
    }

    public void Resume()
    {
        lock (gate)
        {
            paused = false;
        }
    }

    public void Volume(int hundredthDecibels)
    {
        lock (gate)
        {
            gain = Gain(hundredthDecibels);
        }
    }

    private static double Gain(int value)
    {
        return value <= -10000 ? 0 : Math.Pow(10, Math.Clamp(value, -10000, 0) / 2000d);
    }

    public void RenderFrame(out double left, out double right)
    {
        lock (gate)
        {
            left = 0;
            right = 0;
            if (paused || segment is null)
            {
                return;
            }
            if (!playing)
            {
                MixReverb(0, ref left, ref right);
                return;
            }
            double loopEnd = loopEndSeconds;
            if (seconds >= loopEnd && repeats != 0)
            {
                if (repeats != uint.MaxValue)
                {
                    repeats--;
                }
                seconds = loopStartSeconds + seconds - loopEnd;
                eventIndex = 0;
                while (eventIndex < segment.Events.Count && segment.Events[eventIndex].Tick < segment.LoopStart)
                {
                    eventIndex++;
                }
            }
            if (seconds >= lengthSeconds)
            {
                playing = false;
                notes.Clear();
                MixReverb(0, ref left, ref right);
                return;
            }
            while (eventIndex < segment.Events.Count && eventSeconds[eventIndex] <= seconds)
            {
                Dispatch(segment.Events[eventIndex++]);
            }
            double send = 0;
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                Note note = notes[i];
                if (!note.Render(SampleRate, channels[note.Channel], out double l, out double r))
                {
                    notes.RemoveAt(i);
                }
                else
                {
                    left += l;
                    right += r;
                    send += (l + r) / Math.Sqrt(2) * channels[note.Channel].Controllers[91] / 127d;
                }
            }
            MixReverb(send, ref left, ref right);
            seconds += 1d / SampleRate;
        }
    }

    private void MixReverb(double send, ref double left, ref double right)
    {
        reverb.ProcessFrame((float)send, out float wetLeft, out float wetRight);
        left = (left + wetLeft) * gain * systemGain;
        right = (right + wetRight) * gain * systemGain;
    }

    private void Dispatch(SegmentEvent item)
    {
        if ((uint)item.Channel >= channels.Length)
        {
            throw new NotSupportedException($"Performance channel {item.Channel} exceeds the shared synthesizer channel capacity.");
        }
        ChannelState channel = channels[item.Channel];
        switch (item.Status)
        {
            case 0xf0:
                if (item.Data1 == 1)
                {
                    notes.Clear();
                    foreach (ChannelState state in channels)
                    {
                        state.Reset();
                    }
                    systemGain = 1;
                }
                else
                {
                    systemGain = item.Data2 / 16383d;
                }
                break;
            case 0xc0:
                if ((item.Flags & 1) != 0)
                {
                    channel.Program = item.Patch & 127;
                }
                if ((item.Flags & 2) != 0)
                {
                    channel.Bank = (item.Patch & 0x80000000) | ((item.Patch >> 8) & 127) | (((item.Patch >> 16) & 127) << 8);
                }
                if ((item.Flags & 0x20) != 0)
                {
                    channel.Controllers[10] = item.Data1;
                }
                if ((item.Flags & 0x40) != 0)
                {
                    channel.Controllers[7] = item.Data2;
                }
                if ((item.Flags & 0x80) != 0)
                {
                    channel.Transpose = item.Transpose;
                }
                break;
            case 0xb0:
                channel.Controllers[item.Data1] = item.Data2;
                if (item.Data1 == 120)
                {
                    notes.RemoveAll(n => n.Channel == item.Channel);
                }
                if (item.Data1 == 6 && channel.Controllers[101] == 0 && channel.Controllers[100] == 0)
                {
                    channel.BendRange = item.Data2;
                }
                break;
            case 0xe0:
                channel.PitchBend = item.Data2;
                break;
            case 0x90:
                if (item.Data2 == 0)
                {
                    break;
                }
                DlsInstrument instrument = bank.Instruments.FirstOrDefault(i => i.Bank == channel.Bank && i.Program == channel.Program) ?? throw new NotSupportedException($"Unavailable DLS instrument {channel.Bank:X8}:{channel.Program}.");
                int key = Math.Clamp(item.Data1 + channel.Transpose, 0, 127);
                foreach (Note prior in notes)
                {
                    if (prior.Channel == item.Channel && prior.Key == key && !prior.Region.SelfNonExclusive)
                    {
                        prior.Release();
                    }
                }
                foreach (DlsRegion region in instrument.Regions.Where(r => key >= r.KeyLow && key <= r.KeyHigh && item.Data2 >= r.VelocityLow && item.Data2 <= r.VelocityHigh))
                {
                    if (region.KeyGroup != 0)
                    {
                        notes.RemoveAll(n => n.Channel == item.Channel && n.Region.KeyGroup == region.KeyGroup);
                    }
                    double duration = segment!.SecondsAt(item.Tick + item.Duration) - segment.SecondsAt(item.Tick);
                    notes.Add(new Note(item.Channel, key, item.Data2, duration, region));
                }
                break;
        }
    }

    private sealed class ChannelState(bool drum)
    {
        public uint Bank = drum ? 0x80000000u : 0;
        public uint Program;
        public int Transpose;
        public int PitchBend = 8192;
        public int BendRange = 2;
        public int[] Controllers { get; } = new int[128];
        public void Reset()
        {
            Bank = drum ? 0x80000000u : 0;
            Program = 0;
            Transpose = 0;
            PitchBend = 8192;
            BendRange = 2;
            Array.Clear(Controllers);
            Controllers[7] = 100;
            Controllers[10] = 64;
            Controllers[11] = 127;
            Controllers[91] = 40;
            Controllers[100] = 127;
            Controllers[101] = 127;
        }
    }

    private sealed class Note
    {
        public int Channel { get; }
        public DlsRegion Region { get; }
        public int Key => key;
        private readonly int key;
        private readonly int velocity;
        private readonly double duration;
        private readonly double attack;
        private readonly double decay;
        private readonly double sustain;
        private readonly double release;
        private readonly double pitchAttack;
        private readonly double pitchDecay;
        private readonly double pitchSustain;
        private readonly double pitchRelease;
        private readonly double lfoDelay;
        private readonly double lfoFrequency;
        private readonly double attenuation;
        private readonly double regionPan;
        private readonly double basePitch;
        private readonly DlsConnection[] modulation;

        public Note(int channel, int key, int velocity, double duration, DlsRegion region)
        {
            Channel = channel;
            Region = region;
            this.key = key;
            this.velocity = velocity;
            this.duration = duration;
            attack = Time(0x0206, -12000);
            decay = Time(0x0207, -12000);
            sustain = Math.Clamp(Value(0x020a, 1000) / 1000, 0, 1);
            release = Time(0x0209, -12000);
            pitchAttack = Time(0x030a, -12000);
            pitchDecay = Time(0x030b, -12000);
            pitchSustain = Math.Clamp(Value(0x030e, 1000) / 1000, 0, 1);
            pitchRelease = Time(0x030d, -12000);
            lfoDelay = Time(0x0105, -12000);
            lfoFrequency = 8.176 * Math.Pow(2, Value(0x0104, 0) / 1200);
            attenuation = Region.Settings.Attenuation / 65536d + Value(1, 0);
            regionPan = Value(4, 0) / 500;
            basePitch = (key - Region.Settings.UnityNote) * 100 + Region.Settings.FineTune + Value(3, 0);
            modulation = Region.Connections.Where(c => c.Source != 0 && c.Destination is 1 or 3).ToArray();
        }
        private double age;
        private double position;
        private double released = -1;
        private double releaseLevel;

        public void Release()
        {
            if (released < 0)
            {
                released = age;
                releaseLevel = Envelope(0x0200, age);
            }
        }

        public bool Render(int rate, ChannelState channel, out double left, out double right)
        {
            left = 0;
            right = 0;
            if (age >= duration && released < 0 && channel.Controllers[64] < 64)
            {
                released = age;
                releaseLevel = Envelope(0x0200, age);
            }
            double envelope = released < 0 ? Envelope(0x0200, age) : releaseLevel * Math.Pow(10, -4.8 * (age - released) / release);
            if (released >= 0 && envelope < Math.Pow(10, -4.8) || position >= Region.Wave.Samples.Length)
            {
                return false;
            }
            double lfo = age < lfoDelay ? 0 : Math.Sin(2 * Math.PI * (age - lfoDelay) * lfoFrequency);
            double pitch = basePitch + (channel.PitchBend - 8192) / 8192d * channel.BendRange * 100 + Modulation(3, lfo, channel);
            double step = Region.Wave.Rate / (double)rate * Math.Pow(2, pitch / 1200);
            int first = (int)position;
            int next = Math.Min(first + 1, Region.Wave.Samples.Length - 1);
            if (Region.Settings.LoopLength > 0 && next >= Region.Settings.LoopStart + Region.Settings.LoopLength)
            {
                next = Region.Settings.LoopStart;
            }
            double fraction = position - first;
            double sample = Region.Wave.Samples[first] * (1 - fraction) + Region.Wave.Samples[next] * fraction;
            double volume = Math.Pow(velocity / 127d, 2) * Math.Pow(channel.Controllers[7] / 127d, 2) * Math.Pow(channel.Controllers[11] / 127d, 2) * envelope;
            volume *= Math.Pow(10, (attenuation + Modulation(1, lfo, channel)) / 200);
            double pan = Math.Clamp((channel.Controllers[10] - 64) / 63d + regionPan, -1, 1);
            left = sample * volume * Math.Sqrt((1 - pan) / 2);
            right = sample * volume * Math.Sqrt((1 + pan) / 2);
            position += step;
            if (Region.Settings.LoopLength > 0 && position >= Region.Settings.LoopStart + Region.Settings.LoopLength)
            {
                position = Region.Settings.LoopStart + (position - Region.Settings.LoopStart) % Region.Settings.LoopLength;
            }
            age += 1d / rate;
            return true;
        }

        private double Value(int destination, double fallback)
        {
            double result = fallback;
            foreach (DlsConnection connection in Region.Connections)
            {
                if (connection.Source == 0 && connection.Control == 0 && connection.Destination == destination)
                {
                    result = connection.Scale / 65536d;
                }
            }
            return result;
        }

        private double Time(int destination, double fallback)
        {
            double value = Value(destination, fallback);
            foreach (DlsConnection connection in Region.Connections)
            {
                if (connection.Destination == destination && connection.Control == 0)
                {
                    if (connection.Source == 2)
                    {
                        value += velocity / 128d * connection.Scale / 65536d;
                    }
                    else if (connection.Source == 3)
                    {
                        value += key / 128d * connection.Scale / 65536d;
                    }
                }
            }
            return Math.Max(1d / 44100, Math.Pow(2, value / 1200));
        }

        private double Envelope(int prefix, double time)
        {
            double a = prefix == 0x0200 ? attack : pitchAttack;
            double d = prefix == 0x0200 ? decay : pitchDecay;
            double level = prefix == 0x0200 ? sustain : pitchSustain;
            if (time < a)
            {
                return time / a;
            }
            // DLS level 1 decay/release are linear in dB, measured from full scale to -96 dB.
            double decayed = Math.Pow(10, -4.8 * (time - a) / d);
            return Math.Max(level, decayed < Math.Pow(10, -4.8) ? 0 : decayed);
        }

        private double Modulation(int destination, double lfo, ChannelState channel)
        {
            double result = 0;
            foreach (DlsConnection connection in modulation)
            {
                if (connection.Destination != destination || connection.Source == 0)
                {
                    continue;
                }
                double source = connection.Source switch
                {
                    1 => lfo,
                    2 => velocity / 128d,
                    3 => key / 128d,
                    4 => released < 0 ? Envelope(0x0200, age) : releaseLevel,
                    5 => released < 0 ? Envelope(0x0300, age) : Envelope(0x0300, released) * Math.Pow(10, -4.8 * (age - released) / pitchRelease),
                    _ => throw new NotSupportedException($"Unsupported DLS articulation source {connection.Source}.")
                };
                double control = connection.Control == 0 ? 1 : connection.Control >= 128 && connection.Control < 256 ? channel.Controllers[connection.Control - 128] / 128d : throw new NotSupportedException($"Unsupported DLS articulation control {connection.Control}.");
                if (connection.Transform != 0)
                {
                    throw new NotSupportedException($"Unsupported DLS articulation transform {connection.Transform}.");
                }
                result += source * control * connection.Scale / 65536d;
            }
            return result;
        }
    }
}
