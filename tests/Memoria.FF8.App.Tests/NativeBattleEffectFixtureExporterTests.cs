using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffectFixtureExporterTests
{
    [Fact]
    public void Export_one_requested_fresh_original_resource_component()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_FIXTURE_REQUESTS");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "External component fixture requests are required.");
        string id = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_FIXTURE_CASE") ?? throw new InvalidDataException("One external fixture identity per process is required.");
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT_FIXTURE_OUTPUT") ?? throw new InvalidDataException("A distinct external fixture output directory is required.");
        NativeEffectFixtureRequests requests = JsonSerializer.Deserialize<NativeEffectFixtureRequests>(File.ReadAllBytes(path!)) ?? throw new InvalidDataException("Fixture requests are null.");
        NativeEffectFixtureRequest request = requests.Cases.Single(item => item.Id == id);
        NativeEffectFixtureExportResult result = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory, output);
        string resultPath = Path.Combine(output, "fixture-result.json");
        Directory.CreateDirectory(output);
        using (FileStream stream = new(resultPath, FileMode.CreateNew))
        {
            JsonSerializer.Serialize(stream, result, new JsonSerializerOptions { WriteIndented = true });
        }
        Console.WriteLine(JsonSerializer.Serialize(result));
        // A fixture-only export is useful tooling output; it is never accepted effect coverage.
        Assert.NotEmpty(result.Artifacts);
        Assert.NotEqual("blocked", result.Status);
    }

    [Fact]
    public void Requested_kernel_identity_cannot_be_replaced_with_a_different_effect()
    {
        NativeEffectFixtureRequest request = Request();
        BattleNativeAction original = Action();
        Assert.Same(original, NativeBattleEffectFixtureExporter.SelectAction([original], request));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectAction([original with { MagicId = 2 }], request));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectAction([original with { ActionId = 2 }], request));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectAction([original, original], request));
    }

    [Fact]
    public void Blue_magic_component_requires_the_selected_original_crisis_variant()
    {
        NativeEffectFixtureRequest request = Request() with { Command = 15, Effect = 72 };
        BattleNativeAction[] variants = Enumerable.Range(1, 4).Select(crisis => Action() with { Command = 15, MagicId = 72, CrisisLevel = (byte)crisis, SupplementalKernelIndex = 4 + crisis }).ToArray();
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectAction(variants, request));
        foreach (BattleNativeAction variant in variants)
        {
            Assert.Same(variant, NativeBattleEffectFixtureExporter.SelectAction(variants, request with { CrisisLevel = variant.CrisisLevel }));
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectAction(variants, request with { CrisisLevel = 5 }));
    }

    [Fact]
    public void Header_sequence_reuses_original_command_table_and_existing_special_command_contract()
    {
        ImmutableArray<byte> table = Enumerable.Range(0, 39).Select(value => (byte)value).ToImmutableArray();
        Assert.Equal((byte)2, NativeBattleEffectFixtureExporter.SelectSequence(table, 2, 5));
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectSequence(table, 254, 64));
        Assert.Equal((byte)11, NativeBattleEffectFixtureExporter.SelectSequence(table, 254, 70));
        Assert.Equal((byte)0, NativeBattleEffectFixtureExporter.SelectSequence(table, 240, 4));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectSequence([], 2, 5));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectFixtureExporter.SelectSequence(table, 255, 5));
    }

    [Fact]
    public void Fixture_request_requires_explicit_boundaries_and_supported_target_owner()
    {
        NativeEffectFixtureRequest request = Request();
        NativeBattleEffectFixtureExporter.Validate(request);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(request with { EntryMaximumCallbacks = 0 }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(request with { ProcessTimeoutSeconds = 0 }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(request with { TargetActorIndex = 1 }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(request with { AdmissionProbeMaximumCallbacks = -1 }));
    }

    private static NativeEffectFixtureRequest Request()
    {
        return new("case-a", 1, 2, 1, 3, 200, 60);
    }

    private static BattleNativeAction Action()
    {
        return new(2, 1, 1, 1)
        {
            MagicId = 1,
            AttackType = 0,
            Power = 0,
            HitCount = 1,
            TargetFlags = 0,
            AttackFlags = 0,
            DamagePresentation = 5,
            Element = 0,
            ElementPercent = 0,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = 0,
            Critical = 0,
            PowerModifier = 0,
            LevelModifier = 0,
            RawRecord = [],
            SupplementalRecord = []
        };
    }
}
