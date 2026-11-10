using System.Buffers.Binary;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime : IWorldHost, IDisposable
{
    private readonly byte[] persistent;
    private readonly Dictionary<int, Ff7WorldEntity> entities = [];
    private readonly Dictionary<int, Ff7WorldLight> lights = [];
    private readonly int entrySelector;
    private readonly int playerModel;
    private int controlledModel = -1;
    private bool movementEnabled = true;
    private int controlLock;
    private int mainState = -15;
    private int fadeAlpha = 255;
    private int fadeRate;
    private int fadeDisplayedAlpha = 255;
    private bool faulted;
    private bool initialized;
    private long tick;
    private uint presentationCounter;
    private int viewMode;
    private int radarMode;
    private int selectedLight = -1;
    private int encounterRate;
    private Ff7WorldMarsh marsh = new();
    private Ff7WorldCameraState camera = new();
    private readonly Ff7WorldCamera cameraController;

    public static int ResolveWorldId(int selector)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(selector, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(selector, 64);
        return selector is 0x1E or 0x1F or 0x2A or 0x38 ? 2 : selector >= 0x3C ? 3 : 0;
    }

    public Ff7WorldRuntime(Ff7Installation installation, byte[] sharedPersistent, int entrySelector, IReadOnlyList<byte> party, int worldId = 0)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(sharedPersistent);
        ArgumentNullException.ThrowIfNull(party);
        if (sharedPersistent.Length != 1280 || entrySelector is < 0 or > 65535 || party.Count != 3 || party.Any(value => value != 255 && value > 8))
        {
            throw new ArgumentException("FFVII world entry requires valid persistent variables, party and entry selector.");
        }
        persistent = sharedPersistent;
        this.entrySelector = entrySelector;
        playerModel = party.Contains((byte)0) ? 0 : party.Contains((byte)2) ? 1 : party.Contains((byte)8) ? 2 : 0;
        int savedView = BinaryPrimitives.ReadUInt16LittleEndian(persistent.AsSpan(0x3F8));
        viewMode = (savedView >> 12) & 3;
        if (viewMode == 3)
        {
            viewMode = 0;
        }
        radarMode = savedView >> 14;
        Resources = Ff7WorldResources.Load(installation, worldId);
        try
        {
            InitializePlayerInput(installation);
            cameraController = new(Ff7PcExecutableData.LoadWorldCameraProfiles(installation));
            Texts = ReadTexts(Resources.ReadRequired("mes"));
            audioSongs = Ff7PcExecutableData.LoadWorldAudioSongs(installation, ResolveAudioArea(worldId, entrySelector, persistent));
            Machine = new(new WorldProgram(Resources.ReadEvents()), persistent, this);
            Machine.StartSystem(0);
        }
        catch
        {
            Resources.Dispose();
            throw;
        }
    }

    public Ff7WorldResources Resources { get; }
    public WorldMachine Machine { get; }
    public IReadOnlyList<ReadOnlyMemory<byte>> Texts { get; }
    public int ControlledModel => controlledModel;
    public bool MovementEnabled => movementEnabled;
    public Ff7WorldProcess Process
    {
        get
        {
            EnsureHealthy();
            return new(Resources.World, tick, entrySelector, Machine.Capture(), entities.Values.OrderBy(value => value.Model).ToArray(), controlledModel, movementEnabled, viewMode, radarMode, camera, lights.Values.OrderBy(value => value.Index).ToArray(), selectedLight, marsh, playerModel, encounterRate, window, dialogue, lastSelection, controlLock, mainState, fadeAlpha, fadeRate, fadeDisplayedAlpha, initialized, presentationCounter, input with { TerrainCache = input.TerrainCache?.ToArray() }, audio, sky, worldMapState);
        }
    }
    public bool CanSave => !faulted && mainState == 1 && fadeRate == 0 && fadeDisplayedAlpha == 0 && movementEnabled && controlledModel is 0 or 1 or 2 or 6 && entities.TryGetValue(controlledModel, out Ff7WorldEntity? entity) && entity.TerrainResolved && (entity.Terrain & 31) != 14 && dialogue is not { Completed: false } && (persistent[29] & 2) != 0 && (persistent[31] & 2) == 0;

    public byte ReadPersistentByte(int address)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(address);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(address, persistent.Length);
        return persistent[address];
    }

    public void Advance()
    {
        EnsureHealthy();
        try
        {
            AdvanceCore();
        }
        catch
        {
            faulted = true;
            throw;
        }
    }

    private void EnsureHealthy()
    {
        if (faulted)
        {
            throw new InvalidOperationException("FFVII world update failed during a tick; restore the last completed standalone before continuing or saving.");
        }
    }

    private void AdvanceCore()
    {
        if (!initialized)
        {
            int checksum = 0;
            for (int address = 0x380; address < 0x400; address++)
            {
                checksum += persistent[address];
            }
            if ((checksum & 255) != 0)
            {
                throw new InvalidDataException("FFVII world entry has an invalid native world-save checksum.");
            }
            if (Resources.World != 3)
            {
                persistent[31] &= 0xFC;
            }
            BeginWorldAudio();
            Machine.Tick();
            CompleteWorldAudioInitialization();
            initialized = true;
        }
        AdvancePlayerInput();
        Machine.Tick();
        ResolvePlayerTerrain();
        if (mainState < 0)
        {
            mainState++;
            if (mainState == 0)
            {
                mainState = 1;
                fadeRate = fadeAlpha > 0 ? -16 : 0;
            }
        }
        AdvanceWorldDialogue();
        AdvanceBridgeAudio();
        fadeDisplayedAlpha = fadeAlpha;
        if (fadeRate != 0)
        {
            fadeAlpha += fadeRate * (fadeAlpha + 128) >> 8;
            if (fadeAlpha <= 0 || fadeAlpha >= 255)
            {
                fadeAlpha = Math.Clamp(fadeAlpha, 0, 255);
                fadeRate = 0;
            }
        }
        camera = cameraController.Advance(camera, viewMode);
        AdvanceSky();
        tick++;
        presentationCounter = unchecked(presentationCounter + 2);
    }

    internal void UpdateEntity(Ff7WorldEntity entity)
    {
        if (!entities.ContainsKey(entity.Model))
        {
            throw new InvalidDataException("Cannot update an unallocated FFVII world model.");
        }
        entities[entity.Model] = entity;
    }

    public int ReadSpecial(int selector, int activeModel)
    {
        Ff7WorldEntity entity = activeModel == -1 ? new(-1) : RequireEntity(activeModel);
        return selector switch
        {
            0 => entity.X >> 13,
            1 => entity.Z >> 13,
            2 => entity.X & 8191,
            3 => entity.Z & 8191,
            4 => entity.Direction >> 4 & 255,
            5 => 0,
            6 => entrySelector,
            7 => viewMode | radarMode << 2,
            8 => controlledModel < 0 ? 0 : controlledModel,
            12 => dialogue is { Completed: false } ? -1 : lastSelection,
            13 => entity.Terrain >> 5 & 7,
            14 => playerModel,
            15 => entity.Model,
            17 or 18 or 19 or 20 => 0,
            _ => throw new NotSupportedException($"Unsupported FFVII world special selector {selector:X4}.")
        };
    }

    private Ff7WorldEntity RequireEntity(int model)
    {
        return entities.TryGetValue(model, out Ff7WorldEntity? entity) ? entity : throw new InvalidDataException($"FFVII world model {model} is not allocated.");
    }

    public WorldHostReply Execute(WorldHostRequest request)
    {
        int[] values = request.Command.Arguments;
        WorldOperation operation = request.Command.Operation;
        if (operation == WorldOperation.CreateModel)
        {
            int model = values[0];
            Ff7WorldEntity created = new(model);
            for (int i = 0; i < 6; i++)
            {
                uint first = BinaryPrimitives.ReadUInt32LittleEndian(persistent.AsSpan(0x3B8 + i * 8));
                uint second = BinaryPrimitives.ReadUInt32LittleEndian(persistent.AsSpan(0x3BC + i * 8));
                if ((first >> 19 & 31) == model)
                {
                    created = created with { X = (int)(first & 0x7FFFF), Y = unchecked((int)second) >> 18, Z = (int)(second & 0x3FFFF), Direction = (int)(first >> 20 & 0xFF0), TargetDirection = (int)(first >> 20 & 0xFF0) };
                    break;
                }
            }
            if (!entities.TryAdd(model, created))
            {
                throw new InvalidDataException("FFVII world model allocation was duplicated.");
            }
            return new(true);
        }
        if (ExecuteLight(operation, values))
        {
            return new(true);
        }
        if (ExecuteWorldAudio(operation, values))
        {
            return new(true);
        }
        if (operation == WorldOperation.ResetMarsh)
        {
            if (!marsh.Initialized)
            {
                int z = controlledModel < 0 ? 0 : RequireEntity(controlledModel).Z;
                marsh = new(true, z < 0x23A98 ? 0x2008 : 0x1D4C, z < 0x23A98 ? 0x6338 : 0x11F8);
            }
            return new(true);
        }
        if (operation is WorldOperation.FadeIn or WorldOperation.FadeOut)
        {
            fadeRate = operation == WorldOperation.FadeIn ? fadeAlpha > 0 ? -values[0] : 0 : fadeAlpha < 255 ? values[0] : 0;
            return new(true);
        }
        WorldHostReply? dialogueReply = ExecuteDialogue(request);
        if (dialogueReply is not null)
        {
            return dialogueReply;
        }
        if (operation == WorldOperation.WorldState)
        {
            worldMapState = values[0];
            return new(true);
        }
        Ff7WorldEntity entity = RequireEntity(request.ActiveModel);
        switch (operation)
        {
            case WorldOperation.SetPlayer:
                controlledModel = entity.Model;
                SynchronizeFreeDirection(entity);
                break;
            case WorldOperation.EnableControl:
                if (values[0] == 0)
                {
                    movementEnabled = false;
                    controlLock = 3;
                }
                else if (controlLock is 3 or 6)
                {
                    movementEnabled = true;
                }
                break;
            case WorldOperation.ChunkPosition:
                entity = entity with { X = (entity.X & 8191) | values[0] << 13, Z = (entity.Z & 8191) | values[1] << 13, Flags = entity.Flags | 1 };
                break;
            case WorldOperation.LocalPosition:
                entity = entity with { X = (entity.X & ~8191) | (values[0] & 8191), Z = (entity.Z & ~8191) | (values[1] & 8191), Flags = entity.Flags | 1 };
                break;
            case WorldOperation.Direction:
                entity = entity with { Direction = values[0] << 4 & 4095, TargetDirection = values[0] << 4 & 4095 };
                if (entity.Model == controlledModel)
                {
                    SynchronizeFreeDirection(entity);
                }
                break;
            case WorldOperation.TargetDirection:
                entity = entity with { TargetDirection = values[0] << 4 & 4095 };
                break;
            case WorldOperation.Speed:
            case WorldOperation.FastSpeed:
                entity = entity with { Speed = unchecked((byte)values[0]), Flags = operation == WorldOperation.FastSpeed ? entity.Flags | 64 : entity.Flags & ~64 };
                break;
            case WorldOperation.Move:
                if (entity.Speed != 0 || entity.VerticalSpeed != 0 || entity.VerticalVelocity != 0)
                {
                    throw new NotSupportedException("FFVII world scripted movement requires the native terrain movement contract.");
                }
                break;
            case WorldOperation.Elevation:
                entity = entity with { Elevation = unchecked((short)values[0]) };
                break;
            case WorldOperation.VerticalSpeed:
                entity = entity with { VerticalSpeed = unchecked((sbyte)values[0]) };
                break;
            case WorldOperation.VerticalVelocity:
                entity = entity with { VerticalVelocity = unchecked((sbyte)values[0]), Flags = entity.Flags | 128 };
                break;
            case WorldOperation.Height:
                entity = entity with { Y = values[0], Flags = entity.Flags | 128 };
                break;
            case WorldOperation.Animation:
                entity = entity with { Animation = unchecked((sbyte)values[0]), AnimationFrame = 0, Flags = values[1] != 0 ? entity.Flags & ~32 : entity.Flags | 32 };
                break;
            case WorldOperation.Hide:
                entity = entity with { Flags = entity.Flags | 8 };
                break;
            case WorldOperation.Unhide:
                entity = entity with { Flags = entity.Flags & ~8 };
                break;
            case WorldOperation.ViewMode:
                SetWorldViewMode(values[0] & 3);
                radarMode = values[0] >> 2 & 3;
                break;
            case WorldOperation.EncounterRate:
                encounterRate = values[0];
                break;
            default:
                throw new NotSupportedException($"Unsupported FFVII world host operation {operation} at word {request.ProgramCounter:X4}.");
        }
        entities[entity.Model] = entity;
        return new(true);
    }

    private bool ExecuteLight(WorldOperation operation, int[] values)
    {
        if (operation == WorldOperation.SelectLight)
        {
            if ((uint)values[0] < 16)
            {
                selectedLight = values[0];
                Ff7WorldLight selected = lights.GetValueOrDefault(selectedLight) ?? new(selectedLight);
                lights[selectedLight] = values[1] == -1 ? selected : selected with { Enabled = unchecked((byte)values[1]) };
            }
            return true;
        }
        if (operation is < WorldOperation.LightChunkPosition or > WorldOperation.LightColor2)
        {
            return false;
        }
        if (selectedLight < 0)
        {
            return true;
        }
        Ff7WorldLight light = lights[selectedLight];
        lights[selectedLight] = operation switch
        {
            WorldOperation.LightChunkPosition when values[0] is >= 0 and < 36 && values[1] is >= 0 and < 28 => light with { X = light.X & 8191 | values[0] << 13, Z = light.Z & 8191 | values[1] << 13 },
            WorldOperation.LightLocalPosition => light with { X = light.X & ~8191 | values[0] & 8191, Z = light.Z & ~8191 | values[1] & 8191 },
            WorldOperation.AmbientColor => light with { AmbientRed = unchecked((byte)values[0]), AmbientGreen = unchecked((byte)values[1]), AmbientBlue = unchecked((byte)values[2]) },
            WorldOperation.LightRadii => light with { Radius = values[0], Radius2 = values[1] },
            WorldOperation.LightColor => light with { Red = unchecked((byte)values[0]), Green = unchecked((byte)values[1]), Blue = unchecked((byte)values[2]) },
            WorldOperation.LightColor2 => light with { Red2 = unchecked((byte)values[0]), Green2 = unchecked((byte)values[1]), Blue2 = unchecked((byte)values[2]) },
            _ => light
        };
        return true;
    }

    public void Restore(Ff7WorldProcess state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.WorldId != Resources.World || state.EntrySelector != entrySelector || state.PlayerModel != playerModel || state.Entities is null || state.Entities.Any(value => value is null || value.Model is < 0 or > 255) || state.Entities.Select(value => value.Model).Distinct().Count() != state.Entities.Length || state.ControlledModel != -1 && !state.Entities.Any(value => value.Model == state.ControlledModel) || state.Lights is null || state.Lights.Any(value => value is null || value.Index is < 0 or > 15) || state.Lights.Select(value => value.Index).Distinct().Count() != state.Lights.Length || state.SelectedLight != -1 && !state.Lights.Any(value => value.Index == state.SelectedLight) || state.Marsh is null || state.Vm is null || state.Vm.Models is null || state.Vm.Models.Any(value => value is null) || state.Input is { Horizontal: < -1 or > 1 } or { Vertical: < -1 or > 1 } || state.Tick < 0 || state.Vm.Tick != state.Tick + (state.Initialized ? 1 : 0) || state.ControlLock is < 0 or > 6 || state.MainState is < -15 or > 1 || state.ViewMode is < 0 or > 3 || state.RadarMode is < 0 or > 3 || state.FadeAlpha is < 0 or > 255 || state.FadeDisplayedAlpha is < 0 or > 255)
        {
            throw new InvalidDataException("FFVII world process snapshot is corrupt or incompatible with its resources.");
        }
        if (!state.Vm.Models.Select(value => value.Model).Order().SequenceEqual(state.Entities.Select(value => value.Model).Order()))
        {
            throw new InvalidDataException("FFVII world model and script snapshot identities differ.");
        }
        ValidateDialogue(state);
        if (state.Audio is { } savedAudio && (savedAudio.Publications < 0 || savedAudio.Song is < 0 or > 8 || savedAudio.PreviousSong is < 0 or > 8 || savedAudio.LastPublishedTrack is < -1 or > 98 || savedAudio.SoundPriority is < 0 or > 751 || savedAudio.Initialized != state.Initialized))
        {
            throw new InvalidDataException("FFVII world audio snapshot has invalid native song or publication state.");
        }
        ValidatePlayerInput(state.Input, state.WorldMapState);
        Machine.Restore(state.Vm);
        entities.Clear();
        foreach (Ff7WorldEntity entity in state.Entities)
        {
            entities.Add(entity.Model, entity);
        }
        lights.Clear();
        foreach (Ff7WorldLight light in state.Lights)
        {
            lights.Add(light.Index, light);
        }
        controlledModel = state.ControlledModel;
        movementEnabled = state.MovementEnabled;
        viewMode = state.ViewMode;
        radarMode = state.RadarMode;
        camera = state.Camera ?? new();
        marsh = state.Marsh;
        selectedLight = state.SelectedLight;
        encounterRate = state.EncounterRate;
        controlLock = state.ControlLock;
        mainState = state.MainState;
        fadeAlpha = state.FadeAlpha;
        fadeRate = state.FadeRate;
        fadeDisplayedAlpha = state.FadeDisplayedAlpha;
        initialized = state.Initialized;
        tick = state.Tick;
        presentationCounter = state.PresentationCounter;
        input = state.Input is null ? new() : state.Input with { TerrainCache = state.Input.TerrainCache?.ToArray() };
        input = input with { Bridge = input.Bridge ?? initialBridge };
        HeldConfirm = (input.Held & 32) != 0;
        audio = state.Audio ?? new(Initialized: initialized, Song: initialized ? 1 : 0, PlaybackEnabled: initialized);
        sky = state.Sky ?? new();
        worldMapState = state.WorldMapState;
        RestoreDialogue(state);
        faulted = false;
    }

    public void Dispose()
    {
        Resources.Dispose();
    }
}
