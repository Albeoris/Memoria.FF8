using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class ComponentCompletionValidation
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new JsonStringEnumConverter() } };

    public static string ValidateFiles(string measurementPath, string receiptPath, ushort effect, string sourceSha256, int processId)
    {
        using JsonDocument measurement = Read(measurementPath);
        using JsonDocument receipt = Read(receiptPath);
        return Validate(measurement.RootElement, receipt.RootElement, effect, sourceSha256, processId);
    }

    public static string Validate(JsonElement measurement, JsonElement receipt, ushort effect, string sourceSha256, int processId)
    {
        JsonElement identity = receipt.GetProperty("native_identity");
        string kind = measurement.GetProperty("completion_kind").GetString() ?? throw new InvalidDataException("Component completion kind is absent.");
        bool held = kind == "held-native-screen-blend";
        if (!held && kind != "terminal-texture-quiescence")
        {
            throw new InvalidDataException("Component resident completion kind is unsupported.");
        }
        if (processId <= 0 || receipt.GetProperty("state").GetString() != "completed" || receipt.GetProperty("pid").GetInt32() != processId || receipt.GetProperty("expected_effect").GetUInt16() != effect || identity.GetProperty("effect").GetUInt16() != effect || measurement.GetProperty("scope").GetString() != "installed-source-controlled-component-gpu-presentation" || measurement.GetProperty("effect").GetUInt16() != effect || !StringComparer.OrdinalIgnoreCase.Equals(measurement.GetProperty("source_sha256").GetString(), sourceSha256) || identity.GetProperty("completion_kind").GetString() != kind || measurement.GetProperty("drained").GetBoolean() || identity.GetProperty("drained").GetBoolean())
        {
            throw new InvalidDataException("Terminal component receipt does not identify the exited owned host, source and truthful non-drained completion kind.");
        }
        string property = held ? "held_screen_blend_certificate" : "terminal_texture_certificate";
        string otherProperty = held ? "terminal_texture_certificate" : "held_screen_blend_certificate";
        if (measurement.TryGetProperty(otherProperty, out JsonElement otherMeasurement) && otherMeasurement.ValueKind != JsonValueKind.Null || identity.TryGetProperty(otherProperty, out JsonElement otherIdentity) && otherIdentity.ValueKind != JsonValueKind.Null)
        {
            throw new InvalidDataException("Distinct resident completion contracts cannot substitute or combine their certificates.");
        }
        JsonElement payload = measurement.GetProperty(property);
        JsonElement receiptPayload = identity.GetProperty(property);
        foreach (JsonElement evidence in new[] { payload, receiptPayload })
        {
            Require(evidence, "completion_kind", "completed", "drained", "resident_tasks_remain", "effect", "source_sha256", "native_dispatch", "native_admission", "first", "last", "first_presented_frame", "last_presented_frame", "tail_presented_frames", "native_callbacks_advanced");
            if (held)
            {
                Require(evidence, "held_render_work_remains");
                foreach (JsonElement outer in new[] { measurement, identity })
                {
                    Require(outer, "completed", "resident_tasks_remain", "held_render_work_remains");
                    if (!outer.GetProperty("completed").GetBoolean() || !outer.GetProperty("resident_tasks_remain").GetBoolean() || !outer.GetProperty("held_render_work_remains").GetBoolean())
                    {
                        throw new InvalidDataException("Held native screen blend completion must explicitly retain and disclose its resident rendering work in both outer receipts.");
                    }
                }
            }
            foreach (string point in new[] { "first", "last" })
            {
                JsonElement observation = evidence.GetProperty(point);
                Require(observation, "effect", "graphics_callback", "native_active", "canonical_returned_task", "owners", "physical_command_banks_empty", "resident_fingerprint", "raw_blockers");
                JsonElement owners = observation.GetProperty("owners");
                Require(owners, "descriptor_packets", "non_pump_descriptor_tasks", "pending_contacts", "effects", "effect_head", "auxiliary_head", "modules", "module_head", "files", "returned_task_pointer", "detached_returned_task", "camera_mask", "sound_busy", "replacement_resource", "action_or_command", "timed_or_reaction", "action_headers", "contact_rows", "retained_active_header", "retained_active_contact", "observed_original_effect", "selected_returned_descriptor", "header_graphics_busy", "original_pools");
                foreach (JsonElement pool in owners.GetProperty("original_pools").EnumerateArray())
                {
                    Require(pool, "descriptor_address", "bank_address", "stride", "active_rows", "head", "tail");
                }
                if (held)
                {
                    Require(observation, "physical_resident_count", "head", "tail", "screen_blend");
                    Require(observation.GetProperty("screen_blend"), "slot", "flags", "state", "next", "kind", "unknown_byte_mask", "has_program", "payload");
                }
                else
                {
                    Require(observation, "textures");
                    JsonElement textures = observation.GetProperty("textures");
                    Require(textures, "capacity", "physical_active_count", "head", "tail", "complete", "tasks");
                    foreach (JsonElement task in textures.GetProperty("tasks").EnumerateArray())
                    {
                        Require(task, "slot", "flags", "state", "next", "kind", "unknown_byte_mask", "payload_length", "model_pointer", "captured_sequence", "program_offset", "program_length", "deadline", "model_index", "model_flags0", "resource_flags44", "model_sequence");
                    }
                }
            }
            RequireIdentity(evidence.GetProperty("native_dispatch"), "native_dispatch");
            RequireIdentity(evidence.GetProperty("native_admission"), "native_admission");
        }
        foreach (string name in new[] { "native_dispatch", "native_admission" })
        {
            JsonElement actual = measurement.GetProperty(name);
            RequireIdentity(actual, name);
            if (!JsonElement.DeepEquals(actual, payload.GetProperty(name)))
            {
                throw new InvalidDataException("Terminal certificate changed the actual dispatched or admitted native identity.");
            }
        }
        long tailFrames;
        long lastFrame;
        if (held)
        {
            BattleComponentHeldScreenBlendCertificate certificate = ReadMatchingCertificates<BattleComponentHeldScreenBlendCertificate>(payload, receiptPayload);
            BattleComponentHeldScreenBlendCertificateValidation.Validate(certificate, effect, sourceSha256);
            tailFrames = certificate.TailPresentedFrames;
            lastFrame = certificate.LastPresentedFrame;
        }
        else
        {
            BattleComponentTerminalTextureCertificate certificate = ReadMatchingCertificates<BattleComponentTerminalTextureCertificate>(payload, receiptPayload);
            BattleComponentTerminalTextureCertificateValidation.Validate(certificate, effect, sourceSha256);
            tailFrames = certificate.TailPresentedFrames;
            lastFrame = certificate.LastPresentedFrame;
        }
        if (measurement.GetProperty("tail_presented_frames").GetInt64() != tailFrames || receipt.GetProperty("frame").GetInt64() != lastFrame)
        {
            throw new InvalidDataException("Resident component certificate exceeds its actual completion presentation receipt.");
        }
        return kind;
    }

    private static T ReadMatchingCertificates<T>(JsonElement measurement, JsonElement receipt) where T : class
    {
        T first = measurement.Deserialize<T>(Json) ?? throw new InvalidDataException("Component measurement certificate is empty.");
        T last = receipt.Deserialize<T>(Json) ?? throw new InvalidDataException("Component completion certificate is empty.");
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first, Json), JsonSerializer.SerializeToElement(last, Json)))
        {
            throw new InvalidDataException("Component completion and measurement carry different terminal certificates.");
        }
        return first;
    }

    private static void RequireIdentity(JsonElement value, string name)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return;
        }
        if (name == "native_dispatch")
        {
            Require(value, "serial", "callback", "actor_id", "ability", "action_id", "requested_effect", "executed_effect", "native_command");
        }
        else
        {
            Require(value, "effect", "command", "action", "actor", "header_index", "callback");
        }
    }

    private static void Require(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != value.EnumerateObject().Count() || names.Any(name => !value.TryGetProperty(name, out _)))
        {
            throw new InvalidDataException("Terminal component certificate is missing explicit native owner facts or contains duplicate fields.");
        }
    }

    private static JsonDocument Read(string path)
    {
        if (new FileInfo(path).Length is <= 0 or > 1024 * 1024)
        {
            throw new InvalidDataException("Component completion evidence must be a bounded compact report.");
        }
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }
}
