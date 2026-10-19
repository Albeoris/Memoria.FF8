using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedCameraCommandsTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private static readonly NativeBattleWaterScriptProgram Program = new(0x1000, 4, 0x1000);
    private static readonly NativeBattleWaterSlotBank Bank = new(0x1FA8, 0x858, 8, 0x2000);

    [Theory]
    [InlineData(0x0039)]
    [InlineData(0x9039)]
    [InlineData(0xF039)]
    public void Sequence_gate_skips_every_unconsumed_operand_bone_and_camera_owner(ushort instruction)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0);
        frame = frame with { Core = frame.Core.Write(0x2797628 + 9, [0x20]).MarkUnknown(0x27973E8, 4), Arena = frame.Arena.MarkUnknown(0x1002, 2).MarkUnknown(0x208C, 14) };
        NativeBattleScriptedCameraServices services = new((_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedCameraCommands.Execute(frame, services);
        Assert.Equal(0x1004u, Pc(result));
        Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
        Assert.Equal(frame.Arena, result.Arena);
    }

    [Theory]
    [InlineData(0x0039)]
    [InlineData(0x9039)]
    [InlineData(0xA039)]
    [InlineData(0xB039)]
    [InlineData(0xC039)]
    [InlineData(0xD039)]
    [InlineData(0xE039)]
    [InlineData(0xF039)]
    public void Default_modes_use_existing_signed_alias_lookup_and_publish_eye_target_scalars_without_view_recalculation(ushort instruction)
    {
        for (int selector = 0xFFF0; selector <= 0xFFFF; selector++)
        {
            NativeBattleWaterExecutionFrame frame = Fixture(instruction, unchecked((short)selector));
            NativeBattleCameraState camera = Camera() with { CurrentPose = null };
            NativeBattleCameraProjection? projection = camera.Projection;
            int lookups = 0;
            NativeBattleScriptedCameraServices services = Services(() => camera, value => camera = value) with
            {
                ResolveBoneB65370 = (current, signed) =>
                {
                    lookups++;
                    Assert.Equal(unchecked((short)selector), signed);
                    int index = NativeBattleWaterSlots.LookupB65370(current.Arena, Bank, 0, current.Core.Read(0x2797454, 128).ToImmutableArray(), signed);
                    return Base + (uint)(0x2000 + index * 0x100);
                }
            };
            NativeBattleWaterExecutionFrame result = NativeBattleScriptedCameraCommands.Execute(frame, services, Program);
            int target = new[] { 1, 2, 3, 4 }[(~selector) & 3];
            Assert.Equal(1, lookups);
            Assert.Equal(new NativeBattleCameraPoint(-100, 200, -300), camera.CurrentPose!.Eye);
            Assert.Equal(new NativeBattleCameraPoint((short)(target * 10 + 1), (short)(target * 10 + 2), (short)(target * 10 + 3)), camera.CurrentPose.Target);
            Assert.Equal(unchecked((short)0xF123), camera.FocalLength);
            Assert.Equal(unchecked((short)0x8123), camera.Roll);
            Assert.Equal(unchecked((short)0xBEEF), camera.EyePaddingF6);
            Assert.Null(camera.TargetPaddingFE);
            Assert.Same(projection, camera.Projection);
            Assert.Equal(0x1004u, Pc(result));
            Assert.Equal(frame.Arena, result.Arena);
            Assert.Equal((ushort)0x1234, result.ReadTemporaryWord(0x13E));
        }
    }

    [Fact]
    public void Default_camera_reads_current_PC_after_canonical_publication_and_preserves_padding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x0039, 999);
        NativeBattleCameraState camera = Camera();
        NativeBattleScriptedCameraServices services = Services(() => camera, value => camera = value) with
        {
            ResolveBoneB65370 = (current, selector) =>
            {
                int index = NativeBattleWaterSlots.LookupB65370(current.Arena, Bank, 0, current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
                Assert.Equal(0, index);
                return Base + 0x2000;
            },
            PublishCamera = (current, value) =>
            {
                camera = value;
                return WaterSchedulerFixture.CoreDword(current, 0x2797450, 0xFFFFFFFC);
            }
        };
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedCameraCommands.Execute(frame, services, Program);
        Assert.Equal(camera.CurrentPose!.Eye, camera.CurrentPose.Target);
        Assert.Equal(0u, Pc(result));
    }

    [Fact]
    public void Capture_uses_actual_scalar_scratch_values_instead_of_stale_pose_scalars_and_copies_padding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x4039, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2).MarkUnknown(0x208C, 14), Core = frame.Core.MarkUnknown(0x27973E8, 4) };
        NativeBattleCameraState camera = Camera() with { FocalLength = -1234, Roll = -2345, PoseCaptureCount = 4 };
        NativeBattleCameraPose original = camera.CurrentPose!;
        NativeBattleScriptedCameraCommands.Execute(frame, Services(() => camera, value => camera = value));
        Assert.Same(original, camera.CurrentPose);
        Assert.Equal(original.Eye, camera.CapturedPose!.Eye);
        Assert.Equal(original.Target, camera.CapturedPose.Target);
        Assert.Equal((short)-1234, camera.CapturedPose.FocalLength);
        Assert.Equal((short)-2345, camera.CapturedPose.Roll);
        Assert.Equal(unchecked((short)0xBEEF), camera.CapturedEyePadding806);
        Assert.Null(camera.CapturedTargetPadding80E);
        Assert.Equal(5ul, camera.PoseCaptureCount);
    }

    [Fact]
    public void Capture_rejects_unknown_current_pose_or_scalar_focal_without_reconstructing_them()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x4039, 0);
        NativeBattleCameraState camera = Camera() with { FocalLength = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedCameraCommands.Execute(frame, Services(() => camera, value => camera = value)));
        camera = Camera() with { CurrentPose = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleScriptedCameraCommands.Execute(frame, Services(() => camera, value => camera = value)));
    }

    [Theory]
    [InlineData(0x5039, 0xA5A5F055u, 0xA5A5F155u)]
    [InlineData(0x6039, 0xFFFFFFFFu, 0xFFFFFFFFu)]
    [InlineData(0x7039, 0xFFFFFFFFu, 0xFFFFFEFFu)]
    public void Modes_five_six_seven_update_only_canonical_graphics_FLAG100_without_consuming_camera_or_operands(ushort instruction, uint initial, uint expected)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(instruction, 0);
        frame = frame with { Arena = frame.Arena.MarkUnknown(0x1002, 2), Core = frame.Core.MarkUnknown(0x27973E8, 4) };
        uint flags = initial;
        NativeBattleScriptedCameraServices services = new((_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException())
        {
            ReadGraphicsFlags = _ => flags,
            PublishGraphicsFlags = (current, value) =>
            {
                flags = value;
                return current;
            }
        };
        NativeBattleWaterExecutionFrame result = NativeBattleScriptedCameraCommands.Execute(frame, services);
        Assert.Equal(expected, flags);
        Assert.Equal(0x1004u, Pc(result));
    }

    [Fact]
    public void Independent_JSON_camera_continuation_retains_current_capture_and_unknown_padding()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0x0039, -1);
        NativeBattleCameraState live = Camera();
        NativeBattleCameraState restored = JsonSerializer.Deserialize<NativeBattleCameraState>(JsonSerializer.Serialize(live))!;
        NativeBattleWaterExecutionFrame restoredFrame = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterExecutionFrame first = NativeBattleScriptedCameraCommands.Execute(frame, Services(() => live, value => live = value), Program);
        NativeBattleWaterExecutionFrame next = NativeBattleScriptedCameraCommands.Execute(restoredFrame, Services(() => restored, value => restored = value), Program);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(next));
        Assert.Equal(JsonSerializer.Serialize(live), JsonSerializer.Serialize(restored));
    }

    private static NativeBattleScriptedCameraServices Services(Func<NativeBattleCameraState> read, Action<NativeBattleCameraState> publish) => new((_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException())
    {
        ReadCamera = _ => read(),
        PublishCamera = (current, camera) =>
        {
            publish(camera);
            return current;
        },
        ResolveBoneB65370 = (current, selector) =>
        {
            int index = NativeBattleWaterSlots.LookupB65370(current.Arena, Bank, 0, current.Core.Read(0x2797454, 128).ToImmutableArray(), selector);
            return Base + (uint)(0x2000 + index * 0x100);
        }
    };

    private static NativeBattleCameraState Camera() => NativeBattleCameraState.Initialize() with { CurrentPose = new(new(1, 2, 3), new(4, 5, 6), 11, 22), FocalLength = 333, Roll = 444, EyePaddingF6 = unchecked((short)0xBEEF), TargetPaddingFE = null, Projection = new(null, 216, 160, 108) };

    private static NativeBattleWaterExecutionFrame Fixture(ushort instruction, short selector)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryWord(0x14A, instruction).WriteTemporaryWord(0x13E, 0x1234);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797450, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973C0, 0x2797628);
        frame = frame with { Core = frame.Core.Write(0x2797454, [0, 1, 2, 3, 4, 0xFF]), Arena = frame.Arena.Write(0x1002, BitConverter.GetBytes(selector)) };
        for (int index = 0; index < 5; index++)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x12, (ushort)((index + 1) * 10));
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x94, (ushort)(index * 10 + 1));
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x96, (ushort)(index * 10 + 2));
            frame = WaterSchedulerFixture.SlotWord(frame, index, 0x98, (ushort)(index * 10 + 3));
        }
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x14, 20);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x16, 30);
        frame = WaterSchedulerFixture.SlotWord(frame, 2, 0x14, 40);
        frame = WaterSchedulerFixture.SlotWord(frame, 2, 0x16, 50);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)-100));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 200);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, unchecked((ushort)-300));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 0xF123);
        return WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 0x8123);
    }

    private static uint Pc(NativeBattleWaterExecutionFrame frame) => WaterSchedulerFixture.ReadCoreDword(frame, 0x2797450);
}
