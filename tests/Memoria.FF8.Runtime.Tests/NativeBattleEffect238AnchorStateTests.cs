using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect238AnchorStateTests
{
    [Fact]
    public void AnchorPublishesLoadedControlsPackedInputFullMacSaturatedIrAndFlags()
    {
        NativeBattleGeometryRegisterStep incoming = NativeBattleActorAnchorRegisterFixture.Registers();
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0xC6, 0xA5);
        frame.TemporaryFrame.MarkUnknown(0xC7, 1);
        NativeBattleActionMatrix basis = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 40000, -50000, 60000, 731);
        NativeBattleWaterActorAnchorResult result = NativeBattleWaterActorAnchor.EvaluateB66B80(frame, Skeleton(), [Identity, Identity with { X = 10, Y = 20, Z = 30 }], 1, 4096, basis, incoming);
        Assert.Equal(new NativeBattleFullMacVertex(39980, -49990, 60130, 0x81C00000), result.Anchor);
        Assert.Equal(new int?[] { 39980, -49990, 60030 }, result.Registers.Geometry.Translation);
        Assert.Equal(new int?[] { 39980, -49990, 60130 }, result.Registers.Colors.Mac);
        Assert.Equal(new int?[] { short.MaxValue, short.MinValue, short.MaxValue }, result.Registers.Colors.Ir);
        Assert.Equal(0x81C00000u, result.Registers.Colors.Flags);
        Assert.Equal(result.Anchor.Flags, result.Frame.ReadTemporaryDword(0xFC));
        for (int index = 0; index < 18; index++)
        {
            Assert.Equal((byte?)NativeBattleStorageSnapshots.Capture(result.Frame.TemporaryFrame).Bytes[0xD0 + index], result.Registers.Geometry.Rotation20[index]);
        }
        Assert.Null(result.Registers.Geometry.Rotation20[18]);
        Assert.Null(result.Registers.Geometry.Rotation20[19]);
        Assert.False(result.Frame.TemporaryFrame.IsKnown(0xE2, 1));
        Assert.False(result.Frame.TemporaryFrame.IsKnown(0xE3, 1));
        Assert.Equal(new byte[] { 0, 0, 0, 0, 100, 0, 0xA5, NativeBattleStorageSnapshots.Capture(frame.TemporaryFrame).Bytes[0xC7] }, result.Registers.Colors.NormalInput!.Value);
        Assert.Equal(new bool[] { true, true, true, true, true, true, true, false }, result.Registers.Colors.NormalInputKnown!.Value);
        Assert.Equal(incoming.Geometry.V1, result.Registers.Geometry.V1);
        Assert.Equal(incoming.Geometry.V2, result.Registers.Geometry.V2);
        Assert.Equal(incoming.Geometry.Sxy, result.Registers.Geometry.Sxy);
        Assert.Equal(incoming.Geometry.Sz, result.Registers.Geometry.Sz);
        Assert.Equal(incoming.Geometry.Otz, result.Registers.Geometry.Otz);
        Assert.Equal(incoming.Geometry.Mac0, result.Registers.Geometry.Mac0);
        Assert.Equal(incoming.Geometry.AnchorScratch8, result.Registers.Geometry.AnchorScratch8);
        Assert.Equal(incoming.Geometry.Lzcs, result.Registers.Geometry.Lzcs);
        Assert.Equal(incoming.Geometry.Lzcr, result.Registers.Geometry.Lzcr);
        Assert.Equal(incoming.Colors.Ir0, result.Registers.Colors.Ir0);
        Assert.Equal(incoming.Colors.Material, result.Registers.Colors.Material);
        Assert.Equal(incoming.Colors.Rgb, result.Registers.Colors.Rgb);
    }

    [Fact]
    public void ContactCentroidPublishesEachAnchorBeforeTheNextPoseAndFinalUpdate()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, 0x706E);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        uint owner = WaterSchedulerFixture.ReadCoreDword(frame, 0x279744C);
        frame = frame with { Core = frame.Core.Write(owner + 0x41, [2]).Write(owner + 0x48, [0, 1]), Arena = frame.Arena.Write(0x1002, [0, 0]) };
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x60, 0x1D972C0);
        frame = WaterSchedulerFixture.CoreDword(frame, owner + 0x64, 0x1D9735C);
        NativeBattleGeometryRegisterStep registers = NativeBattleActorAnchorRegisterFixture.Registers();
        int reads = 0;
        int publications = 0;
        int updates = 0;
        NativeBattleWaterActorAnchorServices anchors = new(_ =>
        {
            reads++;
            return registers;
        }, (current, value) =>
        {
            publications++;
            registers = value;
            Assert.Equal(publications * 10, registers.Colors.Mac[0]);
            Assert.Equal(unchecked((uint)(publications * 10)), current.ReadTemporaryDword(0xF0));
            return current;
        });
        NativeBattleCinematicActorAnchorServices services = new((current, actor, restore, linked) =>
        {
            Assert.Equal(actor == 0x1D972C0 ? 0 : 1, publications);
            if (publications == 1)
            {
                Assert.Equal(10, registers.Geometry.Translation[0]);
                Assert.Equal(10, registers.Colors.Mac[0]);
            }
            byte[] skeleton = new byte[64];
            skeleton[0] = 1;
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
            return new(current, skeleton.ToImmutableArray(), [Identity], Identity with { X = actor == 0x1D972C0 ? 10 : 20 });
        }, (current, _) =>
        {
            updates++;
            Assert.Equal(2, publications);
            Assert.Equal(20, registers.Geometry.Translation[0]);
            Assert.Equal(20, registers.Colors.Mac[0]);
            Assert.Equal(0u, registers.Colors.Flags);
            return current;
        }, (current, _) => current, anchors);
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorAnchorCommands.Execute(frame, new(0x1000, 4, 0x1000), services);
        Assert.Equal(2, reads);
        Assert.Equal(2, publications);
        Assert.Equal(1, updates);
        Assert.Equal(15 << 16, BinaryPrimitives.ReadInt32LittleEndian(result.Arena.Read(0x205C, 4)));
    }

    [Fact]
    public void AnchorStateAndUnknownPaddingContinueAfterIndependentJsonRestore()
    {
        NativeBattleWaterActorAnchorResult first = NativeBattleWaterActorAnchor.EvaluateB66B80(WaterSchedulerFixture.Create(), Skeleton(), [Identity, Identity with { X = 10 }], 1, 4096, Identity, NativeBattleActorAnchorRegisterFixture.Registers());
        NativeBattleWaterActorAnchorResult restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterActorAnchorResult>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)))!);
        NativeBattleWaterActorAnchorResult expected = NativeBattleWaterActorAnchor.EvaluateB66B80(first.Frame, Skeleton(), [Identity, Identity with { X = -20 }], 0xF1, -2048, Identity, first.Registers);
        NativeBattleWaterActorAnchorResult actual = NativeBattleWaterActorAnchor.EvaluateB66B80(restored.Frame, Skeleton(), [Identity, Identity with { X = -20 }], 0xF1, -2048, Identity, restored.Registers);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(expected)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(actual)));
        Assert.Null(actual.Registers.Geometry.Rotation20[18]);
        Assert.False(actual.Frame.TemporaryFrame.IsKnown(0xE2, 1));
    }

    [Fact]
    public void MissingCurrentRegistersCannotUseAPureTransformFallback()
    {
        Assert.Throws<ArgumentNullException>(() => NativeBattleWaterActorAnchor.EvaluateB66B80(WaterSchedulerFixture.Create(), Skeleton(), [Identity, Identity], 1, 4096, Identity, null!));
    }

    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static ImmutableArray<byte> Skeleton()
    {
        byte[] bytes = new byte[112];
        bytes[0] = 2;
        bytes[5] = 0x81;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(18), 100);
        return bytes.ToImmutableArray();
    }
}

internal static class NativeBattleActorAnchorRegisterFixture
{
    public static NativeBattleGeometryRegisterStep Registers()
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown() with { V1 = [1, 2, 3, 4, 5, 6, 7, 8], V2 = [8, 7, 6, 5, 4, 3, 2, 1], Sxy = [11, 22, 33], Sz = [44, 55, 66, 77], Otz = 88, Mac0 = 99, AnchorScratch8 = [9, 8, 7, 6, 5, 4, 3, 2], Lzcs = 123, Lzcr = 25 };
        NativeBattleScriptedColorRegisters colors = new([101, 102, 103], [201, 202, 203], [301, 302, 303], 0xFFFFFFFF) { Ir0 = 777, Material = 0xAABBCCDD };
        return new(geometry, colors);
    }

    public static NativeBattleWaterActorAnchorServices Services()
    {
        NativeBattleGeometryRegisterStep registers = Registers();
        return new(_ => registers, (frame, current) =>
        {
            registers = current;
            return frame;
        });
    }
}
