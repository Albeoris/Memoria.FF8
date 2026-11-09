using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

/// <summary>FFVII host processes; rendering and devices observe this state without advancing scripts.</summary>
public sealed partial class Ff7FieldHost : IFieldHost
{
    private readonly Ff7FieldResources field;
    private readonly Func<int, int, int> animationLength;
    private readonly Dictionary<long, Ff7HostReceipt> receipts = [];

    public Ff7FieldHost(Ff7FieldResources field, Func<int, int, int> animationLength, Ff7FieldProcess? process = null)
    {
        this.field = field ?? throw new ArgumentNullException(nameof(field));
        this.animationLength = animationLength ?? throw new ArgumentNullException(nameof(animationLength));
        Process = process ?? new Ff7FieldProcess();
        if (Process.CameraFollowPlayer is null)
        {
            Ff7Actor? legacyTarget = Process.CameraEntity < 0 ? null : Process.Actors?.SingleOrDefault(actor => actor.EntityBound && actor.Entity == Process.CameraEntity);
            Process = Process with
            {
                CameraFollowPlayer = Process.CameraEntity < 0 && Process.CameraScroll is null && Process.CameraX == 0 && Process.CameraY == 0,
                CameraTracking = Process.CameraTracking ?? (legacyTarget is not null && Process.CameraScroll is null ? new(1, legacyTarget.Slot, 0, Process.CameraX, Process.CameraY) : null)
            };
        }
        if (Process.CameraFocusOffset is null)
        {
            Process = Process with { CameraFocusOffset = field.Triggers?.CameraFocus ?? 0 };
        }
        if (Process.BlockedTriangles.Length == 0)
        {
            Process = Process with { BlockedTriangles = new byte[(field.Walkmesh.Count + 7) / 8] };
        }
        InitializePaletteState();
        InitializeFadeState();
        InitializeMovieCameraConsumption();
        Process.Validate();
        foreach (Ff7HostReceipt receipt in Process.Receipts)
        {
            receipts.Add(receipt.Request.Id, receipt);
        }
    }

    public Ff7FieldProcess Process { get; private set; }
    public Func<FieldHostRequest, int>? CompleteBattleRequest { get; set; }
    public Func<ushort, int>? CompleteBattle { get; set; }
    public Action<FieldAudioCommand>? AudioRequested { get; set; }
    public Func<FieldCommand, FieldHostReply>? ExternalCommand { get; set; }
    public Ff7NativeMovement? Movement { get; set; }
    public Ff7NativeCamera? CameraProjection { get; set; }
    public Func<int, IReadOnlyList<int>>? ModelPartVertexCounts { get; set; }
    public Func<FieldByteOperand, byte>? ReadFieldByte { get; set; }
    public Func<int[]>? ReadScriptParty { get; set; }
    public Func<ushort>? ReadInputHeld { get; set; }

    public FieldHostReply Execute(FieldHostRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (receipts.TryGetValue(request.Id, out Ff7HostReceipt? previous))
        {
            if (previous.Request != request)
            {
                throw new InvalidDataException("A FFVII host request changed its identity or operands.");
            }
            if (previous.Reply.Completed)
            {
                if (request.Command is FieldBattleCommand && Process.GlobalCommand == Ff7GlobalCommand.Battle && Process.GlobalRequestId == request.Id)
                {
                    Process = Process with { GlobalCommand = Ff7GlobalCommand.None, GlobalRequestId = null };
                }
                return previous.Reply;
            }
        }
        FieldHostReply reply = Dispatch(request);
        receipts[request.Id] = new Ff7HostReceipt(request, reply);
        return reply;
    }

    private FieldHostReply Dispatch(FieldHostRequest request)
    {
        switch (request.Command)
        {
            case FieldPartySplit split:
                return SplitParty(request.Id, split);
            case FieldPartyJoin join:
                return JoinParty(request.Id, join);
            case FieldModelEffectCommand effect:
                return ModelEffect(request.Entity, effect);
            case FieldLineCommand line:
                Line(request.Entity, line);
                return new(true);
            case FieldTriangleCommand triangle:
                if (triangle.Triangle >= field.Walkmesh.Count)
                {
                    throw new InvalidDataException("FFVII triangle lock references an absent native walkmesh triangle.");
                }
                byte[] blocked = (byte[])Process.BlockedTriangles.Clone();
                int mask = 1 << (triangle.Triangle & 7);
                int index = triangle.Triangle >> 3;
                blocked[index] = (byte)(triangle.Blocked ? blocked[index] | mask : blocked[index] & ~mask);
                Process = Process with { BlockedTriangles = blocked };
                return new(true);
            case FieldActorBind bind:
                if ((uint)bind.ModelIndex >= field.Models.Models.Count)
                {
                    throw new InvalidDataException("The FFVII CHAR descriptor does not exist.");
                }
                int scale = field.Scripts.Scale;
                Ff7Actor actor = new() { Slot = Process.Actors.Length, Entity = request.Entity, Model = bind.ModelIndex, Visible = true, MoveSpeed = 1024 * scale / 512, CollisionRadius = 30 * scale / 512, TalkRadius = 80 * scale / 512 };
                Process = Process with { Actors = [.. Process.Actors.Select(current => current.Entity == request.Entity && current.EntityBound ? current with { EntityBound = false, PlayerControlled = false } : current), actor] };
                return new(true);
            case FieldActorPlacement placement:
                SetActor(request.Entity, current => Place(current, placement));
                return new(true);
            case FieldActorProperty property:
                return SetProperty(request.Entity, property);
            case FieldPartyCharacter character:
                SetActor(request.Entity, current => current with { PartyCharacter = character.CharacterId, PlayerControlled = character.PartySlot == 0, Visible = character.PartySlot == 0 && current.Visible, CollisionEnabled = character.PartySlot == 0 && current.CollisionEnabled, TalkEnabled = character.PartySlot == 0 && current.TalkEnabled });
                return new(true);
            case FieldActorQuery query:
                Ff7Actor queried = GetActor(query.Entity);
                if (query.Query == FieldActorQueryKind.Position)
                {
                    return new(true, Position: new(unchecked((short)(queried.X4096 >> 12)), unchecked((short)(queried.Y4096 >> 12)), unchecked((short)(queried.Z4096 >> 12)), unchecked((short)queried.Triangle)));
                }
                int value = query.Query switch
                {
                    FieldActorQueryKind.Triangle => queried.Triangle,
                    FieldActorQueryKind.Direction => queried.Direction,
                    FieldActorQueryKind.Coordinates => ((queried.X4096 >> 12) & 65535) | (queried.Y4096 >> 12) << 16,
                    _ => throw new NotSupportedException("Unsupported FFVII actor query.")
                };
                return new(true, value);
            case FieldAnimation animation:
                return Animate(request, animation);
            case FieldActorOffset offset:
                Offset(request.Entity, offset);
                return new(true);
            case FieldActorJump jump:
                return Jump(request.Id, request.Entity, jump);
            case FieldActorLadder ladder:
                return Ladder(request.Id, request.Entity, ladder);
            case FieldActorFace face:
                return FaceActor(request.Entity, face);
            case FieldActorTurn turn:
                return Turn(request.Entity, turn);
            case FieldActorMove movement:
                return MoveActor(request, movement);
            case FieldWindowCommand window:
                return Window(window);
            case FieldDialogueCommand dialogue:
                Ff7Dialogue? active = Process.Dialogues.SingleOrDefault(item => item.RequestId == request.Id);
                if (active is null)
                {
                    if (Process.Windows.All(window => window.Id != dialogue.Window))
                    {
                        throw new InvalidDataException("A FFVII message requires its configured window.");
                    }
                    if ((uint)dialogue.Text >= field.Scripts.Texts.Count)
                    {
                        throw new InvalidDataException("The FFVII message index is outside the field text table.");
                    }
                    active = new Ff7Dialogue(request.Id, dialogue.Window, dialogue.Text, dialogue.FirstChoice, dialogue.LastChoice, dialogue.Selected);
                    Process = Process with { Dialogues = [.. Process.Dialogues, active] };
                }
                return new(active.Completed, active.Selected);
            case FieldMenuCommand menu:
                return Menu(request.Id, menu);
            case FieldMovieCommand movie:
                return Movie(request.Id, movie);
            case FieldTransitionCommand transition:
                if (Process.GlobalCommand is not (Ff7GlobalCommand.None or Ff7GlobalCommand.Transition))
                {
                    return new(false);
                }
                Process = Process with { Transition = transition, GlobalCommand = Ff7GlobalCommand.Transition, GlobalRequestId = request.Id };
                return new(true);
            case FieldBattleCommand battle:
                if (Process.GlobalCommand != Ff7GlobalCommand.None)
                {
                    return new(false);
                }
                int result = CompleteBattleRequest?.Invoke(request) ?? CompleteBattle?.Invoke(battle.Formation) ?? throw new NotSupportedException("FFVII debug victory requires an attached FFVII result and reward policy.");
                Process = Process with { BattleResult = result, GlobalCommand = Ff7GlobalCommand.Battle, GlobalRequestId = request.Id };
                return new(true, result);
            case FieldPaletteCommand palette:
                Palette(palette);
                return new(true);
            case FieldBackgroundCommand background:
                Background(background);
                return new(true);
            case FieldControlCommand control:
                return Control(control);
            case FieldCameraCommand camera:
                return Camera(camera);
            case FieldAudioCommand audio:
                ApplyAudio(audio);
                return new(true);
            case FieldMusicCommand music:
                ApplyMusic(music);
                return new(true);
            case FieldFadeCommand fade:
                if (fade.Wait)
                {
                    return new(Process.FadeState?.Completed ?? (Process.Fade is null || Process.Fade.Type is 0 or 4));
                }
                Ff7FadeProcess nextFade = Ff7NativeFade.Start(fade, Process.FadeState);
                Process = Process with { Fade = fade, FadeState = nextFade };
                return new(true);
            default:
                if (ExternalCommand is null)
                {
                    throw new NotSupportedException($"FFVII host does not implement {request.Command.GetType().Name}.");
                }
                return ExternalCommand(request.Command);
        }
    }

    public void Cancel(FieldHostRequest request)
    {
        if (request.Command is not FieldActorMove)
        {
            throw new NotSupportedException("Only native movement preemption is supported by this FFVII host.");
        }
        Process = Process with { Motions = Process.Motions.Where(motion => motion.RequestId != request.Id).ToArray() };
        receipts.Remove(request.Id);
    }

    public void AdvanceActors()
    {
        AdvanceViewOffset();
        AdvanceCamera();
        AdvanceOffsets();
        AdvanceTurns();
        AdvanceJumps();
        AdvanceLadders();
        AdvanceAnimation();
        AdvanceModelEffects();
        foreach (Ff7Motion motion in Process.Motions)
        {
            Ff7NativeMovement movement = Movement ?? throw new NotSupportedException("FFVII scripted movement requires its native fixed-point stepping contract.");
            Ff7Actor actor = GetActor(motion.Entity);
            Ff7NativeMoveResult moved = movement.Step(actor, checked((short)motion.TargetX), checked((short)motion.TargetY), Process.Actors, Process.BlockedTriangles, Process.PlayerLocked);
            SetActor(actor.Entity, current => moved.Actor with { Direction = motion.Turn ? moved.Actor.Direction : current.Direction });
            if (moved.Arrived)
            {
                Process = Process with { Motions = Process.Motions.Where(item => item.RequestId != motion.RequestId).ToArray() };
            }
        }
        AdvanceJoinedMovement();
        if (Process.FadeState is Ff7FadeProcess fadeState)
        {
            Process = Process with { FadeState = Ff7NativeFade.Advance(fadeState, Process.Movie is { Playing: true }) };
        }
        Process = Process with { Tick = Process.Tick + 1, MusicTicks = Process.NativeMusicTrack == 0 || Process.NativeMusicTrack is null && Process.Music is null ? Process.MusicTicks : Process.MusicTicks + 1 };
    }

    public void PublishMovieFrame(int frame, bool completed)
    {
        Ff7Movie movie = Process.Movie ?? throw new InvalidOperationException("No FFVII movie is prepared.");
        if (!movie.Playing || Process.GlobalCommand != Ff7GlobalCommand.Movie || movie.RequestId is null || Process.GlobalRequestId is long owner && owner != movie.RequestId)
        {
            throw new InvalidOperationException("Only the active FFVII movie owner can publish frames.");
        }
        if (frame < movie.Frame)
        {
            throw new InvalidDataException("FFVII movie frames must advance monotonically.");
        }
        Process = Process with { Movie = movie with { Frame = frame, Completed = completed, Playing = !completed }, GlobalCommand = completed ? Ff7GlobalCommand.None : Ff7GlobalCommand.Movie, GlobalRequestId = completed ? null : movie.RequestId };
    }

    public bool CanConfirmDialogue(long requestId)
    {
        Ff7Dialogue? dialogue = Process.Dialogues.SingleOrDefault(item => item.RequestId == requestId);
        return dialogue is { Completed: false } && (dialogue.FirstChoice is not null || (Process.Windows.Single(window => window.Id == dialogue.Window).Flags & 1) == 0);
    }

    public void CompleteDialogue(long requestId, int selected)
    {
        if (!CanConfirmDialogue(requestId))
        {
            return;
        }
        Ff7Dialogue dialogue = Process.Dialogues.Single(item => item.RequestId == requestId);
        if (dialogue.FirstChoice is int first && (selected < first || selected > dialogue.LastChoice))
        {
            throw new ArgumentOutOfRangeException(nameof(selected));
        }
        Process = Process with { Dialogues = Process.Dialogues.Select(item => item.RequestId == requestId ? item with { Selected = selected, Completed = true } : item).ToArray() };
    }

    public Ff7FieldProcess Capture()
    {
        return Process with { Receipts = receipts.Values.OrderBy(receipt => receipt.Request.Id).ToArray(), BackgroundStates = (ushort[])Process.BackgroundStates.Clone(), PaletteBuffer = (ushort[])Process.PaletteBuffer.Clone(), PaletteRows = Process.PaletteRows.Select(row => (ushort[])row.Clone()).ToArray() };
    }

    public void RetireCompletedRequests(IReadOnlySet<long> pending)
    {
        foreach (long id in receipts.Where(pair => pair.Value.Reply.Completed && !pending.Contains(pair.Key) && pair.Key != Process.GlobalRequestId).Select(pair => pair.Key).ToArray())
        {
            receipts.Remove(id);
        }
        Process = Process with { AnimationRequests = Process.AnimationRequests.Where(request => pending.Contains(request.RequestId)).ToArray(), Dialogues = Process.Dialogues.Where(dialogue => !dialogue.Completed || pending.Contains(dialogue.RequestId)).ToArray() };
    }

    public Ff7Actor GetActor(int entity)
    {
        return Process.Actors.SingleOrDefault(actor => actor.Entity == entity && actor.EntityBound) ?? throw new InvalidDataException($"FFVII entity {entity} has no allocated model slot.");
    }

    public void ApplyPlayerMovement(Ff7NativeMoveResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Ff7Actor original = GetActor(result.Actor.Entity);
        if (original.Slot != result.Actor.Slot || original.Model != result.Actor.Model)
        {
            throw new InvalidDataException("FFVII movement cannot reassign the actor's model ownership.");
        }
        Ff7Actor selected = result.Actor;
        if (original.Animation != selected.Animation)
        {
            selected = selected with { AnimationFrame16 = 0 };
        }
        SetActor(original.Entity, _ => selected);
    }

    public bool RequestGateway(FieldTransitionCommand transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (Process.GlobalCommand != Ff7GlobalCommand.None || Process.MapJumpDisabled)
        {
            return false;
        }
        Process = Process with { Transition = transition, GlobalCommand = Ff7GlobalCommand.Transition };
        return true;
    }

    private void SetActorSlot(int slot, Func<Ff7Actor, Ff7Actor> update)
    {
        Ff7Actor actor = Process.Actors.Single(actor => actor.Slot == slot);
        Ff7Actor replacement = update(actor);
        Process = Process with { Actors = Process.Actors.Select(current => current.Slot == slot ? replacement : current).ToArray() };
    }

    private void SetActor(int entity, Func<Ff7Actor, Ff7Actor> update)
    {
        Ff7Actor actor = GetActor(entity);
        Ff7Actor replacement = update(actor);
        Process = Process with { Actors = Process.Actors.Select(current => current.Entity == entity && current.EntityBound ? replacement : current).ToArray() };
    }

    private Ff7Actor Place(Ff7Actor actor, FieldActorPlacement placement)
    {
        int triangle = placement.Triangle ?? actor.Triangle;
        float z = placement.Z ?? ResolveHeight(placement.X, placement.Y, triangle);
        return actor with { X4096 = placement.X * 4096, Y4096 = placement.Y * 4096, Z4096 = checked((int)(z * 4096)), Triangle = triangle };
    }

    private float ResolveHeight(float x, float y, int triangle)
    {
        if ((uint)triangle >= field.Walkmesh.Count)
        {
            throw new InvalidDataException("FFVII actor placement requires a valid native walkmesh triangle.");
        }
        PcWalkmeshTriangle face = field.Walkmesh[triangle];
        float denominator = (face.B.Y - face.C.Y) * (face.A.X - face.C.X) + (face.C.X - face.B.X) * (face.A.Y - face.C.Y);
        if (denominator == 0)
        {
            throw new InvalidDataException("FFVII actor placement has a degenerate walkmesh triangle.");
        }
        float a = ((face.B.Y - face.C.Y) * (x - face.C.X) + (face.C.X - face.B.X) * (y - face.C.Y)) / denominator;
        float b = ((face.C.Y - face.A.Y) * (x - face.C.X) + (face.A.X - face.C.X) * (y - face.C.Y)) / denominator;
        return a * face.A.Z + b * face.B.Z + (1 - a - b) * face.C.Z;
    }

    private FieldHostReply SetProperty(int entity, FieldActorProperty property)
    {
        if (property.Property == FieldActorPropertyKind.OffsetWait)
        {
            Ff7Actor offsetActor = GetActor(entity);
            bool completed = offsetActor.Offset?.Completed ?? true;
            if (completed)
            {
                SetActor(entity, current => current with { Offset = null });
            }
            return new(completed);
        }
        if (property.Property == FieldActorPropertyKind.PlayerCharacter)
        {
            GetActor(property.Value);
            Process = Process with { Actors = Process.Actors.Select(actor => actor with { PlayerControlled = actor.Entity == property.Value }).ToArray() };
            return new(true);
        }
        if (property.Property == FieldActorPropertyKind.AnimationWait)
        {
            return WaitForAnimation(entity);
        }
        if (property.Property == FieldActorPropertyKind.StopAnimation)
        {
            return StopAnimation(entity);
        }
        SetActor(entity, actor => property.Property switch
        {
            FieldActorPropertyKind.Visible => actor with { Visible = property.Value != 0 },
            FieldActorPropertyKind.Direction => actor with { Direction = property.Value & 255, TurnMode = 0, TurnStep = 0 },
            FieldActorPropertyKind.MoveSpeed => actor with { MoveSpeed = property.Value },
            FieldActorPropertyKind.AnimationSpeed => actor with { BaseAnimationSpeed = property.Value, AnimationSpeed = property.Value },
            FieldActorPropertyKind.TalkEnabled => actor with { TalkEnabled = property.Value != 0 },
            FieldActorPropertyKind.CollisionEnabled => actor with { CollisionEnabled = property.Value != 0 },
            FieldActorPropertyKind.TalkRadius => actor with { TalkRadius = property.Value },
            FieldActorPropertyKind.CollisionRadius => actor with { CollisionRadius = property.Value },
            FieldActorPropertyKind.PlayerCharacter => actor with { PlayerControlled = true },
            FieldActorPropertyKind.PartyCharacter => actor with { PartyCharacter = property.Value },
            _ => throw new NotSupportedException("Unsupported FFVII actor property.")
        });
        return new(true);
    }

    private FieldHostReply Window(FieldWindowCommand window)
    {
        if (window.Window is < 0 or > 3)
        {
            throw new InvalidDataException("FFVII window references an absent native window slot.");
        }
        Ff7Window current = Process.Windows.SingleOrDefault(item => item.Id == window.Window) ?? new Ff7Window(window.Window, 8, window.Window == 1 ? 8 : 149, 304, 74);
        switch (window.Operation)
        {
            case FieldWindowOperation.Size:
                Ff7Window configured = current with { X = window.X, Y = window.Y, Width = window.Width, Height = window.Height };
                Process = Process with { Windows = [.. Process.Windows.Where(item => item.Id != window.Window), configured] };
                break;
            case FieldWindowOperation.Special:
                Ff7Window special = current with { SpecialType = checked((byte)window.Style), SpecialX = checked((byte)window.X), SpecialY = checked((byte)window.Y) };
                Process = Process with { Windows = [.. Process.Windows.Where(item => item.Id != window.Window), special] };
                break;
            case FieldWindowOperation.Close:
            case FieldWindowOperation.CloseAndWait:
            case FieldWindowOperation.Reset:
                Process = Process with { Dialogues = Process.Dialogues.Select(item => item.Window == window.Window ? item with { Completed = true } : item).ToArray() };
                if (window.Operation == FieldWindowOperation.Reset)
                {
                    Ff7Window reset = new(window.Window, 8, window.Window == 1 ? 8 : 149, 304, 74);
                    Process = Process with { Windows = [.. Process.Windows.Where(item => item.Id != window.Window), reset] };
                }
                break;
            case FieldWindowOperation.Style:
            case FieldWindowOperation.Move:
            case FieldWindowOperation.Rows:
                Ff7Window changed = window.Operation switch
                {
                    FieldWindowOperation.Style => current with { Style = window.Style, Flags = window.Flags },
                    FieldWindowOperation.Move => current with { X = window.X, Y = window.Y },
                    _ => current with { Rows = window.Height }
                };
                Process = Process with { Windows = [.. Process.Windows.Where(item => item.Id != window.Window), changed] };
                break;
            default:
                throw new NotSupportedException("Unsupported FFVII window operation.");
        }
        return new(true);
    }

    private FieldHostReply Movie(long requestId, FieldMovieCommand command)
    {
        if (command.Operation == FieldMovieOperation.Prepare)
        {
            if (Process.GlobalCommand != Ff7GlobalCommand.None)
            {
                return new(false);
            }
            Process = Process with { Movie = new Ff7Movie(command.MovieId, false, 0, false, null, CameraConsumption: new(-1, 0, 0)) };
            return new(true);
        }
        Ff7Movie movie = Process.Movie ?? throw new InvalidDataException("FFVII movie operation requires its prepared resource.");
        switch (command.Operation)
        {
            case FieldMovieOperation.Play:
                if (Process.GlobalCommand is not (Ff7GlobalCommand.None or Ff7GlobalCommand.Movie))
                {
                    return new(false);
                }
                if (movie.RequestId is null)
                {
                    Process = Process with { Movie = movie with { Playing = true, RequestId = requestId }, GlobalCommand = Ff7GlobalCommand.Movie, GlobalRequestId = requestId };
                    return new(false);
                }
                return new(movie.Completed);
            case FieldMovieOperation.ReadFrame:
                return new(true, movie.Frame);
            case FieldMovieOperation.SetBackground:
                Process = Process with { MovieBackgroundEnabled = command.MovieId != 0 };
                return new(true);
            default:
                throw new NotSupportedException("Unsupported FFVII movie operation.");
        }
    }

    private void Background(FieldBackgroundCommand command)
    {
        if ((uint)command.Parameter >= 256 || (uint)command.State >= 16)
        {
            throw new InvalidDataException("FFVII background parameter or state is invalid.");
        }
        ushort[] states = (ushort[])Process.BackgroundStates.Clone();
        ushort value = states[command.Parameter];
        states[command.Parameter] = command.Operation switch
        {
            FieldBackgroundOperation.On => (ushort)(value | 1 << command.State),
            FieldBackgroundOperation.Off => (ushort)(value & ~(1 << command.State)),
            FieldBackgroundOperation.Clear => 0,
            FieldBackgroundOperation.Roll => (ushort)((value << 1) | (value >> 15)),
            FieldBackgroundOperation.RollBack => (ushort)((value >> 1) | (value << 15)),
            _ => throw new NotSupportedException("Unsupported FFVII background operation.")
        };
        Process = Process with { BackgroundStates = states };
    }

    private FieldHostReply Control(FieldControlCommand command)
    {
        Process = command.Property switch
        {
            FieldControlKind.PlayerLocked => SetPlayerControl(command.Value != 0),
            FieldControlKind.BattlesDisabled => Process with { BattlesDisabled = command.Value != 0 },
            FieldControlKind.MapJumpDisabled => Process with { MapJumpDisabled = command.Value != 0 },
            FieldControlKind.MenuDisabled => Process with { MenuDisabled = command.Value != 0 },
            FieldControlKind.BattleMode => Process with { BattleMode = command.Value },
            FieldControlKind.BattleMode2 => Process with { BattleMode2 = command.Value },
            FieldControlKind.BattleTable => Process with { BattleTable = command.Value },
            FieldControlKind.BattleResult => Process,
            FieldControlKind.MovieCameraDisabled => Process with { MovieCameraDisabled = command.Value == 1 },
            FieldControlKind.CursorDisabled => Process with { CursorDisabled = command.Value != 0 },
            FieldControlKind.CurrentMapName => Process with { MapName = command.Value },
            FieldControlKind.MapDisplay => Process with { MapDisplay = command.Value },
            _ => throw new NotSupportedException($"Unsupported FFVII host control {command.Property}.")
        };
        return new(true, command.Property == FieldControlKind.BattleResult ? Process.BattleResult : 0);
    }
}
