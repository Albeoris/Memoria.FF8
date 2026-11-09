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
    public Action<FieldCommand>? PresentationCommand { get; set; }

    public bool CanSave => Machine.Initialized && (Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 29) & 2) != 0 && (Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 31) & 2) == 0 && Host.Process.GlobalCommand == Ff7GlobalCommand.None && !Host.Process.MenuDisabled && !Host.Process.PlayerLocked && Host.Process.Join is null && Host.Process.Transition is null && Host.Process.Movie is not { Playing: true } && Host.Process.Dialogues.All(dialogue => dialogue.Completed || !Host.CanConfirmDialogue(dialogue.RequestId));

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
                Inventory = initial.Inventory.Where(item => item.Encoded != 0xFFFF && item.Quantity > 0).Select(item => new Ff7InventorySlot(item.ItemId, item.Quantity)).ToArray(),
                Materia = initial.Materia.Where(materia => materia.MateriaId != 255).Select(materia => new Ff7MateriaState(materia.MateriaId, materia.AbilityPoints)).ToArray(),
                Gil = initial.Gil,
                Clock = new Ff7ClockState(),
                AvailableCharacters = session.executable.InitialAvailableCharacters
            };
            session.Progress.Validate();
            FieldVariables variables = new(session.executable.CreateInitialPersistentVariables(), new byte[256]);
            session.LoadField(session.executable.InitialField, variables);
            session.Machine.Initialize();
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
        ArgumentNullException.ThrowIfNull(save);
        save.Validate();
        if (save.ResourceProfile != installation.Layout.ToString() || save.Language != installation.Language)
        {
            throw new InvalidDataException("The FFVII save requires a different resource profile or language.");
        }
        Ff7Session session = new(installation);
        try
        {
            int map = Array.IndexOf(session.mapList.ToArray(), save.FieldName);
            if (map < 0)
            {
                throw new InvalidDataException("The FFVII saved field is absent from the original maplist.");
            }
            session.Progress = save.Progress;
            session.FieldGeneration = save.FieldGeneration;
            session.LoadField(checked((ushort)map), null, save.Process);
            session.Machine = FieldMachine.Restore(FieldProgram.FromPcScripts(session.Field.Scripts), save.Vm, session.Host);
            session.Progress = session.Progress with { Clock = session.Clock };
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

    public void AdvanceTick(bool advanceClock)
    {
        if (Host.Process.Transition is not null)
        {
            CompleteFieldTransition();
            AdvanceDialogues();
            return;
        }
        if (advanceClock)
        {
            AdvanceNominalClockCallbacks();
        }
        Ff7NativeClock.PublishField(Clock, Machine.Variables);
        ResumeEncounter();
        AdvanceAudio();
        DispatchLineScripts();
        Machine.AdvanceTick();
        AdvancePlayerInput();
        Host.AdvanceActors();
        FieldVmSnapshot snapshot = Machine.Capture();
        Host.RetireCompletedRequests((snapshot.PendingRequests ?? []).Select(request => request.Id).ToHashSet());
        CompleteFieldTransition();
        AdvanceDialogues();
    }

    private void CompleteFieldTransition()
    {
        if (Host.Process.Transition is FieldTransitionCommand transition)
        {
            FieldVariables variables = Machine.Variables;
            LoadField(transition.Map, variables);
            FieldGeneration++;
            fieldLoadIncomplete = true;
            Machine.Initialize();
            Host.InitializeBackgroundTriggers();
            Ff7Actor? player = Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
            if (player is not null)
            {
                Host.Execute(new FieldHostRequest(long.MaxValue - 1, player.Entity, 7, 0, new FieldActorPlacement(transition.X, transition.Y, null, checked((short)transition.Triangle))));
                Host.Execute(new FieldHostRequest(long.MaxValue, player.Entity, 7, 0, new FieldActorProperty(FieldActorPropertyKind.Direction, transition.Direction)));
            }
            fieldLoadIncomplete = false;
        }
    }

    public Ff7SessionSave Capture()
    {
        if (fieldLoadIncomplete)
        {
            throw new InvalidOperationException("Snapshots require a completed field load.");
        }
        return new Ff7SessionSave { ResourceProfile = installation.Layout.ToString(), Language = installation.Language, FieldName = Field.Name, FieldGeneration = FieldGeneration, Progress = Progress, Vm = Machine.Capture(), Process = Host.Capture() };
    }

    private bool fieldLoadIncomplete;

    private void LoadField(ushort map, FieldVariables? variables, Ff7FieldProcess? process = null)
    {
        bool enteringField = process is null;
        if (enteringField)
        {
            if (Host is not null)
            {
                process = Host.CreateNextFieldProcess();
            }
        }
        if (map >= mapList.Count)
        {
            throw new InvalidDataException("FFVII field transition is outside the original maplist.");
        }
        Ff7FieldResources loadedField = Ff7FieldResources.Load(fields, mapList[map]);
        Ff7FieldModelResources[] loadedModels = loadedField.Models.Models.Select(model => Ff7FieldModelResources.Load(characters, model)).ToArray();
        IReadOnlyList<PcFieldEncounterTable> loadedEncounterTables = PcFieldEncounterTable.Read(loadedField.Container.GetSection(PcFieldSectionKind.Encounters).Span);
        Ff7FieldHost loadedHost = new(loadedField, (model, animation) => loadedModels[model].Animations[Path.ChangeExtension(loadedField.Models.Models[model].Animations[animation], ".a")].Frames.Count, process)
        {
            AudioRequested = Host?.AudioRequested,
            Movement = new Ff7NativeMovement(loadedField.Walkmesh, executable.NativeSinCos, executable.NativeArcAngles),
            CameraProjection = new Ff7NativeCamera(loadedField.Cameras[0], loadedField.Triggers),
            CameraShakeRandomTable = executable.NativeCameraShakeRandomTable,
            CompleteBattleRequest = CompleteDebugBattle,
            ReadInputHeld = () => Machine.Input.Held,
            ReadFieldByte = operand => Machine.Variables.ReadByte(operand.Bank, operand.Value),
            ReadScriptParty = () => Enumerable.Range(9, 3).Select(address => (int)Machine.Variables.ReadByte(FieldBank.Persistent1Byte, (byte)address)).ToArray(),
            ModelPartVertexCounts = model => loadedModels[model].Geometry.Select(part => part.Polygon.Vertices.Count).ToArray(),
            ModelPartPolygonCounts = model => loadedModels[model].Geometry.Select(part => part.Polygon.Polygons.Count).ToArray(),
            ExternalCommand = ExecuteExternal
        };
        FieldMachine loadedMachine = new(FieldProgram.FromPcScripts(loadedField.Scripts), variables, host: loadedHost);
        fieldLoadIncomplete = true;
        Field = loadedField;
        models = loadedModels;
        encounterTables = loadedEncounterTables;
        Host = loadedHost;
        Machine = loadedMachine;
        dialogueTokens.Clear();
        if (enteringField)
        {
            Progress = Progress with { Encounters = Progress.Encounters with { MovementCounter = 0 } };
        }
        fieldLoadIncomplete = false;
    }

    private FieldHostReply ExecuteExternal(FieldCommand command)
    {
        if (command is FieldPartyRecovery)
        {
            Ff7PartyRecoveryResult recovered = Ff7NativePartyRecovery.Apply(Progress, Ff7MateriaResources.LoadRecords(installation), executable.NativeMateriaStatModifiers);
            Progress = recovered.Progress;
            byte flags = Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 122);
            Machine.Variables.WriteByte(FieldBank.Persistent0Byte, 122, (byte)((flags & 127) | (recovered.Underwater ? 128 : 0)));
            return new(true);
        }
        if (command is FieldItemCommand item)
        {
            return AcquireItem(item);
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
        if (command is FieldRandomCommand)
        {
            byte index = unchecked((byte)(Progress.FieldRandomIndex + executable.FieldRandomStep));
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
            Progress = Progress with { Party = updated, AvailableCharacters = (ushort)(Progress.AvailableCharacters | projection.VisibilityOrMask) };
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
            Progress = Progress with { Party = members };
            return new(true);
        }
        if (PresentationCommand is null)
        {
            throw new NotSupportedException($"FFVII presentation has no handler for {command.GetType().Name}.");
        }
        PresentationCommand(command);
        return new(true);
    }

    public void Dispose()
    {
        characters.Dispose();
        fields.Dispose();
    }
}
