using System.Text.Json;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static partial class NativePcSaveConverter
{
    public static byte[] Export(GameState state, NativeCharacterVitals targetSource, bool allowGuardianForceLoss = false)
    {
        NativeGuardianForceLegacyExportPlan plan = NativeGuardianForceLegacyCompatibility.Analyze(state, targetSource);
        if (plan.RequiresConfirmation && !allowGuardianForceLoss)
        {
            throw new NotSupportedException("Original FF8 export requires confirmation of GF configuration/progression losses: " + string.Join("; ", plan.Losses.Select(loss => loss.Description)));
        }
        return ExportCore(plan.ProjectedState);
    }

    public static byte[] Export(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.CharacterVitals is { } vitals && (vitals.GuardianForceProfile is not null || vitals.GuardianForceRecoveryProfile is not null || vitals.GuardianForceOpaqueBaseline || !vitals.MenuDefinitions.Templates.IsDefault || vitals.GuardianForces.Any(gf => gf.Progression is not null)))
        {
            if (vitals.MenuDefinitions.SourceTemplates.IsDefault)
            {
                throw new NotSupportedException("Original FF8 export requires declared target source GF definitions for this managed ability state.");
            }
            NativeCharacterVitals target = vitals with { MenuDefinitions = vitals.MenuDefinitions with { Templates = vitals.MenuDefinitions.SourceTemplates } };
            return Export(state, target);
        }
        return ExportCore(state);
    }

    private static byte[] ExportCore(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.GameplayPreferences.ChocoboWorldMode is not null || state.ChocoboRewards.HasState)
        {
            throw new NotSupportedException("Chocobo World provider choices and acquisition history require a Memoria save; native FFVIII exports cannot retain them.");
        }
        if (state.Scene == SceneKind.Title || state.CharacterVitals is null || state.Economy is null)
        {
            throw new NotSupportedException("Original PC export requires an active game with initialized characters and inventory.");
        }
        if (state.CharacterVitals.MagicSystem.RequiresMemoriaFormat || state.GameplayPreferences.SharedMagicStock || state.GameplayPreferences.InfiniteMagic || state.GameplayPreferences.SmartAttack || state.GameplayPreferences.GuardianFormsEnabled || state.GameplayPreferences.LimitOncePerTurn || state.GameplayPreferences.SelphieCardSlots || state.GameplayPreferences.WeaponMagazineRequired || state.GameplayPreferences.WeaponUpgradeOrder || state.WeaponRecipes?.ObtainedWeaponsBits is > 0 || state.WeaponRecipes?.MagazineRecipeBits is > 0 || state.GameplayPreferences.CardModOnce || !state.RefinedCardIds.IsEmpty || state.GameplayPreferences.DrawOncePerSpell || state.GameplayPreferences.Difficulty != BattleDifficulty.Normal || state.PendingGameplayPreferences is not null)
        {
            throw new NotSupportedException("This game contains gameplay rules or usage history that original FF8 saves cannot preserve. Choose a Memoria save slot.");
        }
        byte[] block = state.OriginalSave?.Block.ToArray() ?? CreateDefaultBlock();
        void W(int offset, ushort value) => WriteWord(block, Main + offset, value);
        void D(int offset, uint value) => WriteDWord(block, Main + offset, value);
        void EW(int address, ushort value) => WriteWord(block, Event + address, value);
        void ED(int address, uint value) => WriteDWord(block, Event + address, value);
        void Copy(int offset, IEnumerable<byte> bytes) => bytes.ToArray().CopyTo(block, Main + offset);
        void EventCopy(int address, IEnumerable<byte> bytes) => bytes.ToArray().CopyTo(block, Event + address);
        foreach ((int address, NativeMemoryValue cell) in state.EventMemory.Cells)
        {
            if (address >= 1536 || address + cell.Width > 1536 || !cell.Value.IsExact)
            {
                continue;
            }
            for (int index = 0; index < cell.Width; index++)
            {
                block[Event + address + index] = unchecked((byte)((uint)cell.Value.Values[0] >> (8 * index)));
            }
        }
        if (state.CharacterVitals is { } vitals)
        {
            vitals.Validate();
            for (int row = 0; row < 8; row++)
            {
                NativeCharacterVital character = vitals.Records[row];
                int start = 1088 + row * 152;
                W(start, character.HitPoints.Minimum);
                W(start + 2, character.HitPointBonus);
                D(start + 4, character.Experience);
                block[Main + start + 8] = character.CharacterId;
                block[Main + start + 9] = character.Weapon;
                Copy(start + 10, character.StatBonuses);
                for (int slot = 0; slot < 32; slot++)
                {
                    block[Main + start + 16 + 2 * slot] = character.MagicInventory[slot].SpellId;
                    block[Main + start + 17 + 2 * slot] = character.MagicInventory[slot].Count;
                }
                D(start + 80, character.Junction.Commands);
                D(start + 84, character.Junction.Abilities);
                W(start + 88, character.Junction.GuardianForces);
                Copy(start + 92, character.Junction.MagicJunctions);
                for (int gf = 0; gf < 16; gf++)
                {
                    W(start + 112 + gf * 2, character.GuardianForceCompatibility?[gf] ?? 0);
                }
                W(start + 144, character.KillCount);
                W(start + 146, character.DeathCount ?? 0);
                W(start + 148, character.MemberFlags);
                W(start + 150, character.Status);
            }
            for (int id = 0; id < 16; id++)
            {
                NativeGuardianForceVital gf = vitals.GuardianForces[id];
                int start = Main + id * 68;
                WriteDWord(block, start + 12, gf.Experience);
                block[start + 17] = gf.Flags;
                WriteWord(block, start + 18, gf.HitPoints);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(block.AsSpan(start + 20), gf.LearnedBank.Low);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(block.AsSpan(start + 28), gf.LearnedBank.High);
                if (gf.AbilityProgress.Length != 21)
                {
                    throw new NotSupportedException("Original FF8 GF AP storage requires a reviewed projection into exactly twenty-one bytes.");
                }
                gf.AbilityProgress.AsSpan().CopyTo(block.AsSpan(start + 36, 21));
                WriteWord(block, start + 60, gf.KillCount ?? 0);
                WriteWord(block, start + 62, gf.DeathCount ?? 0);
                block[start + 64] = gf.Learning;
                block[start + 65] = (byte)gf.ForgottenAbilities;
                block[start + 66] = (byte)(gf.ForgottenAbilities >> 8);
                block[start + 67] = (byte)(gf.ForgottenAbilities >> 16);
            }
            Copy(2724, vitals.CombatParty);
            Copy(2744, vitals.DreamWeapons);
            EventCopy(188, vitals.JunctionPartyBackup);
            block[Main + 3282] = (byte)((block[Main + 3282] & 0xFE) | (vitals.DreamEnabled ? 1 : 0));
        }
        if (state.CharacterAppearance is { } appearance)
        {
            for (int row = 0; row < 8; row++)
            {
                block[Main + 1088 + row * 152 + 91] = appearance.Records[row].Dress;
            }
        }
        if (state.Economy is { } economy)
        {
            economy.Validate();
            for (int slot = 0; slot < NativeEconomy.ItemSlotCount; slot++)
            {
                block[Main + 2804 + 2 * slot] = economy.Items[slot].ItemId;
                block[Main + 2805 + 2 * slot] = economy.Items[slot].Count;
            }
            Copy(2772, economy.ItemOrder ?? Enumerable.Range(0, 32).Select(rank => (byte)rank));
            D(2748, economy.Gil);
            D(2752, economy.PastGil);
            ED(72, economy.EventBankGil72);
            ED(76, economy.EventBankPastGil76);
        }
        if (state.CardInventory is { } cards)
        {
            Copy(4752, cards.Cards);
            Copy(4862, cards.ObtainedRareCards);
            block[Main + 4867] = cards.CollectionFlags ?? 0;
            W(4868, unchecked((ushort)(cards.TripleTriadStatistics?.Wins ?? 0)));
            W(4870, unchecked((ushort)(cards.TripleTriadStatistics?.Losses ?? 0)));
            W(4872, unchecked((ushort)(cards.TripleTriadStatistics?.Draws ?? 0)));
        }
        if (state.SeeD is { } seed)
        {
            ED(8, seed.SalaryMovementCounter);
            EW(16, seed.RankPoints);
            EW(18, seed.KillBaseline18);
            EW(184, seed.SalaryDisplayCountdown184);
            EW(186, seed.PreviousRank186);
        }
        if (state.WorldControls is { } controls)
        {
            block[Event + 103] = controls.EventFlags103;
            ED(104, controls.EventFlags104);
            block[Event + 207] = controls.MusicKeep207;
            block[Event + 209] = controls.Menu209;
        }
        if (state.WorldEntryMemory is { } entry)
        {
            EventCopy(256, entry.Bytes);
        }
        if (state.EventRandom?.DrawPoints is { } draw)
        {
            EventCopy(116, draw.Depletion);
            EW(180, draw.RegenerationCounter180);
        }
        if (state.FieldFade is { } fade)
        {
            for (int index = 0; index < 12; index++)
            {
                EW(216 + 2 * index, unchecked((ushort)fade.Saved.At(index)));
            }
        }
        if (state.ExamTimer is { } timer)
        {
            D(3204, unchecked((uint)timer.Alternatives.Min(row => row.Remaining)));
            if (timer.Event182Mask4 is bool flag182)
            {
                block[Event + 182] = (byte)((block[Event + 182] & ~4) | (flag182 ? 4 : 0));
            }
            if (timer.Event183Mask1 is bool flag183)
            {
                block[Event + 183] = (byte)((block[Event + 183] & ~1) | (flag183 ? 1 : 0));
            }
        }
        block[Event + 204] = state.DiscProcess?.SavedDisc204 ?? 1;
        D(3200, state.Playtime?.Seconds ?? 0);
        if (state.InputConfiguration is { } input)
        {
            D(2708, input.Options);
            Copy(2712, input.ButtonMap);
            block[Main + 2704] = input.NativeBattleSpeedByte ?? 0;
            block[Main + 2705] = input.BattleMessageSpeed ?? 0;
            block[Main + 2706] = input.FieldMessageSpeedByte ?? 0;
        }
        if (state.ShopHistory is { } shops)
        {
            Copy(2304, shops.Bytes);
        }
        D(2728, state.WeaponRecipes?.Bits ?? 0);
        block[Main + 2760] = state.ShotAvailability?.Flags770 ?? 0;
        if (state.LimitProgress is { } limits)
        {
            W(2756, limits.BlueMagicFlags);
            W(2758, limits.DuelFlags);
            block[Main + 2761] = limits.SpecialMagicFlags ?? 0;
            block[Main + 2762] = limits.AngeloFlags;
            block[Main + 2763] = limits.PetPalsFlags ?? 0;
            block[Main + 2747] = limits.TrainingSelection;
            Copy(2764, limits.TrainingCounters);
            W(3280, limits.Options978 ?? 0);
        }
        if (state.PocketStation is { } pocket)
        {
            Copy(4880, pocket.Raw);
            if (pocket.ChocoboName is { } name)
            {
                name.AsSpan().CopyTo(block.AsSpan(444));
            }
        }
        if (state.KnownMagic is { } known)
        {
            for (int index = 0; index < 2; index++)
            {
                D(3252 + index * 4, known.Words[index]);
            }
        }
        if (state.ScannedModels is { } scans)
        {
            for (int index = 0; index < 5; index++)
            {
                D(3260 + index * 4, scans.Words[index]);
            }
        }
        if (state.SavedBattleFlags is { } flags)
        {
            block[Main + 3282] = (byte)((flags.Value & 0xFE) | (state.CharacterVitals?.DreamEnabled == true ? 1 : 0));
        }
        if (state.BattleVariables is { } battle)
        {
            for (int index = 0; index < 8; index++)
            {
                D(3220 + index * 4, battle.Values[index]);
            }
        }
        D(4876, state.GlobalRandom?.State ?? state.CardInventory?.TripleTriadRandomState ?? 1);
        ExportSavedWorld(block, state);
        ExportLocation(block, state);
        UpdatePreview(block, state);
        return NativePcSaveCodec.Encode(new NativePcSaveData(block));
    }

    private static byte[] CreateDefaultBlock()
    {
        byte[] block = new byte[NativePcSaveData.BlockSize];
        block[0] = (byte)'S';
        block[1] = (byte)'C';
        block[2] = 0x11;
        block[3] = 1;
        WriteWord(block, 386, 0x08FF);
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        void Name(int offset, int subject)
        {
            byte[] bytes = encoding.GetBytes(GameNames.DefaultName(subject));
            bytes.AsSpan(0, Math.Min(11, bytes.Length)).CopyTo(block.AsSpan(offset, 12));
        }
        Name(408, 0);
        Name(420, 1);
        Name(432, 2);
        Name(444, GameNames.BokoSubject);
        Name(Main + 2732, GameNames.GrieverSubject);
        for (int gf = 0; gf < 16; gf++)
        {
            Name(Main + gf * 68, 16 + gf);
        }
        for (int row = 0; row < 8; row++)
        {
            block[Main + 1088 + row * 152 + 8] = (byte)row;
        }
        block[Main + 2725] = 255;
        block[Main + 2726] = 255;
        block[Main + 2727] = 255;
        for (int index = 0; index < 32; index++)
        {
            block[Main + 2772 + index] = (byte)index;
        }
        WriteDWord(block, Main + 2708, 0x00020001);
        for (int index = 0; index < 12; index++)
        {
            block[Main + 2712 + index] = (byte)(index + 1);
        }
        block[Event + 204] = 1;
        WriteDWord(block, Event, 0x382D4646);
        return block;
    }

    private static void ExportLocation(byte[] block, GameState state)
    {
        JsonElement? scene = state.EventState is { } snapshot && snapshot.TryGetProperty("Scene", out JsonElement savedScene) ? savedScene : null;
        if (scene is { } tutorialScene && tutorialScene.TryGetProperty("UnlockedTutorials", out JsonElement tutorials))
        {
            if (tutorials.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Original save tutorial availability requires its current 128-bit owner.");
            }
            block.AsSpan(Main + 3283, 16).Clear();
            foreach (JsonElement tutorial in tutorials.EnumerateArray())
            {
                if (tutorial.ValueKind != JsonValueKind.Number || !tutorial.TryGetInt32(out int id) || id is < 0 or >= 128)
                {
                    throw new InvalidDataException("Original save tutorial availability exceeds its 128-bit owner.");
                }
                block[Main + 3283 + id / 8] |= (byte)(1 << (id & 7));
            }
        }
        byte[] party = scene is { } current && current.TryGetProperty("Party", out JsonElement values) && values.GetArrayLength() >= 3 ? values.EnumerateArray().Take(3).Select(value => unchecked((byte)value.GetInt32())).ToArray() : state.OriginalSave?.FieldParty ?? state.CharacterVitals?.CombatParty.Select(row => row == 255 ? (byte)255 : state.CharacterVitals.Records[row].CharacterId).ToArray() ?? [0, 255, 255];
        party.CopyTo(block, Main + 3304);
        block[Main + 3307] = 255;
        int fieldId = state.Scene == SceneKind.Field ? state.FieldId : state.NativeFieldLocation?.FieldId ?? state.FieldArrival?.FieldId ?? state.FieldId;
        if (fieldId < 72 || fieldId > ushort.MaxValue)
        {
            fieldId = 74;
        }
        WriteWord(block, Main + 3312, state.Scene == SceneKind.WorldMap ? (ushort)2 : (ushort)1);
        WriteWord(block, Main + 3314, (ushort)fieldId);
        WriteWord(block, Main + 3316, unchecked((ushort)(state.PreviousFieldId ?? fieldId)));
        WriteWord(block, Event + 84, unchecked((ushort)(state.PreviousFieldId ?? fieldId)));
        bool anyPose = false;
        for (int slot = 0; slot < 3; slot++)
        {
            NativeFieldLocationPose? pose = null;
            if (scene is { } owned && owned.TryGetProperty("NativeFieldInitialization", out JsonElement initialization) && initialization.ValueKind == JsonValueKind.Object && initialization.TryGetProperty("Stage", out JsonElement stage) && (stage.ValueKind == JsonValueKind.String ? stage.GetString() == "Completed" : stage.GetInt32() == (int)NativeFieldInitializationStage.Completed) && state.EventState is { } actorsSnapshot && actorsSnapshot.TryGetProperty("Actors", out JsonElement actors))
            {
                int character = party[slot];
                JsonElement actor = actors.EnumerateArray().FirstOrDefault(actor => actor.TryGetProperty("CharacterId", out JsonElement identity) && identity.GetInt32() == character && character != 255 && actor.TryGetProperty("Active", out JsonElement active) && active.ValueKind == JsonValueKind.True && actor.TryGetProperty("WalkmeshTriangle", out JsonElement triangle) && triangle.TryGetInt32(out int index) && index is >= 0 and < ushort.MaxValue);
                if (actor.ValueKind == JsonValueKind.Object && actor.TryGetProperty("X", out JsonElement x) && actor.TryGetProperty("Y", out JsonElement y) && actor.TryGetProperty("WalkmeshTriangle", out JsonElement triangle) && actor.TryGetProperty("Direction", out JsonElement direction))
                {
                    pose = new NativeFieldLocationPose(unchecked((short)(int)Math.Floor(x.GetDouble())), unchecked((short)(int)Math.Floor(y.GetDouble())), unchecked((ushort)triangle.GetInt32()), unchecked((byte)(int)direction.GetDouble()));
                }
            }
            pose ??= state.NativeFieldLocation?.FieldId == fieldId ? state.NativeFieldLocation.Slots[slot].Pose : null;
            if (pose is null && slot == 0 && state.FieldArrival is { } arrival && arrival.FieldId == fieldId)
            {
                pose = new NativeFieldLocationPose(unchecked((short)(int)Math.Floor(arrival.X)), unchecked((short)(int)Math.Floor(arrival.Y)), unchecked((ushort)arrival.Triangle), unchecked((byte)arrival.Direction));
            }
            if (pose is null)
            {
                continue;
            }
            anyPose = true;
            WriteWord(block, Main + 3318 + 2 * slot, unchecked((ushort)pose.X));
            WriteWord(block, Main + 3324 + 2 * slot, unchecked((ushort)pose.Y));
            WriteWord(block, Main + 3330 + 2 * slot, pose.Triangle);
            block[Main + 3336 + slot] = pose.Direction;
        }
        if (!anyPose && state.OriginalSave is null && state.Scene != SceneKind.WorldMap)
        {
            WriteWord(block, Main + 3314, 74);
            WriteWord(block, Main + 3316, 74);
            WriteWord(block, Event + 84, 74);
        }
        if (scene is { } source)
        {
            if (source.TryGetProperty("Names", out JsonElement names) && names.ValueKind == JsonValueKind.Object)
            {
                FF8Encoding encoding = FF8Encoding.CreateEuropean();
                foreach (JsonProperty name in names.EnumerateObject())
                {
                    if (!int.TryParse(name.Name, out int subject) || name.Value.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }
                    int offset = subject switch { 0 => 408, 1 => 420, 2 => 432, GameNames.BokoSubject => 444, GameNames.GrieverSubject => Main + 2732, >= 16 and <= 31 => Main + (subject - 16) * 68, _ => -1 };
                    if (offset < 0)
                    {
                        continue;
                    }
                    byte[] encoded = encoding.GetBytes(name.Value.GetString()!);
                    block.AsSpan(offset, 12).Clear();
                    encoded.AsSpan(0, Math.Min(encoded.Length, 11)).CopyTo(block.AsSpan(offset));
                }
            }
            foreach ((string property, int offset) in new[] { ("NativeBattleMusic203", 203), ("SoundMusicVolume197", 197), ("SoundMusicVolume198", 198), ("DrawPointIndex", 242) })
            {
                if (source.TryGetProperty(property, out JsonElement value) && value.TryGetByte(out byte number))
                {
                    block[Event + offset] = number;
                }
            }
            if (source.TryGetProperty("PlaceId", out JsonElement place) && place.TryGetUInt16(out ushort placeId))
            {
                WriteWord(block, 388, placeId);
            }
        }
    }

    private static void ExportSavedWorld(byte[] block, GameState state)
    {
        void Pose(int offset, NativeSavedWorldPose pose)
        {
            WriteDWord(block, World + offset, unchecked((uint)pose.X));
            WriteDWord(block, World + offset + 4, unchecked((uint)pose.RawZ));
            WriteWord(block, World + offset + 8, unchecked((ushort)pose.Height));
            WriteWord(block, World + offset + 10, pose.Yaw);
        }
        if (state.SavedWorld is { } saved)
        {
            Pose(48, saved.Slot4.Pose);
            Pose(60, saved.Slot5.Pose);
            block[World + 98] = saved.Slot4.ShapeId;
            block[World + 99] = saved.Slot5.ShapeId;
            if (saved.RagnarokPose2 is { } ragnarok)
            {
                Pose(24, ragnarok);
            }
            if (saved.GardenPose3 is { } garden)
            {
                Pose(36, garden);
            }
            if (saved.FootReturn is { } foot)
            {
                Pose(0, foot.Pose0);
                WriteWord(block, World + 96, foot.CameraYaw96);
                block[World + 105] = foot.EntityType105;
                block[World + 108] = foot.AnimationVariant108 ?? 0;
                block[World + 110] = foot.MapDisplayMode110 ?? 0;
            }
            WriteDWord(block, World + 112, saved.FuelDistance112 ?? 0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(block.AsSpan(World + 116), saved.WorldEventFlags116?.Value ?? 0);
            block[World + 124] = saved.WorldEventVariable124 ?? 0;
            block[World + 125] = saved.WorldEventVariable125 ?? 0;
        }
        if (state.Scene == SceneKind.WorldMap && state.World is { } world)
        {
            byte entity = world.NativeGardenRuntime is not null ? (byte)48 : world.NativeRagnarokRuntime is not null ? (byte)50 : world.NativeFootRuntime?.EntityType ?? 0;
            int offset = entity == 48 ? 36 : entity == 50 ? 24 : 0;
            Pose(offset, new NativeSavedWorldPose(world.X, world.Z, unchecked((short)world.NativeHeight), unchecked((ushort)world.BodyYaw)));
            WriteWord(block, World + 96, unchecked((ushort)world.CameraYaw));
            block[World + 105] = entity;
            block[World + 108] = world.NativeFootRuntime?.AnimationVariant ?? world.NativeGardenRuntime?.AnimationVariant ?? 0;
            block[World + 110] = world.NativeFootRuntime?.MapDisplayMode ?? world.NativeGardenRuntime?.MapDisplayMode ?? 0;
        }
        block[World + 106] = 255;
        block[World + 107] = 255;
    }

    private static void UpdatePreview(byte[] block, GameState state)
    {
        NativeCharacterVitals? vitals = state.CharacterVitals;
        byte leader = vitals?.CombatParty.FirstOrDefault(row => row != 255, (byte)0) ?? 0;
        if (vitals is not null)
        {
            NativeCharacterVital character = vitals.Records[leader];
            WriteWord(block, 390, character.HitPoints.Minimum);
            WriteWord(block, 392, vitals.Definitions.CharacterMaximum(character));
            block[404] = NativeStatDefinitions.Level(character.Experience, vitals.Definitions.Characters[character.CharacterId]);
            for (int slot = 0; slot < 3; slot++)
            {
                byte row = vitals.CombatParty[slot];
                block[405 + slot] = row == 255 ? (byte)255 : vitals.Records[row].CharacterId;
            }
        }
        WriteDWord(block, 396, vitals?.DreamEnabled == true ? state.Economy?.PastGil ?? 0 : state.Economy?.Gil ?? 0);
        WriteDWord(block, 400, state.Playtime?.Seconds ?? 0);
        WriteDWord(block, 456, state.DiscProcess?.CurrentDiscIndex ?? 0);
    }
}
