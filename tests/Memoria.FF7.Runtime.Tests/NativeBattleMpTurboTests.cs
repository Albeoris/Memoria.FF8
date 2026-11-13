using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleMpTurboTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original MP Turbo battle resources: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        SceneFormation formation = formations[300];

        Ff7Progress Progress(bool linked)
        {
            Ff7CharacterState[] characters = original.Progress.Characters.ToArray();
            Ff7CharacterState cloud = characters[0];
            byte[] record = (byte[])cloud.NativeRecord.Clone();
            record[28] = 3;
            record[29] = 3;
            record.AsSpan(64, 64).Fill(byte.MaxValue);
            Write(64, 0x31);
            Write(68, linked ? 0x1A | 10000u << 8 : 0xFF);
            Write(72, 0x17);
            Write(76, 0x31);
            Write(96, linked ? 0x1A | 60000u << 8 : 0xFF);
            Write(100, 0x31);
            Write(104, 0x1A | 120000u << 8);
            Write(108, 0x32);
            characters[0] = cloud with { NativeRecord = record };
            return original.Progress with { Party = [0, 255, 255], Characters = characters };

            void Write(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(offset), value);
            }
        }

        Ff7BattleSession Open(Ff7Progress progress, Ff7BattleSnapshot? state = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: state);

        Ff7BattleSnapshot Ready(Ff7Progress progress, ushort mp)
        {
            Ff7BattleSnapshot state = Open(progress).Capture();
            Ff7BattleActorState player = state.Actors[0];
            state.Actors[0] = player with { AwaitingTurn = true, Gauge = ushort.MaxValue, Actor = player.Actor with { MagicPoints = mp } };
            for (int i = 1; i < state.Actors.Length; i++)
            {
                Ff7BattleActorState enemy = state.Actors[i];
                state.Actors[i] = enemy with { Actor = enemy.Actor with { HitPoints = 1000, MaxHitPoints = 1000, Statuses = 0x20000 } };
            }
            return state;
        }

        Ff7Progress boosted = Progress(true);
        Ff7BattleSpell[] spells = Ff7NativeBattleSpells.Read(boosted.Characters[0], kernel).ToArray();
        Check(spells.Select(spell => (spell.AttackId, spell.Attack.CastingCost, spell.MpTurboLevel)).SequenceEqual(new (byte, ushort, byte)[] { (0x1B, 7, 5), (0x1E, 4, 0) }), "Weapon and armor MP Turbo levels must stack to five, ignoring an unlinked copy.");
        Ff7BattleSession insufficient = Open(boosted, Ready(boosted, 6));
        Check(insufficient.Commands(0).All(command => command.Command != Ff7BattleCommand.Magic || command.Argument != 0x1B), "The command menu must use the adjusted MP threshold.");
        string before = JsonSerializer.Serialize(insufficient.Capture());
        Check(!insufficient.Submit(0, Ff7BattleCommand.Magic, 0x1B, 0x30, group: true) && JsonSerializer.Serialize(insufficient.Capture()) == before, "Insufficient adjusted MP must preserve turn, RNG and resources.");

        Ff7BattleSnapshot boostedResult = Cast(boosted, 7, 7);
        Ff7BattleSnapshot ordinaryResult = Cast(Progress(false), 7, 4);
        Check(boostedResult.Actors[0].Actor.MagicPoints == 0 && ordinaryResult.Actors[0].Actor.MagicPoints == 3, "Each cast must debit its menu cost exactly once.");
        Check(boostedResult.Random.Indices.SequenceEqual(ordinaryResult.Random.Indices) && boostedResult.Random.WordCounter == ordinaryResult.Random.WordCounter, "MP Turbo must preserve the original damage RNG draws.");
        int[] normalDamage = ordinaryResult.Actors.Skip(1).Select(actor => 1000 - (int)actor.Actor.HitPoints).ToArray();
        int[] turboDamage = boostedResult.Actors.Skip(1).Select(actor => 1000 - (int)actor.Actor.HitPoints).ToArray();
        Check(normalDamage.All(damage => damage > 0) && turboDamage.Zip(normalDamage).All(pair => pair.First > pair.Second), $"Linked All + MP Turbo must boost both MBarrier targets. Normal [{string.Join(", ", normalDamage)}], Turbo [{string.Join(", ", turboDamage)}].");
        Console.WriteLine($"Original Fire with linked All against MBarrier: normal [{string.Join(", ", normalDamage)}], Turbo [{string.Join(", ", turboDamage)}], exact adjusted MP and JSON continuation.");

        Ff7BattleSnapshot Cast(Ff7Progress progress, ushort mp, int cost)
        {
            Ff7BattleSession session = Open(progress, Ready(progress, mp));
            Ff7BattleCommandOption option = session.Commands(0).Single(command => command.Command == Ff7BattleCommand.Magic && command.Argument == 0x1B);
            Check(option.Cost == cost && option.GroupUses == 1, "The command must expose the adjusted MP cost and the separate linked All use.");
            Check(session.Submit(0, Ff7BattleCommand.Magic, 0x1B, 0x30, group: true), "The original player command must accept the linked group spell.");
            string queued = JsonSerializer.Serialize(session.Capture());
            Ff7BattleSession cold = Open(progress, JsonSerializer.Deserialize<Ff7BattleSnapshot>(queued)!);
            for (int step = 0; step < 60; step++)
            {
                session.Advance(waitForCommand: true);
                cold.Advance(waitForCommand: true);
                Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Cold continuation diverged across the queued spell.");
                if (cold.Action is null && cold.LastHit is { Source: 0, AttackId: 0x1B })
                {
                    Ff7BattleSnapshot result = cold.Capture();
                    Check(result.Actors[0].AllUses?.GetValueOrDefault((byte)0x1B) == 0, "The group spell must consume one linked All use.");
                    return result;
                }
            }
            throw new InvalidOperationException("The original player command did not reach action completion.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
