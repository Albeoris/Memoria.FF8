using System.Buffers.Binary;
using System.Numerics;
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
    private int entryModule;
    private int playerModel;
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
    private int encounterRate = 1;
    private Ff7WorldMarsh marsh = new();
    private Ff7WorldCameraState camera = new();
    private readonly Ff7WorldCamera cameraController;
    private bool meteorVisible;

    public static int ResolveWorldId(int selector)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(selector, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(selector, 64);
        return ResolveWorldEntryMap(selector);
    }

    internal static int ResolveWorldEntryMap(int selector)
    {
        return selector is 0x1E or 0x1F or 0x2A or 0x38 ? 2 : selector >= 0x3C ? 3 : 0;
    }

    public Ff7WorldRuntime(Ff7Installation installation, byte[] sharedPersistent, int entrySelector, IReadOnlyList<byte> party, int worldId = 0, int entryModule = 0)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(sharedPersistent);
        ArgumentNullException.ThrowIfNull(party);
        if (sharedPersistent.Length != 1280 || entrySelector is < 0 or > 65535 || entryModule is < 0 or > 2 || party.Count != 3 || party.Any(value => value != 255 && value > 8))
        {
            throw new ArgumentException("FFVII world entry requires valid persistent variables, party and entry selector.");
        }
        persistent = sharedPersistent;
        RestorePreviousWorldFormation();
        this.entrySelector = entrySelector;
        this.entryModule = entryModule;
        playerModel = party.Contains((byte)0) ? 0 : party.Contains((byte)2) ? 1 : party.Contains((byte)8) ? 2 : 0;
        int savedView = BinaryPrimitives.ReadUInt16LittleEndian(persistent.AsSpan(0x3F8));
        viewMode = (savedView >> 12) & 3;
        if (viewMode == 3)
        {
            viewMode = 0;
        }
        if (worldId == 2)
        {
            viewMode = 2;
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
    public int PresentationArea => ResolveAudioArea(Resources.World, entrySelector, persistent);
    public bool MovementEnabled => movementEnabled;
    public long PresentationTick => tick;
    public long PresentationRestoreEpoch { get; private set; }
    public long PresentationCameraEpoch { get; private set; }
    public Ff7WorldProcess Process
    {
        get
        {
            EnsureHealthy();
            return new(Resources.World, tick, entrySelector, Machine.Capture(), entities.Values.Select(entity => entity with { TerrainCache = entity.TerrainCache?.ToArray() }).OrderBy(value => value.Model).ToArray(), controlledModel, movementEnabled, viewMode, radarMode, camera, lights.Values.OrderBy(value => value.Index).ToArray(), selectedLight, CopyMarshState(marsh), playerModel, encounterRate, window, dialogue, lastSelection, controlLock, mainState, fadeAlpha, fadeRate, fadeDisplayedAlpha, initialized, presentationCounter, input with { TerrainCache = input.TerrainCache?.ToArray() }, audio, sky, worldMapState)
            {
                FieldDeparture = fieldDeparture,
                BattleState = CaptureWorldBattleState(),
                Chocobo = CopyChocoboState(chocobo),
                ChocoboPathControl = CopyChocoboPathControl(chocoboPathControl),
                Vehicle = CopyVehicleState(vehicle),
                ChocoboTint = chocoboTint,
                Snow = CopySnowState(snow),
                PartyChange = partyChange,
                FreeEntities = freeEntities.Count == 0 ? null : freeEntities.Select(entity => entity with { TerrainCache = entity.TerrainCache?.ToArray() }).ToArray(),
                EntryModule = entryModule,
                SubmarineTint = submarineTint,
                ApproachBubble = approachBubble,
                WhiteFadeCallbacks = whiteFadeCallbacks,
                WhiteFadeDisplayed = whiteFadeDisplayed,
                MapTransfer = mapTransfer,
                SubmarineMapEntry = submarineMapEntry,
                ScriptCamera = scriptCamera,
                StoredPositions = storedPositions?.ToArray(),
                MeteorVisible = meteorVisible,
                Effects = CopyWorldEffects(effects),
                CameraOcclusion = cameraOcclusion,
                ScreenShake = screenShake is { Enabled: false, X: 0, Y: 0 } ? null : screenShake
            };
        }
    }
    public bool CanSave => !faulted && pendingWorldBattle is null && mainState == 1 && fadeRate == 0 && fadeDisplayedAlpha == 0 && movementEnabled && controlledModel is 0 or 1 or 2 or 6 && entities.TryGetValue(controlledModel, out Ff7WorldEntity? entity) && entity.TerrainResolved && (entity.Terrain & 31) != 14 && dialogue is not { Completed: false } && (persistent[29] & 2) != 0 && (persistent[31] & 2) == 0;

    public byte ReadPersistentByte(int address)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(address);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(address, persistent.Length);
        return persistent[address];
    }

    public void Advance()
    {
        EnsureHealthy();
        if (ReadyFieldDeparture is not null || ReadyMapTransfer is not null || pendingWorldBattle is not null)
        {
            return;
        }
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
        _ = WorldRandom;
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
            persistent[0x5F] = 0;
            BeginWorldAudio();
            Machine.Tick();
            AdvanceWorldScriptFacing();
            InitializeUnderwaterVehicle();
            CompleteWorldAudioInitialization();
            initialized = true;
        }
        AdvanceWorldScreenShake();
        AdvancePlayerInput();
        AdvanceScriptCamera();
        Machine.Tick();
        AdvanceWorldScriptFacing();
        ResolvePlayerTerrain();
        AdvanceEmeraldPresentation();
        AdvanceChocoboTransport();
        AdvanceChocoboPathControl();
        AdvanceSurfaceVehicleTransport();
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
        whiteFadeDisplayed = fadeAlpha != 0 && whiteFadeCallbacks > 0;
        if (whiteFadeDisplayed)
        {
            whiteFadeCallbacks--;
            fadeDisplayedAlpha = fadeAlpha;
        }
        if (!AdvanceHighwindFlightCamera())
        {
            camera = cameraController.Advance(camera, viewMode, targetPitch: ScriptedOrTerrainPitch, targetDistance: scriptCamera?.TargetDistance is > 0 ? scriptCamera.TargetDistance : null);
        }
        AdvanceSky();
        AdvanceWorldCameraOcclusion();
        AdvanceWorldMarsh();
        AdvanceWorldEffects();
        tick++;
        presentationCounter = unchecked(presentationCounter + 2);
        AdvanceSnowWindLayers();
        AdvanceRandomWorldBattle();
        if (pendingWorldBattle is { } encounter)
        {
            CompleteSpecialBattleDeparture(encounter);
        }
        if (pendingWorldBattle is null)
        {
            AdvanceMapTransfer();
            AdvanceFieldDeparture();
        }
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
        return ReadSpecial(selector, activeModel, null);
    }

    public int ReadSpecial(int selector, int activeModel, int? freeModelIndex)
    {
        Ff7WorldEntity entity = activeModel == -1 ? new(-1) : ResolveHostEntity(activeModel, freeModelIndex);
        return selector switch
        {
            0 => entity.X >> 13,
            1 => entity.Z >> 13,
            2 => entity.X & 8191,
            3 => entity.Z & 8191,
            4 => entity.TargetDirection >> 4 & 255,
            5 => entryModule,
            6 => entrySelector,
            7 => viewMode | radarMode << 2,
            8 => controlledModel < 0 ? 0 : controlledModel,
            9 => persistent[0x3FC],
            10 => escapedWorldBattle ? 1 : 0,
            11 => marsh.InMarsh ? 1 : 0,
            12 => dialogue is { Completed: false } ? -1 : lastSelection,
            13 => entity.Terrain >> 5 & 7,
            14 => playerModel,
            15 => entity.Model,
            16 => WorldRandom.Next(),
            17 => partyChange?.Joined0 ?? 0,
            18 => partyChange?.Joined1 ?? 0,
            19 => partyChange?.Left0 ?? 0,
            20 => partyChange?.Left1 ?? 0,
            _ => throw new NotSupportedException($"Unsupported FFVII world special selector {selector:X4}.")
        };
    }

    private Ff7WorldEntity RequireEntity(int model)
    {
        return entities.TryGetValue(model, out Ff7WorldEntity? entity) ? entity : throw new InvalidDataException($"FFVII world model {model} is not allocated.");
    }

    public int ReadModelDistance(int activeModel, int targetModel)
    {
        return ReadModelDistance(activeModel, targetModel, null);
    }

    public int ReadModelDistance(int activeModel, int targetModel, int? freeModelIndex)
    {
        if (!Machine.IsModelAttached(targetModel))
        {
            return 0;
        }
        Ff7WorldEntity active = ResolveHostEntity(activeModel, freeModelIndex);
        Ff7WorldEntity target = RequireEntity(targetModel);
        return WorldDistance(active, target.X, target.Y, target.Z) >> 4;
    }

    private static int WorldDistance(Ff7WorldEntity entity, int x, int y, int z)
    {
        int dx = Math.Abs(WrappedDifference(entity.X - x, 0x48000));
        int dz = Math.Abs(WrappedDifference(entity.Z - z, 0x38000));
        return dx + Math.Abs(entity.Y - y) + dz;
    }

    private Ff7WorldLight? SelectLight(int index, int enabled)
    {
        if ((uint)index < 16)
        {
            selectedLight = index;
            Ff7WorldLight light = lights.GetValueOrDefault(index) ?? new(index);
            lights[index] = enabled == -1 ? light : light with { Enabled = unchecked((byte)enabled) };
        }
        return selectedLight < 0 ? null : lights[selectedLight];
    }

    public int ReadZoneDistance(int activeModel, int zone)
    {
        return ReadZoneDistance(activeModel, zone, null);
    }

    public int ReadZoneDistance(int activeModel, int zone, int? freeModelIndex)
    {
        return SelectLight(zone, -1) is { } light ? WorldDistance(ResolveHostEntity(activeModel, freeModelIndex), light.X, 0, light.Z) >> 5 : 0;
    }

    public int ReadZoneDirection(int activeModel, int zone)
    {
        return ReadZoneDirection(activeModel, zone, null);
    }

    public int ReadZoneDirection(int activeModel, int zone, int? freeModelIndex)
    {
        if (SelectLight(zone, -1) is not { } light)
        {
            return 0;
        }
        Ff7WorldEntity entity = ResolveHostEntity(activeModel, freeModelIndex);
        return inputMath.Direction(light.X - entity.X, light.Z - entity.Z);
    }

    public WorldHostReply Execute(WorldHostRequest request)
    {
        int[] values = request.Command.Arguments;
        WorldOperation operation = request.Command.Operation;
        WorldHostReply? pathReply = ExecuteChocoboPathControl(request);
        if (pathReply is not null)
        {
            return pathReply;
        }
        if (ExecuteWorldEffect(request))
        {
            return new(true);
        }
        if (ExecuteStoredPosition(request))
        {
            return new(true);
        }
        if (ExecuteScriptCamera(operation, values))
        {
            return new(true);
        }
        if (operation == WorldOperation.Surface)
        {
            BeginSubmarineMapTransfer(0);
            return new(true);
        }
        if (operation == WorldOperation.Meteor)
        {
            meteorVisible = values[0] != 0;
            return new(true);
        }
        if (operation is WorldOperation.FieldTransition or WorldOperation.FieldTransitionSelector)
        {
            return RequestFieldDeparture(request);
        }
        if (operation == WorldOperation.CreateModel)
        {
            int model = values[0];
            Ff7WorldEntity created = CreateWorldEntity(model);
            if (!entities.TryAdd(model, created))
            {
                throw new InvalidDataException("FFVII world model allocation was duplicated.");
            }
            return new(true);
        }
        if (ExecuteVehicleOperation(request))
        {
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
        if (operation == WorldOperation.Earthquake)
        {
            screenShake = screenShake with { Enabled = values[0] != 0 };
            return new(true);
        }
        if (operation == WorldOperation.ResetMarsh)
        {
            ResetWorldMarsh();
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
        if (operation == WorldOperation.Battle)
        {
            RequestWorldBattle(values[0], false, true, request.Id, request.ProgramCounter);
            return new(true);
        }
        if (operation == WorldOperation.Countdown)
        {
            SetEncounterCountdown?.Invoke(unchecked((uint)values[0]));
            persistent[0x5F] = 1;
            countdownActive = true;
            return new(true);
        }
        if (operation == WorldOperation.EncounterRate)
        {
            encounterRate = values[0];
            return new(true);
        }
        if (operation == WorldOperation.ViewMode)
        {
            SetWorldViewMode(values[0] & 3);
            radarMode = values[0] >> 2 & 3;
            return new(true);
        }
        if (operation == WorldOperation.SubmarineColor)
        {
            if (values[0] is not (0 or 1))
            {
                throw new NotSupportedException($"Unsupported FFVII submarine tint {values[0]} at word {request.ProgramCounter:X4}.");
            }
            submarineTint = values[0] + 5;
            return new(true);
        }
        if (operation == WorldOperation.ChocoboColor)
        {
            if (values[0] is < 0 or > 5)
            {
                throw new InvalidDataException("FFVII world Chocobo tint is outside the native terrain and palette tables.");
            }
            chocoboTint = values[0];
            return new(true);
        }
        if (operation == WorldOperation.EnableControl)
        {
            if (values[0] == 0)
            {
                movementEnabled = false;
                controlLock = 3;
            }
            else if (controlLock is 3 or 6)
            {
                movementEnabled = true;
            }
            return new(true);
        }
        if (operation == WorldOperation.BoardTransport)
        {
            BoardWorldTransport(request.ActiveModel, request.ProgramCounter);
            return new(true);
        }
        if (operation == WorldOperation.ReleaseChocobo)
        {
            BeginChocoboRelease(request.ActiveModel, request.ProgramCounter);
            return new(true);
        }
        if (operation == WorldOperation.PlaceDismountedRider)
        {
            PlaceDismountedRider(request);
            return new(true);
        }
        Ff7WorldEntity entity = ResolveHostEntity(request);
        switch (operation)
        {
            case WorldOperation.SetPlayer:
                if (IsWorldChocobo(entity.Model) && entity.AttachedModel is int attachedRider)
                {
                    chocobo = new(attachedRider) { Model = entity.Model };
                }
                if (IsWorldChocobo(entity.Model) && chocobo?.RiderModel is null && entity.AttachedModel is null || chocobo?.RiderModel is not null && entity.Model != chocobo.Model)
                {
                    throw new NotSupportedException($"FFVII world model {entity.Model} cannot change the attached rider through SetPlayer at word {request.ProgramCounter:X4}.");
                }
                if (controlledModel != entity.Model)
                {
                    PresentationCameraEpoch++;
                }
                SelectControlledWorldModel(entity.Model);
                SynchronizeFreeDirection(entity);
                break;
            case WorldOperation.ChunkPosition:
                entity = entity with { X = (entity.X & 8191) | values[0] << 13, Z = (entity.Z & 8191) | values[1] << 13, Flags = entity.Flags | 1, PresentationEpoch = entity.PresentationEpoch + 1 };
                break;
            case WorldOperation.LocalPosition:
                entity = entity with { X = (entity.X & ~8191) | (values[0] & 8191), Z = (entity.Z & ~8191) | (values[1] & 8191), Flags = entity.Flags | 1, PresentationEpoch = entity.PresentationEpoch + 1 };
                break;
            case WorldOperation.Direction:
                entity = entity with { TargetDirection = values[0] << 4 & 4095, ScriptDirection = null };
                if (entity.Model == controlledModel)
                {
                    SynchronizeFreeDirection(entity);
                }
                break;
            case WorldOperation.TargetDirection:
                int movementDirection = values[0] << 4 & 4095;
                entity = entity with { ScriptDirection = movementDirection };
                break;
            case WorldOperation.FaceZone:
                int zoneDirection = ReadZoneDirection(entity.Model, values[0]) & 4095;
                entity = entity with { TargetDirection = zoneDirection, ScriptDirection = null, TurnOffset = 0 };
                break;
            case WorldOperation.Speed:
            case WorldOperation.FastSpeed:
                entity = entity with { Speed = unchecked((byte)values[0]), Flags = operation == WorldOperation.FastSpeed ? entity.Flags | 64 : entity.Flags & ~64 };
                break;
            case WorldOperation.Move:
                entity = MoveWorldScript(entity);
                break;
            case WorldOperation.RestoreModelPosition:
                entity = entity with { X = entity.PreviousX, Y = (entity.Flags & 4) != 0 ? entity.Y : entity.PreviousY ?? entity.Y, Z = entity.PreviousZ, Flags = entity.Flags & ~1 };
                break;
            case WorldOperation.Effect when values[0] == 6 && Resources.World == 2:
                approachBubble = new(entity.PreviousX, entity.PreviousY ?? entity.Y, entity.PreviousZ);
                break;
            case WorldOperation.FaceModel:
                if (entities.TryGetValue(values[0], out Ff7WorldEntity? target) && target.Model != chocobo?.RiderModel)
                {
                    entity = entity with { ScriptDirection = entity.ScriptDirection ?? entity.TargetDirection, TargetDirection = (inputMath.Direction(target.X - entity.X, target.Z - entity.Z) + (values[1] << 4)) & 4095 };
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
                entity = entity with { Y = values[0], Flags = entity.Flags | 128, PresentationEpoch = entity.PresentationEpoch + 1 };
                break;
            case WorldOperation.Animation:
                entity = entity with { Animation = unchecked((sbyte)values[0]), AnimationFrame = 0, Flags = values[1] != 0 ? entity.Flags & ~32 : entity.Flags | 32, PresentationAnimationEpoch = entity.PresentationAnimationEpoch + 1 };
                break;
            case WorldOperation.Hide:
                entity = entity with { Flags = entity.Flags | 8, PresentationEpoch = entity.PresentationEpoch + 1 };
                break;
            case WorldOperation.Unhide:
                entity = entity with { Flags = entity.Flags & ~8, PresentationEpoch = entity.PresentationEpoch + 1 };
                break;
            default:
                throw new NotSupportedException($"Unsupported FFVII world host operation {operation} at word {request.ProgramCounter:X4}.");
        }
        PublishHostEntity(request, entity);
        return new(true);
    }

    private Ff7WorldEntity MoveWorldScript(Ff7WorldEntity entity)
    {
        if (entity.Speed == 0 && entity.VerticalSpeed == 0 && entity.VerticalVelocity == 0)
        {
            return entity;
        }
        int speed = entity.Speed << ((entity.Flags & 64) != 0 ? 4 : 0);
        Vector3 step = inputMath.TransformRotation(new(0, 0, speed), 0, entity.ScriptDirection ?? entity.TargetDirection, 0);
        entity = OffsetWorldEntity(entity, (int)step.X, (int)step.Z);
        return entity with { Elevation = unchecked((short)(entity.Elevation - entity.VerticalSpeed)), Y = entity.Y + entity.VerticalVelocity };
    }

    private bool ExecuteLight(WorldOperation operation, int[] values)
    {
        if (operation == WorldOperation.SelectLight)
        {
            SelectLight(values[0], values[1]);
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
        if (state.WorldId != Resources.World || state.EntryModule is < 0 or > 2 || state.EntrySelector != entrySelector || state.PlayerModel is < 0 or > 2 || state.Entities is null || state.Entities.Any(value => value is null || value.Model is < 0 or > 255) || state.Entities.Select(value => value.Model).Distinct().Count() != state.Entities.Length || state.ControlledModel != -1 && !state.Entities.Any(value => value.Model == state.ControlledModel) || state.Lights is null || state.Lights.Any(value => value is null || value.Index is < 0 or > 15) || state.Lights.Select(value => value.Index).Distinct().Count() != state.Lights.Length || state.SelectedLight != -1 && !state.Lights.Any(value => value.Index == state.SelectedLight) || state.Marsh is null || state.Vm is null || state.Vm.Models is null || state.Vm.Models.Any(value => value is null) || state.Input is { Horizontal: < -1 or > 1 } or { Vertical: < -1 or > 1 } || state.Tick < 0 || state.Vm.Tick != state.Tick + (state.Initialized ? 1 : 0) || state.ControlLock is < 0 or > 6 || !IsValidFieldDeparture(state) || state.ViewMode is < 0 or > 3 || state.RadarMode is < 0 or > 3 || state.FadeAlpha is < 0 or > 255 || state.FadeDisplayedAlpha is < 0 or > 255)
        {
            throw new InvalidDataException("FFVII world process snapshot is corrupt or incompatible with its resources.");
        }
        if (state.FieldDeparture is { } departure && (!Machine.OwnsHostOperation(departure.ProgramCounter, departure.Operation) || departure.ExecutingModel >= 0 && !state.Vm.Models.Any(model => model.Model == departure.ExecutingModel)))
        {
            throw new InvalidDataException("FFVII world departure has no matching original script owner.");
        }
        if (!state.Vm.Models.Select(value => value.Model).Order().SequenceEqual(state.Entities.Select(value => value.Model).Order()))
        {
            throw new InvalidDataException("FFVII world model and script snapshot identities differ.");
        }
        ValidateDialogue(state);
        ValidateMapTransfer(state);
        ValidateWorldEffects(state.Effects);
        ValidateCameraOcclusion(state.CameraOcclusion);
        ValidateMarshState(state.Marsh);
        if (state.StoredPositions is not null && (state.StoredPositions.Length != 3 || state.StoredPositions.Any(point => point is null)))
        {
            throw new InvalidDataException("FFVII world stored-position bank requires three native vector owners.");
        }
        if (state.Audio is { } savedAudio && (savedAudio.Publications < 0 || savedAudio.Song is < 0 or > 8 || savedAudio.PreviousSong is < 0 or > 8 || savedAudio.LastPublishedTrack is < -1 or > 98 || savedAudio.SoundPriority is < 0 or > 751 || savedAudio.Initialized != state.Initialized))
        {
            throw new InvalidDataException("FFVII world audio snapshot has invalid native song or publication state.");
        }
        ValidatePlayerInput(state.Input, state.WorldMapState);
        foreach (Ff7WorldEntity entity in state.Entities.Where(entity => entity.TerrainCache is not null))
        {
            ValidatePlayerInput(new(TerrainCache: entity.TerrainCache), state.WorldMapState);
        }
        if (state.ScreenShake is { } shake && (shake.X is not (-4 or -2 or 0 or 2) || shake.Y is not (-4 or -2 or 0 or 2)))
        {
            throw new InvalidDataException("FFVII world screen shake has invalid projection offsets.");
        }
        if (state.SubmarineTint is not (null or 5 or 6))
        {
            throw new InvalidDataException("FFVII world submarine tint is outside the native palette.");
        }
        if (state.ApproachBubble is { } bubble && (state.WorldId != 2 || bubble.X is < 0 or >= 0x48000 || bubble.Z is < 0 or >= 0x38000 || bubble.Age is < 0 or >= ApproachBubbleLifetime) || state.WhiteFadeCallbacks is < 0 or > 2)
        {
            throw new InvalidDataException("FFVII world approach presentation has invalid effect state.");
        }
        ValidateChocoboState(state);
        ValidateChocoboRelease(state.ChocoboPathControl?.Path, state.WorldMapState);
        if (state.ChocoboTint is < 0 or > 5)
        {
            throw new InvalidDataException("FFVII world snapshot has an invalid Chocobo tint.");
        }
        ValidateVehicleState(state);
        ValidateSnowState(state.Snow, state.WorldId);
        ValidateFreeWorldEntities(state);
        if (state.PartyChange is { } changed && new[] { changed.Joined0, changed.Joined1, changed.Left0, changed.Left1 }.Any(model => model != 0 && model is < 32 or > 63))
        {
            throw new InvalidDataException("FFVII world party-change model slots are corrupt.");
        }
        ValidateWorldBattleState(state.BattleState);
        Machine.Restore(state.Vm);
        entities.Clear();
        freeEntities.Clear();
        freeEntities.AddRange((state.FreeEntities ?? (state.Vm.FreeModels ?? []).Select(model => new Ff7WorldEntity(model.Model)).ToArray()).Select(entity => entity with { TerrainCache = entity.TerrainCache?.ToArray() }));
        foreach (Ff7WorldEntity entity in state.Entities)
        {
            entities.Add(entity.Model, entity with { TerrainCache = entity.TerrainCache?.ToArray() });
        }
        lights.Clear();
        foreach (Ff7WorldLight light in state.Lights)
        {
            lights.Add(light.Index, light);
        }
        controlledModel = state.ControlledModel;
        playerModel = state.PlayerModel;
        chocobo = CopyChocoboState(state.Chocobo);
        chocoboPathControl = CopyChocoboPathControl(state.ChocoboPathControl);
        chocoboTint = state.ChocoboTint;
        vehicle = CopyVehicleState(state.Vehicle);
        snow = CopySnowState(state.Snow);
        partyChange = state.PartyChange;
        entryModule = state.EntryModule;
        movementEnabled = state.MovementEnabled;
        viewMode = state.ViewMode;
        radarMode = state.RadarMode;
        camera = state.Camera ?? new();
        screenShake = state.ScreenShake ?? new();
        submarineTint = state.SubmarineTint;
        approachBubble = state.ApproachBubble;
        whiteFadeCallbacks = state.WhiteFadeCallbacks;
        whiteFadeDisplayed = state.WhiteFadeDisplayed;
        marsh = CopyMarshState(state.Marsh);
        selectedLight = state.SelectedLight;
        encounterRate = state.EncounterRate;
        controlLock = state.ControlLock;
        mainState = state.MainState;
        fadeAlpha = state.FadeAlpha;
        fadeRate = state.FadeRate;
        fadeDisplayedAlpha = state.FadeDisplayedAlpha;
        initialized = state.Initialized;
        RestoreWorldBattleState(state.BattleState);
        tick = state.Tick;
        presentationCounter = state.PresentationCounter;
        input = state.Input is null ? new() : state.Input with { TerrainCache = state.Input.TerrainCache?.ToArray() };
        input = input with { Bridge = input.Bridge ?? initialBridge };
        HeldConfirm = (input.Held & 32) != 0;
        audio = state.Audio ?? new(Initialized: initialized, Song: initialized ? 1 : 0, PlaybackEnabled: initialized);
        sky = state.Sky ?? new();
        worldMapState = state.WorldMapState;
        fieldDeparture = state.FieldDeparture;
        mapTransfer = state.MapTransfer;
        submarineMapEntry = state.SubmarineMapEntry;
        scriptCamera = state.ScriptCamera;
        storedPositions = state.StoredPositions?.ToArray();
        meteorVisible = state.MeteorVisible;
        RestoreWorldEffects(state.Effects);
        cameraOcclusion = state.CameraOcclusion ?? new();
        RestoreDialogue(state);
        faulted = false;
        PresentationRestoreEpoch++;
    }

    public void Dispose()
    {
        Resources.Dispose();
    }
}
