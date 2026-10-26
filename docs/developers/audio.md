# Shared music and sound

`Memoria.FF8.Audio` contains the shared format readers, sample-clock scheduler, DLS synthesizer and PCM mixer. The same managed implementation runs on every supported RID. SDL3 is the device adapter; sound effects use the same PCM mixer as the music worker. The x64 music worker keeps the released Windows updater entry `Data/audio-host/Memoria.FF8.Music.exe`; the Unix executable name is `Memoria.FF8.Music`.

The worker preserves the line-delimited JSON command protocol: `Play`, `Volume`, `Stop`, `Pause`, `Resume`, `Playing` and `Quit`. Its first compatibility argument is retained for existing callers; playback does not use a window handle. Commands are queued outside the game update thread. Device timing never changes deterministic gameplay state.

## Supported format contract

- RIFF `DMSG` fixed sequences: 768 ticks per quarter note, tempo changes, timed band/program/bank selection, transposition, note offsets and durations, instantaneous controller/pitch curves, initial musical position, finite/infinite segment repeats and musical loop bounds.
- RIFF DLS wave-pool cues, instrument locales, key/velocity regions, sample tuning/gain, forward sample loops, global and region articulation, amplitude/pitch envelopes, LFO modulation, panning, exclusive key groups and same-key retrigger rules. Legacy DLS1 unqualified velocity regions are accepted; DLS2 velocity selectors retain their declared ranges. Duplicate instrument locales resolve in collection order.
- PCM8, PCM16 and Microsoft ADPCM mono/stereo sound decoding. ADPCM uses the file's coefficient table and independent block predictor state, including arithmetic negative prediction rounding.
- Shared PCM16 mixing with linear sample-rate conversion, hundredth-decibel volume/panning, loops, retained pause cursors and completion at sample boundaries.
- XG system reset and universal MIDI master volume. Other system-exclusive effects or instrument routing fail with an explicit unsupported-format error.

The synthesizer consistently uses the user's game-supplied DLS bank for GM and custom instruments. It does not obtain a platform-specific default GM bank. This gives identical instrument selection across operating systems, but can differ from a Windows default GM collection selected by the former DirectMusic backend.

The shared reverb uses the MIT-licensed MeltySynth implementation of Freeverb, with upstream defaults and MIDI reverb-send control. Its source, license and pinned provenance are under `src/Memoria.FF8.Audio/ThirdParty/MeltySynth`. It replaces the former shared WavesReverb audio path; its impulse response is different. Musical completion remains independent of the audible reverb tail.

Unknown essential sequence/codec/articulation features are errors. An existing instrument with no region covering a note produces its defined silence and appears in the instrument-coverage report; an unavailable instrument fails before playback. Original-resource tests inspect every note's band, bank, program, key and velocity selection rather than inferring support from a decoded formation or a short render.

## Thread and device ownership

Each SDL output owns one task that supplies 10 ms stereo blocks at a logical 44.1 kHz rate, maintaining roughly 25–35 ms of managed queued audio. SDL handles conversion to the device's rate. Mixer and synthesizer state are synchronized; commands retain musical/sample cursors while paused. Shutdown cancels and joins the output task before releasing SDL resources.

A missing runtime, unavailable device or failed stream is reported as an audio failure with a repair/reconnect action. The presentation owner reports that degraded state without changing deterministic gameplay. Recreate the output owner after repairing the device or installation. Native device switching and recovery still require acceptance on each supported desktop.

## Validation harness

Run the synthetic and optional original-resource checks separately:

```powershell
dotnet run --project tests/Memoria.FF8.Audio.Tests
$env:MEMORIA_FF8_AUDIO_FIXTURES = '<user-selected music directory>'
dotnet run --project tests/Memoria.FF8.Audio.Tests
```

With legally supplied files, the actual music worker can render PCM without a device, or exercise the default device:

```text
Memoria.FF8.Music --render bank.dls segment.sgt output.wav [seconds] [startTick]
Memoria.FF8.Music --play bank.dls segment.sgt seconds
```

Rendering is a decoder/scheduler/synthesizer check. Device playback, original recordings, and native Linux/macOS acceptance are separate gates. No game music, sound banks or reference recordings are distributed with these tests. Yamaha hardware-XG effect setup remains unsupported; successful software-segment tests do not establish that capability.
