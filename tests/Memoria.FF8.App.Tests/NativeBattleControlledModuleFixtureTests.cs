using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleControlledModuleFixtureTests
{
    [Fact]
    public void Default_component_selection_stays_strict_about_the_original_kernel_effect()
    {
        NativeEffectFixtureRequest request = Request();
        BattleNativeAction shell = Shell();
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectAction([shell], request));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectComponentAction([shell], request));
        Assert.Same(shell, NativeBattleEffectFixtureExporter.SelectComponentAction([shell], request with { Effect = 2 }));
    }

    [Fact]
    public void Explicit_selection_keeps_the_original_shell_but_selects_only_the_requested_header_module()
    {
        NativeEffectFixtureRequest request = Request() with { ControlledShellEffect = 2 };
        BattleNativeAction shell = Shell();
        string before = JsonSerializer.Serialize(shell);
        Assert.Same(shell, NativeBattleEffectFixtureExporter.SelectComponentAction([shell], request));
        Assert.Equal((ushort)77, NativeBattleEffectFixtureExporter.SelectHeaderEffect(shell, request));
        Assert.Equal((ushort)2, NativeBattleEffectFixtureExporter.SelectHeaderEffect(shell, request with { Effect = 2, ControlledShellEffect = null }));
        Assert.Equal(before, JsonSerializer.Serialize(shell));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectComponentAction([shell with { Command = 4 }], request));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectComponentAction([shell with { ActionId = 2 }], request));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectComponentAction([shell, shell], request));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.SelectHeaderEffect(shell with { MagicId = 3 }, request));
    }

    [Fact]
    public void Explicit_module_selection_rejects_formula_admission_and_invalid_shell_effects()
    {
        NativeEffectFixtureRequest request = Request() with { ControlledShellEffect = 2 };
        NativeBattleEffectFixtureExporter.Validate(request);
        NativeEffectFixtureRequest[] forbidden =
        [
            request with { OrdinaryMagicAdmission = true },
            request with { DeferredGuardianFormulaAdmission = true },
            request with { HealingContact = true },
            request with { RevivalContact = true },
            request with { ControlledShellEffect = 0 },
            request with { ControlledShellEffect = 401 }
        ];
        foreach (NativeEffectFixtureRequest invalid in forbidden)
        {
            Assert.Throws<InvalidDataException>(() => NativeBattleEffectFixtureExporter.Validate(invalid));
        }
        NativeBattleEffectFixtureExporter.Validate(request with { Effect = 400, ControlledShellEffect = 1 });
    }

    [Fact]
    public void Direct_module_metadata_does_not_claim_an_original_kernel_effect_reference()
    {
        NativeEffectFixtureSelection selection = new(false, 2, 1, 2, 1, 1, 77);
        NativeEffectFixtureExportResult result = new("direct", 77, "fixture-only", "queued-action104", null, [], selection);
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        JsonElement identity = json.RootElement.GetProperty("Selection");
        Assert.False(identity.GetProperty("OriginalKernelEffectReference").GetBoolean());
        Assert.Equal(2, identity.GetProperty("ShellEffect").GetInt32());
        Assert.Equal(77, identity.GetProperty("RequestedModuleEffect").GetInt32());
    }

    private static NativeEffectFixtureRequest Request()
    {
        return new("direct", 77, 2, 1, 0, 200, 60);
    }

    private static BattleNativeAction Shell()
    {
        return new(2, 1, 1, 1)
        {
            MagicId = 2,
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
