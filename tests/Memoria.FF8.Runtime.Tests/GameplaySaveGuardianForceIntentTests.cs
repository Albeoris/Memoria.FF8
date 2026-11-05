using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplaySaveGuardianForceIntentTests
{
    [Fact]
    public void LocalEditsAndChoicesRecomposeAgainstChangedModsWithoutResettingKnownProgress()
    {
        NativeCharacterVitals source = Authored();
        string original = JsonSerializer.Serialize(source);
        GameplaySaveComponentDocument document = RoundTrip(Document(source));
        NativeCharacterVitals selected = Selected(changed: true);
        string selectedBefore = JsonSerializer.Serialize(selected);

        NativeCharacterVitals restored = document.Characters.Restore(selected);

        Assert.Equal(9, Entry(restored, 100).EarnedPoints);
        Assert.False(Entry(restored, 100).Learned);
        Assert.Equal(7, Entry(restored, 101).EarnedPoints);
        Assert.True(Entry(restored, 103).ItemTaught);
        Assert.True(restored.GuardianForces[0].MoveFindDisabled);
        Assert.False(restored.GuardianForces[0].HasLearnedAbility(100));
        Assert.False(restored.GuardianForces[0].HasLearnedAbility(107));
        Assert.False(restored.GuardianForces[0].HasLearnedAbility(108));
        Assert.Contains(restored.ResolveGuardianForceTemplates()[0], entry => entry.Ability == 105);
        Assert.DoesNotContain(restored.ResolveGuardianForceTemplates()[0], entry => entry.Ability == 104);
        Assert.Contains(restored.ResolveGuardianForceTemplates()[0], entry => entry.Ability == 106);
        Assert.Equal(31, restored.ResolveGuardianForceTemplates()[0].Single(entry => entry.Ability == 100).RequiredPoints);
        int[] order = restored.ResolveGuardianForceTemplates()[0].Select(entry => entry.Ability).ToArray();
        Assert.True(Array.IndexOf(order, 105) < Array.IndexOf(order, 100));
        Assert.Equal("local/a.json", restored.GuardianForceProfile!.LocalOwnershipAliases["shared/exported.json"]);
        Assert.Contains("shared/disabled.json", restored.GuardianForceProfile.ExcludedDocuments);
        Assert.Equal(new[] { "local/a.json", "local/b.json" }, restored.GuardianForceProfile.LocalDocuments.Select(doc => doc.DocumentId));
        Assert.All(restored.GuardianForceProfile.LocalDocuments, doc =>
        {
            Assert.Null(doc.SourceFingerprint);
            Assert.Null(doc.Metadata);
            Assert.All(doc.Operations, operation => Assert.Null(operation.Metadata));
        });
        Assert.Equal(selected.GuardianForceProfile!.SourceFingerprint, restored.GuardianForceProfile.SourceFingerprint);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Equal(selectedBefore, JsonSerializer.Serialize(selected));
        GameplaySaveComponentDocument second = RoundTrip(Document(restored));
        NativeCharacterVitals cold = second.Characters.Restore(Selected(changed: true));
        Assert.Equal(JsonSerializer.Serialize(document.Characters.GuardianForceIntent), JsonSerializer.Serialize(second.Characters.GuardianForceIntent));
        Assert.Equal(JsonSerializer.Serialize(restored.GuardianForces), JsonSerializer.Serialize(cold.GuardianForces));
    }

    [Fact]
    public void ExtensionMetadataAndSourceFingerprintsNeverEnterTheIntentDocument()
    {
        GameplaySaveComponentDocument saved = RoundTrip(Document(Authored()));
        string json = JsonSerializer.Serialize(saved);

        Assert.DoesNotContain("excluded-fingerprint-marker", json, StringComparison.Ordinal);
        Assert.DoesNotContain("excluded-extension-marker", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceFingerprint", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Metadata", json, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeLearningSlots", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RequiredPoints", json, StringComparison.Ordinal);
        GameplaySaveGuardianForceOperation replace = saved.Characters.GuardianForceIntent!.LocalDocuments[1].Operations[0];
        Assert.Equal("local/a.json", replace.Producers.Single().DocumentId);
        Assert.Equal("ensure104", replace.ProducerOperationId);
        Assert.Equal("local/a.json", replace.ProducerDocumentId);
        Type[] types = [typeof(GameplaySaveGuardianForceIntent), typeof(GameplaySaveGuardianForceDocument), typeof(GameplaySaveGuardianForceOperation), typeof(GameplaySaveGuardianForceProducer)];
        Assert.All(types, type => Assert.All(type.GetProperties(), property =>
        {
            Assert.Null(property.GetCustomAttribute<JsonExtensionDataAttribute>());
            Assert.NotEqual(typeof(JsonElement), property.PropertyType);
            Assert.NotEqual(typeof(JsonNode), property.PropertyType);
            Assert.NotEqual(typeof(GuardianForceAbilityProfile), property.PropertyType);
        }));
        string[] operationMembers = ["Id", "GuardianForce", "Kind", "Ability", "Replacement", "InitialLearned", "ExpectedInitialLearned", "Order", "ExpectedOrder", "Producers", "ProducerOperationId", "ProducerDocumentId", "Metadata"];
        Assert.Equal(operationMembers.Order(), typeof(GuardianForceAbilityPatchOperation).GetProperties().Select(property => property.Name).Order());
        Assert.Equal(new[] { "EnsureAbility", "ReplaceAbility", "SetInitialState", "OrderSubset" }, Enum.GetNames<GuardianForceAbilityOperationKind>());
    }

    [Fact]
    public void SavedLocalOwnershipExcludesANewInstalledDocumentWithTheSameIdentity()
    {
        GameplaySaveCharacters saved = RoundTrip(Document(Authored())).Characters;
        NativeCharacterVitals selected = Selected(changed: true, collision: true);

        NativeCharacterVitals restored = saved.Restore(selected);

        Assert.Contains("local/a.json", restored.GuardianForceProfile!.ExcludedDocuments);
        Assert.False(restored.GuardianForces[0].HasLearnedAbility(108));
        Assert.Contains(restored.ResolveGuardianForceTemplates()[0], entry => entry.Ability == 105);
        Assert.Equal(9, Entry(restored, 100).EarnedPoints);
    }

    [Fact]
    public void ChangedProducerConflictIsExplicitAndDoesNotPublishPartialChoices()
    {
        GameplaySaveCharacters saved = RoundTrip(Document(Authored())).Characters;
        NativeCharacterVitals selected = Selected(changed: true, producerConflict: true);
        string before = JsonSerializer.Serialize(selected);
        string savedBefore = JsonSerializer.Serialize(saved);

        NotSupportedException error = Assert.Throws<NotSupportedException>(() => saved.Restore(selected));

        Assert.Contains("MissingProducer", error.Message, StringComparison.Ordinal);
        Assert.Contains("local/b.json/replace104", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, JsonSerializer.Serialize(selected));
        Assert.Equal(savedBefore, JsonSerializer.Serialize(saved));
    }

    [Fact]
    public void OldComponentsWithoutTheOptionalIntentPropertyKeepSelectedDefinitions()
    {
        NativeCharacterVitals source = Selected(changed: false);
        GameplaySaveComponentDocument legacy = RoundTrip(Document(source));
        Assert.Null(legacy.Characters.GuardianForceIntent);
        Assert.DoesNotContain("GuardianForceIntent", JsonSerializer.Serialize(legacy), StringComparison.Ordinal);
        NativeCharacterVitals selected = Selected(changed: true);

        NativeCharacterVitals restored = legacy.Characters.Restore(selected);

        Assert.Same(selected.GuardianForceProfile, restored.GuardianForceProfile);
        Assert.Contains(restored.ResolveGuardianForceTemplates()[0], entry => entry.Ability == 106);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ActualFilesystemReferencesRefuseWithoutChangingTheExactSource(int kind)
    {
        NativeCharacterVitals source = Authored();
        GuardianForceAbilityProfile profile = source.GuardianForceProfile!;
        string path = Path.Combine(Path.GetTempPath(), "local-intent-source.json");
        if (kind == 0)
        {
            profile = profile with { LocalDocuments = profile.LocalDocuments.SetItem(0, profile.LocalDocuments[0] with { DocumentId = path }) };
        }
        else if (kind == 1)
        {
            GuardianForceAbilityPatchDocument document = profile.LocalDocuments[1];
            GuardianForceAbilityPatchOperation operation = document.Operations[0];
            profile = profile with { LocalDocuments = profile.LocalDocuments.SetItem(1, document with { Operations = document.Operations.SetItem(0, operation with { ProducerDocumentId = path }) }) };
        }
        else
        {
            profile = profile with { LocalOwnershipAliases = profile.LocalOwnershipAliases.Add(new Uri(path).AbsoluteUri, "local/a.json") };
        }
        source = source with { GuardianForceProfile = profile };
        string before = JsonSerializer.Serialize(source);

        NotSupportedException error = Assert.Throws<NotSupportedException>(() => GameplaySaveCharacters.Capture(source));

        Assert.Contains("stable document/operation identities", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, JsonSerializer.Serialize(source));
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("LocalDocuments")]
    [InlineData("ExcludedDocuments")]
    [InlineData("LocalOwnershipAliases")]
    public void MissingIntentMembersAndUnrecognizedMetadataCannotBeSilentlyDefaulted(string property)
    {
        byte[] bytes = Encode(Document(Authored()));
        JsonObject json = JsonNode.Parse(bytes.AsSpan(48))!.AsObject();
        json["Characters"]!["GuardianForceIntent"]!.AsObject().Remove(property);
        using MemoryStream altered = Container(Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.Throws<JsonException>(() => GameplaySaveCodec.Read(altered));
    }

    [Fact]
    public void ExtensionDataInAnIncomingIntentIsRejectedByTheUsedCodec()
    {
        JsonObject json = JsonNode.Parse(Encode(Document(Authored())).AsSpan(48))!.AsObject();
        json["Characters"]!["GuardianForceIntent"]!["LocalDocuments"]![0]!["Metadata"] = new JsonObject { ["ignored"] = "must-refuse" };
        using MemoryStream altered = Container(Encoding.UTF8.GetBytes(json.ToJsonString()));
        Assert.Throws<JsonException>(() => GameplaySaveCodec.Read(altered));
    }

    private static NativeCharacterVitals Authored()
    {
        NativeCharacterVitals selected = Selected(changed: false);
        NativeCharacterVitals current = selected.SelectGuardianForceLearning(0, 100).AwardGuardianForceAbilityPoints(0, 9).SelectGuardianForceLearning(0, 101).AwardGuardianForceAbilityPoints(0, 7).SelectGuardianForceLearning(0, 100).TeachGuardianForceAbility(0, 103).SetGuardianForcePassiveAbilityEnabled(0, 79, false);
        GuardianForceAbilityPatchDocument first = new()
        {
            DocumentId = "local/a.json",
            SourceFingerprint = "excluded-fingerprint-marker",
            Metadata = new() { ["excluded-extension-marker"] = JsonSerializer.SerializeToElement(Path.GetTempPath()) },
            Operations = [new() { Id = "ensure104", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.EnsureAbility, Ability = 104, Metadata = new() { ["excluded-extension-marker"] = JsonSerializer.SerializeToElement("ignored") } }]
        };
        GuardianForceAbilityPatchDocument second = new()
        {
            DocumentId = "local/b.json",
            Operations =
            [
                new() { Id = "replace104", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.ReplaceAbility, Ability = 104, Replacement = 105, Producers = [new("local/a.json", "ensure104", 104)], ProducerOperationId = "ensure104", ProducerDocumentId = "local/a.json" },
                new() { Id = "initial100", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.SetInitialState, Ability = 100, InitialLearned = true, ExpectedInitialLearned = false },
                new() { Id = "order", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.OrderSubset, Ability = 0, Order = [105, 100], ExpectedOrder = [100, 105], Producers = [new("local/b.json", "replace104", 105)], ProducerOperationId = "replace104", ProducerDocumentId = "local/b.json" }
            ]
        };
        GuardianForceAbilityProfile profile = selected.GuardianForceProfile! with { LocalDocuments = [first, second], ExcludedDocuments = ["shared/disabled.json", "shared/exported.json"], LocalOwnershipAliases = ImmutableDictionary<string, string>.Empty.Add("shared/exported.json", "local/a.json") };
        GuardianForceAbilityComposition composition = GuardianForceAbilityComposer.Compose(profile.Source, profile.AppliedLayers.Where(layer => !profile.ExcludedDocuments.Contains(layer.Document.DocumentId)).Concat(profile.LocalDocuments.Select(doc => new GuardianForceAbilityPatchLayer("Playthrough", doc))), current.MenuDefinitions.AbilityPoints);
        return current.ApplyGuardianForceTemplates(composition.Templates) with { GuardianForceProfile = profile };
    }

    private static NativeCharacterVitals Selected(bool changed, bool collision = false, bool producerConflict = false)
    {
        NativeCharacterVitals source = MagicSystemFixture.Create().DeepCopy();
        for (int id = 0; id < 16; id++)
        {
            source.GuardianForces[id] = source.GuardianForces[id] with { LearnedBank = new(0, 0) };
        }
        ImmutableArray<byte> points = Enumerable.Repeat((byte)20, 116).ToImmutableArray().SetItem(100, changed ? (byte)31 : (byte)20).SetItem(105, changed ? (byte)42 : (byte)20);
        source = source with { KernelSha256 = new string(changed ? 'a' : 'b', 64), MenuDefinitions = source.MenuDefinitions with { AbilityPoints = points, Learning = source.MenuDefinitions.Learning.SetItem(0, Enumerable.Range(0, 21).Select(index => new NativeGuardianForceLearningRule(1, 255, index < 9 ? checked((byte)(100 + index)) : (byte)0)).ToImmutableArray()) } };
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> baseline = source.ResolveGuardianForceTemplates().SetItem(0, [new(79, true, 10), new(100, false, points[100]), new(101, false, 20)]);
        ImmutableArray<GuardianForceAbilityPatchLayer> layers =
        [
            new("Shared/Primary", new() { DocumentId = "shared/package.json", Operations = [new() { Id = "external", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.EnsureAbility, Ability = producerConflict ? 104 : changed ? 106 : 102 }] }),
            new("Shared/Disabled", new() { DocumentId = "shared/disabled.json", Operations = [new() { Id = "disabled", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.EnsureAbility, Ability = 107, InitialLearned = true }] }),
            new("Shared/Exported", new() { DocumentId = "shared/exported.json", Operations = [new() { Id = "exported", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.EnsureAbility, Ability = changed ? 108 : 104, InitialLearned = changed }] })
        ];
        if (collision)
        {
            layers = layers.Add(new("Shared/Collision", new() { DocumentId = "local/a.json", Operations = [new() { Id = "collision", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.EnsureAbility, Ability = 108, InitialLearned = true }] }));
        }
        GuardianForceAbilityProfile profile = new(baseline, changed ? "second-selected-context" : "first-selected-context", layers, [], []);
        GuardianForceAbilityComposition composition = GuardianForceAbilityComposer.Compose(baseline, layers, points);
        return source.ApplyGuardianForceTemplates(composition.Templates) with { GuardianForceProfile = profile };
    }

    private static GuardianForceAbilityProgress Entry(NativeCharacterVitals source, int ability) => source.GuardianForces[0].Progression!.Abilities.Single(entry => entry.Ability == ability);
    private static GameplaySaveComponentDocument Document(NativeCharacterVitals characters) => GameplaySaveComponentDocument.Capture(new(1, GameplaySaveResumeKind.Field, 106, 105, new(0, 0, 0, 0), null), characters, new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = new string('0', 64), Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 }, new(new string('0', 64), 0, 0, 0x11, 0, ImmutableArray.CreateRange(new byte[8])));
    private static byte[] Encode(GameplaySaveComponentDocument document)
    {
        using MemoryStream stream = new();
        GameplaySaveCodec.Write(stream, document);
        return stream.ToArray();
    }

    private static GameplaySaveComponentDocument RoundTrip(GameplaySaveComponentDocument document)
    {
        using MemoryStream stream = new(Encode(document));
        return GameplaySaveCodec.Read(stream);
    }

    private static MemoryStream Container(byte[] payload)
    {
        MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("MFF8GPC1"u8);
            writer.Write(1);
            writer.Write(payload.Length);
            writer.Write(SHA256.HashData(payload));
            writer.Write(payload);
        }
        stream.Position = 0;
        return stream;
    }
}
