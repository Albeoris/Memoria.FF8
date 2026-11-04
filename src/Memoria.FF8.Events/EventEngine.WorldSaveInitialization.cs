using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private const string WorldEventInitializationRule = NativeMusicBootstrap.WorldSaveInitializationRule;

    private sealed record WorldEventSnapshot(int Version, int FieldId, SceneEventState Scene, NativeEventMemory EventMemory);

    /// <summary>Initializes absent world-save process owners without replaying a field program or changing saved progress.</summary>
    public void InitializeWorldSave(NativeFieldRuntimeProfile profile)
    {
        if (program is not null || profile is not { IsVerified: true } || services is not GameSession session || session.State.Scene != SceneKind.WorldMap || session.State.EventState is not null || session.State.DepartingFieldState is not null || session.State.WorldEventState is not null)
        {
            throw new InvalidOperationException("World save initialization requires a fresh world process without a retained event snapshot.");
        }
        if (session.State.OriginalSave is not null)
        {
            InitializeOriginalSave(profile);
            NativeRuntimeProfile = profile;
            FieldId = session.State.FieldId;
            programHash = profile.ExecutableHash;
            return;
        }

        byte[] party = NativeWorldSaveCompatibility.InitialParty(session.State);
        WorldTransition arrival = session.State.WorldArrival ?? throw new NotSupportedException("World save initialization requires its saved world arrival packet.");
        NativeEventMemory memory = EventMemory;
        byte ReadByte(int address, byte initial)
        {
            return memory.Cells.Any(pair => pair.Key <= address && pair.Key + pair.Value.Width > address) ? checked((byte)memory.Read(address, 1).RequireExact()) : initial;
        }
        byte musicVolume = ReadByte(197, 127);
        byte soundVolume = ReadByte(198, 127);
        byte battleMusic = ReadByte(203, 5);
        byte requestedMusic = ReadByte(213, 0);
        byte completedMusic = ReadByte(214, 0);
        byte drawPoint = ReadByte(242, 0);
        bool retainedSelection = memory.Cells.Any(pair => pair.Key <= 268 && pair.Key + pair.Value.Width > 268);
        NativeWorldMusicSelection selection = retainedSelection
            ? NativeWorldMusicSelection.FromRetainedPhysicalByte(ReadByte(268, 0), Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(memory))))
            : NativeWorldMusicSelection.InitialValue();

        // Resident buffers and pending I/O belong to the new process, exactly as at first initialization.
        // Move persistent scalar aliases to their typed owners before publishing that scene.
        int[] initializedBytes = [108, 109, 110, 111, 112, 113, 114, 115, 197, 198, 199, 200, 201, 202, 203, 213, 214, 242, 268];
        Dictionary<int, NativeMemoryValue> cells = [];
        foreach ((int address, NativeMemoryValue cell) in memory.Cells)
        {
            if (!initializedBytes.Any(value => value >= address && value < address + cell.Width))
            {
                cells.Add(address, cell);
                continue;
            }
            for (int offset = 0; offset < cell.Width; offset++)
            {
                int current = address + offset;
                if (!initializedBytes.Contains(current))
                {
                    cells.Add(current, new NativeMemoryValue(1, memory.Read(current, 1)));
                }
            }
        }
        session.SetEventMemory(new NativeEventMemory(NativeEventMemory.CurrentVersion, cells));
        Scene.Party.Clear();
        Scene.Party.AddRange(party.Select(identity => (int)identity));
        Scene.NativeLayerZeroScrollCurrent = new NativeLayerZeroScrollCurrent(1, 0, 0);
        Scene.NativeBackgroundScrollCurrents = Enumerable.Range(1, 7).ToDictionary(layer => layer, _ => new NativeBackgroundScrollCurrent(1, 0, 0));
        Scene.NativeDrawPointDisplayMask = 0;
        Scene.NativeFieldParticleRandomPhase = 0;
        Scene.DrawPointIndex = drawPoint;
        if (DrawPointParticleTables is not null)
        {
            Scene.NativeDrawPointParticles = NativeDrawPointParticles.Initialize(null, DrawPointParticlePhase(), DrawPointParticleTables);
        }
        InitializeColdMusicProcess(profile, WorldEventInitializationRule);
        Scene.MusicProcess = Scene.MusicProcess! with { Requested213 = requestedMusic, Complete214 = completedMusic };
        Scene.NativeBattleMusic203 = battleMusic;
        Scene.WorldMusicSelection = selection;
        SetSoundVolumeByte(197, musicVolume);
        SetSoundVolumeByte(198, soundVolume);
        Scene.WorldMusicLoad = new NativeWorldMusicLoad(1, session.State.FieldId, 0, arrival, 0, 255, 0, 0, NativeWorldMusicOperation.Stop, null, null, 127, 127, 0);
        NativeExamTimerState? timer = session.State.ExamTimer;
        ushort persistent = (ushort)((timer?.Event182Mask4 == true ? 4 : 0) | (timer?.Event183Mask1 == true ? 256 : 0));
        ushort active = (ushort)((persistent & ~4) | (timer?.SaveGlobalMask4 == true ? 4 : 0));
        Scene.NativeBattleFlagWrites = [new NativeBattleFlagWrite(1, NativeBattleFlagWriter.WorldSaveInitialization, session.State.FieldId, profile.ExecutableHash, 0, -1, -1, -1, null, persistent, active)];
        NativeRuntimeProfile = profile;
        FieldId = session.State.FieldId;
        programHash = profile.ExecutableHash;
    }

    /// <summary>Persists the shared event process when no field VM is allocated.</summary>
    public JsonElement CaptureWorldEventState()
    {
        if (program is not null || services is not GameSession session || !IsUnallocatedWorldContext(session.State) || Scene.MusicProcess is null || Scene.WorldMusicLoad is null)
        {
            throw new InvalidOperationException("An unallocated world process can only be captured without a loaded field VM.");
        }
        PreparingCheckpoint?.Invoke();
        return JsonSerializer.SerializeToElement(new WorldEventSnapshot(1, session.State.FieldId, Scene, EventMemory), CheckpointOptions);
    }

    public void RestoreWorldEventState(JsonElement snapshot, NativeFieldRuntimeProfile profile)
    {
        if (program is not null || profile is not { IsVerified: true } || services is not GameSession session || !IsUnallocatedWorldContext(session.State) || session.State.EventState is not null || session.State.DepartingFieldState is not null)
        {
            throw new InvalidOperationException("World event restoration requires a fresh world process without a field VM.");
        }
        WorldEventSnapshot saved = snapshot.Deserialize<WorldEventSnapshot>(CheckpointOptions) ?? throw new InvalidDataException("Empty world event snapshot.");
        bool matchesField = saved.FieldId == session.State.FieldId || session.State.Scene == SceneKind.Field && saved.FieldId == session.State.PreviousFieldId;
        if (saved.Version != 1 || !matchesField || saved.Scene is null || saved.EventMemory is null || saved.Scene.MusicProcess is null || saved.Scene.WorldMusicLoad is null || saved.Scene.Party is not { Count: 3 } || saved.Scene.Party.Any(identity => identity != 255 && (uint)identity > 10) || saved.Scene.ActiveParty.Distinct().Count() != saved.Scene.ActiveParty.Count())
        {
            throw new InvalidDataException("World event snapshot lost its version, field or saved party identities.");
        }
        ValidateMusicProcessScene(saved.Scene, saved.EventMemory);
        saved.Scene.WorldMusicSelection?.Validate();
        ValidateWorldMusicLoad(saved.Scene);
        ValidateWorldBattleModeState(saved.Scene, session.State.ExamTimer);
        NativeEventMemoryOwnership.Validate(session.State.EventMemory, [.. NativeEventMemoryOwnership.RootRegions(session.State), .. NativeEventMemoryOwnership.SceneRegions(SceneMemoryOwners(saved.Scene))]);
        Scene = saved.Scene;
        NativeRuntimeProfile = profile;
        FieldId = saved.FieldId;
        programHash = profile.ExecutableHash;
    }

    private static bool IsUnallocatedWorldContext(GameState state) => state.Scene == SceneKind.WorldMap || state.Scene == SceneKind.Field && state.DepartingWorldState is not null && state.FieldArrival is not null;
}
