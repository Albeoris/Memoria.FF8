using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Transfers persisted original save owners and supplies cold defaults for process-only state.</summary>
public static partial class NativePcSaveConverter
{
    private const int Main = NativePcSaveData.MainOffset;
    private const int Event = NativePcSaveData.EventBankOffset;
    private const int World = Main + 4624;

    public static GameState Import(byte[] file, GameAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        GameSession session = GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
        session.StartNewGame();
        GameState defaults = session.State with
        {
            CharacterVitals = assets.ReadNewGameCharacterVitals(),
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            Economy = assets.ReadNewGameEconomy(),
            InputConfiguration = assets.ReadNewGameInputConfiguration(),
            ShopHistory = assets.ReadNewGameShopHistory(),
            WeaponRecipes = assets.ReadNewGameWeaponRecipes(),
            ShotAvailability = assets.ReadNewGameShotAvailability(),
            LimitProgress = assets.ReadNewGameLimitProgress(),
            PocketStation = assets.ReadNewGamePocketStation(),
            KnownMagic = assets.ReadNewGameKnownMagic(),
            ScannedModels = assets.ReadNewGameScannedModels(),
            SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(),
            GlobalRandom = assets.ReadNewGameGlobalRandom(),
            SeeD = session.State.SeeD! with { SalaryTable = assets.ReadNativeSalaryTable() }
        };
        return Import(file, defaults, id => assets.LoadField(id).Name);
    }

    public static GameState Import(byte[] file, GameState defaults, Func<int, string> resolveFieldName)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(resolveFieldName);
        NativePcSaveData original = NativePcSaveCodec.Decode(file);
        byte[] block = original.Block;
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        Dictionary<int, string> names = [];
        foreach ((int subject, int offset) in new[] { (0, 408), (1, 420), (2, 432), (GameNames.BokoSubject, 444), (GameNames.GrieverSubject, Main + 2732) }.Concat(Enumerable.Range(0, 16).Select(gf => (16 + gf, Main + gf * 68))))
        {
            int count = Array.IndexOf(block, (byte)0, offset, 12) is int end && end >= offset ? end - offset : 12;
            string name = encoding.GetString(block, offset, count);
            if (GameNames.IsValidDraft(name) && !string.IsNullOrWhiteSpace(name))
            {
                names[subject] = name;
            }
        }
        original = original with { Names = names };
        byte B(int offset) => block[Main + offset];
        ushort W(int offset) => ReadWord(block, Main + offset);
        uint D(int offset) => ReadDWord(block, Main + offset);
        byte E(int address) => block[Event + address];
        ushort EW(int address) => ReadWord(block, Event + address);
        uint ED(int address) => ReadDWord(block, Event + address);
        ushort module = W(3312) == 2 ? (ushort)2 : (ushort)1;
        NativeCharacterVitals vitals = ReadCharacters(block, defaults.CharacterVitals ?? throw new InvalidDataException("Original save import requires installed kernel character definitions."));
        NativeEconomy economy = defaults.Economy ?? throw new InvalidDataException("Original save import requires initialized inventory definitions.");
        NativeItemStock[] items = Enumerable.Range(0, NativeEconomy.ItemSlotCount).Select(slot => new NativeItemStock(B(2804 + 2 * slot), B(2805 + 2 * slot))).ToArray();
        NativeCharacterAppearance? appearance = defaults.CharacterAppearance is { } dress ? dress with { Records = Enumerable.Range(0, 8).Select(row => new NativeCharacterDress(B(1088 + 152 * row + 91))).ToArray() } : null;
        NativeInputConfiguration? configuration = defaults.InputConfiguration is { } input ? input with { Options = (input.Options & 0x00FFFFFF) | (D(2708) & 0xFF000000), NativeBattleSpeedByte = B(2704), BattleMessageSpeed = B(2705), FieldMessageSpeedByte = B(2706) } : null;
        NativeCardInventory cards = NativeCardInventory.CreateNewGame() with
        {
            Cards = block.AsSpan(Main + 4752, 110).ToArray(),
            ObtainedRareCards = block.AsSpan(Main + 4862, 5).ToArray(),
            CollectionFlags = B(4867),
            TripleTriadStatistics = new TripleTriadStatistics(W(4868), W(4870), W(4872)),
            TripleTriadRandomState = D(4876)
        };
        NativeSeeDState seed = (defaults.SeeD ?? NativeSeeDState.CreateNewGame()) with { RankPoints = EW(16), KillBaseline18 = EW(18), SalaryMovementCounter = ED(8), SalaryDisplayCountdown184 = EW(184), PreviousRank186 = EW(186), SalaryUiEnabled = (ED(104) & 16) == 0 };
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame() with { EventFlags103 = E(103), EventFlags104 = ED(104), MusicKeep207 = E(207), Menu209 = E(209) };
        NativeExamTimerState timer = (defaults.ExamTimer ?? NativeExamTimerState.FromColdProcess()).SetRemaining(unchecked((int)D(3204))) with { Event182Mask4 = (E(182) & 4) != 0, Event183Mask1 = (E(183) & 1) != 0, SaveGlobalMask4 = (B(3282) & 4) != 0 };
        NativeFieldFadeState fade = defaults.FieldFade ?? NativeFieldFadeState.FromColdProcess();
        NativeFadeWords savedFade = NativeFadeWords.Zero();
        for (int index = 0; index < 12; index++)
        {
            savedFade = savedFade.With(index, unchecked((short)EW(216 + 2 * index)));
        }
        int fieldId = module == 1 ? W(3314) : EW(84);
        if (fieldId < 72)
        {
            throw new InvalidDataException("Original save has no ordinary field location or previous field.");
        }
        GameState result = defaults with
        {
            OriginalSave = original,
            Scene = module == 1 ? SceneKind.Field : SceneKind.WorldMap,
            FieldId = fieldId,
            FieldName = resolveFieldName(fieldId),
            PreviousFieldId = module == 1 ? W(3316) : EW(84),
            FieldLoadContext = module == 1 ? NativeFieldLoadContext.FromOriginalSave(fieldId) : null,
            FieldArrival = module == 1 ? new FieldTransition(fieldId, unchecked((short)W(3318)), unchecked((short)W(3324)), W(3330), B(3336)) : null,
            X = module == 1 ? unchecked((short)W(3318)) : 0,
            Y = module == 1 ? unchecked((short)W(3324)) : 0,
            EventState = null,
            DepartingFieldState = null,
            World = null,
            Battle = null,
            NativeFieldLocation = null,
            FieldRequestPacket = null,
            CharacterVitals = vitals,
            CharacterAppearance = appearance,
            Economy = economy with { Items = items, ItemOrder = block.AsSpan(Main + 2772, 32).ToArray(), Gil = D(2748), PastGil = D(2752), EventBankGil72 = ED(72), EventBankPastGil76 = ED(76) },
            InputConfiguration = configuration,
            CardInventory = cards,
            SeeD = seed,
            WorldControls = controls,
            DiscProcess = NativeDiscProcessState.CreateNewGame().Assign(E(204)),
            WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame() with { Bytes = block.AsSpan(Event + 256, 12).ToArray().ToImmutableArray() },
            EventRandom = (defaults.EventRandom ?? NativeEventRandomState.FromColdProcess()) with { DrawPoints = new NativeDrawPointMemory(block.AsSpan(Event + 116, 64).ToArray().ToImmutableArray(), EW(180)) },
            FieldFade = fade with { Saved = savedFade },
            ExamTimer = timer,
            Playtime = new NativePlaytimeState(D(3200), 0, 0),
            ShopHistory = defaults.ShopHistory is { } shops ? shops with { Bytes = block.AsSpan(Main + 2304, 400).ToArray().ToImmutableArray() } : null,
            WeaponRecipes = defaults.WeaponRecipes is { } recipes ? recipes with { Bits = D(2728) } : null,
            ShotAvailability = defaults.ShotAvailability is { } shot ? shot with { Flags770 = B(2760) } : null,
            LimitProgress = defaults.LimitProgress is { } limits ? limits with { BlueMagicFlags = W(2756), DuelFlags = W(2758), SpecialMagicFlags = B(2761), AngeloFlags = B(2762), PetPalsFlags = B(2763), TrainingSelection = B(2747), TrainingCounters = block.AsSpan(Main + 2764, 8).ToArray().ToImmutableArray(), Options978 = W(3280) } : null,
            PocketStation = defaults.PocketStation is { } pocket ? pocket with { Raw = block.AsSpan(Main + 4880, 64).ToArray().ToImmutableArray(), ChocoboName = block.AsSpan(444, 12).ToArray().ToImmutableArray() } : null,
            KnownMagic = defaults.KnownMagic is { } magic ? magic with { Words = Enumerable.Range(0, 2).Select(index => D(3252 + index * 4)).ToImmutableArray() } : null,
            ScannedModels = defaults.ScannedModels is { } scans ? scans with { Words = Enumerable.Range(0, 5).Select(index => D(3260 + index * 4)).ToImmutableArray() } : null,
            SavedBattleFlags = defaults.SavedBattleFlags is { } flags ? flags with { Value = B(3282), KnownMask = null, FieldWrite = null, WitchFieldWrite = null } : null,
            BattleVariables = new NativeBattleVariableState(Enumerable.Range(0, 8).Select(index => D(3220 + index * 4)).ToImmutableArray()),
            GlobalRandom = new NativeGlobalRandomState(D(4876)),
            SavedWorld = ReadWorld(block, defaults.SavedWorld ?? NativeSavedWorldState.CreateNewGame(), module == 2),
            WorldArrival = module == 2 ? new WorldTransition(62, block[World + 105], -1) : null
        };
        if (module == 2 && result.SavedWorld?.FootReturn is { } footReturn)
        {
            result = result with { WorldArrival = new WorldTransition(62, footReturn.EntityType105, -1) };
        }
        IReadOnlyList<NativeEventMemoryRegion> rootRegions = NativeEventMemoryOwnership.RootRegions(result);
        IReadOnlyList<NativeEventMemoryRegion> sceneRegions = NativeEventMemoryOwnership.SceneRegions((NativeEventSceneMemoryOwners)((1 << 13) - 1));
        Dictionary<int, NativeMemoryValue> cells = [];
        for (int address = 0; address < 1536; address++)
        {
            if (address is >= 1024 and < 1408 || rootRegions.Any(region => region.Contains(address, 1)) || sceneRegions.Any(region => region.Contains(address, 1)))
            {
                continue;
            }
            cells[address] = new NativeMemoryValue(1, NativeScalarValue.Exact(E(address)));
        }
        result = result with { EventMemory = new NativeEventMemory(NativeEventMemory.CurrentVersion, cells) };
        _ = new GameSession(result);
        return result;
    }

    private static NativeCharacterVitals ReadCharacters(byte[] block, NativeCharacterVitals defaults)
    {
        NativeCharacterVital[] characters = new NativeCharacterVital[8];
        for (int index = 0; index < characters.Length; index++)
        {
            int start = Main + 1088 + index * 152;
            ReadOnlySpan<byte> row = block.AsSpan(start, 152);
            ushort hp = ReadWord(block, start);
            NativeCharacterJunction junction = new(ReadWord(block, start + 88), ReadDWord(block, start + 80), ReadDWord(block, start + 84), row.Slice(92, 20).ToArray());
            NativeMagicStock[] inventory = Enumerable.Range(0, 32).Select(slot => new NativeMagicStock(block[start + 16 + slot * 2], block[start + 17 + slot * 2])).ToArray();
            characters[index] = new NativeCharacterVital(row[8], new NativeHpWord(hp, hp), ReadWord(block, start + 150), junction, ReadDWord(block, start + 4), ReadWord(block, start + 2), row[9], row.Slice(10, 6).ToArray(), inventory, ReadWord(block, start + 148), ReadWord(block, start + 144)) { DeathCount = ReadWord(block, start + 146), GuardianForceCompatibility = Enumerable.Range(0, 16).Select(gf => ReadWord(block, start + 112 + gf * 2)).ToArray() };
        }
        NativeGuardianForceVital[] guardianForces = new NativeGuardianForceVital[16];
        for (int index = 0; index < guardianForces.Length; index++)
        {
            int start = Main + index * 68;
            ReadOnlySpan<byte> row = block.AsSpan(start, 68);
            guardianForces[index] = new NativeGuardianForceVital(ReadDWord(block, start + 12), row[17], ReadWord(block, start + 18), new NativeGuardianForceLearnedBank(BinaryPrimitives.ReadUInt64LittleEndian(row[20..]), BinaryPrimitives.ReadUInt64LittleEndian(row[28..])), null, row.Slice(36, 21).ToArray().ToImmutableArray(), row[64], (uint)(row[65] | row[66] << 8 | row[67] << 16)) { KillCount = ReadWord(block, start + 60), DeathCount = ReadWord(block, start + 62) };
        }
        return (defaults with { Records = characters, GuardianForces = guardianForces, CombatParty = block.AsSpan(Main + 2724, 3).ToArray(), DreamWeapons = block.AsSpan(Main + 2744, 3).ToArray(), DreamEnabled = (block[Main + 3282] & 1) != 0, JunctionPartyBackup = block.AsSpan(Event + 188, 6).ToArray() }).RebuildParty();
    }

    private static NativeSavedWorldState ReadWorld(byte[] block, NativeSavedWorldState defaults, bool active)
    {
        NativeSavedWorldPose Pose(int offset) => new(unchecked((int)ReadDWord(block, World + offset)), unchecked((int)ReadDWord(block, World + offset + 4)), unchecked((short)ReadWord(block, World + offset + 8)), ReadWord(block, World + offset + 10));
        byte entity = block[World + 105];
        NativeSavedWorldState result = defaults with
        {
            Origin = new NativeSavedWorldOrigin(NativeSavedWorldState.CurrentProofVersion, NativeSavedWorldOriginKind.OriginalSave, NativeSavedWorldState.OriginalSaveInitializationRule),
            InitialMapDisplayMode110 = block[World + 110] == 3 ? (byte)0 : block[World + 110],
            Slot4 = new NativeSavedWorldVehicleSlot(block[World + 98], Pose(48)),
            Slot5 = new NativeSavedWorldVehicleSlot(block[World + 99], Pose(60)),
            GardenPose3 = Pose(36),
            RagnarokPose2 = Pose(24),
            FuelDistance112 = ReadDWord(block, World + 112),
            WorldEventFlags116 = new NativeWorldEventFlags(BinaryPrimitives.ReadUInt64LittleEndian(block.AsSpan(World + 116)), ulong.MaxValue),
            WorldEventVariable124 = block[World + 124],
            WorldEventVariable125 = block[World + 125],
            FootReturn = null,
            CarReturn = null,
            ScriptedReturn = null,
            GardenReturn = null,
            RagnarokReturn = null
        };
        ushort lastField = ReadWord(block, Event + 84);
        ushort story = ReadWord(block, Event + 256);
        ushort camera = ReadWord(block, World + 96);
        byte map = block[World + 110] == 3 ? (byte)0 : block[World + 110];
        // A field save's preview names its current field, not the earlier saved world location.
        short location = active ? unchecked((short)ReadWord(block, 388)) : (short)0;
        if (entity <= 2)
        {
            result = result with { FootReturn = new NativeSavedWorldFootReturn { ProofVersion = 1, Rule = NativeSavedWorldFootReturn.OriginalSaveRule, SourceWorldFrame = 0, LastFieldId = lastField, StoryWord = story, WorldMode = 0, Pose0 = Pose(0), CameraYaw96 = camera, EntityType105 = entity, Channel106 = 255, Channel107 = 255, AnimationVariant108 = block[World + 108], MapDisplayMode110 = map, MapLocationId = location } };
        }
        else if (entity == 48)
        {
            result = result with { GardenReturn = new NativeSavedWorldGardenReturn(1, NativeSavedWorldGardenReturn.OriginalSaveRule, 0, lastField, story, Pose(36), camera, entity, 255, 255, block[World + 108], map, location) };
        }
        else if (entity == 50)
        {
            result = result with { RagnarokReturn = new NativeSavedWorldRagnarokReturn(NativeSavedWorldRagnarokReturn.OriginalSaveRule, 0, lastField, story, Pose(24), camera, map, location) };
        }
        else
        {
            result = result with { FootReturn = new NativeSavedWorldFootReturn { ProofVersion = 1, Rule = NativeSavedWorldFootReturn.OriginalSaveRule, SourceWorldFrame = 0, LastFieldId = lastField, StoryWord = story, WorldMode = 0, Pose0 = Pose(0), CameraYaw96 = camera, EntityType105 = 0, Channel106 = 255, Channel107 = 255, AnimationVariant108 = 0, MapDisplayMode110 = map, MapLocationId = location } };
        }
        return result;
    }

    private static ushort ReadWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static uint ReadDWord(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static void WriteWord(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void WriteDWord(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
