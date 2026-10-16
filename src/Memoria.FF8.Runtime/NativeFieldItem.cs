namespace Memoria.FF8.Runtime;

/// <summary>The four original menu/mitem.bin bytes consumed by4F9365.</summary>
public sealed record NativeFieldItem(byte Type, byte TargetFlags, byte Power, byte StatusMask)
{
    public NativeSolomonRingDefinition? SolomonRingDefinition { get; init; }
    public bool SupportsInventoryUse => Type == 15 && TargetFlags == 1 && SolomonRingDefinition is not null;
    public bool SupportsCharacterUse => Type is 0 or 1 or 2 or 16 or 20 or 21 && (TargetFlags & 3) == 3 && (TargetFlags & 4) == 0;
    public bool SupportsGuardianForceUse => Type is 3 or 4 or 5 or 17 or 18 && (TargetFlags & 5) == 5 && (TargetFlags & 2) == 0;
    public bool SupportsBlueMagicUse => Type == 19 && (TargetFlags & 0x43) == 0x43 && Power < 16;

    public (NativeEconomy Economy, NativeLimitProgress Progress, bool Changed) LearnBlueMagic(NativeCharacterVitals vitals, NativeEconomy economy, NativeLimitProgress progress, int itemId, int row)
    {
        vitals.Validate();
        economy.Validate();
        progress.Validate();
        if (!SupportsBlueMagicUse || (uint)row >= 8 || vitals.InitOutSha256 != economy.InitOutSha256 || progress.InitOutSha256 != economy.InitOutSha256)
        {
            throw new NotSupportedException("Blue Magic items require their original metadata and matching saved owners.");
        }
        NativeCharacterVital character = vitals.Records[row];
        if (vitals.DreamEnabled || character.CharacterId != 3 || (character.MemberFlags & 1) == 0)
        {
            throw new InvalidOperationException("Blue Magic items require the current native Quistis menu target.");
        }
        int slot = Array.FindIndex(economy.Items, stock => stock.ItemId == itemId && stock.Count > 0);
        if (slot < 0)
        {
            throw new InvalidOperationException("The selected Blue Magic item stock is exhausted.");
        }
        ushort flag = (ushort)(1 << Power);
        if ((progress.BlueMagicFlags & flag) != 0)
        {
            return (economy, progress, false);
        }
        return (Consume(economy, slot), progress with { BlueMagicFlags = (ushort)(progress.BlueMagicFlags | flag) }, true);
    }

    public (NativeCharacterVitals Vitals, NativeEconomy Economy, bool Changed) Use(NativeCharacterVitals vitals, NativeEconomy economy, int itemId, int row, BattleNativePlayerStatRules? statRules = null, int? forgottenAbility = null)
    {
        vitals.Validate();
        economy.Validate();
        if (SupportsInventoryUse)
        {
            return SolomonRingDefinition!.Use(vitals, economy, itemId);
        }
        bool guardian = SupportsGuardianForceUse && row is >= 16 and < 32;
        if ((!SupportsCharacterUse || (uint)row >= 8) && !guardian || itemId is <= 0 or > 255 || vitals.InitOutSha256 != economy.InitOutSha256)
        {
            throw new NotSupportedException("Field item requires its original supported character record and matching saved owners.");
        }
        if ((TargetFlags & 8) != 0)
        {
            NativeFieldItem single = this with { TargetFlags = (byte)(TargetFlags & ~8) };
            NativeCharacterVitals current = vitals;
            NativeEconomy groupInventory = economy;
            bool changed = false;
            IEnumerable<int> targets = guardian ? Enumerable.Range(0, 16).Where(id => (vitals.GuardianForces[id].Flags & 1) != 0).Select(id => id + 16) : Enumerable.Range(0, 8).Where(id => (vitals.Records[id].MemberFlags & 1) != 0 && (!vitals.DreamEnabled || vitals.CombatParty.Contains((byte)id)));
            foreach (int groupTarget in targets)
            {
                var result = single.Use(current, economy, itemId, groupTarget);
                current = result.Vitals;
                if (result.Changed)
                {
                    groupInventory = result.Economy;
                    changed = true;
                }
            }
            return (current, groupInventory, changed);
        }
        if (guardian)
        {
            return UseGuardianForce(vitals, economy, itemId, row - 16, forgottenAbility);
        }
        NativeCharacterVital target = vitals.Records[row];
        if ((target.MemberFlags & 1) == 0 || vitals.DreamEnabled && !vitals.CombatParty.Contains((byte)row))
        {
            throw new InvalidOperationException("The selected character is not a current menu member.");
        }
        int slot = Array.FindIndex(economy.Items, stock => stock.ItemId == itemId && stock.Count > 0);
        if (slot < 0)
        {
            throw new InvalidOperationException("The selected field item stock is exhausted.");
        }
        if (Type == 16)
        {
            ushort[] compatibility = target.GuardianForceCompatibility?.ToArray() ?? throw new NotSupportedException("Compatibility item use requires the original sixteen saved compatibility WORDs.");
            if (Power != 255 && (Power >= 16 || (vitals.GuardianForces[Power].Flags & 1) == 0 || compatibility[Power] == 1000))
            {
                return (vitals, economy, false);
            }
            bool changed = false;
            for (int id = 0; id < 16; id++)
            {
                if (Power == 255 && (vitals.GuardianForces[id].Flags & 1) == 0)
                {
                    continue;
                }
                int amount = Power == 255 || Power == id ? -StatusMask : (TargetFlags & 0x80) != 0 ? StatusMask / 2 : 0;
                ushort value = (ushort)Math.Clamp(compatibility[id] + amount, 1000, 6000);
                changed |= value != compatibility[id];
                compatibility[id] = value;
            }
            if (!changed)
            {
                return (vitals, economy, false);
            }
            NativeCharacterVitals compatible = vitals.DeepCopy();
            compatible.Records[row] = target with { GuardianForceCompatibility = compatibility };
            return (compatible, Consume(economy, slot), true);
        }
        if (target.HitPoints.Minimum != target.HitPoints.Maximum)
        {
            throw new NotSupportedException("Field item use requires exact saved HP; explicitly resolve the current HP domain first.");
        }
        if (Type == 20)
        {
            BattleNativePlayerStatRules rules = statRules ?? throw new NotSupportedException("Stat items require the original unjunctioned, initial-weapon stat projection.");
            BattleNativePlayerStatistics stats = rules.Project(target.MagicInventory, new BattleNativeCharacterGrowth(target.HitPointBonus, System.Collections.Immutable.ImmutableArray.CreateRange(target.StatBonuses)));
            int[] values = [stats.MaximumHp, stats.Strength, stats.Vitality, stats.Magic, stats.Spirit, stats.Speed, stats.Luck];
            ushort hpBonus = target.HitPointBonus;
            byte[] bonuses = target.StatBonuses.ToArray();
            bool changed = false;
            for (int stat = 0; stat < 7; stat++)
            {
                if ((StatusMask & (1 << stat)) == 0 || values[stat] >= (stat == 0 ? 9999 : 255))
                {
                    continue;
                }
                changed = true;
                if (stat == 0)
                {
                    hpBonus = (ushort)Math.Min(9999, hpBonus + Power);
                }
                else
                {
                    bonuses[stat - 1] = (byte)Math.Min(255, bonuses[stat - 1] + Power);
                }
            }
            if (!changed)
            {
                return (vitals, economy, false);
            }
            NativeCharacterVitals grown = vitals.DeepCopy();
            NativeCharacterVital updated = target with { HitPointBonus = hpBonus, StatBonuses = bonuses };
            ushort maximumHp = grown.Definitions.CharacterMaximum(updated);
            ushort hitPoints = Math.Min(updated.HitPoints.Minimum, maximumHp);
            grown.Records[row] = updated with { HitPoints = new NativeHpWord(hitPoints, hitPoints), Status = RecomputeStatus(hitPoints, maximumHp, updated.Status) };
            return (grown.RebuildParty(), Consume(economy, slot), true);
        }
        int hp = target.HitPoints.Minimum;
        int maximum = vitals.Definitions.CharacterMaximum(target);
        ushort status = target.Status;
        if (Type is 0 or 21)
        {
            //4FC1F0: Zombie rejects ordinary recovery; KO suppresses HP recovery.
            bool zombie = (status & 0x40) != 0 && (StatusMask & 0x40) == 0;
            if (zombie && Type == 0)
            {
                return (vitals, economy, false);
            }
            hp = Math.Clamp(hp + ((status & 1) == 0 && !zombie ? Power * 50 : 0), 0, maximum);
            status = (ushort)(RecomputeStatus(hp, maximum, status) & ~StatusMask);
        }
        else
        {
            //4F93E4 and4FC350 accept only KO, then restore maximum/8 or maximum HP.
            if ((status & 1) == 0)
            {
                return (vitals, economy, false);
            }
            hp = Type == 1 ? maximum / 8 : maximum;
            status = RecomputeStatus(hp, maximum, status);
        }
        if (hp == target.HitPoints.Minimum && status == target.Status)
        {
            return (vitals, economy, false);
        }
        NativeCharacterVitals next = vitals.DeepCopy();
        next.Records[row] = target with { HitPoints = new NativeHpWord((ushort)hp, (ushort)hp), Status = status };
        NativeEconomy inventory = economy.DeepCopy();
        NativeItemStock stock = inventory.Items[slot];
        inventory.Items[slot] = stock.Count == 1 ? new NativeItemStock(0, 0) : stock with { Count = (byte)(stock.Count - 1) };
        return (next, inventory, true);
    }

    private (NativeCharacterVitals Vitals, NativeEconomy Economy, bool Changed) UseGuardianForce(NativeCharacterVitals vitals, NativeEconomy economy, int itemId, int id, int? forgottenAbility)
    {
        NativeGuardianForceVital gf = vitals.GuardianForces[id];
        if ((gf.Flags & 1) == 0)
        {
            throw new InvalidOperationException("The selected GF has not been acquired.");
        }
        int slot = Array.FindIndex(economy.Items, stock => stock.ItemId == itemId && stock.Count > 0);
        if (slot < 0)
        {
            throw new InvalidOperationException("The selected field item stock is exhausted.");
        }
        if (Type == 18)
        {
            int ability = forgottenAbility ?? throw new ArgumentException("Amnesia Greens require an explicit ability to forget.");
            return (vitals.ForgetGuardianForceAbility(id, ability), Consume(economy, slot), true);
        }
        if (Type == 17)
        {
            IReadOnlyList<NativeGuardianForceAbility> abilities = vitals.GuardianForceAbilities(id);
            NativeGuardianForceAbility? existing = abilities.FirstOrDefault(ability => ability.Ability == Power);
            if (Power >= 128 || existing?.Learned == true || existing is null && abilities.Count >= 22)
            {
                return (vitals, economy, false);
            }
            NativeGuardianForceLearnedBank learned = Power < 64 ? gf.LearnedBank with { Low = gf.LearnedBank.Low | (1UL << Power) } : gf.LearnedBank with { High = gf.LearnedBank.High | (1UL << (Power - 64)) };
            int rule = -1;
            for (int index = 0; index < vitals.MenuDefinitions.Learning[id].Length; index++)
            {
                if (vitals.MenuDefinitions.Learning[id][index].Ability == Power)
                {
                    rule = index;
                    break;
                }
            }
            NativeCharacterVitals taught = vitals.DeepCopy();
            taught.GuardianForces[id] = gf with { LearnedBank = learned, ForgottenAbilities = rule < 0 ? gf.ForgottenAbilities : gf.ForgottenAbilities & ~(1u << rule) };
            return (taught.RebuildParty(), Consume(economy, slot), true);
        }
        int maximum = vitals.Definitions.GuardianForceMaximum(id, gf);
        int hp = Type == 3 ? gf.HitPoints == 0 ? 0 : Math.Min(maximum, gf.HitPoints + Power * 50) : gf.HitPoints != 0 ? gf.HitPoints : Type == 4 ? maximum / 8 : maximum;
        if (hp == gf.HitPoints)
        {
            return (vitals, economy, false);
        }
        NativeCharacterVitals next = vitals.DeepCopy();
        next.GuardianForces[id] = gf with { HitPoints = (ushort)hp };
        NativeEconomy inventory = economy.DeepCopy();
        NativeItemStock stock = inventory.Items[slot];
        inventory.Items[slot] = stock.Count == 1 ? new NativeItemStock(0, 0) : stock with { Count = (byte)(stock.Count - 1) };
        return (next, inventory, true);
    }

    private static ushort RecomputeStatus(int hp, int maximum, ushort status)
    {
        //4BFB00 refreshes KO and the two menu HP threshold bits.
        if (hp <= 0)
        {
            return 1;
        }
        int result = status & 0xFCFE;
        if (hp < maximum / 2)
        {
            result |= 0x200;
        }
        if (hp < maximum / 4)
        {
            result |= 0x300;
        }
        return (ushort)result;
    }

    private static NativeEconomy Consume(NativeEconomy economy, int slot)
    {
        NativeEconomy inventory = economy.DeepCopy();
        NativeItemStock stock = inventory.Items[slot];
        inventory.Items[slot] = stock.Count == 1 ? new NativeItemStock(0, 0) : stock with { Count = (byte)(stock.Count - 1) };
        return inventory;
    }
}
