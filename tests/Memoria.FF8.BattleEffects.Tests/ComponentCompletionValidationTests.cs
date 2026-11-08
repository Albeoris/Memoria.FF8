using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ComponentCompletionValidationTests
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    private static readonly string SourceHash = new('b', 64);

    private static BattleComponentTerminalTextureCertificate Certificate()
    {
        BattleDiagnosticInvocationOwners owners = new(0, 0, 0, 0, false, true, 0, false, 0, 0, false, 0, false, false, false, false) { ObservedOriginalEffect = true, SelectedReturnedDescriptor = 0, OriginalPools = [], ActionHeaders = 1, ContactRows = 1, RetainedActiveHeader = 0, RetainedActiveContact = 0 };
        BattleDiagnosticTextureTask task = new(0, 1, 0, null, NativeBattleAuxiliaryKind.TextureAnimation5057D0, 0, 32, 0x1D972C0, 2, 0, 1, 255, 0, 2, 0, 2);
        BattleDiagnosticTextureQuiescence textures = new(16, 1, 0, 0, true, [task]);
        BattleComponentTextureQuiescenceObservation first = new(101, 10, 0, 0, owners, true, textures, new string('a', 64), ["effect-or-auxiliary-task"]);
        BattleDiagnosticEffectDispatch dispatch = new(1, 5, "party", BattleAbility.Magic, 1, 101, 101, 2);
        return new("terminal-texture-quiescence", true, false, true, 101, SourceHash, dispatch, null, first, first with { GraphicsCallback = 11 }, 100, 102, 2, 1);
    }

    private static (JsonObject Measurement, JsonObject Receipt) Reports()
    {
        BattleComponentTerminalTextureCertificate certificate = Certificate();
        JsonObject measurement = JsonSerializer.SerializeToNode(new { scope = "installed-source-controlled-component-gpu-presentation", effect = 101, source_sha256 = SourceHash, completion_kind = certificate.CompletionKind, drained = false, terminal_texture_certificate = certificate, native_dispatch = certificate.NativeDispatch, native_admission = certificate.NativeAdmission, tail_presented_frames = 2 }, Json)!.AsObject();
        JsonObject receipt = JsonSerializer.SerializeToNode(new { state = "completed", pid = 123, expected_effect = 101, frame = 102, native_identity = new { effect = 101, completion_kind = certificate.CompletionKind, drained = false, terminal_texture_certificate = certificate } }, Json)!.AsObject();
        return (measurement, receipt);
    }

    private static string Validate(JsonObject measurement, JsonObject receipt)
    {
        using JsonDocument first = JsonDocument.Parse(measurement.ToJsonString());
        using JsonDocument second = JsonDocument.Parse(receipt.ToJsonString());
        return ComponentCompletionValidation.Validate(first.RootElement, second.RootElement, 101, SourceHash, 123);
    }

    [Fact]
    public void Resident_terminal_texture_completion_is_explicitly_distinct_from_full_drain()
    {
        var (measurement, receipt) = Reports();
        Assert.Equal("terminal-texture-quiescence", Validate(measurement, receipt));
        Assert.False(Certificate().Drained);
        Assert.True(Certificate().ResidentTasksRemain);
    }

    [Fact]
    public void Terminal_predicate_ignores_unread_resource_and_model_flag_bits_while_retaining_full_observations()
    {
        BattleComponentTerminalTextureCertificate certificate = Certificate();
        BattleDiagnosticTextureTask first = certificate.First.Textures.Tasks[0] with { ResourceFlags44 = 2, ModelFlags0 = 7 };
        BattleDiagnosticTextureTask last = first with { ResourceFlags44 = 6, ModelFlags0 = 3 };
        certificate = certificate with
        {
            First = certificate.First with { Textures = certificate.First.Textures with { Tasks = [first] } },
            Last = certificate.Last with { Textures = certificate.Last.Textures with { Tasks = [last] } }
        };
        BattleComponentTerminalTextureCertificateValidation.Validate(certificate, 101, SourceHash);
        Assert.Equal((ushort)6, certificate.Last.Textures.Tasks[0].ResourceFlags44);
        Assert.Equal((ushort)3, certificate.Last.Textures.Tasks[0].ModelFlags0);
    }

    [Fact]
    public void String_and_numeric_enum_serializers_describe_the_same_native_certificate()
    {
        var (measurement, receipt) = Reports();
        receipt["native_identity"]!["terminal_texture_certificate"] = JsonSerializer.SerializeToNode(Certificate(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        Assert.Equal("terminal-texture-quiescence", Validate(measurement, receipt));
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
        receipt["native_identity"]!["terminal_texture_certificate"] = numeric;
        Assert.Throws<InvalidDataException>(() => Validate(measurement, receipt));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stationary_tail_cannot_precede_its_live_dispatch_or_native_admission(bool admission)
    {
        BattleComponentTerminalTextureCertificate certificate = Certificate();
        if (admission)
        {
            certificate = certificate with { NativeDispatch = null, NativeAdmission = new(certificate.Effect, 2, 1, 0, 0, certificate.First.GraphicsCallback + 1) };
        }
        else
        {
            certificate = certificate with { NativeDispatch = certificate.NativeDispatch! with { Callback = certificate.First.GraphicsCallback + 1 } };
        }
        Assert.Throws<InvalidDataException>(() => BattleComponentTerminalTextureCertificateValidation.Validate(certificate, certificate.Effect, SourceHash));
        certificate = admission
            ? certificate with { NativeAdmission = certificate.NativeAdmission! with { Callback = certificate.First.GraphicsCallback } }
            : certificate with { NativeDispatch = certificate.NativeDispatch! with { Callback = certificate.First.GraphicsCallback } };
        BattleComponentTerminalTextureCertificateValidation.Validate(certificate, certificate.Effect, SourceHash);
    }

    [Fact]
    public void Certificate_rejects_effect_outside_the_existing_component_preview_range()
    {
        BattleComponentTerminalTextureCertificate certificate = Certificate();
        certificate = certificate with { Effect = 401, First = certificate.First with { Effect = 401 }, Last = certificate.Last with { Effect = 401 }, NativeDispatch = certificate.NativeDispatch! with { RequestedEffect = 401, ExecutedEffect = 401 } };
        Assert.Throws<InvalidDataException>(() => BattleComponentTerminalTextureCertificateValidation.Validate(certificate, 401, SourceHash));
    }

    [Theory]
    [InlineData("drained")]
    [InlineData("pid")]
    [InlineData("source")]
    [InlineData("certificate_mismatch")]
    [InlineData("owner_fact_missing")]
    [InlineData("resident_changed")]
    [InlineData("other_owner")]
    [InlineData("not_terminal")]
    [InlineData("screen_blend")]
    [InlineData("dispatch")]
    [InlineData("tail")]
    [InlineData("callback")]
    [InlineData("packet_storage_changed")]
    [InlineData("resource_predicate")]
    [InlineData("loaded_predicate")]
    [InlineData("sequence")]
    [InlineData("knownness")]
    public void Certificate_cannot_hide_another_owner_or_replace_native_identity_and_stationarity(string fault)
    {
        var (measurement, receipt) = Reports();
        JsonObject certificate = measurement["terminal_texture_certificate"]!.AsObject();
        switch (fault)
        {
            case "drained":
                measurement["drained"] = true;
                break;
            case "pid":
                receipt["pid"] = 124;
                break;
            case "source":
                certificate["source_sha256"] = new string('c', 64);
                break;
            case "certificate_mismatch":
                receipt["native_identity"]!["terminal_texture_certificate"]!["tail_presented_frames"] = 3;
                break;
            case "owner_fact_missing":
                certificate["first"]!["owners"]!.AsObject().Remove("effects");
                break;
            case "resident_changed":
                certificate["last"]!["resident_fingerprint"] = new string('c', 64);
                break;
            case "other_owner":
                certificate["last"]!["owners"]!["pending_contacts"] = 1;
                break;
            case "not_terminal":
                certificate["last"]!["textures"]!["tasks"]![0]!["deadline"] = 254;
                break;
            case "screen_blend":
                certificate["last"]!["textures"]!["tasks"]![0]!["kind"] = "ScreenBlend501E80";
                break;
            case "dispatch":
                measurement["native_dispatch"]!["serial"] = 2;
                break;
            case "tail":
                certificate["tail_presented_frames"] = 1;
                break;
            case "packet_storage_changed":
                certificate["last"]!["owners"]!["action_headers"] = 2;
                break;
            case "resource_predicate":
                certificate["last"]!["textures"]!["tasks"]![0]!["resource_flags44"] = 0x40;
                break;
            case "loaded_predicate":
                certificate["last"]!["textures"]!["tasks"]![0]!["model_flags0"] = 0;
                break;
            case "sequence":
                certificate["last"]!["textures"]!["tasks"]![0]!["model_sequence"] = 3;
                break;
            case "knownness":
                certificate["last"]!["textures"]!["tasks"]![0]!["unknown_byte_mask"] = 1;
                break;
            default:
                certificate["native_callbacks_advanced"] = 0;
                break;
        }
        if (fault != "certificate_mismatch")
        {
            receipt["native_identity"]!["terminal_texture_certificate"] = certificate.DeepClone();
        }
        Assert.Throws<InvalidDataException>(() => Validate(measurement, receipt));
    }
}
