using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFieldBarAliasTests
{
    [Fact]
    public void AdjacentWriteDoesNotCreateOrModifyEitherActualBar()
    {
        NativeFieldBarsState bars = Bars();
        NativeFieldBarsState written = bars.ClearAdjacentMirrorWord();

        Assert.Null(bars.AdjacentMirrorWord);
        Assert.Equal((ushort)0, written.AdjacentMirrorWord);
        Assert.Same(bars.First, written.First);
        Assert.Same(bars.Second, written.Second);
        Assert.Equal(2, written.VisibleBars.Count());
        Assert.Throws<NotSupportedException>(() => written.Hide(2));
        Assert.Equal(written, written.ClearAdjacentMirrorWord());
    }

    [Fact]
    public void PointerTransformsComposeWithoutInventingAnAddressOrResettingOnQueueOperations()
    {
        NativeVibrationProcessState cold = NativeVibrationProcessState.FromColdProcess();
        Assert.Null(cold.ResourcePointerAndMask);
        NativeVibrationProcessState transformed = cold.AndResourcePointer(0xFFFF0FFF).AndResourcePointer(0xFFFFFF7F);

        Assert.Equal((uint)0xFFFF0F7F, transformed.ResourcePointerAndMask);
        Assert.Equal(transformed.ResourcePointerAndMask, transformed.StopAll().ClearControllerRequests().Advance(4).ResourcePointerAndMask);
        Assert.Equal(transformed.ResourcePointerAndMask, transformed.AndResourcePointer(0xFFFFFF7F).ResourcePointerAndMask);
        Assert.Equal(cold.Slots, transformed.Slots);
        Assert.Equal(cold.Ports, transformed.Ports);
    }

    [Fact]
    public void CopiedActiveChannelsAdvanceAfterTheUnrelatedBankPointerTransform()
    {
        NativeVibrationProcessState queued = NativeVibrationProcessState.FromColdProcess().Enqueue(Bank(), 0, 10).State;
        NativeVibrationProcessState changed = queued.AndResourcePointer(0xFFFFFF7F);
        NativeVibrationProcessState advanced = changed.Advance(4);

        Assert.NotEqual(changed.Slots[0].A.Counter, advanced.Slots[0].A.Counter);
        Assert.Equal(changed.Slots[0].A.Bytes, advanced.Slots[0].A.Bytes);
        Assert.Equal(changed.ResourcePointerAndMask, advanced.ResourcePointerAndMask);
        Assert.Equal(queued.Slots[0].Priority, advanced.Slots[0].Priority);
        Assert.Throws<NotSupportedException>(() => changed.Enqueue(Bank(), 0, 10));
        Assert.Equal((byte)0, changed.StopAll().Slots[0].Priority);
    }

    [Fact]
    public void NativeFullQueueReturnDoesNotDereferenceTheTransformedBankPointer()
    {
        NativeVibrationProcessState cold = NativeVibrationProcessState.FromColdProcess();
        NativeVibrationProcessState busy = cold with { Slots = cold.Slots.Select(slot => slot with { Priority = 127 }).ToImmutableArray() };
        busy = busy.AndResourcePointer(0xFFFFFF7F);
        NativeVibrationBank unread = new(string.Empty, default, default);

        NativeVibrationEnqueue result = busy.Enqueue(unread, 0, 1);

        Assert.Equal(0, result.Handle);
        Assert.Same(busy, result.State);
        Assert.Throws<NotSupportedException>(() => busy.StopAll().Enqueue(unread, 0, 1));
    }

    [Fact]
    public void AnExplicitIdentityTransformStillAllowsTheAuthenticatedBank()
    {
        NativeVibrationProcessState identity = NativeVibrationProcessState.FromColdProcess().AndResourcePointer(uint.MaxValue);
        Assert.NotEqual(0, identity.Enqueue(Bank(), 0, 1).Handle);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompositePublicationValidatesBothOwnersBeforeChangingEither(bool invalidBars)
    {
        GameSession session = GameSession.CreateColdProcess();
        NativeFieldBarsState bars = Bars();
        NativeVibrationProcessState vibration = session.VibrationProcess!;
        session.SetNativeFieldBarAliasOwners(bars, vibration);
        GameState before = session.State;

        Assert.Throws<InvalidDataException>(() => session.SetNativeFieldBarAliasOwners(invalidBars ? bars with { Version = 2 } : bars.ClearAdjacentMirrorWord(), invalidBars ? vibration.AndResourcePointer(0xFFFFFF7F) : vibration with { Rule = "invalid" }));
        Assert.Same(before, session.State);
        session.SetNativeFieldBarAliasOwners(bars.ClearAdjacentMirrorWord(), vibration.AndResourcePointer(0xFFFFFF7F));
        Assert.Equal((ushort)0, session.State.FieldBars!.AdjacentMirrorWord);
        Assert.Equal((uint)0xFFFFFF7F, session.State.VibrationProcess!.ResourcePointerAndMask);
        Assert.Same(bars.First, session.State.FieldBars.First);
        Assert.Same(bars.Second, session.State.FieldBars.Second);
    }

    [Fact]
    public void CanonicalSaveLoadRetainsTheMirrorWordAndSymbolicTransform()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.SetNativeFieldBarAliasOwners(Bars().ClearAdjacentMirrorWord(), session.VibrationProcess!.AndResourcePointer(0xFFFFFF7F));
        string directory = Path.Combine(Path.GetTempPath(), "ff8-bar-alias-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "standalone.json");
            session.Save(path);
            GameState restored = GameSession.Load(path);
            Assert.Equal(session.State.FieldBars, restored.FieldBars);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.VibrationProcess), JsonSerializer.SerializeToElement(restored.VibrationProcess)));
            Assert.Equal((uint)0xFFFFFF7F, restored.VibrationProcess!.ResourcePointerAndMask);
            Assert.Throws<NotSupportedException>(() => restored.VibrationProcess.Enqueue(Bank(), 0, 1));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static NativeFieldBarsState Bars()
    {
        return NativeFieldBarsState.CreateHidden().Display(0, 10, 20, 0, 0, 100, 40).Display(1, 30, 40, 0, 0, 80, 20);
    }

    private static NativeVibrationBank Bank()
    {
        byte[] bytes = [4, 0, 0, 0, 4, 0, 0, 0, 20, 1, 40, 255, 60, 1];
        return new(Convert.ToHexString(SHA256.HashData(bytes)), ImmutableArray.Create(4), ImmutableArray.CreateRange(bytes));
    }
}
