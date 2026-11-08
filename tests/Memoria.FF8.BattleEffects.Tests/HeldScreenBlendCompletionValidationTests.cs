using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class HeldScreenBlendCompletionValidationTests
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    private static readonly string SourceHash = new('b', 64);

    private static BattleComponentHeldScreenBlendCertificate Certificate()
    {
        BattleDiagnosticInvocationOwners owners = new(0, 0, 0, 0, false, true, 0, false, 0, 0, false, 0, false, false, false, false) { ObservedOriginalEffect = true, SelectedReturnedDescriptor = 0, OriginalPools = [], ActionHeaders = 1, ContactRows = 1, RetainedActiveHeader = 0, RetainedActiveContact = 0 };
        byte[] payload = new byte[32];
        payload[0] = 60;
        payload[2] = 60;
        payload[5] = 255;
        BattleComponentHeldScreenBlendTask task = new(0, 1, 0, null, NativeBattleAuxiliaryKind.ScreenBlend501E80, 0, false, payload.ToImmutableArray());
        BattleComponentHeldScreenBlendObservation first = new(77, 10, 0, 0, owners, true, 1, 0, 0, task, task.Fingerprint, ["effect-or-auxiliary-task"]);
        BattleDiagnosticEffectDispatch dispatch = new(1, 5, "enemy", BattleAbility.EnemyAbility, 1, 77, 77, 8);
        return new("held-native-screen-blend", true, false, true, true, 77, SourceHash, dispatch, null, first, first with { GraphicsCallback = 11 }, 100, 102, 2, 1);
    }

    private static (JsonObject Measurement, JsonObject Receipt) Reports()
    {
        BattleComponentHeldScreenBlendCertificate certificate = Certificate();
        JsonObject measurement = JsonSerializer.SerializeToNode(new { scope = "installed-source-controlled-component-gpu-presentation", effect = 77, source_sha256 = SourceHash, completion_kind = certificate.CompletionKind, completed = true, drained = false, resident_tasks_remain = true, held_render_work_remains = true, held_screen_blend_certificate = certificate, native_dispatch = certificate.NativeDispatch, native_admission = certificate.NativeAdmission, tail_presented_frames = 2 }, Json)!.AsObject();
        JsonObject receipt = JsonSerializer.SerializeToNode(new { state = "completed", pid = 123, expected_effect = 77, frame = 102, native_identity = new { effect = 77, completion_kind = certificate.CompletionKind, completed = true, drained = false, resident_tasks_remain = true, held_render_work_remains = true, held_screen_blend_certificate = certificate } }, Json)!.AsObject();
        return (measurement, receipt);
    }

    private static string Validate(JsonObject measurement, JsonObject receipt)
    {
        using JsonDocument first = JsonDocument.Parse(measurement.ToJsonString());
        using JsonDocument second = JsonDocument.Parse(receipt.ToJsonString());
        return ComponentCompletionValidation.Validate(first.RootElement, second.RootElement, 77, SourceHash, 123);
    }

    [Fact]
    public void Held_native_render_work_is_certified_separately_from_texture_quiescence_and_full_drain()
    {
        var (measurement, receipt) = Reports();
        Assert.Equal("held-native-screen-blend", Validate(measurement, receipt));
        Assert.True(Certificate().HeldRenderWorkRemains);
        Assert.True(Certificate().ResidentTasksRemain);
        Assert.False(Certificate().Drained);
    }

    [Fact]
    public void String_and_numeric_enum_serializers_describe_the_same_native_certificate()
    {
        var (measurement, receipt) = Reports();
        receipt["native_identity"]!["held_screen_blend_certificate"] = JsonSerializer.SerializeToNode(Certificate(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        Assert.Equal("held-native-screen-blend", Validate(measurement, receipt));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Numeric_enum_normalization_cannot_hide_changed_values_or_omitted_zero_owner_facts(bool missingFact)
    {
        var (measurement, receipt) = Reports();
        JsonObject numeric = JsonSerializer.SerializeToNode(Certificate(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!.AsObject();
        if (missingFact)
        {
            numeric["first"]!["owners"]!.AsObject().Remove("effects");
        }
        else
        {
            numeric["last"]!["native_active"] = 1;
        }
        receipt["native_identity"]!["held_screen_blend_certificate"] = numeric;
        Assert.Throws<InvalidDataException>(() => Validate(measurement, receipt));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stationary_tail_cannot_precede_its_live_dispatch_or_native_admission(bool admission)
    {
        BattleComponentHeldScreenBlendCertificate certificate = Certificate();
        if (admission)
        {
            certificate = certificate with { NativeDispatch = null, NativeAdmission = new(certificate.Effect, 2, 1, 0, 0, certificate.First.GraphicsCallback + 1) };
        }
        else
        {
            certificate = certificate with { NativeDispatch = certificate.NativeDispatch! with { Callback = certificate.First.GraphicsCallback + 1 } };
        }
        Assert.Throws<InvalidDataException>(() => BattleComponentHeldScreenBlendCertificateValidation.Validate(certificate, certificate.Effect, SourceHash));
        certificate = admission
            ? certificate with { NativeAdmission = certificate.NativeAdmission! with { Callback = certificate.First.GraphicsCallback } }
            : certificate with { NativeDispatch = certificate.NativeDispatch! with { Callback = certificate.First.GraphicsCallback } };
        BattleComponentHeldScreenBlendCertificateValidation.Validate(certificate, certificate.Effect, SourceHash);
    }

    [Fact]
    public void Certificate_rejects_effect_outside_the_existing_component_preview_range()
    {
        BattleComponentHeldScreenBlendCertificate certificate = Certificate();
        certificate = certificate with { Effect = 401, First = certificate.First with { Effect = 401 }, Last = certificate.Last with { Effect = 401 }, NativeDispatch = certificate.NativeDispatch! with { RequestedEffect = 401, ExecutedEffect = 401 } };
        Assert.Throws<InvalidDataException>(() => BattleComponentHeldScreenBlendCertificateValidation.Validate(certificate, 401, SourceHash));
    }

    [Theory]
    [InlineData("render_work_hidden")]
    [InlineData("full_drain")]
    [InlineData("task_fact_missing")]
    [InlineData("unknown_native_fields")]
    [InlineData("program")]
    [InlineData("extra_resident")]
    [InlineData("different_kind")]
    [InlineData("different_padding")]
    [InlineData("fingerprint")]
    [InlineData("native_callback")]
    [InlineData("packet_owner")]
    [InlineData("combined_contracts")]
    [InlineData("outer_work_hidden")]
    [InlineData("outer_fact_missing")]
    public void Held_certificate_rejects_forged_payload_ownership_or_presentation_boundaries(string fault)
    {
        var (measurement, receipt) = Reports();
        JsonObject certificate = measurement["held_screen_blend_certificate"]!.AsObject();
        switch (fault)
        {
            case "render_work_hidden":
                certificate["held_render_work_remains"] = false;
                break;
            case "full_drain":
                certificate["drained"] = true;
                break;
            case "task_fact_missing":
                certificate["first"]!["screen_blend"]!.AsObject().Remove("has_program");
                break;
            case "unknown_native_fields":
                certificate["last"]!["screen_blend"]!["unknown_byte_mask"] = 1;
                break;
            case "program":
                certificate["last"]!["screen_blend"]!["has_program"] = true;
                break;
            case "extra_resident":
                certificate["last"]!["physical_resident_count"] = 2;
                break;
            case "different_kind":
                certificate["last"]!["screen_blend"]!["kind"] = "TextureAnimation5057D0";
                break;
            case "different_padding":
                BattleComponentHeldScreenBlendTask task = Certificate().Last.ScreenBlend;
                byte[] bytes = task.Payload.ToArray();
                bytes[31] = 1;
                task = task with { Payload = bytes.ToImmutableArray() };
                certificate["last"]!["screen_blend"] = JsonSerializer.SerializeToNode(task, Json);
                certificate["last"]!["resident_fingerprint"] = task.Fingerprint;
                break;
            case "fingerprint":
                certificate["last"]!["resident_fingerprint"] = new string('a', 64);
                break;
            case "native_callback":
                certificate["native_callbacks_advanced"] = 0;
                break;
            case "packet_owner":
                certificate["last"]!["owners"]!["retained_active_contact"] = null;
                break;
            case "outer_work_hidden":
                measurement["held_render_work_remains"] = false;
                break;
            case "outer_fact_missing":
                receipt["native_identity"]!.AsObject().Remove("completed");
                break;
            default:
                measurement["terminal_texture_certificate"] = certificate.DeepClone();
                break;
        }
        receipt["native_identity"]!["held_screen_blend_certificate"] = certificate.DeepClone();
        Assert.Throws<InvalidDataException>(() => Validate(measurement, receipt));
    }
}
