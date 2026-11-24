using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCommandProjectionTests
{
    public static void Run()
    {
        CheckGenericPassiveProjection();
        CheckOriginalCommandProjection();
    }

    private static void CheckGenericPassiveProjection()
    {
        KernelSection[] kernel = Kernel();
        short[] modifiers = new short[17 * 8];
        Ff7CharacterState bare = Character();
        Ff7BattlePlayerStats baseline = Ff7NativeBattlePlayerStats.Create(bare, kernel, modifiers);
        foreach (var (id, category, command) in new (byte, byte, byte)[] { (77, 0xF6, 0x1F), (91, 0x06, 0x0B), (53, 0xA6, 6) })
        {
            KernelSection[] authored = WithCommand(kernel, id, category, command, byte.MaxValue);
            Ff7CharacterState equipped = Equip(bare, id);
            string before = JsonSerializer.Serialize(equipped);
            Ff7BattlePlayerStats actual = Ff7NativeBattlePlayerStats.Create(equipped, authored, modifiers);
            Check(actual == (baseline with { Actor = baseline.Actor with { EquippedMateriaCount = 1 } }), "A native class6 command invented passive statistics or depended on a particular materia id, high nibble or command parameter.");
            Check(before == JsonSerializer.Serialize(equipped), "Projecting native command materia changed its source character.");
        }
        short[] row = [7, -3, 4, 6, 9, -2, 0, 0];
        row.CopyTo(modifiers, 5 * 8);
        Ff7BattlePlayerStats modified = Ff7NativeBattlePlayerStats.Create(Equip(bare, 77), WithCommand(kernel, 77, 0xF6, 0x1F, 5), modifiers);
        Check(modified.Actor.Attack == baseline.Actor.Attack + 7 && modified.Actor.Defense == baseline.Actor.Defense - 3 && modified.Actor.Magic == baseline.Actor.Magic + 4 && modified.Actor.MagicDefense == baseline.Actor.MagicDefense + 6 && modified.Actor.Dexterity == baseline.Actor.Dexterity + 9 && modified.Actor.Luck == baseline.Actor.Luck - 2, "Generic command admission skipped its ordinary universal statistic modifier row.");
        Reject<NotSupportedException>(() => Ff7NativeBattlePlayerStats.Create(Equip(bare, 77), WithCommand(kernel, 77, 0x0E, 0x1F, byte.MaxValue), modifiers));
        Reject<InvalidDataException>(() => Ff7NativeBattlePlayerStats.Create(Equip(bare, 77), WithCommand(kernel, 77, 0xF6, 0x1F, 17), modifiers));
        Reject<InvalidDataException>(() => Ff7NativeBattlePlayerStats.Create(Equip(bare, 96), kernel, modifiers));
    }

    private static void CheckOriginalCommandProjection()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP original command projection: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(value => value.FormationId == 516);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleCommandResources names = Ff7BattleCommandResources.Load(installation);
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_COMMAND_SAVE");
        byte[]? bytes = null;
        Ff7Progress progress;
        if (!string.IsNullOrWhiteSpace(path))
        {
            bytes = File.ReadAllBytes(path);
            using MemoryStream input = new(bytes, writable: false);
            progress = (CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The command fixture requires an FFVII continuation.")).Progress;
        }
        else
        {
            using Ff7Session original = Ff7Session.NewGame(installation);
            Ff7CharacterNamingResources naming = Ff7CharacterNamingResources.Load(installation);
            byte[] record = naming.CreateTemplate(6);
            progress = original.Progress with { Party = [6, 255, 255], Characters = original.Progress.Characters.Select(character => character.Id == 6 ? character with { Name = Ff7NativeSaveSlot.ReadText(record.AsSpan(16, 12)), Level = record[1], Experience = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(60)), HitPoints = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(44)), MagicPoints = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(48)), NativeRecord = record } : character).ToArray() };
            progress = Ff7NativePartyRecovery.Publish(progress, Ff7MateriaResources.LoadRecords(installation), executable.NativeMateriaStatModifiers).Progress;
        }
        Ff7CharacterState cait = progress.Characters.Single(character => character.Id == 6);
        Check(cait.NativeRecord[0] == 6 && cait.Level is >= 1 and <= 99, "The command projection fixture must contain an initialized Cait native profile.");
        if (!progress.Party.Contains(6))
        {
            progress = progress with { Party = [6, 255, 255] };
        }
        int slot = Array.IndexOf(progress.Party, 6);
        string beforeProgress = JsonSerializer.Serialize(progress);
        Ff7BattleSession initial = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, commandResources: names);
        Ff7BattleSnapshot ready = Ready(initial.Capture(), slot);
        Ff7BattleSession battle = Restore(ready);
        Check(battle.ReadyPlayer == slot, "The prepared Cait command boundary did not retain its physical ready slot.");
        IReadOnlyList<Ff7NativeBattleCommandGrant> grants = battle.NativeCommandGrants(slot);
        Check(grants.Any(grant => grant.Command == 0x0B), "The original Cait profile lost its native Manipulate grant.");
        Ff7BattleCommandOption option = battle.Commands(slot).Single(value => (byte)value.Command == 0x0B && value.Argument == 0);
        Check(names.Name(0x0B) == "Manip." && option.DisplayName == names.Name(0x0B) && option.ValidTargets == 0 && !string.IsNullOrWhiteSpace(option.UnsupportedReason), "An unsupported original command lost its raw identity, resource-backed label or explicit unavailable state.");
        string before = JsonSerializer.Serialize(battle.Capture());
        Reject<NotSupportedException>(() => battle.Submit(slot, (Ff7BattleCommand)0x0B, 0, 0));
        Reject<NotSupportedException>(() => battle.Submit(slot, (Ff7BattleCommand)0x0B, 0, 0x10));
        Check(before == JsonSerializer.Serialize(battle.Capture()), "Selecting an unsupported native command mutated RNG, queue, gauges or inventory.");
        Ff7BattleSession cold = Restore(battle.Capture());
        Check(JsonSerializer.Serialize(grants) == JsonSerializer.Serialize(cold.NativeCommandGrants(slot)) && JsonSerializer.Serialize(battle.Commands(slot)) == JsonSerializer.Serialize(cold.Commands(slot)), "Cold restoration changed command provenance, identity, admission or labels.");
        Ff7BattleCommandOption attack = battle.Commands(slot).Single(value => value.Command == Ff7BattleCommand.Attack && value.Argument == 0);
        ushort target = (ushort)(attack.ValidTargets & -attack.ValidTargets);
        Check(attack.UnsupportedReason is null && target != 0 && battle.Submit(slot, Ff7BattleCommand.Attack, 0, target), "An unavailable native grant disabled the existing ordinary Attack transaction.");

        Ff7CharacterState sense = Equip(cait, 37);
        Ff7Progress senseProgress = progress with { Characters = progress.Characters.Select(character => character.Id == cait.Id ? sense : character).ToArray() };
        Ff7BattleSession senseInitial = new(formation, senseProgress, kernel, executable.NativeMateriaStatModifiers, 1, commandResources: names);
        Ff7BattleSession senseBattle = new(formation, senseProgress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: Ready(senseInitial.Capture(), slot), commandResources: names);
        Ff7BattleCommandOption senseOption = senseBattle.Commands(slot).Single(value => value.Command == Ff7BattleCommand.Sense);
        Check(senseBattle.NativeCommandGrants(slot).Any(grant => grant.Command == 6) && senseOption.ValidTargets != 0 && (senseOption.ValidTargets & 15) == 0 && senseOption.DisplayName == names.Name(6) && senseOption.UnsupportedReason is null, "Original Sense must use the resource-backed command and select living enemies.");
        ushort senseTarget = (ushort)(senseOption.ValidTargets & -senseOption.ValidTargets);
        int senseSlot = System.Numerics.BitOperations.TrailingZeroCount(senseTarget);
        Check(senseBattle.Submit(slot, Ff7BattleCommand.Sense, 0, senseTarget), "Sense did not enter the ordinary command queue.");
        for (int tick = 0; tick < 120 && senseBattle.LastSense is null; tick++)
        {
            senseBattle.Advance();
        }
        Check(senseBattle.LastSense is { } scan && scan.Slot == senseSlot && scan.Name == senseBattle.Name(senseSlot) && scan.ElementResistances.Length == 9, "Sense did not publish the target's live inspection data.");

        KernelSection[] duplicateKernel = kernel.ToArray();
        byte[] duplicateMateria = kernel[8].Data.ToArray();
        duplicateMateria[37 * 20 + 14] = (byte)Ff7BattleCommand.Attack;
        duplicateKernel[8] = kernel[8] with { Data = duplicateMateria };
        Ff7BattleSession duplicateInitial = new(formation, senseProgress, duplicateKernel, executable.NativeMateriaStatModifiers, 1, commandResources: names);
        Ff7BattleSession duplicate = new(formation, senseProgress, duplicateKernel, executable.NativeMateriaStatModifiers, 1, snapshot: Ready(duplicateInitial.Capture(), slot), commandResources: names);
        Check(duplicate.NativeCommandGrants(slot).Any(grant => grant.Command == (byte)Ff7BattleCommand.Attack) && duplicate.Commands(slot).Count(value => value.Command == Ff7BattleCommand.Attack) == 1 && duplicate.Commands(slot).Single(value => value.Command == Ff7BattleCommand.Attack).UnsupportedReason is null, "A native grant duplicated or disabled an already supported command identity.");

        Ff7BattleSession guestInitial = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, commandResources: names);
        guestInitial.BindGuestVitals(new(Guid.NewGuid(), new string('A', 64), slot, 6, 0, 0, 400, 400, 17, 20, 20, 20));
        Ff7BattleSession guest = Restore(Ready(guestInitial.Capture(), slot));
        Check(guest.Commands(slot).All(value => (byte)value.Command is not (0x0B or 6)), "Story-side native materia grants leaked into an origin-owned guest command menu.");
        Check(beforeProgress == JsonSerializer.Serialize(progress), "Projecting battle command admission changed its input progression facts.");
        if (bytes is not null)
        {
            Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path!)), "Original command admission checks changed their external standalone source.");
        }

        Ff7BattleSession Restore(Ff7BattleSnapshot snapshot)
        {
            return new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(snapshot))!, commandResources: names);
        }
    }

    private static Ff7BattleSnapshot Ready(Ff7BattleSnapshot snapshot, int slot)
    {
        return snapshot with { Actors = snapshot.Actors.Select(actor => actor with { AwaitingTurn = actor.Actor.Slot == slot, Gauge = actor.Actor.Slot == slot ? ushort.MaxValue : (ushort)0 }).ToArray() };
    }

    private static Ff7CharacterState Character()
    {
        byte[] record = new byte[132];
        record[1] = 10;
        record.AsSpan(2, 6).Fill(20);
        record[30] = byte.MaxValue;
        record.AsSpan(64, 64).Fill(byte.MaxValue);
        return new(0, "Authored", 10, 0, 100, 10, record);
    }

    private static Ff7CharacterState Equip(Ff7CharacterState character, byte id)
    {
        byte[] record = character.NativeRecord.ToArray();
        record.AsSpan(64, 64).Fill(byte.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), id);
        return character with { NativeRecord = record };
    }

    private static KernelSection[] Kernel()
    {
        KernelSection[] kernel = Enumerable.Range(0, 9).Select(index => new KernelSection((ushort)index, ReadOnlyMemory<byte>.Empty)).ToArray();
        byte[] weapons = new byte[128 * 44];
        weapons[2] = 0x11;
        weapons[8] = 100;
        weapons.AsSpan(20, 4).Fill(byte.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(weapons.AsSpan(40), ushort.MaxValue);
        byte[] armor = new byte[32 * 36];
        armor.AsSpan(24, 4).Fill(byte.MaxValue);
        kernel[5] = new(5, weapons);
        kernel[6] = new(6, armor);
        kernel[7] = new(7, new byte[32 * 16]);
        kernel[8] = new(8, new byte[96 * 20]);
        return kernel;
    }

    private static KernelSection[] WithCommand(KernelSection[] kernel, byte id, byte category, byte command, byte row)
    {
        KernelSection[] result = kernel.ToArray();
        byte[] materia = kernel[8].Data.ToArray();
        materia.AsSpan(id * 20, 8).Fill(byte.MaxValue);
        materia[id * 20 + 8] = row;
        materia[id * 20 + 13] = category;
        materia.AsSpan(id * 20 + 14, 5).Fill(byte.MaxValue);
        materia[id * 20 + 14] = command;
        result[8] = new(8, materia);
        return result;
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException("An unsupported native command projection was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
