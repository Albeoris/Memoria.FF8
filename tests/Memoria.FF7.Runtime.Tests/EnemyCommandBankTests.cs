using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class EnemyCommandBankTests
{
    public static void Run()
    {
        foreach ((ushort attack, Ff7BattleCommand bank, ushort argument) in new (ushort, Ff7BattleCommand, ushort)[]
        {
            (0, Ff7BattleCommand.Magic, 0), (0x37, Ff7BattleCommand.Magic, 0x37),
            (0x38, Ff7BattleCommand.Summon, 0), (0x47, Ff7BattleCommand.Summon, 15),
            (0x48, Ff7BattleCommand.EnemySkill, 0), (0x52, Ff7BattleCommand.EnemySkill, 10), (0x5F, Ff7BattleCommand.EnemySkill, 23),
            (0x60, Ff7BattleCommand.Limit, 0), (0xFF, Ff7BattleCommand.Limit, 159)
        })
        {
            Check(Ff7NativeEnemyCommandBanks.Resolve(attack) == (bank, argument), "Native global enemy command bank boundary or relative argument changed.");
        }
        Ff7BattleQueuedCommand valid = new(2, 4, (byte)Ff7BattleCommand.EnemySkill, 10, 1, 0x52);
        Ff7BattleCommandQueue.ValidateCommand(valid);
        Reject(() => Ff7BattleCommandQueue.ValidateCommand(valid with { AttackId = 0x53 }));
        Reject(() => Ff7BattleCommandQueue.ValidateCommand(valid with { Argument = 24 }));
        Reject(() => Ff7BattleCommandQueue.ValidateCommand(valid with { ActorSlot = 0 }));
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed enemy command banks: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 255, 255] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> scenes = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        TestMatraMagic(scenes.Single(scene => scene.FormationId == 33), progress, kernel, executable.NativeMateriaStatModifiers, animations);
        using Ff7WorldResources world = Ff7WorldResources.Load(installation);
        byte[] table = world.ReadRequired("enc_w.bin");
        HashSet<int> grasslands = [];
        for (int ground = 0; ground < 4; ground++)
        {
            for (int offset = 2; offset < 32; offset += 2)
            {
                ushort entry = BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan(0xA0 + ground * 32 + offset));
                if (entry >> 10 != 0)
                {
                    grasslands.Add(entry & 1023);
                }
            }
        }
        foreach (int id in grasslands.Order())
        {
            SceneFormation formation = scenes.Single(scene => scene.FormationId == id);
            Ff7BattleSession initial = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, id + 1);
            Ff7BattleSnapshot snapshot = initial.Capture();
            snapshot = snapshot with { Actors = snapshot.Actors.Select(actor => actor.Actor.Slot < 3 ? actor with { Actor = actor.Actor with { HitPoints = 10000, MaxHitPoints = 10000 } } : actor).ToArray() };
            Ff7BattleSession warm = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, id + 1, snapshot: snapshot);
            Ff7BattleSession cold = Restore(formation, progress, kernel, executable.NativeMateriaStatModifiers, warm);
            for (int tick = 0; tick < 6000 && warm.LastHit is null; tick++)
            {
                warm.Advance();
                cold.Advance();
            }
            Check(warm.LastHit is { Source: >= 4 }, $"Grasslands formation {id} did not execute an original enemy turn.");
            Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), $"Grasslands formation {id} changed initialization or first turn after JSON restore.");
            cold = Restore(formation, progress, kernel, executable.NativeMateriaStatModifiers, warm);
            for (int tick = 0; tick < 8; tick++)
            {
                warm.Advance();
                cold.Advance();
            }
            Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), $"Grasslands formation {id} changed its next callback after the enemy turn.");
        }
        Console.WriteLine($"Native enemy command bank boundaries, Matra Magic and {grasslands.Count} original Grasslands initialization/turn/cold continuations passed.");
    }

    private static void TestMatraMagic(SceneFormation formation, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers, Ff7BattleAnimationResources animations)
    {
        SceneBattleData data = SceneBattleData.Read(formation);
        SceneBattleEnemy enemy = data.Enemies.First(enemy => enemy.Attacks.Any(attack => attack.AttackId == 0x52));
        SceneEnemyAttack selector = enemy.Attacks.Single(attack => attack.AttackId == 0x52);
        SceneBattleAttack record = data.Attacks.Single(attack => attack.Id == 0x52);
        Ff7BattleAttack global = Ff7BattleAttack.Read(kernel[1].Data.Span.Slice(0x52 * 28, 28));
        Check(global.CastingCost == 8 && selector.ActionAnimation == 4, "Installed Matra Magic contract changed.");
        Ff7BattleSession initial = new(formation, progress, kernel, modifiers, 1, animations: animations);
        initial.BindGuestVitals(new(Guid.NewGuid(), new('A', 64), 0, 0, 0, 0, 486, 486, 6, 14, 24, 17));
        Ff7BattleSnapshot snapshot = initial.Capture();
        int slot = enemy.FormationSlot + 4;
        uint initialMp = snapshot.Actors.Single(actor => actor.Actor.Slot == slot).Actor.MagicPoints;
        snapshot = snapshot with { Queue = Ff7BattleCommandQueue.Enqueue(snapshot.Queue, new(2, slot, 0x20, checked((ushort)record.Slot), 1, 0x52)) };
        Ff7BattleSession warm = new(formation, progress, kernel, modifiers, 1, snapshot: snapshot, animations: animations);
        bool seen = false;
        bool restored = false;
        for (int tick = 0; tick < 3500; tick++)
        {
            warm.Advance(waitForCommand: true);
            if (warm.Action is { Command.CommandId: (byte)Ff7BattleCommand.EnemySkill } action)
            {
                seen = true;
                Check(action.Command.Argument == 10 && action.Command.AttackId == 0x52 && action.Animation == selector.ActionAnimation, "Enemy skill lost its relative bank argument, global identity or original caster animation.");
                if (!restored && action.Applied)
                {
                    Ff7BattleSession cold = Restore(formation, progress, kernel, modifiers, warm, animations);
                    for (int next = 0; next < 4; next++)
                    {
                        warm.Advance(waitForCommand: true);
                        cold.Advance(waitForCommand: true);
                    }
                    Check(JsonSerializer.Serialize(warm.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Live Matra Magic changed after JSON restore.");
                    warm = cold;
                    restored = true;
                }
            }
            if (seen && warm.Action is null)
            {
                Check(restored && warm.LastHit is { Source: >= 4, AttackId: 0x52 } && warm.Actors.Single(actor => actor.Actor.Slot == slot).Actor.MagicPoints == initialMp - 8, "Matra Magic did not finish with its original global hit identity and one MP charge.");
                return;
            }
        }
        throw new InvalidOperationException("Original Matra Magic did not finish its enemy action.");
    }

    private static Ff7BattleSession Restore(SceneFormation formation, Ff7Progress progress, IReadOnlyList<KernelSection> kernel, IReadOnlyList<short> modifiers, Ff7BattleSession source, Ff7BattleAnimationResources? animations = null)
    {
        Ff7BattleSnapshot snapshot = JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(source.Capture()))!;
        return new(formation, progress, kernel, modifiers, source.RequestId, snapshot: snapshot, animations: animations);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("An invalid enemy skill bank identity was accepted.");
    }
}
