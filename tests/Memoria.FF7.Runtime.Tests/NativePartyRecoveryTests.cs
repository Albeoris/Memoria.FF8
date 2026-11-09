using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePartyRecoveryTests
{
    public static void RunAll()
    {
        byte[] underwater = new byte[20];
        underwater[14] = 12;
        ReadOnlyMemory<byte>[] materia = [underwater];
        short[] modifiers = new short[17 * 8];
        Ff7CharacterState[] characters = Enumerable.Range(0, 9).Select(id => Character(id, (ushort)(200 + id), (ushort)(50 + id))).ToArray();
        foreach (Ff7CharacterState character in characters)
        {
            character.NativeRecord[31] = 0x30;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(characters[8].NativeRecord.AsSpan(64), 0);
        Ff7Progress progress = new() { InitialKernelData = [1], Characters = characters, Party = [0, 2, 255], Inventory = [], Materia = [], Gil = 678 };
        Ff7PartyRecoveryResult result = Ff7NativePartyRecovery.ApplyAll(progress, materia, modifiers);
        Equal(true, result.Progress.Party.SequenceEqual(progress.Party));
        Equal(false, result.Underwater);
        Equal(678u, result.Progress.Gil);
        for (int id = 0; id < 9; id++)
        {
            Equal(200 + id, result.Progress.Characters[id].HitPoints);
            Equal(50 + id, result.Progress.Characters[id].MagicPoints);
            Equal((byte)0, result.Progress.Characters[id].NativeRecord[31]);
            Equal((byte)0x30, characters[id].NativeRecord[31]);
            Equal(1, characters[id].HitPoints);
            foreach (int index in Enumerable.Range(0, 132).Except(new[] { 31, 44, 45, 48, 49, 56, 57, 58, 59 }))
            {
                Equal(characters[id].NativeRecord[index], result.Progress.Characters[id].NativeRecord[index]);
            }
        }
        Equal(true, Ff7NativePartyRecovery.ApplyAll(progress with { Party = [8, 255, 255] }, materia, modifiers).Underwater);
        Ff7CharacterState active = Character(0, 340, 62);
        Ff7CharacterState reserve = Character(1, 340, 62);
        BinaryPrimitives.WriteUInt16LittleEndian(active.NativeRecord.AsSpan(44), 40000);
        BinaryPrimitives.WriteUInt16LittleEndian(reserve.NativeRecord.AsSpan(44), 40000);
        result = Ff7NativePartyRecovery.ApplyAll(progress with { Characters = [active, reserve], Party = [0, 255, 255] }, materia, modifiers);
        Equal(60000, result.Progress.Characters[0].HitPoints);
        Equal(50000, result.Progress.Characters[1].HitPoints);
        Ff7Progress owner = progress with { Characters = [active, reserve], Party = [0, 255, 255] };
        int applications = 0;
        var field = new Ff7FieldResources("recovery-fixture", null!, null!, null!, [], [], null!, null!, null!);
        var host = new Ff7FieldHost(field, (_, _) => 1, new() { BlinkStateInitialized = true });
        host.ExternalCommand = command =>
        {
            Equal(new FieldPartyRecovery(true), command);
            applications++;
            owner = Ff7NativePartyRecovery.ApplyAll(owner, materia, modifiers).Progress;
            return new(true);
        };
        var request = new FieldHostRequest(1, 0, 6, 16, new FieldPartyRecovery(true));
        host.Execute(request);
        host.Execute(request);
        Equal(1, applications);
        Equal(60000, owner.Characters[0].HitPoints);
        var cold = new Ff7FieldHost(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        cold.ExternalCommand = _ => throw new Exception("A saved all-character recovery must not reapply the signed-word additions.");
        cold.Execute(request);
        Equal(60000, owner.Characters[0].HitPoints);
        InstalledContinuation(allCharacters: true);
    }

    public static void Run()
    {
        short[] modifiers = new short[17 * 8];
        modifiers[14] = -2;
        modifiers[15] = 2;
        byte[] magic = new byte[20];
        magic[8] = 1;
        magic[13] = 0xB;
        byte[] plus = Enumerable.Repeat((byte)255, 20).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(plus, 1);
        plus[8] = 0;
        plus[13] = 0x20;
        plus[14] = 8;
        plus[15] = 10;
        plus[16] = 20;
        byte[] swap = new byte[20];
        swap[14] = 98;
        byte[] underwater = new byte[20];
        underwater[14] = 12;
        ReadOnlyMemory<byte>[] materia = [magic, plus, swap, underwater];
        Ff7CharacterState active = Character(0, 340, 62);
        BinaryPrimitives.WriteUInt32LittleEndian(active.NativeRecord.AsSpan(64), 70u << 8);
        BinaryPrimitives.WriteUInt32LittleEndian(active.NativeRecord.AsSpan(68), 70u << 8);
        Ff7CharacterState reserve = Character(1, 500, 80);
        Ff7Progress progress = new() { InitialKernelData = [], Characters = [active, reserve], Party = [0, 255, 255], Inventory = [], Materia = [] };
        Ff7PartyRecoveryResult result = Ff7NativePartyRecovery.Apply(progress, materia, modifiers);
        Equal(327, result.Progress.Characters[0].HitPoints);
        Equal(64, result.Progress.Characters[0].MagicPoints);
        Equal((ushort)327, BinaryPrimitives.ReadUInt16LittleEndian(result.Progress.Characters[0].NativeRecord.AsSpan(56)));
        Equal(true, ReferenceEquals(reserve, result.Progress.Characters[1]));
        Equal(1, active.HitPoints);
        Equal((byte)255, active.NativeRecord[56]);
        foreach (int index in Enumerable.Range(0, active.NativeRecord.Length).Except(new[] { 44, 45, 48, 49, 56, 57, 58, 59 }))
        {
            Equal(active.NativeRecord[index], result.Progress.Characters[0].NativeRecord[index]);
        }
        byte[] record = active.NativeRecord.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(64), (100u << 8) | 1);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(68), uint.MaxValue);
        progress = progress with { Characters = [active with { NativeRecord = record }, reserve] };
        Equal(408, Ff7NativePartyRecovery.Apply(progress, materia, modifiers).Progress.Characters[0].HitPoints);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(72), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(76), 3);
        result = Ff7NativePartyRecovery.Apply(progress, materia, modifiers);
        Equal(62, result.Progress.Characters[0].HitPoints);
        Equal(408, result.Progress.Characters[0].MagicPoints);
        Equal(true, result.Underwater);
        plus[13] = 0x40;
        plus[16] = 255;
        result = Ff7NativePartyRecovery.Apply(progress, materia, modifiers);
        Equal(374, result.Progress.Characters[0].MagicPoints);
        modifiers[14] = 200;
        modifiers[15] = -200;
        progress = progress with { Characters = [active, reserve] };
        result = Ff7NativePartyRecovery.Apply(progress, materia, modifiers);
        Equal(680, result.Progress.Characters[0].HitPoints);
        Equal(10, result.Progress.Characters[0].MagicPoints);
        Ff7CharacterState signed = Character(0, 340, 62);
        BinaryPrimitives.WriteUInt16LittleEndian(signed.NativeRecord.AsSpan(44), 40000);
        progress = progress with { Characters = [signed], Party = [0, 255, 255] };
        Equal(50000, Ff7NativePartyRecovery.Apply(progress, materia, modifiers).Progress.Characters[0].HitPoints);
        InstalledContinuation();
    }

    private static Ff7CharacterState Character(int id, ushort hp, ushort mp)
    {
        byte[] record = Enumerable.Repeat((byte)255, 132).ToArray();
        record[0] = (byte)id;
        record[1] = 8;
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(46), hp);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(48), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(50), mp);
        return new(id, "Fixture", 8, 0, 1, 1, record);
    }

    private static void InstalledContinuation(bool allCharacters = false)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original-resource party recovery: optional fixture inputs are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        Equal(17 * 8, native.NativeMateriaStatModifiers.Count);
        Equal((short)-2, native.NativeMateriaStatModifiers[11 * 8 + 6]);
        Equal((short)2, native.NativeMateriaStatModifiers[11 * 8 + 7]);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        string vm = JsonSerializer.Serialize(session.Capture().Vm);
        Ff7Progress before = session.Progress;
        IReadOnlyList<ReadOnlyMemory<byte>> materiaRecords = Ff7MateriaResources.LoadRecords(installation);
        var expected = allCharacters ? Ff7NativePartyRecovery.ApplyAll(before, materiaRecords, native.NativeMateriaStatModifiers) : Ff7NativePartyRecovery.Apply(before, materiaRecords, native.NativeMateriaStatModifiers);
        FieldHostRequest request = new(long.MaxValue - 300, 0, 7, 0, new FieldPartyRecovery(allCharacters));
        Equal(new FieldHostReply(true), session.Host.Execute(request));
        Equal(JsonSerializer.Serialize(expected.Progress.Characters), JsonSerializer.Serialize(session.Progress.Characters));
        Equal(vm, JsonSerializer.Serialize(session.Capture().Vm));
        Ff7SessionSave saved = JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!;
        using Ff7Session cold = Ff7Session.Restore(installation, saved);
        Equal(new FieldHostReply(true), cold.Host.Execute(request));
        Equal(JsonSerializer.Serialize(session.Progress), JsonSerializer.Serialize(cold.Progress));
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
