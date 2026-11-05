using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplaySaveComponentTests
{
    [Fact]
    public void Format_detection_preserves_position_and_does_not_accept_a_truncated_marker()
    {
        using MemoryStream stream = new();
        stream.WriteByte(42);
        GameplaySaveCodec.Write(stream, Document());
        stream.Position = 1;
        Assert.True(GameplaySaveCodec.HasSignature(stream));
        Assert.Equal(1, stream.Position);
        stream.Position = stream.Length - 3;
        long position = stream.Position;
        Assert.False(GameplaySaveCodec.HasSignature(stream));
        Assert.Equal(position, stream.Position);
        stream.Position = 0;
        Assert.False(GameplaySaveCodec.HasSignature(stream));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Partial_components_are_not_presented_as_loadable_player_summary()
    {
        using MemoryStream stream = new();
        GameplaySaveCodec.Write(stream, Document());
        stream.Position = 0;
        Assert.Throws<NotSupportedException>(() => GameplaySaveCodec.ReadSummary(stream));
    }

    [Fact]
    public void Component_round_trip_preserves_nondefault_progress_and_uses_current_definitions()
    {
        NativeCharacterVitals source = Characters();
        NativeEconomy economy = Economy();
        NativeLimitProgress limits = Limits();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.Capture(FieldResume(), source, economy, limits);
        using MemoryStream stream = new();
        GameplaySaveCodec.Write(stream, document);
        stream.Position = 0;
        GameplaySaveComponentDocument loaded = GameplaySaveCodec.Read(stream);
        NativeCharacterVitals installed = MagicSystemFixture.Create() with { InitOutSha256 = new string('a', 64), KernelSha256 = new string('b', 64) };
        NativeCharacterVitals restored = loaded.Characters.Restore(installed);

        Assert.Equal(source.Records, restored.Records);
        Assert.Equal(source.CombatParty, restored.CombatParty);
        Assert.Equal(source.DreamEnabled, restored.DreamEnabled);
        Assert.Equal(source.DreamWeapons, restored.DreamWeapons);
        Assert.Equal(source.JunctionPartyBackup, restored.JunctionPartyBackup);
        Assert.Equal(source.GuardianForces[0].Experience, restored.GuardianForces[0].Experience);
        Assert.Equal(source.GuardianForces[0].HitPoints, restored.GuardianForces[0].HitPoints);
        Assert.Equal(source.GuardianForces[0].LearnedBank, restored.GuardianForces[0].LearnedBank);
        Assert.Equal(source.GuardianForces[0].AbilityProgress, restored.GuardianForces[0].AbilityProgress);
        Assert.Equal(source.GuardianForces[0].NativeOpaqueBytes, restored.GuardianForces[0].NativeOpaqueBytes);
        Assert.Equal(source.GuardianForces[0].KillCount, restored.GuardianForces[0].KillCount);
        Assert.Equal(source.GuardianForces[0].DeathCount, restored.GuardianForces[0].DeathCount);
        Assert.Equal(source.MagicSystem.PersonalFieldMasks[0], restored.MagicSystem.PersonalFieldMasks[0]);
        Assert.Equal(source.MagicSystem.CommittedActionIds, restored.MagicSystem.CommittedActionIds);
        Assert.Equal(source.MagicSystem.RecoveredResultIds, restored.MagicSystem.RecoveredResultIds);
        Assert.Same(installed.Definitions, restored.Definitions);
        Assert.Same(installed.MenuDefinitions, restored.MenuDefinitions);
        Assert.Equal(installed.InitOutSha256, restored.InitOutSha256);
        Assert.Equal(installed.KernelSha256, restored.KernelSha256);
        NativeEconomy restoredEconomy = loaded.Economy.Restore(Economy());
        Assert.Equal(economy.Items, restoredEconomy.Items);
        Assert.Equal(economy.ItemOrder, restoredEconomy.ItemOrder);
        Assert.Equal(economy.Gil, restoredEconomy.Gil);
        Assert.Equal(economy.PastGil, restoredEconomy.PastGil);
        Assert.Equal(economy.EventBankGil72, restoredEconomy.EventBankGil72);
        Assert.Equal(economy.EventBankPastGil76, restoredEconomy.EventBankPastGil76);
        NativeLimitProgress restoredLimits = loaded.Limits.Restore(limits with { InitOutSha256 = installed.InitOutSha256 });
        Assert.Equal(installed.InitOutSha256, restoredLimits.InitOutSha256);
        Assert.Equal(limits.BlueMagicFlags, restoredLimits.BlueMagicFlags);
        Assert.Equal(limits.DuelFlags, restoredLimits.DuelFlags);
        Assert.Equal(limits.AngeloFlags, restoredLimits.AngeloFlags);
        Assert.Equal(limits.TrainingSelection, restoredLimits.TrainingSelection);
        Assert.Equal(limits.TrainingCounters, restoredLimits.TrainingCounters);
        Assert.Equal(limits.SpecialMagicFlags, restoredLimits.SpecialMagicFlags);
        Assert.Equal(limits.PetPalsFlags, restoredLimits.PetPalsFlags);
        Assert.Equal(limits.Options978, restoredLimits.Options978);
        Assert.Equal(document.Resume, loaded.Resume);
    }

    [Fact]
    public void Capture_and_restore_detach_mutable_arrays_from_both_owners()
    {
        NativeCharacterVitals source = Characters();
        NativeEconomy economy = Economy();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.Capture(FieldResume(), source, economy, Limits());
        source.Records[0].StatBonuses[0] = 99;
        source.Records[0].Junction.MagicJunctions[0] = 99;
        source.CombatParty[0] = 7;
        source.MagicSystem.PersonalFieldMasks[0][0] = 0;
        economy.Items[0] = new(1, 1);
        economy.ItemOrder![0] = 31;

        Assert.Equal((byte)8, document.Characters.Records[0].StatBonuses[0]);
        Assert.Equal((byte)5, document.Characters.Records[0].Junction.MagicJunctions[0]);
        Assert.Equal((byte)2, document.Characters.CombatParty[0]);
        Assert.Equal((byte)3, document.Characters.Magic.PersonalFieldMasks[0][0]);
        Assert.Equal(new NativeItemStock(17, 43), document.Economy.Items[0]);
        Assert.Equal((byte)0, document.Economy.ItemOrder![0]);

        NativeCharacterVitals installed = MagicSystemFixture.Create();
        NativeCharacterVitals restored = document.Characters.Restore(installed);
        restored.Records[0].StatBonuses[0] = 23;
        restored.MagicSystem.PersonalFieldMasks[0][0] = 9;
        Assert.Equal((byte)8, document.Characters.Records[0].StatBonuses[0]);
        Assert.Equal((byte)3, document.Characters.Magic.PersonalFieldMasks[0][0]);
        Assert.Equal((byte)0, installed.Records[0].StatBonuses[0]);
    }

    [Fact]
    public void Document_graph_has_no_execution_or_installed_resource_owners()
    {
        HashSet<Type> visited = [];
        Queue<Type> pending = new([typeof(GameplaySaveComponentDocument)]);
        while (pending.TryDequeue(out Type? type))
        {
            if (!visited.Add(type))
            {
                continue;
            }
            Assert.DoesNotContain(type.Name, new[] { nameof(GameState), nameof(NativeCharacterVitals), nameof(NativeBattleRetainedMemory), nameof(NativeStatDefinitions), nameof(NativeGuardianForceMenuDefinitions), nameof(NativeBattleFileStaging), nameof(NativeBattleEffectArena), nameof(NativeBattleModelCpuResources), nameof(GuardianForceAbilityProfile), nameof(GuardianForceCompatibilityProjection) });
            if (type.IsArray)
            {
                pending.Enqueue(type.GetElementType()!);
            }
            foreach (Type argument in type.GenericTypeArguments)
            {
                pending.Enqueue(argument);
            }
            if (type.Namespace != typeof(GameState).Namespace)
            {
                continue;
            }
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always))
            {
                if (property.DeclaringType == typeof(GameplaySaveComponentDocument) && property.Name == nameof(GameplaySaveComponentDocument.Progression))
                {
                    continue;
                }
                pending.Enqueue(property.PropertyType);
            }
        }
        Assert.Contains(typeof(GameplaySaveGuardianForce), visited);
        Assert.Contains(typeof(NativeCharacterJunction), visited);
        Type[] allowed = [typeof(GameplaySaveComponentDocument), typeof(GameplaySaveResumeDescriptor), typeof(GameplaySaveResumeKind), typeof(GameplaySaveFieldPosition), typeof(GameplaySaveWorldPosition), typeof(GameplaySaveCharacters), typeof(GameplaySaveGuardianForce), typeof(GameplaySaveGuardianForceIntent), typeof(GameplaySaveGuardianForceDocument), typeof(GameplaySaveGuardianForceOperation), typeof(GameplaySaveGuardianForceProducer), typeof(GuardianForceAbilityOperationKind), typeof(GameplaySaveMagic), typeof(GameplaySaveEconomy), typeof(GameplaySaveLimitProgress), typeof(NativeCharacterVital), typeof(NativeHpWord), typeof(NativeCharacterJunction), typeof(NativeMagicStock), typeof(NativeGuardianForceLearnedBank), typeof(NativeGuardianForceOpaqueBytes), typeof(GuardianForceProgression), typeof(GuardianForceAbilityProgress), typeof(MagicAttackMode), typeof(NativeItemStock)];
        Assert.All(visited.Where(type => !type.IsArray && type.Namespace == typeof(GameState).Namespace), type => Assert.Contains(type, allowed));
        string[] characterMembers = ["CharacterId", "HitPoints", "Status", "Junction", "Experience", "HitPointBonus", "Weapon", "StatBonuses", "MagicInventory", "MemberFlags", "KillCount", "NativeByte90", "DeathCount", "GuardianForceCompatibility"];
        Assert.Equal(characterMembers.Order(), typeof(NativeCharacterVital).GetProperties().Select(property => property.Name).Order());
    }

    [Fact]
    public void Projection_rejects_pending_casts_and_unresolved_GF_history_without_mutating_source()
    {
        NativeCharacterVitals source = Characters();
        NativeCharacterVitals casting = source with { MagicSystem = source.MagicSystem with { Reservations = [new("pending", 0, 0, 1, MagicCastContext.Field, 1, 0)] } };
        Assert.Throws<NotSupportedException>(() => GameplaySaveCharacters.Capture(casting));
        Assert.Single(casting.MagicSystem.Reservations);
        Assert.Throws<NotSupportedException>(() => GameplaySaveCharacters.Capture(source with { GuardianForceOpaqueBaseline = true }));
        NativeGuardianForceVital[] forces = source.GuardianForces.ToArray();
        forces[0] = forces[0] with { LastProjection = new(forces[0].LearnedBank, forces[0].AbilityProgress, 0, 0, source.MenuDefinitions.Learning[0]) };
        GameplaySaveCharacters projected = GameplaySaveCharacters.Capture(source with { GuardianForces = forces });
        Assert.NotNull(projected.GuardianForces[0].Progression);
        Assert.Null(source.GuardianForces[0].LastProjection);
    }

    [Fact]
    public void Uncertain_HP_is_not_invented_as_an_exact_saved_value()
    {
        NativeCharacterVitals source = Characters();
        source.Records[0] = source.Records[0] with { HitPoints = new(1, 10) };
        Assert.Throws<InvalidDataException>(() => GameplaySaveCharacters.Capture(source));
        Assert.Equal(new NativeHpWord(1, 10), source.Records[0].HitPoints);
    }

    [Fact]
    public void Explicit_zero_empty_and_null_values_survive_without_template_reset()
    {
        NativeCharacterVitals source = Characters();
        source.Records[0] = source.Records[0] with { HitPoints = new(0, 0), Experience = 0, DeathCount = 0, NativeByte90 = null };
        NativeCharacterVitals restored = GameplaySaveCharacters.Capture(source).Restore(MagicSystemFixture.Create());
        GameplaySaveEconomy economy = GameplaySaveEconomy.Capture(Economy() with { Gil = 0, ItemOrder = null });
        NativeEconomy restoredEconomy = economy.Restore(Economy());
        NativeLimitProgress restoredLimits = GameplaySaveLimitProgress.Capture(Limits() with { AngeloFlags = 0, TrainingCounters = ImmutableArray.CreateRange(new byte[8]), SpecialMagicFlags = null, PetPalsFlags = null }).Restore(Limits());

        Assert.Equal((ushort)0, restored.Records[0].HitPoints.Minimum);
        Assert.Equal(0u, restored.Records[0].Experience);
        Assert.Equal((ushort)0, restored.Records[0].DeathCount);
        Assert.Null(restored.Records[0].NativeByte90);
        Assert.Equal(0u, restoredEconomy.Gil);
        Assert.Null(restoredEconomy.ItemOrder);
        Assert.Equal((byte)0, restoredLimits.AngeloFlags);
        Assert.All(restoredLimits.TrainingCounters, value => Assert.Equal((byte)0, value));
        Assert.Null(restoredLimits.SpecialMagicFlags);
        Assert.Null(restoredLimits.PetPalsFlags);
    }

    [Theory]
    [InlineData("Characters")]
    [InlineData("Economy")]
    [InlineData("Limits")]
    [InlineData("Resume")]
    [InlineData("Coverage")]
    public void Missing_mandatory_component_members_fail_instead_of_resetting_progress(string property)
    {
        byte[] bytes = Encode(Document());
        JsonObject json = JsonNode.Parse(bytes.AsSpan(48))!.AsObject();
        json.Remove(property);
        using MemoryStream stream = Container(JsonSerializer.SerializeToUtf8Bytes(json));
        Assert.Throws<JsonException>(() => GameplaySaveCodec.Read(stream));
    }

    [Fact]
    public void Future_or_unknown_payload_members_are_rejected_explicitly()
    {
        GameplaySaveComponentDocument document = Document();
        Assert.Throws<InvalidDataException>(() => GameplaySaveCodec.Write(new MemoryStream(), document with { Version = GameplaySaveComponentDocument.CurrentVersion + 1 }));
        byte[] bytes = Encode(document);
        JsonObject json = JsonNode.Parse(bytes.AsSpan(48))!.AsObject();
        json["UnexpectedExecutionOwner"] = new JsonObject();
        using MemoryStream stream = Container(JsonSerializer.SerializeToUtf8Bytes(json));
        Assert.Throws<JsonException>(() => GameplaySaveCodec.Read(stream));
    }

    [Fact]
    public void Invalid_container_checksum_truncation_trailing_data_and_snapshot_signature_fail()
    {
        byte[] valid = Encode(Document());
        byte[] changed = valid.ToArray();
        changed[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => GameplaySaveCodec.Read(new MemoryStream(changed)));
        Assert.Throws<InvalidDataException>(() => GameplaySaveCodec.Read(new MemoryStream(valid[..^1])));
        Assert.Throws<InvalidDataException>(() => GameplaySaveCodec.Read(new MemoryStream([.. valid, 0])));
        Assert.Throws<InvalidDataException>(() => GameplaySaveCodec.Read(new MemoryStream("MFF8SAVE"u8.ToArray())));
        byte[] invalidLength = valid.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(invalidLength.AsSpan(12, 4), GameplaySaveCodec.MaximumPayloadBytes + 1);
        Assert.Throws<InvalidDataException>(() => GameplaySaveCodec.Read(new MemoryStream(invalidLength)));
    }

    [Fact]
    public void Oversized_component_fails_before_writing_the_destination()
    {
        GameplaySaveComponentDocument document = Document();
        document = document with { Characters = document.Characters with { Magic = document.Characters.Magic with { CommittedActionIds = [new string('x', GameplaySaveCodec.MaximumPayloadBytes)] } } };
        using MemoryStream destination = new();
        Assert.Throws<InvalidDataException>(() => GameplaySaveCodec.Write(destination, document));
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public void Field_and_world_descriptors_validate_structure_without_claiming_reconstruction()
    {
        FieldResume().Validate();
        new GameplaySaveResumeDescriptor(1, GameplaySaveResumeKind.World, 106, 105, null, new(123456, -789, 23, 2048, 0)).Validate();
        Assert.Throws<InvalidDataException>(() => (FieldResume() with { World = new(0, 0, 0, 0, 0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (FieldResume() with { Field = new(float.NaN, 0, 0, 0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (FieldResume() with { Version = 2 }).Validate());
    }

    private static GameplaySaveResumeDescriptor FieldResume() => new(1, GameplaySaveResumeKind.Field, 106, 105, new(123, -45, 7, 192), null);
    private static GameplaySaveComponentDocument Document() => GameplaySaveComponentDocument.Capture(FieldResume(), Characters(), Economy(), Limits());

    private static NativeCharacterVitals Characters()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create().DeepCopy();
        source.Records[0] = source.Records[0] with { HitPoints = new(37, 37), Experience = 123456, Weapon = 12, StatBonuses = [8, 7, 6, 5, 4, 3], Junction = source.Records[0].Junction with { MagicJunctions = [5, .. new byte[19]] }, DeathCount = 7, GuardianForceCompatibility = Enumerable.Repeat((ushort)123, 16).ToArray() };
        source.CombatParty[0] = 2;
        source.DreamWeapons[0] = 7;
        source.JunctionPartyBackup[0] = 3;
        source.GuardianForces[0] = source.GuardianForces[0] with { Experience = 654321, HitPoints = 23, Learning = 12, AbilityProgress = ImmutableArray.CreateRange(Enumerable.Repeat((byte)19, 21)), KillCount = 9, DeathCount = 2, NativeOpaqueBytes = new(1, 2, 3, 4) };
        source.MagicSystem.PersonalFieldMasks[0][0] = 3;
        return source with { DreamEnabled = true, MagicSystem = source.MagicSystem with { CommittedActionIds = ["cast-17"], RecoveredResultIds = ["result-19"] } };
    }

    private static NativeEconomy Economy()
    {
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        items[0] = new(17, 43);
        return new NativeEconomy { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = items, ItemOrder = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(), Gil = 12345, PastGil = 6789, EventBankGil72 = 3456, EventBankPastGil76 = 9876 };
    }

    private static NativeLimitProgress Limits() => new(new string('0', 64), 0x1234, 0x5678, 0x91, 3, [1, 2, 3, 4, 5, 6, 7, 8]) { SpecialMagicFlags = 17, PetPalsFlags = 23, Options978 = 0x87 };

    private static byte[] Encode(GameplaySaveComponentDocument document)
    {
        using MemoryStream stream = new();
        GameplaySaveCodec.Write(stream, document);
        return stream.ToArray();
    }

    private static MemoryStream Container(byte[] payload)
    {
        MemoryStream stream = new();
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write("MFF8GPC1"u8);
        writer.Write(1);
        writer.Write(payload.Length);
        writer.Write(SHA256.HashData(payload));
        writer.Write(payload);
        stream.Position = 0;
        return stream;
    }
}
