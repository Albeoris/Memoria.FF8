using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeLimitMenuTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP FFVII Limit level selection requires MEMORIA_FF7_GAME_DIR.");
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        Ff7SessionSave source = initial.Capture();
        Ff7CharacterState cloud = source.Progress.Characters.Single(character => character.Id == 0);
        cloud.NativeRecord[14] = 1;
        cloud.NativeRecord[15] = 203;
        BinaryPrimitives.WriteUInt16LittleEndian(cloud.NativeRecord.AsSpan(34), (1 << 0) | (1 << 4) | (1 << 9));
        Ff7CharacterState barret = source.Progress.Characters.Single(character => character.Id == 1);
        barret.NativeRecord[15] = 89;
        BinaryPrimitives.WriteUInt16LittleEndian(barret.NativeRecord.AsSpan(34), 1);
        Ff7CharacterState tifa = source.Progress.Characters.Single(character => character.Id == 2);
        BinaryPrimitives.WriteUInt16LittleEndian(tifa.NativeRecord.AsSpan(34), 1);

        Ff7SessionSave fixture = source with
        {
            Process = source.Process with { MenuDisabled = false, PlayerLocked = false, Movie = null },
            Progress = source.Progress with { Party = [0, 1, 255] }
        };
        using Ff7Session session = Ff7Session.Restore(installation, fixture);
        Check(session.CanUsePlayerMenu, "Limit fixture requires the ordinary player menu.");
        session.GuestFieldVitals = new Guest();
        Check(!session.TrySetMenuLimitLevel(0, 0) && !session.TrySetMenuLimitLevel(0, 5), "Limit level accepts only values 1 through 4.");
        Check(!session.TrySetMenuLimitLevel(0, 3), "An unlearned level cannot be set.");
        Check(!session.TrySetMenuLimitLevel(1, 1), "A guest's dormant FFVII Limit record cannot be edited.");
        Check(!session.TrySetMenuLimitLevel(2, 1), "A reserve character's Limit level cannot be edited.");
        Check(!session.TrySetMenuLimitLevel(255, 1), "An empty party slot does not own a Limit level.");
        Check(ReferenceEquals(fixture.Progress.Characters, session.Progress.Characters), "Rejected selections changed character records.");

        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Check(Ff7NativeBattleLimits.ReadLearnedIds(cloud, kernel, 2).SequenceEqual(new byte[] { 0x83 }), "Check must read the selected level and honor its individual learned slots.");
        Check(Ff7NativeBattleLimits.ReadLearnedIds(cloud, kernel, 3).Count == 0, "Check must leave unlearned levels empty.");

        Check(session.TrySetMenuLimitLevel(0, 4), "A learned level can be set even with an intervening unlearned level.");
        Ff7CharacterState changed = session.Progress.Characters.Single(character => character.Id == 0);
        byte[] expected = (byte[])cloud.NativeRecord.Clone();
        expected[14] = 4;
        expected[15] = 0;
        Check(changed.NativeRecord.SequenceEqual(expected), "A successful selection changes exactly the level and gauge bytes.");
        Check(cloud.NativeRecord[14] == 1 && cloud.NativeRecord[15] == 203, "Applying the level changed the earlier capture's character bytes.");
        Check(ReferenceEquals(barret, session.Progress.Characters.Single(character => character.Id == 1)), "A successful selection changed a dormant guest record.");

        using MemoryStream serialized = new();
        CombinedSessionCodec.Write(serialized, new() { ActiveGame = SessionGame.Ff7, Ff7 = session.Capture() });
        serialized.Position = 0;
        Ff7SessionSave restored = CombinedSessionCodec.Read(serialized).Ff7!;
        using Ff7Session cold = Ff7Session.Restore(installation, restored);
        Check(cold.Progress.Characters.Single(character => character.Id == 0).NativeRecord.SequenceEqual(expected), "The selected level and reset gauge were lost across cold restore.");

        Ff7SessionSave blockedSave = fixture with { Process = fixture.Process with { MenuDisabled = true } };
        using Ff7Session blocked = Ff7Session.Restore(installation, blockedSave);
        Check(!blocked.TrySetMenuLimitLevel(0, 4), "A disabled player menu cannot change Limit level.");

        using Ff7Session same = Ff7Session.Restore(installation, fixture);
        Check(same.TrySetMenuLimitLevel(0, 1), "The original Set menu permits the currently selected level.");
        Check(same.Progress.Characters.Single(character => character.Id == 0).NativeRecord[15] == 0, "Confirming the current level must also clear the gauge.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private sealed class Guest : IFf7GuestFieldVitals
    {
        public Ff7GuestFieldVitalsView Read() => new(1, "Squall", 10, 300, 300);
        public void Recover(bool clearStatuses) => throw new InvalidOperationException();
        public void AddHitPoints(short amount) => throw new InvalidOperationException();
    }
}
