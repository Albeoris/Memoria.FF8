using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.TextEncoding;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Original battle.fs resource ownership, scene.out formations and cached DAT/X models.</summary>
public sealed partial class BattleAssets : IDisposable
{
    private readonly IFlArchive archive;
    private readonly Dictionary<string, IFlArchiveEntry> entries;
    private readonly Dictionary<string, BattleModel> models = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, BattleStage> stages = [];
    private readonly Dictionary<string, byte[]> resourceBytes = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] encounters;
    private readonly BattlePlayerStatReader playerStats;
    private readonly Albeoris.Games.FF8.KernelBin.KernelData battleKernel;
    private readonly ResourceService? resources;
    private readonly AssetTextureScope textureScope;

    public BattleAssets(string gameDirectory, ResourceService? resources = null)
    {
        this.resources = resources;
        textureScope = new AssetTextureScope(resources);
        archive = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "battle.fs"), FlArchiveRepresentation.Files);
        entries = archive.Entries.ToDictionary(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1], StringComparer.OrdinalIgnoreCase);
        using (IFlArchive main = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files))
        {
            IFlArchiveEntry kernel = main.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals("kernel.bin", StringComparison.OrdinalIgnoreCase));
            using Stream input = kernel.OpenForRead();
            using MemoryStream bytes = new();
            input.CopyTo(bytes);
            input.Dispose();
            WeaponDefinitions = BattleKernelReader.Weapons(bytes.ToArray());
            IFlArchiveEntry initial = main.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals("init.out", StringComparison.OrdinalIgnoreCase));
            using Stream initialInput = initial.OpenForRead();
            using MemoryStream initialBytes = new();
            initialInput.CopyTo(initialBytes);
            byte[] initialContent = initialBytes.ToArray();
            if (initialContent.Length != 2812)
            {
                throw new NotSupportedException("Battle GF labels require the authentic New Game name template.");
            }
            string GuardianName(int id)
            {
                ReadOnlySpan<byte> name = initialContent.AsSpan(id * 68, 12);
                int end = name.IndexOf((byte)0);
                return FF8Encoding.CreateEuropean().GetString(end < 0 ? name : name[..end]);
            }
            BattleActionCatalog actionCatalog = BattleActionKernelReader.ReadCatalogue(bytes.ToArray());
            ActionDefinitions = actionCatalog.Actions.Select(action => action.Ability == Memoria.FF8.Runtime.BattleAbility.GuardianForce ? action with { DisplayName = GuardianName(action.Id) } : action).ToArray();
            NativeActions = actionCatalog.NativeActions;
            NativeKernelRecords = actionCatalog.Records;
            playerStats = new BattlePlayerStatReader(bytes.ToArray());
            battleKernel = Albeoris.Games.FF8.KernelBin.KernelData.ReadFromArray(bytes.ToArray(), FF8Encoding.CreateEuropean());
        }
        encounters = Read("scene.out");
        if (encounters.Length != 1024 * 128)
        {
            throw new InvalidDataException("scene.out does not contain the original 1024 formations.");
        }
    }

    public IReadOnlyList<BattleWeaponDefinition> WeaponDefinitions { get; }
    public IReadOnlyList<Memoria.FF8.Runtime.BattleActionDefinition> ActionDefinitions { get; }
    public IReadOnlyList<BattleNativeAction> NativeActions { get; }
    public IReadOnlyList<BattleNativeKernelRecord> NativeKernelRecords { get; }
    public string?[] ReadNativeGlobalBattleMessages() => battleKernel.MiscTexts.ToArray();
    public BattlePlayerStats ReadPlayerStats(Memoria.FF8.Runtime.NativeCharacterVitals vitals, int row) => playerStats.Read(vitals, row);

    public Memoria.FF8.Runtime.BattleDrawSpell[] ReadEnemyDrawSpells(int modelId, int level)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        int sections = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (sections is not (2 or 11))
        {
            throw new NotSupportedException("Enemy Draw requires an original information section.");
        }
        int information = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > bytes.Length - 284)
        {
            throw new InvalidDataException("Enemy Draw records exceed the original information section.");
        }
        // Native48C7A0/48FD20 read four BYTE spell/resistance pairs at info+260+8*levelBand.
        int band = level < bytes[information + 244] ? 0 : level < bytes[information + 245] ? 1 : 2;
        List<Memoria.FF8.Runtime.BattleDrawSpell> spells = [];
        for (int slot = 0; slot < 4; slot++)
        {
            int offset = information + 260 + band * 8 + slot * 2;
            int id = bytes[offset];
            if (id is > 0 and < 64)
            {
                spells.Add(new Memoria.FF8.Runtime.BattleDrawSpell(id, bytes[offset + 1]));
            }
        }
        return spells.ToArray();
    }

    public string EncounterSourceHash => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(encounters));
    public string FormationSourceHash(int encounterId)
    {
        if ((uint)encounterId >= 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(encounterId));
        }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(encounters.AsSpan(encounterId * 128, 128)));
    }
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
        return BattleFormationReader.Read(encounterId, encounters.AsSpan(encounterId * 128, 128));
    }

    public BattleStage LoadStage(int stageId)
    {
        if (!stages.TryGetValue(stageId, out BattleStage? stage))
        {
            stage = BattleResourceReader.ReadStage(stageId, Read($"a0stg{stageId:000}.x"));
            stage = stage with { Textures = textureScope.RegisterList(new ResourceKey($"battle/a0stg{stageId:000}.x"), stage.Textures, "classic-battle") };
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

    public int ReadEnemyAbilityPoints(int modelId)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        int sections = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (sections is not (2 or 11))
        {
            throw new NotSupportedException("Enemy AP requires the original information section.");
        }
        int information = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > bytes.Length - 336)
        {
            throw new InvalidDataException("Enemy AP exceeds the original information section.");
        }
        // Native494AF0 LABEL37 accumulates this BYTE into the reward AP WORD.
        return bytes[information + 335];
    }
    public BattleEnemyNativeStats ReadEnemyNativeStats(int modelId) => BattleEnemyNativeStats.ReadMonster(Read($"c0m{modelId:000}.dat"));
    public BattleEnemyRewardDefinition ReadEnemyRewards(int modelId) => BattleEnemyRewards.Read(Read($"c0m{modelId:000}.dat"));

    public (int Power, int DrainPercent) ReadEnemyPrimitiveAttack(int modelId, int level)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        if (System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes) != 11)
        {
            throw new NotSupportedException("Enemy primitive action requires its original ability bank.");
        }
        int information = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28));
        if (information < 0 || information > bytes.Length - 284)
        {
            throw new InvalidDataException("Enemy ability bank exceeds its information section.");
        }
        int band = level < bytes[information + 244] ? 0 : level < bytes[information + 245] ? 1 : 2;
        // TODO(TD-009): Native487DF0 enemy AI is not executed; this bounded dispatcher uses the first installed enemy-attack command in its actual level band
        for (int slot = 0; slot < 16; slot++)
        {
            int offset = information + 52 + band * 64 + slot * 4;
            if (bytes[offset] != 8)
            {
                continue;
            }
            int id = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 2));
            if (id >= battleKernel.EnemyAttacks.Count)
            {
                throw new InvalidDataException("Enemy ability references an absent kernel attack.");
            }
            var attack = battleKernel.EnemyAttacks[id];
            if (attack.AttackType != 1 || attack.Element != 0 || attack.Statuses0 != 0 || (attack.Statuses1 & ~0x8000u) != 0)
            {
                throw new NotSupportedException($"Enemy model {modelId} primitive action {id} requires an unsupported attack contract.");
            }
            return (attack.AttackPower, (attack.Statuses1 & 0x8000) != 0 ? attack.StatusAttack : 0);
        }
        throw new NotSupportedException("Enemy level band has no primitive physical attack command.");
    }

    public BattleModel LoadEnemy(int modelId) => LoadModel($"c0m{modelId:000}.dat");

    public int ReadEnemyAttackSequence(int modelId)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        using BinaryReader reader = new(new MemoryStream(bytes, false));
        int sections = reader.ReadInt32();
        if (sections != 11)
        {
            throw new NotSupportedException("A renderable enemy requires its original eleven-section monster directory.");
        }
        reader.BaseStream.Position = 28;
        int information = reader.ReadInt32();
        int informationEnd = reader.ReadInt32();
        if (information < 0 || informationEnd > bytes.Length || informationEnd - information < 244)
        {
            throw new InvalidDataException("Enemy ability records exceed their original information section.");
        }
        // Native487DF0 reads command/sequence/id at info+52+4*(16*levelBand+ability),
        // preserving byte+53 as48D200's enemy sequence argument. Command8 selects enemy attacks.
        // TODO(TD-009): The scaffold presents the first installed enemy-attack entry, not native AI/level-band selection
        for (int ability = 0; ability < 48; ability++)
        {
            int offset = information + 52 + ability * 4;
            if (bytes[offset] == 8)
            {
                return bytes[offset + 1];
            }
        }
        throw new NotSupportedException($"Enemy model {modelId} has no installed enemy-attack sequence binding.");
    }

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

    public BattleSequenceSet LoadPlayerSequences(int characterId, int? savedWeaponId, BattleModel character)
    {
        return LoadPlayerSequenceOwner(characterId, savedWeaponId, character).Sequences ?? throw new NotSupportedException($"Character {characterId} has no original battle sequence section.");
    }

    private BattleModel LoadPlayerSequenceOwner(int characterId, int? savedWeaponId, BattleModel character)
    {
        if (character.Sequences is not null)
        {
            return character;
        }
        IReadOnlyList<int> installed = WeaponModelIds(characterId);
        if (installed.Count == 0)
        {
            throw new NotSupportedException($"Character {characterId} has no installed battle sequence owner.");
        }
        int index = 0;
        if (savedWeaponId is int id)
        {
            BattleWeaponDefinition[] choices = WeaponDefinitions.Where(weapon => weapon.CharacterId == characterId).ToArray();
            index = Array.FindIndex(choices, weapon => weapon.Id == id);
            if (choices.Length != installed.Count || index < 0)
            {
                throw new NotSupportedException($"Weapon {id} has no installed character {characterId} battle sequence binding.");
            }
        }
        return LoadWeapon(characterId, installed[index], character);
    }

    public BattleModel LoadWeapon(int characterId, int weaponModelId, BattleModel character)
    {
        string name = $"d{characterId:X}w{weaponModelId:000}.dat";
        string key = $"{name}|{character.Name}";
        if (!models.TryGetValue(key, out BattleModel? model))
        {
            model = BattleResourceReader.ReadWeapon(name, Read(name), character);
            model = model with { Textures = textureScope.RegisterList(new ResourceKey("battle/" + name), model.Textures, "classic-battle") };
            models.Add(key, model);
        }
        return model;
    }

    public BattleModel LoadModel(string name)
    {
        if (!models.TryGetValue(name, out BattleModel? model))
        {
            model = BattleResourceReader.ReadModel(name, Read(name));
            model = model with { Textures = textureScope.RegisterList(new ResourceKey("battle/" + name), model.Textures, "classic-battle") };
            models.Add(name, model);
        }
        return model;
    }

    private byte[] Read(string name)
    {
        if (resourceBytes.TryGetValue(name, out byte[]? cached))
        {
            return cached;
        }
        if (resources is not null)
        {
            byte[] content = resources.Read(new ResourceKey("battle/" + name));
            resourceBytes.Add(name, content);
            return content;
        }
        if (!entries.TryGetValue(name, out IFlArchiveEntry? entry))
        {
            throw new FileNotFoundException($"Original battle resource {name} is absent.", name);
        }
        using Stream input = entry.OpenForRead();
        using MemoryStream output = new(checked((int)entry.Size));
        input.CopyTo(output);
        byte[] bytes = output.ToArray();
        resourceBytes.Add(name, bytes);
        return bytes;
    }

    public void Dispose()
    {
        textureScope.Dispose();
        archive.Dispose();
    }
}
