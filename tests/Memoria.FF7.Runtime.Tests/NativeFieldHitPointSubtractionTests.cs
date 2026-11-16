using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeFieldHitPointSubtractionTests
{
    internal static void Run()
    {
        foreach ((ushort current, short amount, ushort expected) in new (ushort, short, ushort)[] { (500, 501, 0), (500, -501, 1001), (32767, short.MinValue, 65535), (0, short.MinValue, 32768), (40000, short.MinValue, 7232), (65535, 1, 0), (0, short.MaxValue, 0) })
        {
            Check(Ff7NativePartyRecovery.SubtractHitPointWord(current, amount) == expected, "Field HP subtraction must use signed WORD current HP and signed amount, an int subtraction, only a zero lower bound and a WORD store.");
        }
        ushort wrapped = Ff7NativePartyRecovery.SubtractHitPointWord(32767, -1);
        Check(wrapped == 32768 && Ff7NativePartyRecovery.SubtractHitPointWord(wrapped, 1) == 0, "The next native subtraction must read a stored high WORD as signed rather than apply an added maximum-HP cap.");
        byte[] underwater = new byte[20];
        underwater[8] = 255;
        underwater[14] = 12;
        ReadOnlyMemory<byte>[] materia = [underwater];
        short[] modifiers = new short[17 * 8];
        Ff7CharacterState target = Character(0, 500, 1000);
        Ff7CharacterState other = Character(1, 1200, 1000);
        BinaryPrimitives.WriteUInt32LittleEndian(other.NativeRecord.AsSpan(64), 0);
        Ff7CharacterState reserve = Character(2, 333, 1000);
        Ff7Progress source = new() { InitialKernelData = [], Characters = [target, other, reserve], Party = [0, 1, 255], Inventory = [], Materia = [], Gil = 89, FieldRandomIndex = 17 };
        string original = JsonSerializer.Serialize(source);
        Ff7PartyRecoveryResult damaged = Ff7NativePartyRecovery.SubtractHitPoints(source, 0, 501, materia, modifiers);
        Check(damaged.Progress.Characters[0].HitPoints == 0 && BinaryPrimitives.ReadUInt16LittleEndian(damaged.Progress.Characters[0].NativeRecord.AsSpan(44)) == 0, "The native 501-point field hazard must publish zero HP consistently to progress and the native character record.");
        Check(damaged.Progress.Characters[1].HitPoints == 1000 && BinaryPrimitives.ReadUInt16LittleEndian(damaged.Progress.Characters[1].NativeRecord.AsSpan(56)) == 1000 && damaged.Underwater, "Every main-party derived record and underwater publication must refresh before subtracting from the selected target.");
        Check(ReferenceEquals(damaged.Progress.Characters[2], reserve), "An inactive reserve must retain its original native record.");
        Ff7Progress recovered = Ff7NativePartyRecovery.AddHitPoints(damaged.Progress, 0, 1, materia, modifiers).Progress;
        Check(recovered.Characters[0].HitPoints == 1 && recovered.Characters[0].NativeRecord[31] == target.NativeRecord[31], "The actual hazard's following HP+1 must restore one HP without changing unrelated native status ownership.");
        foreach (int absent in new[] { 255, 2 })
        {
            Ff7PartyRecoveryResult skipped = Ff7NativePartyRecovery.SubtractHitPoints(source, absent, 501, materia, modifiers);
            Check(skipped.Progress.Characters[0].HitPoints == 500 && skipped.Progress.Characters[1].HitPoints == 1000 && ReferenceEquals(skipped.Progress.Characters[2], reserve) && skipped.Underwater, "Absent or non-main script bindings must skip target subtraction after the full native party publication.");
        }
        Ff7Progress overflow = Ff7NativePartyRecovery.SubtractHitPoints(source, 0, short.MinValue, materia, modifiers).Progress;
        Check(overflow.Characters[0].HitPoints == 33268 && BinaryPrimitives.ReadUInt16LittleEndian(overflow.Characters[0].NativeRecord.AsSpan(44)) == 33268, "Native negative subtraction must heal above the derived maximum without short negation or an HP+ cap.");
        Guest guest = new(0, 500, 1000);
        int publications = 0;
        void Publish(Action action)
        {
            publications++;
            action();
        }
        Ff7PartyRecoveryResult guestDamaged = Ff7Session.ApplyFieldHitPointAdjustment(source, new(0, 501, Subtract: true), materia, modifiers, guest, Publish);
        Check(guest.Hp == 0 && guest.KnockedOut && guest.Subtractions == 1 && guest.Additions == 0 && publications == 1, "The runtime field command must route resolved guest subtraction to its explicit adapter and preserve its intermediate KO status.");
        Check(ReferenceEquals(guestDamaged.Progress.Characters[0], target), "The guest's dormant FFVII story character must remain unchanged while origin HP owns the effect.");
        _ = Ff7Session.ApplyFieldHitPointAdjustment(guestDamaged.Progress, new(0, 1), materia, modifiers, guest, Publish);
        Check(guest.Hp == 1 && !guest.KnockedOut && guest.Additions == 1 && guest.Subtractions == 1, "The guest's following ordinary HP+1 must clear its existing KO projection without disguising subtraction as addition.");
        foreach (int absent in new[] { 255, 2 })
        {
            _ = Ff7Session.ApplyFieldHitPointAdjustment(source, new(absent, 501, Subtract: true), materia, modifiers, guest, Publish);
        }
        Check(publications == 2 && guest.Subtractions == 1, "An absent or non-main resolved script character must not invoke a guest adapter.");
        _ = Ff7Session.ApplyFieldHitPointAdjustment(source, new(1, 501, Subtract: true), materia, modifiers, guest, Publish);
        Check(guest.Hp == 1 && guest.Subtractions == 1, "A normal main-party character's effect must not mutate another character's guest binding.");
        Guest reorderedGuest = new(0, 500, 1000);
        Ff7PartyRecoveryResult reordered = Ff7Session.ApplyFieldHitPointAdjustment(source with { Party = [1, 0, 255] }, new(0, 501, Subtract: true), materia, modifiers, reorderedGuest, action => action());
        Check(reorderedGuest.Hp == 0 && reordered.Progress.Characters[1].HitPoints == 1000, "Runtime HP commands must use their resolved script-party character, not reinterpret it as a main-party slot.");
        Ff7Progress flashback = source with { Characters = [Character(6, 800, 1000)], Party = [9, 255, 255] };
        Check(Ff7NativePartyRecovery.SubtractHitPoints(flashback, 9, 501, materia, modifiers).Progress.Characters.Single().HitPoints == 299 && Ff7NativePartyRecovery.SubtractHitPoints(flashback, 6, 501, materia, modifiers).Progress.Characters.Single().HitPoints == 800, "Native main-party aliases must retain exact story identity matching and reuse the proved physical character record mapping.");
        bool unsupported = false;
        try
        {
            ((IFf7GuestFieldVitals)new LegacyGuest()).SubtractHitPoints(1);
        }
        catch (NotSupportedException)
        {
            unsupported = true;
        }
        Check(unsupported, "An older guest adapter without subtraction support must fail explicitly rather than silently negate into HP+.");
        Check(original == JsonSerializer.Serialize(source), "Field subtraction, full publication and guest routing must preserve their authored source state, RNG and unrelated progress.");
        FieldHitPointCommand legacy = JsonSerializer.Deserialize<FieldHitPointCommand>("{\"Character\":0,\"Amount\":1}")!;
        Check(!legacy.Subtract, "Older native HP+ command receipts must retain their additive default.");
    }

    private static Ff7CharacterState Character(int id, ushort hp, ushort maximum)
    {
        byte[] record = new byte[132];
        record.AsSpan(64, 64).Fill(255);
        record[31] = 0x30;
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), hp);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(46), maximum);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(48), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(50), 100);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(56), 77);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(58), 77);
        return new(id, "Member", 1, 0, hp, 10, record);
    }

    private sealed class Guest(int storyCharacter, ushort hp, ushort maximum) : IFf7GuestFieldVitals
    {
        public ushort Hp { get; private set; } = hp;
        public bool KnockedOut { get; private set; }
        public int Additions { get; private set; }
        public int Subtractions { get; private set; }

        public Ff7GuestFieldVitalsView Read() => new(storyCharacter, "Guest", 1, Hp, maximum);
        public void Recover(bool clearStatuses) => throw new InvalidOperationException("The field hazard must not recover the guest.");
        public void AddHitPoints(short amount)
        {
            Additions++;
            Hp = checked((ushort)Math.Clamp(Hp + (int)amount, 0, maximum));
            KnockedOut = Hp == 0;
        }
        public void SubtractHitPoints(short amount)
        {
            Subtractions++;
            Hp = Ff7NativePartyRecovery.SubtractHitPointWord(Hp, amount);
            KnockedOut = Hp == 0;
        }
    }

    private sealed class LegacyGuest : IFf7GuestFieldVitals
    {
        public Ff7GuestFieldVitalsView Read() => new(0, "Guest", 1, 500, 1000);
        public void Recover(bool clearStatuses) => throw new InvalidOperationException();
        public void AddHitPoints(short amount) => throw new InvalidOperationException("Subtraction must not fall back to addition.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
