using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Compact input encoding. Omitted fields use only ReplayJournalEntry's declared scalar/empty defaults.</summary>
internal sealed class ReplayJournalEntryStorage : JsonConverter<ReplayJournalEntry>
{
    internal static readonly JsonSerializerOptions Options = new() { Converters = { new ReplayJournalEntryStorage() } };

    internal static byte[] Encode(ReplayJournalEntry entry)
    {
        return JsonSerializer.SerializeToUtf8Bytes(entry, Options);
    }

    public override ReplayJournalEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<ReplayJournalEntry>(ref reader) ?? throw new InvalidDataException("Missing compact replay input.");
    }

    public override void Write(Utf8JsonWriter writer, ReplayJournalEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(value.Kind), (int)value.Kind);
        writer.WriteNumber(nameof(value.Scene), (int)value.Scene);
        writer.WriteNumber(nameof(value.FieldId), value.FieldId);
        writer.WriteNumber(nameof(value.Frame), value.Frame);
        Number(writer, nameof(value.Code), value.Code);
        Number(writer, nameof(value.Value), value.Value);
        Number(writer, nameof(value.MappedPulse), value.MappedPulse);
        Optional(writer, nameof(value.Quantity), value.Quantity);
        Optional(writer, nameof(value.Text), value.Text);
        Number(writer, nameof(value.Horizontal), value.Horizontal);
        Number(writer, nameof(value.Vertical), value.Vertical);
        Number(writer, nameof(value.CameraHorizontal), value.CameraHorizontal);
        Number(writer, nameof(value.Frames), value.Frames, 1);
        Number(writer, nameof(value.Advanced), value.Advanced);
        Number(writer, nameof(value.Repeat), value.Repeat, 1);
        if (!value.Accepted)
        {
            writer.WriteBoolean(nameof(value.Accepted), false);
        }
        Optional(writer, nameof(value.ResultScene), value.ResultScene);
        Optional(writer, nameof(value.ResultFieldId), value.ResultFieldId);
        Optional(writer, nameof(value.ResultFrame), value.ResultFrame);
        Number(writer, nameof(value.Trigger), value.Trigger);
        Number(writer, nameof(value.ResultTrigger), value.ResultTrigger);
        Optional(writer, nameof(value.MessageId), value.MessageId);
        Optional(writer, nameof(value.ObjectId), value.ObjectId);
        Optional(writer, nameof(value.ScriptId), value.ScriptId);
        Optional(writer, nameof(value.Operation), value.Operation);
        Optional(writer, nameof(value.ResultMessageId), value.ResultMessageId);
        Optional(writer, nameof(value.ResultObjectId), value.ResultObjectId);
        Optional(writer, nameof(value.ResultScriptId), value.ResultScriptId);
        Optional(writer, nameof(value.ResultOperation), value.ResultOperation);
        Optional(writer, nameof(value.StartPose), value.StartPose);
        Optional(writer, nameof(value.EndPose), value.EndPose);
        Array(writer, nameof(value.Navigation), value.Navigation);
        Array(writer, nameof(value.WorldNavigation), value.WorldNavigation);
        Array(writer, nameof(value.Receipts), value.Receipts);
        Number(writer, nameof(value.CommandCount), value.CommandCount);
        if (value.CommandDigest != 0)
        {
            writer.WriteNumber(nameof(value.CommandDigest), value.CommandDigest);
        }
        Number(writer, nameof(value.MovieSeconds), value.MovieSeconds);
        Optional(writer, nameof(value.MoviePlayback), value.MoviePlayback);
        Optional(writer, nameof(value.MovieSkipFrames), value.MovieSkipFrames);
        Optional(writer, nameof(value.MovieSourceHash), value.MovieSourceHash);
        if (value.Skipped)
        {
            writer.WriteBoolean(nameof(value.Skipped), true);
        }
        Number(writer, nameof(value.ElapsedTicks), value.ElapsedTicks);
        Optional(writer, nameof(value.NativeSourceHash), value.NativeSourceHash);
        Optional(writer, nameof(value.BattleInput), value.BattleInput);
        Optional(writer, nameof(value.SemanticTrigger), value.SemanticTrigger);
        Optional(writer, nameof(value.Approach), value.Approach);
        Array(writer, nameof(value.SemanticAdmissions), value.SemanticAdmissions);
        Optional(writer, nameof(value.ScriptGoal), value.ScriptGoal);
        Optional(writer, nameof(value.EffectArguments), value.EffectArguments);
        Optional(writer, nameof(value.WorldAllocation), value.WorldAllocation);
        Optional(writer, nameof(value.NativeTraversal), value.NativeTraversal);
        Array(writer, nameof(value.WorldEffects), value.WorldEffects);
        writer.WriteEndObject();
    }

    private static void Number(Utf8JsonWriter writer, string name, long value, long omitted = 0)
    {
        if (value != omitted)
        {
            writer.WriteNumber(name, value);
        }
    }

    private static void Number(Utf8JsonWriter writer, string name, double value)
    {
        if (BitConverter.DoubleToInt64Bits(value) != 0)
        {
            writer.WriteNumber(name, value);
        }
    }

    private static void Number(Utf8JsonWriter writer, string name, float value)
    {
        if (BitConverter.SingleToInt32Bits(value) != 0)
        {
            writer.WriteNumber(name, value);
        }
    }

    private static void Optional<T>(Utf8JsonWriter writer, string name, T value)
    {
        if (value is not null)
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, value);
        }
    }

    private static void Array<T>(Utf8JsonWriter writer, string name, T[] values)
    {
        if (values.Length != 0)
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, values);
        }
    }
}
