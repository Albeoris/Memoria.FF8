using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionGeometryMaskTests
{
    public static IEnumerable<object[]> PhysicalSelections()
    {
        for (int actorIndex = 0; actorIndex < 7; actorIndex++)
        {
            for (ushort mask = 1; mask < 128; mask++)
            {
                yield return [actorIndex, mask];
            }
        }
    }

    [Theory]
    [MemberData(nameof(PhysicalSelections))]
    public void Every_physical_mask_prepares_native_scalars_rotations_flags_and_all_matrices(int actorIndex, ushort mask)
    {
        NativeBattleActionGeometryGlobals[] retained = [NativeBattleActionGeometryGlobals.FromColdProcess(), new(-44000, -12345), new(70000, 12345)];
        foreach (NativeBattleActionGeometryGlobals globals in retained)
        {
            NativeBattleGraphicsState original = CreateGraphics();
            NativeBattleGraphicsState actual = NativeBattleActionGeometryRules.PrepareGeometry50D300(original, actorIndex, mask, mask, globals, ReadBone);
            NativeBattleActionGeometry context = Assert.IsType<NativeBattleActionGeometry>(actual.ActionContext);
            int recipientIndex = BitOperations.Log2(mask);
            bool group = BitOperations.PopCount(mask) >= 2;
            Assert.Equal(actorIndex, context.ActorIndex);
            Assert.Equal(mask, context.SourceTargetMask);
            Assert.Equal(mask, context.PhysicalTargetMask);
            Assert.Equal((byte)recipientIndex, context.TargetIndex);
            Assert.Equal(5, context.Matrices!.Value.Length);
            Assert.NotNull(context.TargetMatrix);
            context.Validate();
            NativeBattleGraphicsModel actor = original.Models[actorIndex];
            NativeBattleModelTransform first = actor.Transform!;
            NativeBattleGraphicsModel recipient = original.Models[recipientIndex];
            NativeBattleModelTransform second = recipient.Transform!;
            NativeBattleVector actorPosition = new(first.Position28.X, first.Angle36, first.Position28.Z);
            NativeBattleVector targetPosition = group
                ? NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(index => (mask & (1 << index)) != 0).Select(index => original.Models[index].Transform!.Position28).ToArray())
                : recipient.ModelId4 is 96 or 97 ? ReadBone(actual, recipientIndex, 241, 2048) : second.Position28;
            if (!group)
            {
                targetPosition = targetPosition with { Y = second.Angle36 };
            }
            short expectedScale = NativeBattleActionGeometryRules.DistanceScale(actorPosition, targetPosition);
            bool self = mask == 1 << actorIndex;
            Assert.Equal(expectedScale, context.DistanceScale);
            Assert.Equal(self ? 1000 : 5000 * expectedScale >> 12, context.UnadjustedDistance);
            Assert.Equal(group ? globals.ContactDistance : self ? 1000 : (5000 * expectedScale >> 12) - (2000 * (first.Scale38 + second.Scale38) >> 12), context.ContactDistance);
            Assert.Equal(group || self ? globals.TargetYaw : unchecked((short)NativeWorldScriptedPathMath.NativeAngle(targetPosition.X - actorPosition.X, targetPosition.Z - actorPosition.Z)), context.TargetYaw);
            short actorYaw = group || self ? actorIndex >= 3 ? (short)2048 : (short)0 : unchecked((short)NativeWorldScriptedPathMath.NativeAngle(actorPosition.X - targetPosition.X, actorPosition.Z - targetPosition.Z));
            Assert.Equal(actorYaw, actual.Models[actorIndex].Rotation14);
            Assert.Equal(group || self ? (short)0 : actor.Rotation12, actual.Models[actorIndex].Rotation12);
            Assert.Equal(group || self ? (short)0 : actor.Rotation16, actual.Models[actorIndex].Rotation16);
            NativeBattleVector fallback = new(group || self ? (short)0 : actor.Rotation12!.Value, unchecked((short)(actorYaw + 2048)), group || self ? (short)0 : actor.Rotation16!.Value);
            NativeBattleVector actorFirst = ReadBone(actual, actorIndex, 240, 2048);
            NativeBattleVector actorSecond = ReadBone(actual, actorIndex, 241, 2048);
            NativeBattleVector targetFirst = group ? new(targetPosition.X, actorFirst.Y, targetPosition.Z) : ReadBone(actual, recipientIndex, 240, 2048);
            NativeBattleVector targetSecond = group ? new(targetPosition.X, actorSecond.Y, targetPosition.Z) : ReadBone(actual, recipientIndex, 241, 2048);
            NativeBattleActionMatrix[] expectedMatrices =
            [
                NativeBattleActionGeometryRules.CaptureMatrix(actorPosition, targetPosition, fallback),
                NativeBattleActionGeometryRules.CaptureMatrix(actorFirst, targetFirst, fallback),
                NativeBattleActionGeometryRules.CaptureMatrix(actorFirst, targetSecond, fallback),
                NativeBattleActionGeometryRules.CaptureMatrix(actorSecond, targetFirst, fallback),
                NativeBattleActionGeometryRules.CaptureMatrix(actorSecond, targetSecond, fallback)
            ];
            for (int index = 0; index < expectedMatrices.Length; index++)
            {
                AssertMatrix(expectedMatrices[index], context.Matrices.Value[index]);
            }
            AssertMatrix(new(NativeBattlePoseMatrices.RotationWords(new(0, (mask & 7) == 0 ? (short)2048 : (short)0, 0)), targetPosition.X, targetPosition.Y, targetPosition.Z, 4096), context.TargetMatrix!);
            for (int index = 0; index < 7; index++)
            {
                ushort flags = original.Models[index].Flags2!.Value;
                ushort expected = (mask & (1 << index)) == 0 ? flags : group ? (ushort)(flags | 0x10) : (ushort)(flags & ~0x10);
                Assert.Equal(expected, actual.Models[index].Flags2);
                if (index != actorIndex)
                {
                    Assert.Equal(original.Models[index].Rotation12, actual.Models[index].Rotation12);
                    Assert.Equal(original.Models[index].Rotation14, actual.Models[index].Rotation14);
                    Assert.Equal(original.Models[index].Rotation16, actual.Models[index].Rotation16);
                }
            }
            NativeBattleActionGeometryGlobals next = globals.Observe50D300(context);
            Assert.Equal(context.ContactDistance, next.ContactDistance);
            Assert.Equal(context.TargetYaw, next.TargetYaw);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Full_source_mask_equality_controls_self_distance_without_discarding_retained_yaw(int actorIndex)
    {
        ushort physical = checked((ushort)(1 << actorIndex));
        ushort source = (ushort)(physical | 0xA000);
        NativeBattleGraphicsState original = CreateGraphics();
        NativeBattleGraphicsState actual = NativeBattleActionGeometryRules.PrepareGeometry50D300(original, actorIndex, source, physical, new(-44000, -12345), ReadBone);
        NativeBattleActionGeometry context = actual.ActionContext!;
        NativeBattleModelTransform transform = original.Models[actorIndex].Transform!;
        NativeBattleVector position = new(transform.Position28.X, transform.Angle36, transform.Position28.Z);
        NativeBattleVector target = original.Models[actorIndex].ModelId4 is 96 or 97 ? ReadBone(actual, actorIndex, 241, 2048) with { Y = transform.Angle36 } : position;
        short scale = NativeBattleActionGeometryRules.DistanceScale(position, target);
        Assert.Equal(source, context.SourceTargetMask);
        Assert.Equal(5000 * scale >> 12, context.UnadjustedDistance);
        Assert.Equal((5000 * scale >> 12) - (2000 * (transform.Scale38 + transform.Scale38) >> 12), context.ContactDistance);
        Assert.Equal((short)-12345, context.TargetYaw);
        Assert.Equal((short)0, actual.Models[actorIndex].Rotation12);
        Assert.Equal(actorIndex >= 3 ? (short)2048 : (short)0, actual.Models[actorIndex].Rotation14);
        Assert.Equal((short)0, actual.Models[actorIndex].Rotation16);
        NativeBattleVector fallback = new(0, actorIndex >= 3 ? (short)4096 : (short)2048, 0);
        AssertMatrix(NativeBattleActionGeometryRules.CaptureMatrix(position, target, fallback), context.Matrices!.Value[0]);
    }

    [Fact]
    public void A_single_target_followed_by_group_and_self_preserves_the_actual_process_globals_across_JSON_restore()
    {
        NativeBattleGraphicsState single = NativeBattleActionGeometryRules.PrepareGeometry50D300(CreateGraphics(), 0, 8, 8, NativeBattleActionGeometryGlobals.FromColdProcess(), ReadBone);
        NativeBattleActionGeometryGlobals retained = NativeBattleActionGeometryGlobals.FromColdProcess().Observe50D300(single.ActionContext!);
        retained = JsonSerializer.Deserialize<NativeBattleActionGeometryGlobals>(JsonSerializer.Serialize(retained))!;
        NativeBattleGraphicsState group = NativeBattleActionGeometryRules.PrepareGeometry50D300(single, 1, 0x78, 0x78, retained, ReadBone);
        Assert.Equal(retained.ContactDistance, group.ActionContext!.ContactDistance);
        Assert.Equal(retained.TargetYaw, group.ActionContext.TargetYaw);
        NativeBattleGraphicsState self = NativeBattleActionGeometryRules.PrepareGeometry50D300(group, 1, 2, 2, retained.Observe50D300(group.ActionContext), ReadBone);
        Assert.Equal(1000, self.ActionContext!.ContactDistance);
        Assert.Equal(retained.TargetYaw, self.ActionContext.TargetYaw);
    }

    [Fact]
    public void Retained_memory_serializes_the_globals_without_resetting_them_at_battle_initialization()
    {
        NativeBattleEffectMemory memory = new(null, 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0)
        {
            ActionGeometryGlobals = new(-44000, -12345)
        };
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Equal(memory.ActionGeometryGlobals, restored.ActionGeometryGlobals);
        Assert.Equal(memory.ActionGeometryGlobals, restored.InitializeReturnedTask500870().ActionGeometryGlobals);
        Assert.Equal(new NativeBattleActionGeometryGlobals(0, 0), NativeBattleEffectMemory.FromColdProcess().ActionGeometryGlobals);
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory with { ActionGeometryGlobals = null }))!.ActionGeometryGlobals);
    }

    [Fact]
    public void Unknown_retained_globals_stay_unknown_until_the_native_branch_writes_them()
    {
        NativeBattleGraphicsState group = NativeBattleActionGeometryRules.PrepareGeometry50D300(CreateGraphics(), 0, 0x78, 0x78, NativeBattleActionGeometryGlobals.Unknown(), ReadBone);
        Assert.Null(group.ActionContext!.ContactDistance);
        Assert.Null(group.ActionContext.TargetYaw);
        NativeBattleGraphicsState self = NativeBattleActionGeometryRules.PrepareGeometry50D300(group, 0, 1, 1, NativeBattleActionGeometryGlobals.Unknown(), ReadBone);
        Assert.Equal(1000, self.ActionContext!.ContactDistance);
        Assert.Null(self.ActionContext.TargetYaw);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(32768)]
    [InlineData(65535)]
    public void Empty_or_nonphysical_resolved_masks_fail_before_reading_bones(int mask)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleActionGeometryRules.PrepareGeometry50D300(CreateGraphics(), 0, (ushort)mask, (ushort)mask, NativeBattleActionGeometryGlobals.FromColdProcess(), (_, _, _, _) => throw new InvalidOperationException("Unexpected bone read.")));
    }

    [Theory]
    [InlineData(16, 40000, -25536)]
    [InlineData(16, -44000, 21536)]
    [InlineData(17, 40000, -25536)]
    [InlineData(17, -44000, 21536)]
    [InlineData(17, 70000, 4464)]
    public void Expression_distance_variables_read_signed_WORDs_from_their_DWORD_owners(byte variable, int value, int expected)
    {
        BattleSession session = (BattleSession)RuntimeHelpers.GetUninitializedObject(typeof(BattleSession));
        MethodInfo read = typeof(BattleSession).GetMethod("ReadNativeGraphicsActionVariable", BindingFlags.NonPublic | BindingFlags.Instance)!;
        NativeBattleGraphicsState graphics = CreateGraphics() with { ActionContext = new(0, 8) { UnadjustedDistance = value, ContactDistance = value } };
        Assert.Equal(expected, Assert.IsType<int>(read.Invoke(session, [graphics, variable])));
    }

    private static void AssertMatrix(NativeBattleActionMatrix expected, NativeBattleActionMatrix actual)
    {
        Assert.Equal(expected.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.Z, actual.Z);
        Assert.Equal(expected.Scale, actual.Scale);
    }

    private static NativeBattleGraphicsState CreateGraphics()
    {
        NativeBattleGraphicsModel[] models = new NativeBattleGraphicsModel[7];
        for (int index = 0; index < models.Length; index++)
        {
            NativeBattleVector position = new(checked((short)(-9000 + index * 2700)), checked((short)(100 + index * 20)), checked((short)(7000 - index * 1800)));
            models[index] = new(index, 3)
            {
                ModelId4 = index == 5 ? (byte)96 : index == 6 ? (byte)97 : (byte)index,
                Flags2 = (ushort)(0xA040 | (index % 2 == 0 ? 0x10 : 0)),
                Rotation12 = (short)(101 + index),
                Rotation14 = (short)(301 + index),
                Rotation16 = (short)(501 + index),
                Transform = new(position, position, 0, checked((short)(index * 120 - 240)), checked((short)(2048 + index * 256)))
            };
        }
        return new(models.ToImmutableArray(), Enumerable.Range(0, 7).Select(index => (string?)$"actor{index}").ToImmutableArray(), 0, 0);
    }

    private static NativeBattleVector ReadBone(NativeBattleGraphicsState graphics, int index, byte bone, short fraction)
    {
        Assert.Equal((short)2048, fraction);
        Assert.True(bone is 240 or 241);
        NativeBattleVector position = graphics.Models[index].Transform!.Position28;
        return new(checked((short)(position.X + (bone == 240 ? 100 : 300))), checked((short)(position.Y + (bone == 240 ? 500 : 900))), checked((short)(position.Z - (bone == 240 ? 50 : 150))));
    }
}
