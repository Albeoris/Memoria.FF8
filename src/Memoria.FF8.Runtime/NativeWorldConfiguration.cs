using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Executed BYTE1(2036B4C) authority, retained between world and field scenes.</summary>
public sealed record NativeWorldConfiguration([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] byte EntityByte, [property: JsonRequired] ushort SourceField, long? SourceWorldFrame = null, WorldTransition? FieldPacket = null)
{
    public const string PacketRule = "ff8-en-world-config-471f70-byte1-v1";
    public const string ReturnRule = "ff8-en-world-config-53fac0-byte1-v1";

    public const string OriginalSaveRule = "ff8-original-save-world-configuration-v1";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalSaveSha256 { get; init; }

    public static NativeWorldConfiguration FromOriginalSave(byte entity, int fieldId, string sourceHash)
    {
        return new(1, OriginalSaveRule, entity, checked((ushort)fieldId)) { OriginalSaveSha256 = sourceHash };
    }

    public static NativeWorldConfiguration FromFieldPacket(WorldTransition transition, int fieldId)
    {
        return new NativeWorldConfiguration(1, PacketRule, unchecked((byte)transition.Mode), checked((ushort)fieldId), FieldPacket: transition);
    }

    public static NativeWorldConfiguration FromWorldReturn(WorldState world, int fieldId)
    {
        byte entity;
        long sourceFrame = world.Frame;
        if (world.NativeScriptedRuntime is NativeScriptedWorldRuntimeState boat)
        {
            boat.Validate();
            entity = boat.Path.EntityType;
            sourceFrame = boat.Return?.SourceWorldFrame ?? throw new NotSupportedException("Scripted world configuration requires its executed endpoint packet write.");
        }
        else if (world.NativeCarRuntime is NativeCarWorldRuntimeState car)
        {
            car.Validate();
            entity = car.EntityType;
        }
        else if (world.NativeFootRuntime is NativeFootWorldRuntimeState foot)
        {
            foot.RequireSupportedNormalFoot();
            entity = foot.EntityType;
        }
        else if (world.NativeRagnarokRuntime is { } ragnarok)
        {
            ragnarok.RequireSceneState();
            entity = 50;
        }
        else if (world.NativeGardenRuntime is NativeGardenWorldRuntimeState garden)
        {
            garden.Validate();
            if (garden.Transfer is not null)
            {
                throw new NotSupportedException("Garden world return requires ordinary controlled48 outside an unfinished transfer.");
            }
            entity = 48;
        }
        else
        {
            throw new NotSupportedException("Native world configuration requires its executed current entity runtime.");
        }
        return new NativeWorldConfiguration(1, ReturnRule, entity, checked((ushort)fieldId), sourceFrame);
    }

    public void Validate()
    {
        if (Version != 1 || Rule == PacketRule && (FieldPacket is null || SourceWorldFrame is not null || EntityByte != unchecked((byte)FieldPacket.Mode)) || Rule == ReturnRule && (FieldPacket is not null || SourceWorldFrame is null or < 0 || EntityByte is not (0 or 1 or 2 or 32 or 33 or 35 or 48 or 50 or >= 16 and <= 22 or 65 or 66)) || Rule == OriginalSaveRule && (SourceWorldFrame is not null || FieldPacket is not null || OriginalSaveSha256 is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit)) || Rule != OriginalSaveRule && OriginalSaveSha256 is not null || Rule != PacketRule && Rule != ReturnRule && Rule != OriginalSaveRule)
        {
            throw new InvalidDataException("Native world configuration lacks its original executed packet/entity authority.");
        }
    }
}
