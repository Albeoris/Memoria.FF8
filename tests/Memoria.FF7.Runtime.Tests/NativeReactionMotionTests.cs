using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeReactionMotionTests
{
    public static void Run()
    {
        Ff7BattleMotion wrapped = new(new(32760, 123456, -32760), new(30000, 0, -30000), 3) { WrapHorizontalPosition = true };
        wrapped = wrapped.Advance();
        Check(wrapped.Position == new Ff7BattlePosition(-2776, 123456, 2776) && wrapped.Remaining == 2, "Signed C8 steps must wrap only horizontal WORD positions on their first callback.");
        wrapped = wrapped.Advance().Advance();
        Check(wrapped.Position == new Ff7BattlePosition(-8312, 123456, 8312) && wrapped.Remaining == 0, "C8 count must retire after exactly the unsigned operand's number of movements.");
        Check(wrapped.Advance() == wrapped, "A retired C8 callback must not move again.");
        Ff7BattleMotion empty = new(new(10, 20, 30), new(-1, 0, 1), 0) { WrapHorizontalPosition = true };
        Check(empty.Advance() == empty, "A zero-duration C8 callback must not move.");

        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed C8 reaction motion: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 255, 255] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        IReadOnlyList<short> materia = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(models, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));

        foreach (int formationId in new[] { 84, 85, 86, 87 })
        {
            foreach ((int slot, short action) in formationId == 84 ? new (int, short)[] { (7, 7), (4, 9), (5, 10), (6, 11) } : [(7, 7)])
            {
                SceneFormation formation = formations[formationId];
                Ff7BattleSession initial = new(formation, progress, kernel, materia, 1, animations: animations);
                Ff7BattleSnapshot snapshot = Ready(initial.Capture(), slot, action);
                Ff7BattleSession battle = Restore(formation, snapshot);
                Ff7BattlePosition start = Ff7BattlePosition.FromVector(battle.VisualPlacement(slot).Position);
                short enemy = formation.Enemies[slot - 4].EnemyId;
                string prefix = Ff7BattleAnimationResources.ModelPrefix(enemy, true);
                BattleActionBank bank = BattleActionBank.Read(Read(prefix + "ab"));
                BattleModelData model = BattleModelData.Read(Read(prefix + "aa"), Read(prefix + "da"));
                IReadOnlyList<BattleActionInstruction> moves = BattleActionInstructions.ReadUntilIdle(bank.Scripts[action], model.BodyClips.Count).Where(instruction => instruction.Opcode == 0xC8).ToArray();
                Check(moves.Count == 2, "The reached original linked placement must contain both C8 relocations.");
                Ff7BattlePosition expected = start;
                foreach (BattleActionInstruction move in moves)
                {
                    ReadOnlySpan<byte> operands = move.Operands.Span;
                    for (int count = 0; count < operands[4]; count++)
                    {
                        expected = new(unchecked((short)(expected.X + BinaryPrimitives.ReadInt16LittleEndian(operands))), expected.Y, unchecked((short)(expected.Z + BinaryPrimitives.ReadInt16LittleEndian(operands[2..]))));
                    }
                }
                Ff7BattleSession? cold = null;
                bool completed = false;
                for (int tick = 0; tick < 400 && !completed; tick++)
                {
                    battle.Advance(waitForCommand: true);
                    Ff7BattleSnapshot current = battle.Capture();
                    if (cold is not null)
                    {
                        cold.Advance(waitForCommand: true);
                        Check(JsonSerializer.Serialize(current) == JsonSerializer.Serialize(cold.Capture()), "Cold reaction C8 continuation changed the next native callback.");
                    }
                    Ff7BattleActorModelState actor = current.ActorModels.Single(candidate => candidate.Slot == slot);
                    if (cold is null && actor.Position is not null)
                    {
                        cold = Restore(formation, JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(current))!);
                    }
                    Check(battle.RenderActorPlacement(slot).Position == battle.VisualPlacement(slot).Position, "The moving reaction model did not reach the ordinary rendered actor placement.");
                    completed = actor.Ready && !actor.Reaction;
                }
                Check(cold is not null && completed && Ff7BattlePosition.FromVector(battle.VisualPlacement(slot).Position) == expected, $"Original reaction action {action} in formation {formationId} did not finish both native relocations.");
            }
        }

        foreach (bool early in new[] { false, true })
        {
            SceneFormation formation = formations[84];
            const int slot = 4;
            const short action = 127;
            string prefix = Ff7BattleAnimationResources.ModelPrefix(formation.Enemies[slot - 4].EnemyId, true);
            BattleActionBank bank = BattleActionBank.Read(Read(prefix + "ab"));
            BattleModelData model = BattleModelData.Read(Read(prefix + "aa"), Read(prefix + "da"));
            byte[] script = [0xC8, 0x30, 0x75, 0xD0, 0x8A, 3, 0xEE];
            BattleActionSequence sequence = BattleActionSequence.Read(bank with { Scripts = [script] }, model, 0);
            var cache = (Dictionary<(string, int, ushort?), BattleActionSequence>)typeof(Ff7BattleAnimationResources).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(animations)!;
            cache[(prefix, action, null)] = sequence;
            Ff7BattleSession initial = new(formation, progress, kernel, materia, 1, animations: animations);
            Ff7BattleSnapshot snapshot = Ready(initial.Capture(), slot, action);
            Ff7BattlePosition start = new(32760, 123456, -32760);
            snapshot = snapshot with { ActorModels = snapshot.ActorModels.Select(actor => actor.Slot == slot ? actor with { Position = start, LinkedAction = false } : actor).ToArray() };
            Ff7BattleSession battle = Restore(formation, snapshot);
            if (early)
            {
                Invoke(battle, "BeginCameraPresentation");
                Invoke(battle, "AdvanceActorReactionModels", slot, slot + 1);
                Invoke(battle, "AdvanceDetachedActorMotion");
            }
            else
            {
                battle.Advance(waitForCommand: true);
            }
            Ff7BattleActorModelState first = battle.Capture().ActorModels.Single(actor => actor.Slot == slot);
            Check(first.Position == new Ff7BattlePosition(-2776, 123456, 2776) && first.Motion?.Remaining == 2 && !first.Reaction && first.Ready, "Reaction C8 must move once on admission and survive same-callback idle transfer in either ordered actor phase.");
            Ff7BattleSession cold = Restore(formation, JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            Ff7BattlePosition expected = first.Position ?? throw new InvalidOperationException("The admitted C8 motion has no position.");
            for (int callback = 0; callback < 3; callback++)
            {
                long previous = battle.PresentationTick;
                do
                {
                    battle.Advance(waitForCommand: true);
                    cold.Advance(waitForCommand: true);
                    Check(JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Capture()), "A detached reaction motion did not restore exactly.");
                }
                while (battle.PresentationTick == previous);
                if (callback < 2)
                {
                    expected = new(unchecked((short)(expected.X + 30000)), expected.Y, unchecked((short)(expected.Z - 30000)));
                }
                Ff7BattleActorModelState current = battle.Capture().ActorModels.Single(actor => actor.Slot == slot);
                Check(current.Position == expected && (callback == 0 ? current.Motion?.Remaining == 1 : current.Motion is null), "Detached C8 motion advanced more than once, lost idle ownership or failed to retire.");
            }
        }

        byte[] Read(string name) => models.ReadEntry(models.Find(name).Single());
        Ff7BattleSession Restore(SceneFormation formation, Ff7BattleSnapshot snapshot) => new(formation, progress, kernel, materia, 1, snapshot: snapshot, animations: animations);
        Console.WriteLine("PASS native C8 reaction ownership, WORD motion, ordered callbacks, idle retention and cold continuation.");
    }

    private static Ff7BattleSnapshot Ready(Ff7BattleSnapshot snapshot, int slot, short action)
    {
        return snapshot with
        {
            Intro = null,
            Actors = snapshot.Actors.Select(actor => actor with { AwaitingTurn = actor.Actor.Slot == 0, Gauge = actor.Actor.Slot == 0 ? ushort.MaxValue : (ushort)0 }).ToArray(),
            ActorModels = snapshot.ActorModels.Select(actor => actor.Slot == slot ? actor with { Action = action, Ready = false, Pose = null, Reaction = true, LinkedAction = true, PendingSince = -1, IdleStarted = null } : actor).ToArray()
        };
    }

    private static void Invoke(Ff7BattleSession battle, string name, params object[] arguments)
    {
        typeof(Ff7BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, arguments);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
