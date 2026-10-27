using System.Buffers.Binary;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Validates registered binary families against one coherent selected resource publication.</summary>
public sealed class NativeDataResourceValidator(ResourceService candidate) : IDisposable
{
    private BattleAssets? battle;
    private bool menuValidated;
    private bool disposed;

    public void Validate(ResourceContent content, ResourceContent? original)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(content);
        if (original is not null && original.Key != content.Key)
        {
            throw new ArgumentException("Selected and installed resource identities must agree.", nameof(original));
        }
        NativeDataResourceKind kind = NativeDataResourceFiles.Classify(content.Key) ?? throw new NotSupportedException($"No native data contract is registered for {content.Key}.");
        byte[] bytes = Read(content);
        try
        {
            switch (kind)
            {
                case NativeDataResourceKind.InitialData:
                    ValidateInitialData(bytes);
                    break;
                case NativeDataResourceKind.BattleScenes:
                    ValidateScenes(bytes);
                    break;
                case NativeDataResourceKind.MenuTable:
                    ValidateMenuTable(content.Key, bytes);
                    break;
                case NativeDataResourceKind.Kernel:
                    ValidateKernel(bytes);
                    break;
                case NativeDataResourceKind.Enemy:
                    ValidateEnemy(content.Key, bytes, original);
                    break;
                case NativeDataResourceKind.NameDictionary:
                    _ = NamedicBin.ReadStrings(bytes, FF8Encoding.CreateEuropean());
                    break;
                case NativeDataResourceKind.MenuGroupData:
                case NativeDataResourceKind.MenuGroupIndex:
                    ValidateMenu();
                    break;
                case NativeDataResourceKind.WorldData:
                    ValidateWorldData(content.Key, bytes);
                    break;
                case NativeDataResourceKind.WorldSettings:
                    ValidateWorld(bytes);
                    break;
                case NativeDataResourceKind.MenuSpriteIndex:
                    if (content.Key.Path == "menu/icon.sp1")
                    {
                        NativeMenuArtworkDataReader.ValidateIconDirectory(bytes);
                    }
                    else
                    {
                        NativeMenuArtworkDataReader.ValidateFaceDirectory(bytes);
                    }
                    break;
                case NativeDataResourceKind.AreaNames:
                    NativeMenuArtworkDataReader.ValidateAreaNames(bytes);
                    break;
                case NativeDataResourceKind.Texture:
                    NativeMenuArtworkDataReader.ValidateTexture(content.Key, bytes);
                    break;
                case NativeDataResourceKind.CompressedImage:
                    _ = content.Key.Path == "main/ff8.lzs" ? NativeStartupArtwork.Decode(bytes) : NativeLzsArtwork.Decode(bytes);
                    break;
                default:
                    throw new NotSupportedException($"No native data validator is available for {content.Key}.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException or OverflowException or EndOfStreamException or KeyNotFoundException or InvalidCastException or FormatException)
        {
            throw new InvalidDataException($"Native resource {content.Key} does not satisfy its bounded data contract: {exception.Message}", exception);
        }
    }

    private void ValidateInitialData(byte[] bytes)
    {
        NativeInitialData.Validate(bytes);
        byte[] kernel = candidate.Read(new ResourceKey("main/kernel.bin"));
        _ = NativeCharacterInitialization.Read(bytes, kernel);
        _ = NativeEconomyInitialization.Read(bytes);
        _ = NativeInputInitialization.Read(bytes);
        _ = NativeLimitProgressInitialization.Read(bytes);
        _ = NativePocketStationInitialization.Read(bytes);
        _ = NativeAppearanceInitialization.Read(bytes);
        _ = NativeShotAvailabilityInitialization.Read(bytes, kernel);
        _ = NativeShopDefinitionReader.ReadNewGameHistory(bytes);
    }

    private void ValidateScenes(byte[] bytes)
    {
        if (bytes.Length != 1024 * 128)
        {
            throw new InvalidDataException("Native scene data requires 1024 complete formation records.");
        }
        HashSet<int> models = [];
        for (int row = 0; row < 1024; row++)
        {
            BattleEncounter formation = BattleFormationReader.Read(row, bytes.AsSpan(row * 128, 128));
            foreach (BattleEnemyPlacement slot in formation.Slots.Where(slot => slot.Enabled))
            {
                if (slot.ModelId < 0)
                {
                    throw new InvalidDataException("An enabled native encounter slot requires an enemy resource identity.");
                }
                _ = candidate.Get(new ResourceKey($"battle/c0m{slot.ModelId:000}.dat"));
                models.Add(slot.ModelId);
            }
            if (formation.Slots.Any(slot => slot.Enabled))
            {
                _ = candidate.Get(new ResourceKey($"battle/a0stg{formation.StageId:000}.x"));
            }
        }
        if (models.Count > 0)
        {
            BattleAssets assets = battle ??= new BattleAssets(string.Empty, candidate);
            foreach (int model in models)
            {
                _ = assets.ReadEnemyAi(model, 0);
            }
        }
    }

    private void ValidateMenuTable(ResourceKey key, byte[] bytes)
    {
        switch (key.Path)
        {
            case "menu/m000.bin":
            case "menu/m001.bin":
            case "menu/m002.bin":
            case "menu/m003.bin":
            case "menu/m004.bin":
                NativeMenuDataReader.ValidateRefinementTable(bytes, key.Path[8] - '0');
                break;
            case "menu/shop.bin":
                NativeMenuDataReader.ValidateShopCatalogs(bytes);
                break;
            case "menu/price.bin":
                NativeMenuDataReader.ValidateShopPrices(bytes);
                break;
            case "menu/mwepon.bin":
                NativeMenuDataReader.ValidateWeaponRecipes(bytes);
                break;
            case "menu/mwepon.msg":
                NativeMenuDataReader.ValidateWeaponMessages(bytes);
                break;
            case "menu/mthomas.bin":
                NativeMenuDataReader.ValidateSolomonRingIngredients(bytes);
                break;
            case "menu/pet_exp.bin":
            case "menu/pet_exp.msg":
                _ = NativeAngeloTrainingDefinitionReader.Read(candidate.Read(new ResourceKey("menu/pet_exp.bin")), candidate.Read(new ResourceKey("menu/pet_exp.msg")), FF8Encoding.CreateEuropean());
                break;
            case "menu/mtmag.bin":
            case "menu/mmag.bin":
                IGameNativeMemorySource memory = candidate.GetSource<IGameNativeMemorySource>() ?? throw new NotSupportedException("Native tutorial metadata requires its authenticated executable descriptors.");
                _ = NativeTutorialBrowserCatalog.Read(memory, candidate.Read(new ResourceKey("menu/mngrphd.bin")), candidate.Read(new ResourceKey("menu/mngrp.bin")), candidate.Read(new ResourceKey("menu/mtmag.bin")), candidate.Read(new ResourceKey("menu/mmag.bin")));
                break;
            case "menu/cyocobo.bin":
                new NativeChocoboWorldTransferTable(System.Collections.Immutable.ImmutableArray.CreateRange(bytes)).Validate();
                break;
            case "menu/mitem.bin":
                _ = NativeMenuDataReader.ReadSolomonRingItemId(bytes);
                break;
        }
    }

    private void ValidateKernel(byte[] bytes)
    {
        _ = NativeKernelDirectory.Read(bytes);
        _ = KernelData.ReadFromArray(bytes, FF8Encoding.CreateEuropean());
        _ = BattleKernelReader.Weapons(bytes);
        _ = BattleActionKernelReader.ReadCatalogue(bytes);
        _ = NativeBattleExperienceDefinitionReader.Read(bytes);
        NativeCharacterVitals vitals = NativeCharacterInitialization.Read(candidate.Read(new ResourceKey("main/init.out")), bytes);
        BattlePlayerStatReader stats = new(bytes);
        for (int row = 0; row < vitals.Records.Length; row++)
        {
            _ = stats.ReadRules(vitals, row);
        }
    }

    private void ValidateEnemy(ResourceKey key, byte[] bytes, ResourceContent? original)
    {
        int count = bytes.Length >= 4 ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : 0;
        if (count is not (2 or 11))
        {
            throw new InvalidDataException("Enemy replacements require a two- or eleven-section native DAT directory.");
        }
        _ = NativeBattleDatDirectory.Section(bytes, 0);
        if (original is not null)
        {
            byte[] installed = Read(original);
            if (bytes.AsSpan().SequenceEqual(installed))
            {
                return;
            }
            _ = NativeBattleDatDirectory.Section(installed, 0);
            if (BinaryPrimitives.ReadInt32LittleEndian(installed) != count)
            {
                throw new NotSupportedException("An enemy replacement must preserve its installed native section layout.");
            }
        }
        int information = NativeBattleDatDirectory.Section(bytes, count == 11 ? 6 : 0);
        int informationEnd = NativeBattleDatDirectory.Section(bytes, count == 11 ? 7 : 1);
        if (informationEnd - information < 379)
        {
            throw new InvalidDataException("Enemy information does not contain its complete native stats, rewards and status records.");
        }
        int model = int.Parse(key.Path.AsSpan(10, 3), System.Globalization.CultureInfo.InvariantCulture);
        BattleAssets assets = battle ??= new BattleAssets(string.Empty, candidate);
        _ = assets.ReadEnemyName(model);
        _ = assets.ReadEnemyNativeStats(model);
        _ = assets.ReadEnemyRewards(model);
        _ = assets.ReadEnemyAbilityPoints(model);
        _ = assets.ReadEnemyNativeMugData(model);
        _ = assets.ReadEnemyNativeStatusData(model);
        _ = assets.ReadEnemyNativeInformation246(model);
        _ = assets.ReadEnemyNativeEntryFlags(model);
        _ = assets.ReadEnemyNativeCombatantFlags(model);
        _ = assets.ReadNativeEnemySpecial(model);
        _ = assets.ReadNativeRenzokukenMagicIds(model);
        _ = assets.ReadEnemyAi(model, 0);
        foreach (int level in new[] { 0, bytes[information + 244], bytes[information + 245] })
        {
            _ = assets.ReadEnemyDrawSlots(model, level);
            _ = assets.ReadEnemyDrawSpells(model, level);
            _ = assets.ReadEnemyDrawGuardianForces(model, level);
        }
        if (count == 11)
        {
            _ = assets.LoadEnemy(model);
            _ = assets.ReadEnemyNativeGraphicsResource(model);
        }
    }

    private void ValidateMenu()
    {
        if (menuValidated)
        {
            return;
        }
        byte[] content = candidate.Read(new ResourceKey("menu/mngrp.bin"));
        byte[] header = candidate.Read(new ResourceKey("menu/mngrphd.bin"));
        MngrpArchive archive = MngrpArchive.Read(content, header, FF8Encoding.CreateEuropean());
        _ = NativeTutorialExamCatalog.Read(header, content, candidate.Source.Language);
        _ = NativeRefinementGroupReader.ReadTables(archive);
        _ = NativeRefinementGroupReader.ReadCardDescriptions(archive);
        _ = archive.GetSection<MngrpStringTableSection>(87);
        _ = archive.GetSection<MngrpOpaqueSection>(7);
        menuValidated = true;
    }

    private static void ValidateWorldData(ResourceKey key, byte[] bytes)
    {
        switch (key.Path)
        {
            case "world/wmx.obj":
                NativeWorldDataReader.ValidateTerrain(bytes);
                break;
            case "world/texl.obj":
                NativeWorldDataReader.ValidateTerrainTextures(bytes);
                break;
            case "world/rail.obj":
                NativeWorldDataReader.ValidateRail(bytes);
                break;
            case "world/chara.one":
                NativeWorldDataReader.ValidateCharacters(bytes);
                break;
            default:
                _ = NativeWorldDataReader.ReadMusicFile(bytes, key.Path[11] - '0');
                break;
        }
    }

    private void ValidateWorld(byte[] bytes)
    {
        int[] sections = WorldAssetReader.ReadSections(bytes);
        _ = new WorldMapLocations(bytes);
        _ = WorldAssetReader.ReadSpawns(bytes, sections);
        _ = WorldFieldEntryReader.Read(bytes.AsSpan(sections[7], sections[8] - sections[7]), candidate.Read(new ResourceKey("main/wm2field.tbl")));
        foreach (int pointer in WorldAssetReader.InnerPointers(bytes, sections[32]))
        {
            _ = WorldAssetReader.ReadSky(bytes, checked(sections[32] + pointer));
        }
        ReadOnlySpan<byte> text = bytes.AsSpan(sections[13], sections[14] - sections[13]);
        if (text.Length < 4)
        {
            throw new InvalidDataException("World messages require their native pointer directory.");
        }
        int tableBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(text));
        if (tableBytes < 4 || tableBytes % 4 != 0 || tableBytes > text.Length)
        {
            throw new InvalidDataException("World message pointer directory is invalid.");
        }
        for (int index = 0; index < tableBytes / 4; index++)
        {
            int pointer = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(text[(index * 4)..]));
            if (pointer == 0 && index == tableBytes / 4 - 1)
            {
                break;
            }
            if (pointer < tableBytes || pointer >= text.Length)
            {
                throw new InvalidDataException("World message pointer exceeds its native text section.");
            }
            int end = text[pointer..].IndexOf((byte)0);
            if (end < 0)
            {
                throw new InvalidDataException("World message has no bounded terminator.");
            }
            _ = FF8Encoding.CreateEuropean().GetString(text.Slice(pointer, end));
        }
    }

    private static byte[] Read(ResourceContent content)
    {
        using Stream stream = content.OpenRead();
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        battle?.Dispose();
    }
}
