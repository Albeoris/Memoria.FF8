using System.Globalization;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7Session? navigationSession;
    private Ff7AutoplayDriver? navigationDriver;

    public object ObserveDiagnostic()
    {
        Ff7Session session = Session ?? throw new InvalidOperationException("FFVII observation requires a loaded session.");
        Ff7FieldProcess process = session.Host.Process;
        Ff7WorldProcess? world = session.World?.Process;
        Ff7BattleSession? battle = session.Battle;
        return new
        {
            Field = session.Field.Name,
            session.MapId,
            session.FieldGeneration,
            process.Tick,
            session.HasPendingTick,
            Error,
            AutoPlay = new { Status = AutoPlayStatus, Running = AutoPlayRunning, Paused = AutoPlayPaused, Error = AutoPlayError },
            Player = process.Actors.Where(actor => actor.PlayerControlled).Select(ObserveActor).SingleOrDefault(),
            Locks = new { process.PlayerLocked, process.MenuDisabled, process.MapJumpDisabled, process.LineBlocked, process.Join, process.GlobalCommand, process.Transition },
            Actors = process.Actors.Take(256).Select(ObserveActor).ToArray(),
            Lines = process.Lines.Take(256).ToArray(),
            Gateways = session.Field.Triggers.Gateways.Take(256).Select(gateway => new { gateway.DestinationField, gateway.DestinationTriangle, A = new { gateway.A.X, gateway.A.Y, gateway.A.Z }, B = new { gateway.B.X, gateway.B.Y, gateway.B.Z } }).ToArray(),
            Dialogues = (world?.Dialogues ?? process.Dialogues).Where(dialogue => !dialogue.Completed).Take(32).Select(dialogue => ObserveDialogue(session, dialogue)).ToArray(),
            process.Movie,
            Menu = process.Menu is null ? null : new { process.Menu.RequestId, process.Menu.Mode, process.Menu.Parameter, process.Menu.Completed },
            Battle = battle is null ? null : new { battle.Tick, battle.Outcome, battle.ResultReady, battle.ReadyPlayer, battle.Message, Actors = battle.Actors.Take(16).Select(state => new { state.Identity, state.Actor.Slot, Name = battle.Name(state.Actor.Slot), state.Actor.HitPoints, state.Actor.MaxHitPoints, state.Actor.MagicPoints, state.Actor.MaxMagicPoints, state.Actor.Statuses, state.AwaitingTurn }).ToArray() },
            BattleInput = battle is null ? null : new { Owner = battleInputOwner, Category = highlightedBattleCategory?.Name, Page = battleCommandPage?.Name, Argument = highlightedBattleArgument, TargetSlot = highlightedBattleTarget?.Slot, TargetIdentity = highlightedBattleTarget?.Identity, Selected = selectedBattleCommand, Unavailable = unavailableBattleCommand, Group = battleTargetGroup },
            World = world is null ? null : new { world.WorldId, world.Tick, world.ControlledModel, world.MovementEnabled, world.ControlLock, world.MainState, Player = world.Entities.Where(entity => entity.Model == world.ControlledModel).Select(entity => new { entity.Model, entity.X, entity.Y, entity.Z, entity.Direction }).FirstOrDefault() },
            StoryProgress = session.World is null ? session.Machine.Variables.ReadWord(FieldBank.Persistent0Word, 0) : session.World.ReadPersistentByte(0) | session.World.ReadPersistentByte(1) << 8,
            Party = session.Progress.Party.Where(identity => identity != 255).Select(identity =>
            {
                int recordId = Ff7NativeCharacterIdentity.RecordId(identity);
                Ff7CharacterState character = session.Progress.Characters.Single(character => character.Id == recordId);
                return new { Id = identity, RecordId = recordId, Name = Ff7CharacterDisplayName.Resolve(session.Progress.Characters, identity), character.HitPoints, character.MagicPoints };
            }).ToArray()
        };
    }

    private static object ObserveActor(Ff7Actor actor)
    {
        return new { actor.Slot, actor.Entity, actor.Model, actor.PartyCharacter, actor.PlayerControlled, actor.Visible, actor.TalkEnabled, actor.CollisionEnabled, actor.X4096, actor.Y4096, actor.Z4096, actor.Triangle, actor.Direction, actor.TalkRadius, actor.CollisionRadius };
    }

    private static object ObserveDialogue(Ff7Session session, Ff7Dialogue dialogue)
    {
        IReadOnlyList<ReadOnlyMemory<byte>> texts = session.World?.Texts ?? session.Field.Scripts.Texts;
        IReadOnlyList<FieldTextToken> tokens = FieldText.Read(texts[dialogue.Text].Span);
        object[] readable = tokens.Take(2048).Select(token => (object)new { token.Kind, token.Offset, token.Code, token.Argument, token.Argument2, Text = DiagnosticTokenText(session, dialogue, token) }).ToArray();
        return new { State = dialogue, Tokens = readable, Truncated = tokens.Count > 2048 };
    }

    private static string DiagnosticTokenText(Ff7Session session, Ff7Dialogue dialogue, FieldTextToken token)
    {
        return token.Kind switch
        {
            FieldTextTokenKind.Glyph or FieldTextTokenKind.CharacterName or FieldTextTokenKind.PartyName or FieldTextTokenKind.DecimalVariable or FieldTextTokenKind.HexVariable or FieldTextTokenKind.RightAlignedVariable => session.ExpandDialogueToken(dialogue, token),
            FieldTextTokenKind.NewLine or FieldTextTokenKind.NewPage => "\n",
            FieldTextTokenKind.ChoiceTab or FieldTextTokenKind.Tab => "\t",
            _ => $"[{token.Kind}:{token.Code:X2}]"
        };
    }

    public Ff7AutoplayInput PlanDiagnostic(string target)
    {
        Ff7Session session = Session ?? throw new InvalidOperationException("FFVII planning requires a loaded session.");
        if (session.World is not null || session.Battle is not null)
        {
            throw new NotSupportedException("FFVII target planning requires an active field outside battle.");
        }
        Func<Ff7AutoplayDriver, Ff7AutoplayInput> plan = ParseDiagnosticTarget(session, target);
        if (!ReferenceEquals(navigationSession, session))
        {
            navigationSession = session;
            navigationDriver = new(session);
        }
        return plan(navigationDriver!);
    }

    private static Func<Ff7AutoplayDriver, Ff7AutoplayInput> ParseDiagnosticTarget(Ff7Session session, string target)
    {
        string[] parts = target.Split(':', StringSplitOptions.TrimEntries);
        int Parse(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        if (parts.Length == 2 && parts[0] == "gateway-index")
        {
            int index = Parse(parts[1]);
            if ((uint)index >= session.Field.Triggers.Gateways.Count || session.Field.Triggers.Gateways[index].DestinationField == short.MaxValue)
            {
                throw new ArgumentException("The gateway index must identify an active current field gateway.", nameof(target));
            }
            return driver => driver.GatewayAt(index);
        }
        if (parts.Length == 2 && parts[0] == "gateway")
        {
            int destination = Parse(parts[1]);
            if (session.Field.Triggers.Gateways.Count(gateway => gateway.DestinationField == destination) != 1)
            {
                throw new ArgumentException("The gateway destination must identify exactly one current field gateway.", nameof(target));
            }
            return driver => driver.Gateway(destination);
        }
        if (parts.Length == 2 && parts[0] == "actor")
        {
            int slot = Parse(parts[1]);
            Ff7Actor actor = session.Host.Process.Actors.SingleOrDefault(actor => actor.Slot == slot) ?? throw new ArgumentException("The actor slot is absent from the current field.", nameof(target));
            if (actor.PlayerControlled || !actor.EntityBound || !actor.TalkEnabled)
            {
                throw new ArgumentException("The actor slot must identify an enabled interaction target.", nameof(target));
            }
            return driver => driver.TalkTo(actor.Entity);
        }
        if (parts.Length == 2 && parts[0] == "line")
        {
            int entity = Parse(parts[1]);
            if (!session.Host.Process.Lines.Any(line => line.Entity == entity && line.Enabled == 1))
            {
                throw new ArgumentException("The line entity must identify an enabled current field line.", nameof(target));
            }
            return driver => driver.TalkToLine(entity);
        }
        if (parts.Length is 2 or 3 && parts[0] == "point")
        {
            string[] coordinates = parts[1].Split(',', StringSplitOptions.TrimEntries);
            if (coordinates.Length != 3)
            {
                throw new ArgumentException("A point requires x,y,z coordinates.", nameof(target));
            }
            float[] values = coordinates.Select(value => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
            if (values.Any(value => !float.IsFinite(value)))
            {
                throw new ArgumentException("Point coordinates must be finite.", nameof(target));
            }
            int? triangle = parts.Length == 3 ? Parse(parts[2]) : null;
            if (triangle is not null && !session.Field.Walkmesh.Any(item => item.Index == triangle))
            {
                throw new ArgumentException("The point triangle is absent from the current field.", nameof(target));
            }
            Vector3 point = new(values[0], values[1], values[2]);
            return driver => driver.Approach(point, triangle);
        }
        throw new ArgumentException("Targets: gateway:<mapId>, gateway-index:<index>, actor:<slot>, line:<entity>, point:<x>,<y>,<z>[:<triangle>].", nameof(target));
    }
}
