using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class CharacterDisplayNameTests
{
    public static void Run()
    {
        byte[] young = new byte[132];
        young[0] = 9;
        young.AsSpan(16, 12).Fill(255);
        byte[] sephiroth = new byte[132];
        sephiroth[0] = 10;
        sephiroth.AsSpan(16, 12).Fill(255);
        byte[] encoded = "General".Select(value => checked((byte)(value - 32))).ToArray();
        encoded.CopyTo(sephiroth, 16);
        Ff7CharacterState[] characters = [new(0, "Neo", 1, 0, 1, 1, new byte[132]), new(6, "Lucky", 1, 0, 1, 1, young), new(7, "Valentine", 1, 0, 1, 1, sephiroth)];
        string before = JsonSerializer.Serialize(characters);
        Check(Ff7CharacterDisplayName.Resolve(characters, 9) == "Neo", "Young Cloud did not inherit the custom protagonist name.");
        Check(Ff7CharacterDisplayName.Resolve(characters, 10) == "General", "Temporary identity ignored its encoded original name.");
        Check(Ff7CharacterDisplayName.Resolve(characters, 6) == "Lucky" && Ff7CharacterDisplayName.Resolve(characters, 7) == "Valentine", "Ordinary Cait Sith or Vincent lost a custom progression name.");
        Check(JsonSerializer.Serialize(characters) == before, "Name presentation changed a progression name or native record.");
        try
        {
            Ff7CharacterDisplayName.Resolve(characters, 11);
            throw new InvalidOperationException("Unsupported identity was accepted.");
        }
        catch (InvalidDataException)
        {
        }
        Console.WriteLine("PASS logical display names, custom protagonist inheritance, encoded temporary name and unchanged ordinary records");

        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original formation292 display names: select original FFVII resources.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session initial = Ff7Session.NewGame(installation);
        Ff7Progress progress = initial.Progress with { Party = [9, 10, 255] };
        string progressBefore = JsonSerializer.Serialize(progress);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[292];
        Ff7BattleSession battle = new(formation, progress, kernel, Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers, 1);
        Check(battle.Name(0) == progress.Characters.Single(character => character.Id == 0).Name && battle.Name(1) == "Sephiroth", "Original formation292 HUD names use physical Cait Sith/Vincent labels.");
        Ff7BattleSession cold = new(formation, progress, kernel, Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Check(cold.Name(0) == battle.Name(0) && cold.Name(1) == battle.Name(1), "Cold battle restore changed derived logical display names.");
        Check(JsonSerializer.Serialize(progress) == progressBefore, "Battle name projection changed source progression.");
        Console.WriteLine("PASS original formation292 player and target names with cold restore; no battle callbacks");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
