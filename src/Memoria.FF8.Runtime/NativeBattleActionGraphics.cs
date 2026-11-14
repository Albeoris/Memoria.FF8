using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>50BF90's copied globals; header and contact storage remain in their canonical physical arenas.</summary>
public sealed record NativeBattleActionGraphicsState([property: JsonRequired] BattleActionHeaderReference Header, [property: JsonRequired] BattleModelOwner Actor, [property: JsonRequired] BattleContactCursor FirstContact, [property: JsonRequired] BattleModelTargets TargetActors, [property: JsonRequired] BattleModelTargets CoverActors, [property: JsonRequired] BattleModelOwner? CoverSource, [property: JsonRequired] BattleModelOwner? FirstTarget, [property: JsonRequired] byte HitFlags, [property: JsonRequired] NativeBattleActionDataReference? PendingData, [property: JsonRequired] uint Callback)
{
    [JsonRequired]
    public bool? UseActionCamera { get; init; }

    [JsonRequired]
    public ushort? ReflectionWatchdog4A { get; init; }
    public ushort? CastingWatchdog8E { get; init; }
    public NativeBattleGuardianServiceState? Guardian { get; init; }
    public int? FirstHeaderTargetCount { get; init; }
    public BattleModelTargets? HiddenPartyActors { get; init; }
    public BattleModelOwner? CameraActor { get; init; } = Actor;
}
public sealed record NativeBattleGuardianServiceState(BattleModelTargets HiddenPartyActors, byte CameraSelector4E, ushort Control44, byte? Neighbor70 = null);
public sealed record NativeBattleActionServiceSelection(uint Callback, uint SetGraphicsFlags = 0, byte? Sequence = null);

public static class NativeBattleActionGraphics
{
    public static byte ResolvePlayerSequence(byte command, byte supplied, int action, ImmutableArray<byte> commandSequences)
    {
        if (command < 39 && commandSequences.Length != 39)
        {
            throw new InvalidDataException("Native player dispatch requires the original39-byte command sequence table.");
        }
        byte sequence = supplied;
        if (command < 39)
        {
            sequence = commandSequences[command];
        }
        else
        {
            sequence = command switch { 236 or 240 or 245 => 0, 239 => 28, 244 or 254 => 14, 247 => 11, _ => supplied };
        }
        return command is 244 or 254 && action is 15 or 70 ? (byte)11 : sequence;
    }

    public static short ReadCameraFlag50A6E0(NativeBattleActionPacketsState packets)
    {
        return NativeBattleActionPacketBoundary.ReadActiveCameraFlag50A6E0(packets);
    }

    public static int ReadFirstHeaderTargetCount5044B0(NativeBattleActionGraphicsState? action)
    {
        return action?.FirstHeaderTargetCount ?? throw new NotSupportedException("Native sequence count requires its50BF90 cached first-header BYTE16; migrate the standalone snapshot explicitly.");
    }

    public static NativeBattleGraphicsModel WriteSequenceTarget5048E0(NativeBattleGraphicsModel target, byte variable, int value)
    {
        short word = unchecked((short)value);
        return variable switch
        {
            33 or 48 => target with { Rotation12 = word },
            49 => target with { Rotation14 = word },
            50 => target with { Rotation16 = word },
            45 => target with { Transform = (target.Transform ?? throw new NotSupportedException("Native sequence target scale requires its WORD150 owner.")) with { Scale148 = target.Transform.Scale148 with { Y = word } } },
            52 => target with { Transform = (target.Transform ?? throw new NotSupportedException("Native sequence target position requires its WORD30 owner.")) with { Position28 = target.Transform.Position28 with { Y = word } } },
            _ => throw new NotSupportedException($"Native sequence write{variable:X2} is not a target model WORD write.")
        };
    }

    public static short AnyOtherModelBusy5044B0(BattleActorModels models, BattleModelOwner current)
    {
        ArgumentNullException.ThrowIfNull(models);
        if (!models.Contains(current))
        {
            throw new InvalidDataException("Native sequence busy query requires its current model owner.");
        }
        foreach (BattleModelOwner owner in models.Owners)
        {
            if (owner == current)
            {
                continue;
            }
            NativeBattleGraphicsModel model = models.Get(owner);
            if ((model.Flags0 & 2) != 0 && IsModelBusy(model, 0x40))
            {
                return 1;
            }
        }
        return 0;
    }

    public static bool AreActionModelsBusy(BattleActorModels models, NativeBattleActionGraphicsState context)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Actor is null || !models.Contains(context.Actor) || context.TargetActors is null || context.CoverActors is null || context.TargetActors.Enumerate().Any(owner => !models.Contains(owner)) || context.CoverActors.Enumerate().Any(owner => !models.Contains(owner)))
        {
            throw new InvalidDataException("Native action wait requires its bound source, target and cover model owners.");
        }
        if (context.UseActionCamera ?? throw new InvalidDataException("Native action service lost its505F00 result."))
        {
            return models.Entries.Any(entry => (entry.State.Flags0 & 2) != 0 && IsModelBusy(entry.State, 0x40));
        }
        if (IsModelBusy(models.Get(context.Actor), 0x40))
        {
            return true;
        }
        return models.Owners.Any(owner => (context.TargetActors.Contains(owner) || context.CoverActors.Contains(owner)) && IsModelBusy(models.Get(owner), 0x140));
    }

    public static bool IsModelBusy(NativeBattleGraphicsModel model, ushort resourceMask) => IsModelBusy(model, resourceMask, 26);

    public static bool IsModelBusy(NativeBattleGraphicsModel model, ushort resourceMask, uint statusMask)
    {
        return (model.Flags0 & 8) == 0 && ((model.Flags8 ?? throw new NotSupportedException("Native508540 requires its reached model DWORD8.")) & statusMask) == 0 && ((model.Kind5 ?? throw new NotSupportedException("Native508540 requires its reached model BYTE5.")) != 0 || ((model.ResourceFlags44 ?? throw new NotSupportedException("Native508540 requires its reached resource WORD44.")) & resourceMask) == 0);
    }

    public static NativeBattleActionGraphicsState Normalize(NativeBattleActionPacketsState packets, BattleActionHeaderReference header, NativeBattleActionGraphicsState? previous = null)
    {
        packets.Validate();
        NativeBattleActionHeaderSlot first = packets.ReadHeader(header);
        int groups = first.ReadRepeatModeMinusOne() + 1;
        HashSet<BattleModelOwner> targets = [];
        HashSet<BattleModelOwner> covers = [];
        BattleModelOwner? coverSource = previous?.CoverSource;
        BattleModelOwner? firstTarget = previous?.FirstTarget;
        byte flags = 0;
        for (int group = 0; group < groups; group++)
        {
            ImmutableArray<NativeBattleContactPacket> contacts = packets.ReadHeaderContacts(header.Advance(group));
            if (group == 0 && !contacts.IsEmpty)
            {
                firstTarget = contacts[0].Primary.Recipient;
            }
            foreach (NativeBattleContactPacket contact in contacts)
            {
                NativeBattleContactHalf half = contact.Primary;
                targets.Add(half.Recipient);
                if (half.CoverSource is { } source)
                {
                    covers.Add(source);
                    coverSource = source;
                }
                if ((half.Flags2 & 0x30) == 0x30)
                {
                    flags |= 2;
                }
            }
        }
        if (coverSource is not null && !packets.Roster.Contains(coverSource) || firstTarget is not null && !packets.Roster.Contains(firstTarget))
        {
            throw new InvalidDataException("Native50BF90 cannot retain a retired model owner.");
        }

        BattleModelTargets targetActors = BattleModelTargets.From(packets.Roster.Owners.Where(targets.Contains));
        BattleModelTargets coverActors = BattleModelTargets.From(packets.Roster.Owners.Where(covers.Contains));
        NativeBattleActionServiceSelection selection = SelectService(first);
        return new(header, first.Source, first.FirstContact, targetActors, coverActors, coverSource, firstTarget, flags, first.Data, selection.Callback)
        {
            FirstHeaderTargetCount = first.ReadContactCount()
        };
    }

    public static NativeBattleActionServiceSelection SelectService(NativeBattleActionHeaderSlot header)
    {
        byte command = header.Command;
        if (command == 0)
        {
            return new(header.ReadContactCount() != 0 ? 0x50BD80u : 0x50BD00u);
        }
        if (command is 0x26 or 0xF4 or 0xFE)
        {
            return header.Action is 70 or 15 ? new(0x50A9A0, 0x40000000, 11) : new(0x50B2A0);
        }
        uint special = command switch
        {
            0xEC or 0xF5 => 0x50B830,
            0xED or 0xEE => 0x50BEE0,
            0xF1 => 0x50BDC0,
            0xF7 => 0x50B0C0,
            0xFC => 0x50B190,
            _ => 0
        };
        if (special != 0)
        {
            return new(special);
        }
        uint setFlags = command == 0x1C ? 0x10000000u : 0;
        uint callback = header.Action == 65535 ? 0x50BC20u : header.Effect == 0 ? 0x50BB00u : header.Sequence != 0 ? 0x50A9A0u : 0x50B190u;
        return new(callback, setFlags);
    }
}

public sealed record NativeBattleEffectBinding(ushort Effect, uint Entry, uint Initializer)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStatusMagicResources? StatusMagicResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStorm11Resources? Storm11Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleReflectResources? ReflectResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleProtectResources? ProtectResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShellResources? ShellResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAuraResources? AuraResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBadBreath113Resources? BadBreath113Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMeltdownResources? MeltdownResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDarkMist13Resources? DarkMist13Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStickyWeb153Resources? StickyWeb153Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleUltraWaves154Resources? UltraWaves154Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSandStorm176Resources? SandStorm176Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFunguarLaser277Resources? FunguarLaser277Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRayBomb10Resources? RayBomb10Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleXatmDeath276Resources? XatmDeath276Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBombGlow179Resources? BombGlow179Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSuicide180Resources? Suicide180Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleUltrasonic152Resources? Ultrasonic152Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDeathResources? DeathResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoom8Resources? Doom8Resources { get; init; }
    public NativeBattleEffect9Resources? Effect9Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCurse47Resources? Curse47Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBerserk108Resources? Berserk108Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSlow123Resources? Slow123Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHaste125Resources? Haste125Resources { get; init; }
    public NativeBattleMadRush273Resources? MadRush273Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFloat115Resources? Float115Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBlindResources? BlindResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRegen147Resources? Regen147Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFullCure280Resources? FullCure280Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStatusCureItemResources? StatusCureItemResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFlare124Resources? Flare124Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleZombieResources? ZombieResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStop119Resources? Stop119Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRapture288Resources? Rapture288Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBreak117Resources? Break117Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleUltima149Resources? Ultima149Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWall189Resources? Wall189Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDrain39Resources? Drain39Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDrain39Resources? Absorb56Resources { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleTornado146Resources? Tornado146Resources { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleQuake38Resources? Quake38Resources { get; init; }
    public NativeBattleTheEnd85Resources? TheEnd85Resources { get; init; }
    public NativeBattlePain34Resources? Pain34Resources { get; init; }
    public NativeBattleDispel109Resources? Dispel109Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleControl110Resources? Control110Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleLanding17Resources? Landing17Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleConfuse36Resources? Confuse36Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWendigoResources? Dribble41Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWendigoResources? Shoot42Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSleep145Resources? Sleep145Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleClash26Resources? Clash26Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWhiteWind226Resources? WhiteWind226Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWhiteWind297Resources? WhiteWind297Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMicroMissiles299Resources? MicroMissiles299Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBadBreath300Resources? BadBreath300Resources { get; init; }
    public NativeBattleDegenerator306Resources? Degenerator306Resources { get; init; }
    public NativeBattleGatlingGun305Resources? GatlingGun305Resources { get; init; }
    public NativeBattleAcid317Resources? Acid317Resources { get; init; }
    public NativeBattlePetrifyStare127Resources? PetrifyStare127Resources { get; init; }
    public NativeBattleEerieSoundWave130Resources? EerieSoundWave130Resources { get; init; }
    public NativeBattleLaserEye71Resources? LaserEye71Resources { get; init; }
    public NativeBattleEffect30Resources? Effect30Resources { get; init; }
    public NativeBattleNeedlesFamilyResources? NeedlesFamilyResources { get; init; }
    public NativeBattleHypnotize133Resources? Hypnotize133Resources { get; init; }
    public NativeBattleBeamLaser134Resources? BeamLaser134Resources { get; init; }
    public NativeBattleEffect101Resources? Effect101Resources { get; init; }
    public NativeBattleEnergyBomber217Resources? EnergyBomber217Resources { get; init; }
    public NativeBattleMeltEye158Resources? MeltEye158Resources { get; init; }
    public NativeBattleOilBlast137Resources? OilBlast137Resources { get; init; }
    public NativeBattleBadBreath131Resources? BadBreath131Resources { get; init; }
    public NativeBattleReflectBeam135Resources? ReflectBeam135Resources { get; init; }
    public NativeBattleSaliva138Resources? Saliva138Resources { get; init; }
    public NativeBattleGastricJuice128Resources? GastricJuice128Resources { get; init; }
    public NativeBattleSandStorm155Resources? SandStorm155Resources { get; init; }
    public NativeBattleWildCannonBlow156Resources? WildCannonBlow156Resources { get; init; }
    public NativeBattleMagmaBreath48Resources? MagmaBreath48Resources { get; init; }
    public NativeBattleSigh46Resources? Sigh46Resources { get; init; }
    public NativeBattleStare45Resources? Stare45Resources { get; init; }
    public NativeBattleJunk44Resources? Junk44Resources { get; init; }
    public NativeBattleGravija214Resources? Gravija214Resources { get; init; }
    public NativeBattleShotgun281Resources? Shotgun281Resources { get; init; }
    public NativeBattleEvilEye282Resources? EvilEye282Resources { get; init; }
    public NativeBattleMiniPulseCannon286Resources? MiniPulseCannon286Resources { get; init; }
    public NativeBattleEnemyMicroMissiles284Resources? EnemyMicroMissiles284Resources { get; init; }
    public NativeBattleSnipeLaser302Resources? SnipeLaser302Resources { get; init; }
    public NativeBattleBoomerangSword304Resources? BoomerangSword304Resources { get; init; }
    public NativeBattleEffect303Resources? Effect303Resources { get; init; }
    public NativeBattleEffect283Resources? Effect283Resources { get; init; }
    public NativeBattleEffect315Resources? Effect315Resources { get; init; }
    public NativeBattleEffect289Resources? Effect289Resources { get; init; }
    public NativeBattleEffect79Resources? Effect79Resources { get; init; }
    public NativeBattleEffect69Resources? Effect69Resources { get; init; }
    public NativeBattleEffect70Resources? Effect70Resources { get; init; }
    public NativeBattleEffect344Resources? Effect344Resources { get; init; }
    public NativeBattleEffect80Resources? Effect80Resources { get; init; }
    public NativeBattleEffect64Resources? Effect64Resources { get; init; }
    public NativeBattleEffect65Resources? Effect65Resources { get; init; }
    public NativeBattleEffect66Resources? Effect66Resources { get; init; }
    public NativeBattleEffect68Resources? Effect68Resources { get; init; }
    public NativeBattleEffect76Resources? Effect76Resources { get; init; }
    public NativeBattleEffect140Resources? Effect140Resources { get; init; }
    public NativeBattleEffect166Resources? Effect166Resources { get; init; }
    public NativeBattleEffect167Resources? Effect167Resources { get; init; }
    public NativeBattleEffect173Resources? Effect173Resources { get; init; }
    public NativeBattleEffect174Resources? Effect174Resources { get; init; }
    public NativeBattleEffect207Resources? Effect207Resources { get; init; }
    public NativeBattleEffect209Resources? Effect209Resources { get; init; }
    public NativeBattleEffect210Resources? Effect210Resources { get; init; }
    public NativeBattleEffect213Resources? Effect213Resources { get; init; }
    public NativeBattleEffect227Resources? Effect227Resources { get; init; }
    public NativeBattleEffect319Resources? Effect319Resources { get; init; }
    public NativeBattleEffect324Resources? Effect324Resources { get; init; }
    public NativeBattleEffect285Resources? Effect285Resources { get; init; }
    public NativeBattleEffect322Resources? Effect322Resources { get; init; }
    public NativeBattleEffect323Resources? Effect323Resources { get; init; }
    public NativeBattleEffect318Resources? Effect318Resources { get; init; }
    public NativeBattleEffect321Resources? Effect321Resources { get; init; }
    public NativeBattleEffect301Resources? Effect301Resources { get; init; }
    public NativeBattleEffect287Resources? Effect287Resources { get; init; }
    public NativeBattleEffect313Resources? Effect313Resources { get; init; }
    public NativeBattleEffect290Resources? Effect290Resources { get; init; }
    public NativeBattleEffect54Resources? Effect54Resources { get; init; }
    public NativeBattleEffect63Resources? Effect63Resources { get; init; }
    public NativeBattleEffect316Resources? Effect316Resources { get; init; }
    public NativeBattleEffect345Resources? Effect345Resources { get; init; }
    public NativeBattleEffect320Resources? Effect320Resources { get; init; }
    public NativeBattleEffect308Resources? Effect308Resources { get; init; }
    public NativeBattleEffect312Resources? Effect312Resources { get; init; }
    public NativeBattleEffect31Resources? Effect31Resources { get; init; }
    public NativeBattleEffect251Resources? Effect251Resources { get; init; }
    public NativeBattleEffect83Resources? Effect83Resources { get; init; }
    public NativeBattleEffect298Resources? Effect298Resources { get; init; }
    public NativeBattleEffect314Resources? Effect314Resources { get; init; }
    public NativeBattleEffect338Resources? Effect338Resources { get; init; }
    public NativeBattleLevelResources? LevelResources { get; init; }
    public NativeBattleEffect187Resources? Effect187Resources { get; init; }
    public NativeBattleEffect218Resources? Effect218Resources { get; init; }
    public NativeBattleEffect326Resources? Effect326Resources { get; init; }
    public NativeBattleMegaSpark279Resources? MegaSpark279Resources { get; init; }
    public NativeBattleResonance49Resources? Resonance49Resources { get; init; }
    public NativeBattleBladeShot12Resources? BladeShot12Resources { get; init; }
    public NativeBattleAcid170Resources? Acid170Resources { get; init; }
    public NativeBattleMorph172Resources? Morph172Resources { get; init; }
    public NativeBattlePoisonGas171Resources? PoisonGas171Resources { get; init; }
    public NativeBattleAdditional181Resources? Additional181Resources { get; init; }
    public NativeBattleChainGun190Resources? ChainGun190Resources { get; init; }
    public NativeBattleAdditional182Resources? Additional182Resources { get; init; }
    public NativeBattleBlaster186Resources? Blaster186Resources { get; init; }
    public NativeBattleCannonBlow151Resources? CannonBlow151Resources { get; init; }
    public NativeBattleGatlingGun150Resources? GatlingGun150Resources { get; init; }
    public NativeBattleBreath129Resources? Breath129Resources { get; init; }
    public NativeBattleSonic139Resources? Sonic139Resources { get; init; }
    public NativeBattleEffect183Resources? Effect183Resources { get; init; }
    public NativeBattleBreath157Resources? Breath157Resources { get; init; }
    public NativeBattleDisolvingAcid132Resources? DisolvingAcid132Resources { get; init; }
    public NativeBattleOilShot136Resources? OilShot136Resources { get; init; }
    public NativeBattleElectricDischarge126Resources? ElectricDischarge126Resources { get; init; }
    public NativeBattleElectrocute275Resources? Electrocute275Resources { get; init; }
    public NativeBattleUltraWaves184Resources? UltraWaves184Resources { get; init; }
    public NativeBattleRayBomb307Resources? RayBomb307Resources { get; init; }
    public NativeBattleShockwavePulsar260Resources? ShockwavePulsar260Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSleepingGas168Resources? SleepingGas168Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGastricJuice169Resources? GastricJuice169Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokuken160Resources? Renzokuken160Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokuken159Resources? Renzokuken159Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokuken160Resources? RenzokukenSpecialResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleSearchSpriteResource>? RenzokukenSpecialExtraSprites { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokuken160Resources? Renzokuken141Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokuken160Resources? Renzokuken259Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokuken160Resources? Renzokuken332Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRenzokuken160Resources? Renzokuken333Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShot188Resources? Shot188Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShot196Resources? Shot196Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShot193Resources? Shot193Resources { get; init; }
    public NativeBattleShot195Resources? Shot195Resources { get; init; }
    public NativeBattleShot197Resources? Shot197Resources { get; init; }
    public NativeBattleShot198Resources? Shot198Resources { get; init; }
    public NativeBattleAngelWing84Resources? AngelWing84Resources { get; init; }
    public NativeBattleRinoa86Resources? Rinoa86Resources { get; init; }
    public NativeBattleRinoa87Resources? Rinoa87Resources { get; init; }
    public NativeBattleRinoa88Resources? Rinoa88Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRinoa89Resources? Rinoa89Resources { get; init; }
    public NativeBattleRoughDivide163Resources? RoughDivide163Resources { get; init; }
    public NativeBattleBlastingZone164Resources? BlastingZone164Resources { get; init; }
    public NativeBattleLionHeart165Resources? LionHeart165Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleNoMercy200Resources? NoMercy200Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel112Resources? Duel112Resources { get; init; }
    public NativeBattleDuel274Resources? Duel274Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel212Resources? Duel212Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel215Resources? Duel215Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel216Resources? Duel216Resources { get; init; }
    public NativeBattleFatedCircle162Resources? FatedCircle162Resources { get; init; }
    public NativeBattleShot194Resources? Shot194Resources { get; init; }
    public NativeBattleShot192Resources? Shot192Resources { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBlizzardResources? BlizzardResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFiraResources? FiraResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFiragaResources? FiragaResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleThundaraResources? ThundaraResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBlizzaraResources? BlizzaraResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBlizzagaResources? BlizzagaResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAeroResources? AeroResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSearchResources? SearchResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRecover93Resources? Recover93Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRecover93Resources? Reverse94Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRush91Resources? Rush91Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMoogle96Resources? Moogle96Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleTonberry90Resources? Tonberry90Resources { get; init; }
    public NativeBattleSiren95Resources? Siren95Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoomtrain191Resources? Doomtrain191Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePandemona291Resources? Pandemona291Resources { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDiablos325Resources? Diablos325Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCarbuncle278Resources? Carbuncle278Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleChoco97Resources? Choco97Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleChoco98Resources? Choco98Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleChoco99Resources? Choco99Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleChoco100Resources? Choco100Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWater222Resources? Water222Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScriptedInitializationDirectories? ScriptedInitializationDirectories { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCinematicResources? CinematicResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCactuar199Resources? Cactuar199Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleThundaga105Resources? Thundaga105Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleHoly175Resources? Holy175Resources { get; init; }
    public bool? InitializerIsRet { get; init; }
    public NativeBattleEffectInitializerAsset? InitializerAsset { get; init; }
    public NativeBattleEffectInitializerResource? InitializerResource { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleEffectInitializerResource>? InitializerResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleEffectInitializerAsset>? InitializerAssets { get; init; }
    public NativeBattleEffectProgramResources? ProgramResources { get; init; }
    public NativeBattleEffect3Resources? Effect3Resources { get; init; }
    public NativeBattleEffect107Resources? Effect107Resources { get; init; }
    public NativeBattleEffect311Resources? Effect311Resources { get; init; }
    public NativeBattleQuezacotlResources? NativeQuezacotlResources { get; init; }
    public NativeBattleCureResources? CureResources { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleLifeResources? LifeResources { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoubleResources? DoubleResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleTripleResources? TripleResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBio22Resources? Bio22Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGilgameshEffectResources? GilgameshResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShivaResources? ShivaResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScanTextResources? ScanTextResources { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleScanVisualResources? ScanVisualResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleScanCameraProfile>? ScanCameraProfiles { get; init; }
    public NativeBattleCuraResources? CuraResources { get; init; }
    public NativeBattleCuragaResources? CuragaResources { get; init; }
    public NativeBattleAquaResources? AquaResources { get; init; }
    public NativeBattleDrawStock15Resources? DrawStock15Resources { get; init; }
    public NativeBattleActorDirectorResources? ActorDirectorResources { get; init; }
    public NativeBattleHeroResources? HeroResources { get; init; }
    public NativeBattleMightyGuardResources? MightyGuardResources { get; init; }
    public NativeBattleDrinkMagic37Resources? DrinkMagic37Resources { get; init; }
    public NativeBattleRaldo19Resources? Raldo19Resources { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffect20Resources? Effect20Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePsychoBlast23Resources? PsychoBlast23Resources { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffect60Resources? Effect60Resources { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWindBlast29Resources? WindBlast29Resources { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDevour62Resources? Devour62Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMeltingBubble43Resources? MeltingBubble43Resources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRestorationResources? RestorationResources { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRecoveryResources? RecoveryResources { get; init; }
}

public sealed record NativeBattleEffectInitializerAsset(string Name, System.Collections.Immutable.ImmutableArray<byte> Data);
public sealed record NativeBattleEffectInitializerResource(string Name, uint GlobalAddress);

public sealed record NativeBattleEffectProgramResources(NativeBattleEffectHeapAllocation Animation, System.Collections.Immutable.ImmutableArray<NativeBattleModuleResource> Sprites, uint Sound);
