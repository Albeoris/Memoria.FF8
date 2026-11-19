using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session : IDisposable
{
    private readonly Ff7Installation installation;
    private readonly Ff7PcExecutableData executable;
    private readonly byte[] materiaTypes;
    private readonly LgpArchive fields;
    private readonly LgpArchive characters;
    private readonly IReadOnlyList<string> mapList;
    private Ff7FieldModelResources[] models = [];

    private Ff7Session(Ff7Installation installation)
    {
        this.installation = installation;
        executable = Ff7PcExecutableData.Load(installation);
        materiaTypes = Ff7MateriaResources.LoadTypes(installation);
        fields = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        try
        {
            characters = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.CharacterArchive).Path);
            IReadOnlyList<LgpEntry> entries = fields.Find("maplist");
            if (entries.Count != 1)
            {
                throw new InvalidDataException("FFVII requires its original unique maplist.");
            }
            mapList = PcFieldMapList.Read(fields.ReadEntry(entries[0]));
        }
        catch
        {
            characters?.Dispose();
            fields.Dispose();
            throw;
        }
    }

    private Ff7Session(Ff7Session source)
    {
        installation = source.installation;
        executable = source.executable;
        materiaTypes = source.materiaTypes;
        mapList = source.mapList;
        fields = source.fields.ForkReader();
        try
        {
            characters = source.characters.ForkReader();
        }
        catch
        {
            fields.Dispose();
            throw;
        }
    }

    public Ff7Progress Progress { get; private set; } = null!;
    public Ff7FieldResources Field { get; private set; } = null!;
    public IReadOnlyList<Ff7FieldModelResources> Models => models;
    public FieldMachine Machine { get; private set; } = null!;
    public Ff7FieldHost Host { get; private set; } = null!;
    public long FieldGeneration { get; private set; }
    public ushort MapId { get; private set; }
    public float FieldNearPlane => executable.NativePresentation.NearPlane(MapId);
    public Action<FieldCommand>? PresentationCommand { get; set; }
    public Ff7WorldRuntime? World { get; private set; }
    public event Action<Ff7Session>? PartyChanged;

    public bool CanSave => Battle is null && (World?.CanSave ?? (!HasPendingTick && !tickTailInProgress && Machine.Initialized && (Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 29) & 2) != 0 && (Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 31) & 2) == 0 && Host.Process.GlobalCommand == Ff7GlobalCommand.None && !Host.Process.MenuDisabled && !Host.Process.PlayerLocked && Host.Process.Join is null && Host.Process.Transition is null && Host.Process.Movie is not { Playing: true } && Host.Process.Dialogues.All(dialogue => dialogue.Completed || !Host.CanConfirmDialogue(dialogue.RequestId))));

    public bool CanSaveMemoria => Battle is null && (World is not null ? World.CanSave : !fieldLoadIncomplete && !HasPendingTick && !tickTailInProgress && Machine.Initialized && (ChocoboRace is not null || (Host.Process.GlobalCommand is Ff7GlobalCommand.None or Ff7GlobalCommand.Movie || Host.Process.Menu is { Mode: 14, Completed: false })));

    public static Ff7Session NewGame(Ff7Installation installation)
    {
        Ff7Session session = new(installation);
        try
        {
            KernelInitialData initial = Ff7InitialGameData.Load(installation);
            string[] names = ["Cloud", "Barret", "Tifa", "Aeris", "Red XIII", "Yuffie", "Cait Sith", "Vincent", "Cid"];
            session.Progress = new Ff7Progress
            {
                InitialKernelData = initial.NativeData.ToArray(),
                Characters = initial.Characters.Select((character, slot) => new Ff7CharacterState(slot, names[slot], character.Level, character.Experience, character.CurrentHp, character.CurrentMp, character.Raw.ToArray())).ToArray(),
                Party = session.executable.InitialParty.Select(character => (int)character).ToArray(),
                Inventory = initial.Inventory.Select((item, index) => (item, index)).Where(value => value.item.Encoded != 0xFFFF).Select(value => new Ff7InventorySlot(value.item.ItemId, value.item.Quantity, value.index)).ToArray(),
                Materia = initial.Materia.Where(materia => materia.MateriaId != 255).Select(materia => new Ff7MateriaState(materia.MateriaId, materia.AbilityPoints)).ToArray(),
                Gil = initial.Gil,
                Clock = new Ff7ClockState(),
                NativeBattleSpeed = Ff7Progress.DefaultNativeBattleSpeed,
                AvailableCharacters = session.executable.InitialAvailableCharacters
            };
            session.Progress.Validate();
            FieldVariables variables = new(session.executable.CreateInitialPersistentVariables(), new byte[256]);
            session.LoadField(session.executable.InitialField, variables);
            session.Machine.Initialize();
            session.InitializeNativePartyPresentation();
            session.Host.InitializePlayerMovement(session.Field.Scripts.Scale);
            session.Host.InitializeBackgroundTriggers();
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public static Ff7Session Restore(Ff7Installation installation, Ff7SessionSave save)
    {
        return Restore(installation, save, null);
    }

    private Ff7Session RestoreNavigationSnapshot(Ff7SessionSave save)
    {
        // Windows read-only archive handles pin the selected resource epoch until every reader closes.
        return Restore(installation, save, OperatingSystem.IsWindows() ? this : null);
    }

    private static Ff7Session Restore(Ff7Installation installation, Ff7SessionSave save, Ff7Session? resourceSource)
    {
        ArgumentNullException.ThrowIfNull(save);
        save.Validate();
        if (save.World is null && (save.Vm.PendingRequests ?? []).Any(request => request.Command is FieldActorMove or FieldActorJump or FieldActorLadder && save.Vm.Entities[request.Entity].Priority < request.Priority))
        {
            throw new NotSupportedException("FFVII saved scripted movement uses the obsolete preemption ownership contract; migrate this continuation before resuming.");
        }
        if (save.ResourceProfile != installation.Layout.ToString() || save.Language != installation.Language)
        {
            throw new InvalidDataException("The FFVII save requires a different resource profile or language.");
        }
        Ff7Session session = resourceSource is null ? new(installation) : new(resourceSource);
        try
        {
            int map = Array.IndexOf(session.mapList.ToArray(), save.FieldName);
            if (map < 0)
            {
                throw new InvalidDataException("The FFVII saved field is absent from the original maplist.");
            }
            session.nativeSaveData = save.NativeSaveData?.ToArray();
            session.Progress = save.Progress with { NativeBattleSpeed = Ff7NativeSaveCodec.ResolveBattleSpeed(save.Progress.NativeBattleSpeed, session.nativeSaveData) };
            session.battleBigShotChargeRotation = save.BattleBigShotChargeRotation;
            session.battleBigShotCounterRotation = save.BattleBigShotCounterRotation;
            session.battleSearchScopeScaleCounter = save.BattleSearchScopeScaleCounter;
            session.battleSilkDirection = save.BattleSilkDirection;
            session.battleCameraPresentationCounter = save.BattleCameraPresentationCounter;
            session.FieldGeneration = save.FieldGeneration;
            Ff7FieldProcess process = resourceSource is null ? save.Process : save.Process with { Actors = save.Process.Actors.ToArray() };
            session.LoadField(checked((ushort)map), null, process, resourceSource: resourceSource);
            session.Host.RestorePlayerSelection();
            session.Host.RestoreDialoguePresentation();
            FieldEntitySnapshot[] fieldEntities = save.Vm.Entities.ToArray();
            foreach (Ff7Actor actor in save.Process.Actors.Where(actor => actor.EntityBound && actor.PartyCharacter >= 0).Reverse())
            {
                if ((uint)actor.Entity < fieldEntities.Length && fieldEntities[actor.Entity] is { ModelBound: true, PartyCharacter: null } && !fieldEntities.Any(entity => entity?.PartyCharacter == actor.PartyCharacter))
                {
                    fieldEntities[actor.Entity] = fieldEntities[actor.Entity] with { PartyCharacter = actor.PartyCharacter };
                }
            }
            session.Machine = FieldMachine.Restore(FieldProgram.FromPcScripts(session.Field.Scripts), save.Vm with { Entities = fieldEntities }, session.Host);
            session.Machine.ScriptReturned = session.ObserveNativeScriptReturn;
            session.Machine.ScriptDispatched = session.ObserveNativeScriptDispatch;
            session.Machine.InstructionExecuted = session.ObserveNativeInstruction;
            session.InitializeNativePartyPresentation(restoring: true);
            session.GatewayDeparture = save.GatewayDeparture;
            if (session.GatewayDeparture is { } gateway && !session.HasNativeGatewayDeparture(gateway))
            {
                throw new InvalidDataException("The accepted native gateway receipt does not match its selected source resource and crossing.");
            }
            session.tickContinuation = save.TickContinuation is null ? null : save.TickContinuation with { ScriptReturns = save.TickContinuation.ScriptReturns?.ToArray() };
            session.EventCoverage = save.EventCoverage is null ? null : new Memoria.AutoPlay.AutoPlayEventCoverageLedger(save.EventCoverage).Capture();
            session.AutoPlayState = save.AutoPlay?.Clone();
            session.Progress = session.Progress with { Clock = session.Clock };
            if (save.World is Ff7WorldProcess world)
            {
                byte[] persistent = (byte[])save.Vm.PersistentVariables.Clone();
                session.World = new(installation, persistent, world.EntrySelector, Enumerable.Range(9, 3).Select(address => persistent[256 + address]).ToArray(), world.WorldId);
                session.World.Restore(world);
                session.worldPartyMenuPending = save.WorldPartyMenuPending ?? session.World.RequiresMenuPartySynchronization(session.Progress.Party);
                session.ConfigureWorldPresentation();
            }
            if (save.Battle is { } battle)
            {
                session.RestoreBattle(battle);
            }
            session.RestoreChocobo(save);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public void AdvanceTick()
    {
        AdvanceTick(true);
    }

    public void AdvanceTick(bool advanceClock, bool waitForBattleCommand = false, bool confirmBattleLimit = false, bool escapeBattleHeld = false, bool turnBasedBattle = false)
    {
        if (World is { } world)
        {
            FlushNativePartyStatistics();
            if (world.PendingBattle is null && world.ReadyMapTransfer is int pendingMap)
            {
                CompleteWorldMapTransfer(world, pendingMap);
                return;
            }
            if (world.PendingBattle is null && world.ReadyFieldDeparture is { } pendingDeparture)
            {
                CompleteWorldDeparture(world, pendingDeparture);
                return;
            }
            if (advanceClock)
            {
                AdvanceNominalClockCallbacks();
            }
            if (Battle is { } worldBattle)
            {
                if (world.PendingBattle is { Timed: true } && Clock.CountdownSeconds == 0)
                {
                    worldBattle.ApplyWorldCountdownExpiry();
                }
                AdvanceBattle(waitForBattleCommand, confirmBattleLimit, escapeBattleHeld, turnBasedBattle);
                return;
            }
            if (worldPartyMenuPending)
            {
                CompleteWorldPlayerMenu();
            }
            ConfigureWorldPresentation();
            world.Advance();
            if (TryBeginWorldBattle(world))
            {
                return;
            }
            Host.AdvanceNativeMusicClock();
            if (world.ReadyMapTransfer is int destinationMap)
            {
                CompleteWorldMapTransfer(world, destinationMap);
                return;
            }
            if (world.ReadyFieldDeparture is { } departure)
            {
                CompleteWorldDeparture(world, departure);
            }
            return;
        }
        if (tickTailInProgress)
        {
            throw new InvalidOperationException("A failed field update tail requires its preceding standalone checkpoint.");
        }
        if (Host.HasUnfinishedBattle)
        {
            FlushNativePartyStatistics();
            if (Battle is not null)
            {
                if (advanceClock)
                {
                    AdvanceNominalClockCallbacks();
                }
                AdvanceBattle(waitForBattleCommand, confirmBattleLimit, escapeBattleHeld, turnBasedBattle);
            }
            return;
        }
        if (tickContinuation is null && Host.PendingMinigame is { } minigame)
        {
            AdvanceMinigame(minigame, advanceClock);
            return;
        }
        if (tickContinuation is null && Host.Process.Transition is not null)
        {
            CompleteFieldTransition();
            return;
        }
        if (tickContinuation is null)
        {
            FlushNativePartyStatistics();
            if (advanceClock)
            {
                AdvanceNominalClockCallbacks();
            }
            Ff7NativeClock.PublishField(Clock, Machine.Variables);
            ResumeEncounter();
            AdvanceAudio();
            DispatchLineScripts();
            tickContinuation = new(Machine.Input);
        }
        Machine.AdvanceTick();
        Ff7TickContinuation completedContinuation = tickContinuation;
        tickTailInProgress = true;
        tickContinuation = null;
        Host.PublishCurrentNativeMovieCameraFrame();
        AdvancePlayerInput();
        Host.AdvanceActors();
        Host.AdvanceNativeMenu();
        FieldVmSnapshot snapshot = Machine.Capture();
        Host.RetireCompletedRequests((snapshot.PendingRequests ?? []).Select(request => request.Id).ToHashSet());
        tickTailInProgress = false;
        Ff7NativeTickObservation? observation = CompleteNativeObservation(completedContinuation, snapshot);
        CompleteFieldTransition();
        if (deferNavigationFieldDeparture && Host.Process.Transition is not null)
        {
            return;
        }
        if (observation is not null)
        {
            NativeTickCompleted?.Invoke(observation);
        }
        ApplyDeferredInput();
    }

    private void CompleteFieldTransition()
    {
        if (Host.Process.Transition is FieldTransitionCommand transition)
        {
            CompleteNativeFieldDeparture(transition, GatewayDeparture, deferNavigationFieldDeparture, NativeFieldDeparting, LoadAcceptedFieldTransition);
        }
    }

    internal static void CompleteNativeFieldDeparture(FieldTransitionCommand transition, Ff7NativeGatewayDeparture? receipt, bool defer, Action<Ff7NativeGatewayDeparture?>? observed, Action<FieldTransitionCommand> load)
    {
        observed?.Invoke(receipt);
        if (!defer)
        {
            load(transition);
        }
    }

    private void LoadAcceptedFieldTransition(FieldTransitionCommand transition)
    {
        if (transition.Map is >= 1 and <= 64)
        {
            byte[] persistent = Machine.Variables.CopyPersistent();
            World = new(installation, persistent, transition.Map, Enumerable.Range(9, 3).Select(address => persistent[256 + address]).ToArray(), Ff7WorldRuntime.ResolveWorldId(transition.Map));
            ConfigureWorldPresentation();
            GatewayDeparture = null;
            return;
        }
        FieldVariables variables = Machine.Variables;
        LoadField(transition.Map, variables);
        FieldGeneration++;
        fieldLoadIncomplete = true;
        Machine.Initialize();
        InitializeNativePartyPresentation();
        Host.InitializePlayerMovement(Field.Scripts.Scale);
        Host.InitializeBackgroundTriggers();
        Host.PlacePlayerAtEntry(transition);
        fieldLoadIncomplete = false;
    }

    public Ff7SessionSave Capture()
    {
        return Capture(includeObservations: true);
    }

    private Ff7SessionSave Capture(bool includeObservations)
    {
        if (fieldLoadIncomplete || tickTailInProgress || Highway is not null)
        {
            throw new InvalidOperationException("Snapshots require a completed field load outside an active highway chase and an atomic field-update tail.");
        }
        FieldVmSnapshot vm = Machine.Capture();
        if (World is not null)
        {
            vm = vm with { PersistentVariables = World.Machine.CopyPersistent() };
        }
        return new Ff7SessionSave { GatewayDeparture = GatewayDeparture, NativeSaveData = nativeSaveData?.ToArray(), ResourceProfile = installation.Layout.ToString(), Language = installation.Language, FieldName = Field.Name, FieldGeneration = FieldGeneration, Progress = Progress, Vm = vm, Process = Host.Capture(), TickContinuation = tickContinuation is null ? null : tickContinuation with { ScriptReturns = tickContinuation.ScriptReturns?.ToArray() }, World = World?.Process, WorldPartyMenuPending = World is null ? null : worldPartyMenuPending, EventCoverage = !includeObservations || EventCoverage is null ? null : new Memoria.AutoPlay.AutoPlayEventCoverageLedger(EventCoverage).Capture(), AutoPlay = includeObservations ? AutoPlayState : null, Battle = Battle?.Capture(), ChocoboRace = ChocoboRace?.Capture(), ChocoboCarry = chocoboCarry, BattleBigShotChargeRotation = Battle?.BigShotChargeRotation ?? battleBigShotChargeRotation, BattleBigShotCounterRotation = Battle?.BigShotCounterRotation ?? battleBigShotCounterRotation, BattleSearchScopeScaleCounter = Battle?.SearchScopeScaleCounter ?? battleSearchScopeScaleCounter, BattleSilkDirection = Battle?.SilkDirection ?? battleSilkDirection, BattleCameraPresentationCounter = Battle?.CameraPresentationCounter ?? battleCameraPresentationCounter };
    }

    private bool fieldLoadIncomplete;

    private void LoadField(ushort map, FieldVariables? variables, Ff7FieldProcess? process = null, Action<Ff7FieldHost>? prepareHost = null, Ff7Session? resourceSource = null)
    {
        bool enteringField = process is null;
        if (enteringField)
        {
            if (Host is not null)
            {
                process = Host.CreateNextFieldProcess(MapId);
            }
            else
            {
                process = Ff7FieldHost.CreateInitialFieldProcess(map);
            }
        }
        if (map >= mapList.Count)
        {
            throw new InvalidDataException("FFVII field transition is outside the original maplist.");
        }
        bool sameField = resourceSource?.Field.Name == mapList[map];
        Ff7FieldResources loadedField = sameField ? resourceSource!.Field : Ff7FieldResources.Load(fields, mapList[map]);
        Ff7FieldModelResources[] loadedModels = sameField ? resourceSource!.models.ToArray() : loadedField.Models.Models.Select(model => Ff7FieldModelResources.Load(characters, model, executable, fields)).ToArray();
        IReadOnlyList<PcFieldEncounterTable> loadedEncounterTables = PcFieldEncounterTable.Read(loadedField.Container.GetSection(PcFieldSectionKind.Encounters).Span);
        Ff7FieldHost loadedHost = new(loadedField, (model, animation) => loadedModels[model].Animations[Path.ChangeExtension(loadedField.Models.Models[model].Animations[animation], ".a")].Frames.Count, process, map)
        {
            AudioRequested = Host?.AudioRequested,
            NativeSoundLoopAlreadyPlaying = Host?.NativeSoundLoopAlreadyPlaying,
            NativeSoundClockAdvanced = Host?.NativeSoundClockAdvanced,
            NativeMusicTimeline = ReadNativeMusicTimeline,
            NativeBlinkCamera = Host?.NativeBlinkCamera ?? ReadNativeMovieCamera,
            NativeMovieCameraRecordCount = ReadNativeMovieCameraRecordCount,
            NativeBlinkRandomTable = executable.NativeFieldRandomTable,
            Movement = new Ff7NativeMovement(loadedField.Walkmesh, executable.NativeSinCos, executable.NativeArcAngles, map),
            NativeFieldEntryFadeAlpha = executable.NativeFieldEntryFadeAlpha,
            CameraProjection = new Ff7NativeCamera(loadedField.Cameras[0], loadedField.Triggers),
            CameraShakeRandomTable = executable.NativeCameraShakeRandomTable,
            BeginBattleRequest = BeginFieldBattle,
            ReadInputHeld = () => Machine.Input.Held,
            ReadFieldByte = operand => Machine.Variables.ReadByte(operand.Bank, operand.Value),
            ReadScriptParty = () => Enumerable.Range(9, 3).Select(address => (int)Machine.Variables.ReadByte(FieldBank.Persistent1Byte, (byte)address)).ToArray(),
            ReadScriptPartyByte = slot => Machine.Variables.ReadByte(FieldBank.Persistent1Byte, checked((byte)(9 + slot))),
            ModelPartVertexCounts = model => loadedModels[model].Geometry.Select(part => part.Polygon.Vertices.Count).ToArray(),
            ModelPartPolygonCounts = model => loadedModels[model].Geometry.Select(part => part.Polygon.Polygons.Count).ToArray(),
            ExternalCommand = ExecuteExternal,
            NamingStarted = BeginNativeNaming,
            NamingCompleted = CompleteNativeNaming,
            AdvanceDialogue = AdvanceDialogue
        };
        loadedHost.NativeModelConfiguration = slot => ReadNativeModelPresentation(map, loadedHost.Process, slot);
        ConfigureNativeWater(loadedHost, loadedField, loadedModels, map);
        loadedHost.ConfigureNativeSoundResources(ReadNativeSoundDescription, restoring: !enteringField);
        prepareHost?.Invoke(loadedHost);
        FieldMachine loadedMachine = new(FieldProgram.FromPcScripts(loadedField.Scripts), variables, host: loadedHost);
        fieldLoadIncomplete = true;
        GatewayDeparture = null;
        Field = loadedField;
        MapId = map;
        models = loadedModels;
        encounterTables = loadedEncounterTables;
        Host = loadedHost;
        Machine = loadedMachine;
        Machine.ScriptReturned = ObserveNativeScriptReturn;
        Machine.ScriptDispatched = ObserveNativeScriptDispatch;
        Machine.InstructionExecuted = ObserveNativeInstruction;
        Host.NativeOperationExecuted += ObserveNativeHostOperation;
        if (prepareHost is null)
        {
            dialogueTokens.Clear();
        }
        if (enteringField)
        {
            Progress = Progress with { Encounters = Progress.Encounters with { MovementCounter = 0 } };
        }
        fieldLoadIncomplete = false;
    }

    private FieldHostReply ExecuteExternal(FieldCommand command)
    {
        if (command is FieldMenuCommand { Mode: 21 })
        {
            ApplyHighwayHealth();
            return new(true);
        }
        if (command is FieldMenuCommand { Mode: 17 } equipment)
        {
            Ff7EquipmentRemovalResult removed = Ff7NativeEquipmentRemoval.Apply(Progress, equipment.Parameter, materiaTypes, executable.NativeMateriaClasses);
            Progress = removed.Progress;
            byte flags = Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 75);
            Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 75, (byte)(flags | removed.MateriaFlags));
            return new(true);
        }
        if (command is FieldPartyRecovery recovery)
        {
            IReadOnlyList<ReadOnlyMemory<byte>> materiaRecords = Ff7MateriaResources.LoadRecords(installation);
            Ff7PartyRecoveryResult recovered = recovery.AllCharacters
                ? Ff7NativePartyRecovery.ApplyAll(Progress, materiaRecords, executable.NativeMateriaStatModifiers)
                : Ff7NativePartyRecovery.Apply(Progress, materiaRecords, executable.NativeMateriaStatModifiers);
            Ff7Progress recoveredProgress = PreserveGuestFieldCharacters(Progress, recovered.Progress);
            if (GuestFieldVitals is { } guest)
            {
                IReadOnlyList<int> characters = recovery.AllCharacters ? guest.StoryCharacters : Progress.Party;
                PublishFieldEntryEffect(() => guest.RecoverCharacters(characters, recovery.AllCharacters));
            }
            Progress = recoveredProgress;
            byte flags = Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 122);
            Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 122, (byte)((flags & 127) | (recovered.Underwater ? 128 : 0)));
            return new(true);
        }
        if (command is FieldItemCommand item)
        {
            return UpdateInventory(item);
        }
        if (command is FieldItemQuantityQuery itemQuantity)
        {
            return new(true, Ff7NativeInventory.Quantity(Progress.Inventory, itemQuantity.ItemId));
        }
        if (command is FieldGilCommand gil)
        {
            Progress = gil.Subtract ? Progress with { Gil = Progress.Gil - Math.Min(Progress.Gil, gil.Amount) } : Progress.AddGil(gil.Amount);
            return new(true);
        }
        if (command is FieldGilQuery)
        {
            return new(true, unchecked((int)Progress.Gil));
        }
        if (command is FieldHitPointCommand health)
        {
            IReadOnlyList<ReadOnlyMemory<byte>> healthMateriaRecords = Ff7MateriaResources.LoadRecords(installation);
            Ff7PartyRecoveryResult adjusted = ApplyFieldHitPointAdjustment(Progress, health, healthMateriaRecords, executable.NativeMateriaStatModifiers, GuestFieldVitals, PublishFieldEntryEffect);
            Progress = adjusted.Progress;
            byte flags = Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 122);
            Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 122, (byte)((flags & 127) | (adjusted.Underwater ? 128 : 0)));
            return new(true);
        }
        if (command is FieldTimerCommand timer)
        {
            Progress = Progress with { Clock = Clock with { CountdownSeconds = timer.Seconds } };
            return new(true);
        }
        if (command is FieldMateriaCommand materia)
        {
            return AcquireMateria(materia);
        }
        if (command is FieldRandomStrideCommand stride)
        {
            Progress = Progress with { FieldRandomStride = stride.Stride };
            return new(true);
        }
        if (command is FieldRandomCommand)
        {
            byte index = unchecked((byte)(Progress.FieldRandomIndex + (Progress.FieldRandomStride ?? executable.FieldRandomStep)));
            byte value = executable.NativeFieldRandomTable[index];
            Progress = Progress with { FieldRandomIndex = index };
            return new(true, value);
        }
        if (command is FieldPreloadCommand preload)
        {
            return Preload(preload);
        }
        if (command is FieldPartyProjection projection)
        {
            int[] requested = [projection.Member0, projection.Member1, projection.Member2];
            int[] updated = Progress.Party.Select(member => requested.Contains(member) ? member : 255).ToArray();
            foreach (int member in requested.Where(member => member != 255))
            {
                if (updated.Contains(member))
                {
                    continue;
                }
                int slot = Array.IndexOf(updated, 255);
                if (slot >= 0)
                {
                    updated[slot] = member;
                }
            }
            PublishFieldParty(Progress with { Party = updated, AvailableCharacters = (ushort)(Progress.AvailableCharacters | projection.VisibilityOrMask) });
            return new(true);
        }
        if (command is FieldPartyCommand party)
        {
            int[] members = (int[])Progress.Party.Clone();
            switch (party.Operation)
            {
                case FieldPartyOperation.Replace:
                    members = [party.Member0, party.Member1, party.Member2];
                    break;
                case FieldPartyOperation.Add:
                    if (!members.Contains(party.Member0))
                    {
                        int slot = Array.IndexOf(members, 255);
                        if (slot >= 0)
                        {
                            members[slot] = party.Member0;
                        }
                    }
                    break;
                case FieldPartyOperation.Remove:
                    members = members.Where(member => member != party.Member0 && member != 255).Concat(Enumerable.Repeat(255, 3)).Take(3).ToArray();
                    break;
                case FieldPartyOperation.Contains:
                    return new(true, members.Contains(party.Member0) ? 1 : 0);
                case FieldPartyOperation.Available:
                    return new(true, (Progress.AvailableCharacters & 1 << party.Member0) != 0 ? 1 : 0);
                case FieldPartyOperation.SetAvailable:
                    Progress = Progress with { AvailableCharacters = checked((ushort)(party.Member1 != 0 ? Progress.AvailableCharacters | 1 << party.Member0 : Progress.AvailableCharacters & ~(1 << party.Member0))) };
                    return new(true);
                case FieldPartyOperation.SetLocked:
                    Progress = Progress with { LockedCharacters = checked((ushort)(party.Member1 != 0 ? Progress.LockedCharacters | 1 << party.Member0 : Progress.LockedCharacters & ~(1 << party.Member0))) };
                    return new(true);
                default:
                    throw new NotSupportedException("FFVII party availability and locking require their separate native masks.");
            }
            PublishFieldParty(Progress with { Party = members });
            return new(true);
        }
        if (PresentationCommand is null)
        {
            throw new NotSupportedException($"FFVII presentation has no handler for {command.GetType().Name}.");
        }
        Action<FieldCommand> publish = PresentationCommand;
        PublishFieldEntryEffect(() => publish(command));
        return new(true);
    }

    private void PublishFieldParty(Ff7Progress progress)
    {
        bool changed = !Progress.Party.SequenceEqual(progress.Party);
        worldPartyMenuPending |= World is not null && changed;
        Progress = progress with { NativePartyStatisticsDirty = true };
        if (changed)
        {
            PublishFieldEntryEffect(() => PartyChanged?.Invoke(this));
        }
    }

    public void Dispose()
    {
        CoverageOwner?.Dispose();
        World?.Dispose();
        battleArchive?.Dispose();
        battleMagicArchive?.Dispose();
        characters.Dispose();
        fields.Dispose();
    }
}
