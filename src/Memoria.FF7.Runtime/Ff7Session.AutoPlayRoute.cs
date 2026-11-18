using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7AutoPlayRouteStep(string Before, Ff7AutoplayInput Input);
public sealed record Ff7AutoPlayNativeRoute(string EventId, string ConditionFingerprint, long Generation, Ff7AutoPlayRouteStep[] Steps, string After, bool BattleBoundary = false, int Cursor = 0);

public sealed partial class Ff7Session
{
    internal Ff7Session ForkAutoPlayNavigation()
    {
        return Restore(installation, Capture());
    }
}

public sealed partial class Ff7AutoplayDriver
{
    internal string? LastNativeRouteProbe { get; private set; }

    internal Ff7AutoPlayNativeRoute? ProveNativeRoute(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        if (target.Kind is < Ff7AutoPlayTargetKind.LineTalk or > Ff7AutoPlayTargetKind.LineLeave)
        {
            return null;
        }
        if (!HasNativeRouteClockContract(session))
        {
            return null;
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        int initialDistance = ReadNativeRouteDistance(target, targets);
        if (initialDistance == int.MaxValue)
        {
            return null;
        }
        foreach (bool run in new[] { true, false })
        {
            foreach (var direction in Directions)
            {
                Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(run)) };
                Ff7NativeMoveResult move = session.Host.Movement!.StepDirection(controlled, unchecked((byte)(direction.Direction + session.Field.Triggers.ControlDirection)), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
                if (!move.Moved || !AllowsGatewayMove(session.Field.Triggers.Gateways, controlled, move.Actor, ReadTargetGatewayIndices(target, session.Field.Triggers.Gateways)) || !AllowsSpatialMove(controlled, move.Actor, ReadSpatialBoundaryTriangles(targets), target.Triangle))
                {
                    continue;
                }
                using Ff7Session branch = session.ForkAutoPlayNavigation();
                Ff7AutoplayDriver driver = new(branch);
                driver.ConfigureSpatialBoundaries(targets, target);
                List<Ff7AutoPlayRouteStep> steps = [];
                bool accepted = false;
                bool ladderObserved = false;
                bool returned = false;
                bool clockAccess = false;
                string? boundary = null;
                branch.InteractionScriptRequested += (entity, script, admitted) => accepted |= admitted && entity == target.Entity && script == target.Script;
                branch.Machine.InstructionVariableAccessed += (access, _) => clockAccess |= IsNativeClockAccess(access);
                try
                {
                    for (int callback = 0; callback < 256; callback++)
                    {
                        Ff7Actor current = branch.Host.Process.Actors.Single(actor => actor.PlayerControlled);
                        Ff7AutoplayInput input;
                        if (callback == 0)
                        {
                            input = new(direction.X, direction.Y, Run: run);
                        }
                        else if (current.Ladder is { Completed: false } ladder)
                        {
                            ladderObserved = true;
                            input = new(Held: BackwardLadderKey(ladder.Mode), Run: false);
                        }
                        else if (callback < 4 && !ladderObserved || current.Ladder is not null || branch.Host.Process.PlayerLocked)
                        {
                            input = default;
                        }
                        else
                        {
                            if (!ladderObserved)
                            {
                                break;
                            }
                            returned = true;
                            accepted = false;
                            try
                            {
                                input = driver.ChooseTargetInput(target, targets, mode => driver.ApproachLine(target.Entity, target.Kind, mode));
                            }
                            catch (Ff7AutoPlayNavigationUnavailableException)
                            {
                                input = driver.ApproachLine(target.Entity, target.Kind, false);
                            }
                        }
                        steps.Add(new(ReadNativeRouteFingerprint(branch), input));
                        driver.Apply(input);
                        branch.AdvanceTick(false);
                        if (clockAccess || !HasNativeRouteClockContract(branch))
                        {
                            break;
                        }
                        if (accepted && !returned)
                        {
                            break;
                        }
                        if (branch.Battle is not null || branch.World is not null || branch.FieldGeneration != session.FieldGeneration)
                        {
                            boundary = $"battle:{branch.Battle is not null}, world:{branch.World is not null}, generation:{branch.FieldGeneration}";
                            if (branch.Battle is not null && ladderObserved && returned && branch.FieldGeneration == session.FieldGeneration && driver.ReadNativeRouteDistance(target, targets) < initialDistance)
                            {
                                LastNativeRouteProbe = $"Native battle handoff after {steps.Count} callbacks; route distance {initialDistance}->{driver.ReadNativeRouteDistance(target, targets)}";
                                return new(target.Id, target.ConditionFingerprint, session.FieldGeneration, steps.ToArray(), ReadNativeRouteFingerprint(branch), BattleBoundary: true);
                            }
                            break;
                        }
                        if (accepted && returned)
                        {
                            LastNativeRouteProbe = $"Accepted {target.Id} after {steps.Count} native callbacks";
                            return new(target.Id, target.ConditionFingerprint, session.FieldGeneration, steps.ToArray(), ReadNativeRouteFingerprint(branch));
                        }
                    }
                    LastNativeRouteProbe = $"No final admission: {target.Id}, callbacks {steps.Count}, triangle {branch.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle}, boundary:{boundary}";
                }
                catch (Ff7AutoPlayNavigationUnavailableException error)
                {
                    LastNativeRouteProbe = $"{target.Id}, callbacks {steps.Count}: {error.Message}";
                }
            }
        }
        return null;
    }

    internal static ushort BackwardLadderKey(int mode)
    {
        return mode switch
        {
            0 => 0x1000,
            1 => 0x4000,
            2 => 0x8000,
            3 => 0x2000,
            _ => throw new NotSupportedException("The native ladder mode is unsupported.")
        };
    }

    internal static string ReadNativeRouteFingerprint(Ff7Session source)
    {
        return ReadNativeRouteFingerprint(source.Capture());
    }

    internal static string ReadNativeRouteFingerprint(Ff7SessionSave state)
    {
        // A route is admitted only after every executed instruction has proved
        // independent of these host clock publications, with no active timer/fade.
        byte[] persistent = state.Vm.PersistentVariables.ToArray();
        Ff7BattleSnapshot? battle = state.Battle;
        if (battle is { Tick: 0, AiSaveBank1: { Length: >= 24 } bank } && state.Process.EncounterRequest is not null && bank.AsSpan(16, 8).SequenceEqual(persistent.AsSpan(16, 8)))
        {
            // A field battle copies the same clock publication into its initial
            // AI save bank. No battle callback is part of the navigation proof.
            byte[] initialBank = bank.ToArray();
            Array.Clear(initialBank, 16, 8);
            battle = battle with { AiSaveBank1 = initialBank };
        }
        Array.Clear(persistent, 16, 8);
        state = state with { AutoPlay = null, EventCoverage = null, Progress = state.Progress with { Clock = new(CountdownSeconds: state.Progress.Clock?.CountdownSeconds ?? 0) }, Vm = state.Vm with { PersistentVariables = persistent }, Process = state.Process with { SoundVolumeClockSubmillisecondTicks = 0 }, Battle = battle };
        string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
        return hash;
    }

    internal static bool IsNativeClockAccess(FieldVariableRead access)
    {
        if (access.Bank is not (FieldBank.Persistent0Byte or FieldBank.Persistent0Word))
        {
            return false;
        }
        int address = unchecked((byte)access.Operand);
        int width = access.Word && access.Bank == FieldBank.Persistent0Word ? 2 : 1;
        return address < 24 && address + width > 16;
    }

    internal static bool HasNativeRouteClockContract(Ff7Session source)
    {
        Ff7FieldProcess process = source.Host.Process;
        return source.Clock.CountdownSeconds == 0 && !process.Dialogues.Any(dialogue => !dialogue.Completed) && process.Menu is not { Completed: false } && process.MusicVolumeFade is null && process.SoundChannelVolumeFades.All(fade => fade is null) && process.SoundChannelPanFades.All(fade => fade is null) && process.SoundChannelFrequencyFades.All(fade => fade is null);
    }

    private int ReadNativeRouteDistance(Ff7AutoPlayTarget target, IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Line line = session.Host.Process.Lines.Last(line => line.Entity == target.Entity);
        var contacts = DiscoverLineContacts(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, line);
        return contacts.Select(contact => ReadApproachRoutes(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle, contact.Triangle, ReadSpatialBoundaryTriangles(targets), target.Triangle).Distance[player.Triangle]).DefaultIfEmpty(int.MaxValue).Min();
    }
}
