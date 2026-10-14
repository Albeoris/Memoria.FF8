using System.Numerics;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.TextEncoding;
using Albeoris.Games.FF8.FlArchives.Abstractions;

namespace Memoria.FF8.Assets;

/// <summary>Original battle.fs resource ownership, scene.out formations and cached DAT/X models.</summary>
public sealed class BattleAssets : IDisposable
{
    private readonly IFlArchive archive;
    private readonly Dictionary<string, IFlArchiveEntry> entries;
    private readonly Dictionary<string, BattleModel> models = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, BattleStage> stages = [];
    private readonly byte[] encounters;

    public BattleAssets(string gameDirectory)
    {
        archive = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "battle.fs"), FlArchiveRepresentation.Files);
        entries = archive.Entries.ToDictionary(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1], StringComparer.OrdinalIgnoreCase);
        using (IFlArchive main = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files))
        {
            IFlArchiveEntry kernel = main.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals("kernel.bin", StringComparison.OrdinalIgnoreCase));
            using Stream input = kernel.OpenForRead();
            using MemoryStream bytes = new();
            input.CopyTo(bytes);
            WeaponDefinitions = BattleKernelReader.Weapons(bytes.ToArray());
        }
        encounters = Read("scene.out");
        if (encounters.Length != 1024 * 128)
        {
            throw new InvalidDataException("scene.out does not contain the original 1024 formations.");
        }
    }

    public IReadOnlyList<BattleWeaponDefinition> WeaponDefinitions { get; }

    public string EncounterSourceHash => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(encounters));
    public IReadOnlyList<int> EnemyModelIds => entries.Keys.Where(name => name.StartsWith("c0m", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)).Select(name => int.Parse(name.AsSpan(3, 3))).Order().ToArray();
    public IReadOnlyList<int> StageIds => entries.Keys.Where(name => name.StartsWith("a0stg", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".x", StringComparison.OrdinalIgnoreCase)).Select(name => int.Parse(name.AsSpan(5, 3))).Order().ToArray();
    public IReadOnlyList<int> PlayerCharacterIds => entries.Keys.Where(name => name.Length == 10 && name[0] == 'd' && name[2] == 'c').Select(name => Convert.ToInt32(name[1].ToString(), 16)).Distinct().Order().ToArray();
    public IReadOnlyList<int> WeaponModelIds(int characterId) => entries.Keys.Where(name => entries[name].Size > 0 && name.StartsWith($"d{characterId:X}w", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)).Select(name => int.Parse(name.AsSpan(3, 3))).Order().ToArray();
    public IReadOnlyList<string> PlayerModelNames(int characterId) => entries.Keys.Where(name => name.StartsWith($"d{characterId:X}c", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public BattleEncounter LoadEncounter(int encounterId)
    {
        if ((uint)encounterId >= 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(encounterId));
        }
        using BinaryReader reader = new(new MemoryStream(encounters, encounterId * 128, 128, false));
        byte stage = reader.ReadByte();
        byte flags = reader.ReadByte();
        byte primary = reader.ReadByte();
        byte alternative = reader.ReadByte();
        byte hidden = reader.ReadByte();
        byte unloaded = reader.ReadByte();
        byte untargetable = reader.ReadByte();
        byte enabled = reader.ReadByte();
        Vector3[] coordinates = new Vector3[8];
        for (int index = 0; index < 8; index++)
        {
            coordinates[7 - index] = new Vector3(reader.ReadInt16(), reader.ReadInt16(), -reader.ReadInt16()) / 100f;
        }
        byte[] ids = reader.ReadBytes(8);
        reader.BaseStream.Position = 120;
        byte[] levels = reader.ReadBytes(8);
        List<BattleEnemyPlacement> enemies = [];
        for (int slot = 0; slot < 8; slot++)
        {
            int mask = 0x80 >> slot;
            if ((enabled & mask) == 0)
            {
                continue;
            }
            if ((unloaded & mask) != 0 || ids[slot] < 16)
            {
                throw new NotSupportedException($"Encounter {encounterId} slot {slot} requires an unloaded/replaced model contract.");
            }
            enemies.Add(new BattleEnemyPlacement(slot, ids[slot] - 16, coordinates[slot], levels[7 - slot], (hidden & mask) != 0, (untargetable & mask) != 0));
        }
        return new BattleEncounter(encounterId, stage, flags, primary, alternative, enemies);
    }

    public BattleStage LoadStage(int stageId)
    {
        if (!stages.TryGetValue(stageId, out BattleStage? stage))
        {
            stage = BattleResourceReader.ReadStage(stageId, Read($"a0stg{stageId:000}.x"));
            stages.Add(stageId, stage);
        }
        return stage;
    }

    public string ReadEnemyName(int modelId)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        using BinaryReader reader = new(new MemoryStream(bytes, false));
        int sections = reader.ReadInt32();
        if (sections is not (2 or 11))
        {
            throw new NotSupportedException("Enemy information requires the original monster directory.");
        }
        reader.BaseStream.Position = sections == 11 ? 28 : 4;
        int offset = reader.ReadInt32();
        if (offset < 0 || offset > bytes.Length - 24)
        {
            throw new InvalidDataException("Original enemy name lies outside its information section.");
        }
        ReadOnlySpan<byte> name = bytes.AsSpan(offset, 24);
        int end = name.IndexOf((byte)0);
        if (end < 0)
        {
            throw new InvalidDataException("Original enemy name has no terminator.");
        }
        return FF8Encoding.CreateEuropean().GetString(name[..end]);
    }

    public BattleModel LoadEnemy(int modelId) => LoadModel($"c0m{modelId:000}.dat");

    public BattleModel LoadPlayer(int characterId, int costume = 0)
    {
        IReadOnlyList<string> names = PlayerModelNames(characterId);
        if ((uint)costume >= names.Count)
        {
            throw new NotSupportedException($"Character {characterId} costume {costume} has no installed battle model.");
        }
        return LoadModel(names[costume]);
    }

    public BattleModel? ResolveWeaponModel(int characterId, int savedWeaponId, BattleModel body)
    {
        if ((uint)savedWeaponId >= WeaponDefinitions.Count || WeaponDefinitions[savedWeaponId].CharacterId != characterId)
        {
            throw new NotSupportedException($"Saved weapon {savedWeaponId} does not belong to character {characterId}.");
        }
        IReadOnlyList<int> installed = WeaponModelIds(characterId);
        if (installed.Count == 0)
        {
            if (WeaponDefinitions[savedWeaponId].Name == "None")
            {
                return null;
            }
            throw new FileNotFoundException($"Character {characterId} has no installed weapon model.");
        }
        BattleWeaponDefinition[] choices = WeaponDefinitions.Where(weapon => weapon.CharacterId == characterId).ToArray();
        int index = Array.FindIndex(choices, weapon => weapon.Id == savedWeaponId);
        if (choices.Length != installed.Count || index < 0)
        {
            throw new NotSupportedException("Original character weapon records do not match the installed nonempty model catalog.");
        }
        // TODO(TD-009): Match original per-character kernel order to installed model order until the executable party-model table is proved
        BattleModel model = LoadWeapon(characterId, installed[index], body);
        // The original reduced loader supplies sequence/sound services for Zell and Kiros; it creates no second render header.
        return characterId is 1 or 9 ? null : model;
    }

    public BattleModel LoadWeapon(int characterId, int weaponModelId, BattleModel character)
    {
        string name = $"d{characterId:X}w{weaponModelId:000}.dat";
        if (!models.TryGetValue(name, out BattleModel? model))
        {
            model = BattleResourceReader.ReadWeapon(name, Read(name), character);
            models.Add(name, model);
        }
        return model;
    }

    public BattleModel LoadModel(string name)
    {
        if (!models.TryGetValue(name, out BattleModel? model))
        {
            model = BattleResourceReader.ReadModel(name, Read(name));
            models.Add(name, model);
        }
        return model;
    }

    private byte[] Read(string name)
    {
        if (!entries.TryGetValue(name, out IFlArchiveEntry? entry))
        {
            throw new FileNotFoundException($"Original battle resource {name} is absent.", name);
        }
        using Stream input = entry.OpenForRead();
        using MemoryStream output = new(checked((int)entry.Size));
        input.CopyTo(output);
        return output.ToArray();
    }

    public void Dispose() => archive.Dispose();
}
