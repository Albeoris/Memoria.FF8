using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDescriptorActionCompletionTests
{
    [Theory]
    [InlineData("known")]
    [InlineData("unknown")]
    [InlineData("invalid-pointer")]
    [InlineData("invalid-program")]
    public void Pending_texture_context_observes_only_known_bounded_fields_without_mutation(string fault)
    {
        NativeBattleAuxiliaryState auxiliary = NativeBattleAuxiliaryState.Clear().AppendTextureAnimation(OriginalOwner(0), 7, new([255], 0), OriginalAliases);
        NativeBattleAuxiliaryTask task = auxiliary.Slots[auxiliary.Head!.Value];
        if (fault == "unknown")
        {
            task = task with { UnknownByteMask = task.UnknownByteMask | 0x20F };
        }
        if (fault == "invalid-pointer")
        {
            task = task with { Payload = task.Payload.SetItem(0, 0).SetItem(1, 0).SetItem(2, 0).SetItem(3, 0) };
        }
        if (fault == "invalid-program")
        {
            task = task with { Program = task.Program! with { Offset = 4 } };
        }
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, OriginalOwner(0), new(2) { ResourceFlags44 = 0, Sequence = 7 })]);
        string original = NativeBattleTestSnapshots.Json(new { task, graphics });
        object? context = typeof(BattleSession).GetMethod("ObserveDiagnosticTextureAnimation", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [task, graphics]);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(new { task, graphics }));
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(context));
        JsonElement observed = document.RootElement;
        byte[] observedPayload = observed.GetProperty("Payload").GetBytesFromBase64();
        Assert.Equal(32, observedPayload.Length);
        Assert.Equal(task.Payload.ToArray(), observedPayload);
        Assert.False(observed.GetProperty("PayloadTruncated").GetBoolean());
        Assert.Equal(task.UnknownByteMask, observed.GetProperty("UnknownByteMask").GetUInt32());
        Assert.Equal(fault != "unknown", observed.GetProperty("PointerAndSequenceKnown").GetBoolean());
        JsonElement observedModel = observed.GetProperty("Model");
        if (fault is "unknown" or "invalid-pointer")
        {
            Assert.Equal(JsonValueKind.Null, observedModel.GetProperty("Index").ValueKind);
        }
        else
        {
            Assert.Equal(0, observedModel.GetProperty("Index").GetInt32());
        }
        Assert.Equal(2, observedModel.GetProperty("Flags0").GetInt32());
        Assert.Equal(0, observedModel.GetProperty("ResourceFlags44").GetInt32());
        Assert.Equal(7, observedModel.GetProperty("Sequence").GetInt32());
        Assert.Equal(fault == "invalid-program" ? 4 : 0, observed.GetProperty("Program").GetProperty("Offset").GetInt32());
        Assert.Equal(1, observed.GetProperty("Program").GetProperty("SourceLength").GetInt32());
        if (fault == "invalid-program")
        {
            Assert.Equal(JsonValueKind.Null, observed.GetProperty("Program").GetProperty("ActualByteAtOffset").ValueKind);
        }
        else
        {
            Assert.Equal(255, observed.GetProperty("Program").GetProperty("ActualByteAtOffset").GetInt32());
        }
        if (fault == "unknown")
        {
            Assert.Equal(JsonValueKind.Null, observed.GetProperty("ModelPointer").ValueKind);
            Assert.Equal(JsonValueKind.Null, observed.GetProperty("CapturedSequence").ValueKind);
        }
        else
        {
            Assert.Equal(7, observed.GetProperty("CapturedSequence").GetInt32());
        }
    }
}
