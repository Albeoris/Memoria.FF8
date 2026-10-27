using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicActorAnimation2CTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint AnimationAddress = 0x1942824;

    [Theory]
    [InlineData(0xFF123456u, 0x00808080u, 0x00123456u)]
    [InlineData(0x00FFFFFFu, 0x02FFFFFFu, 0x02FFFFFFu)]
    [InlineData(0x00404040u, 0u, 0u)]
    public void Color_adds_signed_bias_clamps_each_channel_and_keeps_only_control_bit25(uint original, uint control, uint expected)
    {
        Assert.Equal(expected, NativeBattleCinematicActorAnimation2C.BlendColor(original, control));
    }

    [Theory]
    [InlineData((byte)26)]
    [InlineData((byte)27)]
    [InlineData((byte)28)]
    public void Clone_copies_variable_skeleton_records_and_external_resource_pointers_without_mutating_actor_or_filling_opaque_input(byte animation)
    {
        byte[] skeleton = Skeleton();
        byte[] original = skeleton.ToArray();
        int captures = 0;
        NativeBattleCinematicActorAnimation2CServices services = Services(skeleton, () => captures++, (frame, clone) =>
        {
            Assert.Equal(Base + 0x4000, clone);
            Assert.Equal(Base + 0x4030, Dword(frame, 0x4014));
            Assert.Equal(Base + 0x4080, Dword(frame, 0x4030));
            Assert.Equal(0x193F000u, Dword(frame, 0x4034));
            Assert.Equal(AnimationAddress, Dword(frame, 0x4038));
            Assert.Equal(0x193F800u, Dword(frame, 0x403C));
            Assert.Equal(animation, frame.Arena.Read(0x4009, 1)[0]);
            Assert.Equal(animation, frame.Arena.Read(0x4020, 1)[0]);
            Assert.Equal((ushort)1, Word(frame, 0x4000));
            Assert.Equal(0x00102030u, Dword(frame, 0x405C));
            Assert.Equal(Base + 0x40F0, Dword(frame, 0x4044));
            Assert.Throws<NotSupportedException>(() => frame.Arena.Read(0x4010, 4));
            Assert.Throws<NotSupportedException>(() => frame.Arena.Read(0x4018, 4));
            Assert.Throws<NotSupportedException>(() => frame.Arena.Read(0x4008, 1));
            Assert.Equal((byte)2, frame.Arena.Read(0x4080, 1)[0]);
            Assert.Equal((byte)0x5A, frame.Arena.Read(0x4080 + 6, 1)[0]);
            return new(frame, 0x12345698);
        });
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicActorAnimation2C.Draw(Fixture(animation), services);
        Assert.Equal(1, captures);
        Assert.Equal(original, skeleton);
        Assert.Equal(Base + 0x40F0, WaterSchedulerFixture.ReadCoreDword(result, 0x2797704));
        Assert.Equal(0x12345698u, WaterSchedulerFixture.ReadCoreDword(result, 0x279770C));
    }

    [Fact]
    public void Retained_clone_advances_its_own_animation_after_JSON_restore_without_recapturing_or_reallocating()
    {
        int captures = 0;
        NativeBattleCinematicActorAnimation2CServices services = Services(Skeleton(), () => captures++, (frame, _) => new(frame, 0x12345678));
        NativeBattleWaterExecutionFrame first = NativeBattleCinematicActorAnimation2C.Draw(Fixture(), services);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleWaterExecutionFrame second = NativeBattleCinematicActorAnimation2C.Draw(first, services);
        Assert.Equal(1, captures);
        Assert.Equal((ushort)2, Word(second, 0x4000));
        Assert.Equal((byte)2, second.Arena.Read(0x4026, 1)[0]);
        Assert.Equal(Base + 0x40F0, WaterSchedulerFixture.ReadCoreDword(second, 0x2797704));
        Assert.Equal(NativeBattleTestSnapshots.Json(second), NativeBattleTestSnapshots.Json(NativeBattleCinematicActorAnimation2C.Draw(restored, services)));
        Assert.Equal(1, captures);
    }

    [Theory]
    [InlineData(1u, 0, false)]
    [InlineData(2u, 1, false)]
    [InlineData(3u, 1, false)]
    [InlineData(3u, 1, true)]
    public void Projection_publishes_actual_last_selected_group_including_zero_vertices_and_preserves_other_registers(uint mask, int expectedBone, bool unknownPadding)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x8000, [2]);
        for (int bone = 0; bone < 2; bone++)
        {
            byte[] packed = new byte[32];
            BinaryPrimitives.WriteInt16LittleEndian(packed, (short)(100 + bone));
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(16), -123);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(18), unchecked((short)0xFEDC));
            BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(20), 10 + bone);
            BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(24), 20 + bone);
            BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(28), 30 + bone);
            arena = arena.Write(0x8020 + bone * 48, packed);
        }
        if (unknownPadding)
        {
            arena = arena.MarkUnknown(0x8050 + 18, 1);
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleGeometryRegisters retained = NativeBattleGeometryRegisters.Unknown() with { Translation = [701, 702, 703], Sxy = [101u, 102u, 103u], Sz = [201u, 202u, 203u, 204u], Mac0 = 301, Otz = 302 };
        memory = memory with { GeometryRegisters = retained, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Mac = [901, 902, 903], Ir = [801, 802, 803], Flags = 0x1234, NormalInput = Enumerable.Repeat((byte)0x71, 8).ToImmutableArray() } };
        ImmutableArray<NativeBattleRawMesh> meshes = [new([], []) { PackedVertexGroups = [new(0, 1, new byte[6].ToImmutableArray())] }, new([], []) { PackedVertexGroups = [new(1, 0, [])] }];
        NativeBattleEffectMemory result = NativeBattleCinematicActorAnimation2C.PublishProjectionRegisters(memory, arena, Base + 0x8000, meshes, mask);
        NativeBattleGeometryRegisters actual = result.GeometryRegisters!;
        Assert.Equal(new int?[] { 10 + expectedBone, 20 + expectedBone, 30 + expectedBone }, actual.Translation);
        Assert.Equal((byte?)(100 + expectedBone), actual.Rotation20[0]);
        Assert.Equal((byte?)0xFE, actual.Rotation20[19]);
        Assert.Equal(unknownPadding ? null : (byte?)0xDC, actual.Rotation20[18]);
        Assert.Equal(retained.V1, actual.V1);
        Assert.Equal(retained.V2, actual.V2);
        Assert.Equal(retained.Sxy, actual.Sxy);
        Assert.Equal(retained.Sz, actual.Sz);
        Assert.Equal(retained.Mac0, actual.Mac0);
        Assert.Equal(retained.Otz, actual.Otz);
        Assert.Same(memory.ScriptedColorRegisters, result.ScriptedColorRegisters);
        Assert.Same(memory.ScriptedGraphics, result.ScriptedGraphics);
        NativeBattleEffectArena restoredArena = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(arena))!;
        NativeBattleEffectMemory restoredMemory = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(NativeBattleCinematicActorAnimation2C.PublishProjectionRegisters(restoredMemory, restoredArena, Base + 0x8000, meshes, mask)));
    }

    [Fact]
    public void Masked_or_absent_groups_retain_basis_and_missing_selected_group_owner_fails_fast()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleEffectArena.Length);
        ImmutableArray<NativeBattleRawMesh> unknownGroups = [new([], [])];
        Assert.Same(memory, NativeBattleCinematicActorAnimation2C.PublishProjectionRegisters(memory, arena, Base, unknownGroups, 0));
        ImmutableArray<NativeBattleRawMesh> emptyGroups = [new([], []) { PackedVertexGroups = [] }];
        Assert.Same(memory, NativeBattleCinematicActorAnimation2C.PublishProjectionRegisters(memory, arena, Base, emptyGroups, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicActorAnimation2C.PublishProjectionRegisters(memory, arena, Base, unknownGroups, 1));
    }

    [Theory]
    [InlineData((ushort)1, (ushort)0)]
    [InlineData((ushort)0, (ushort)1)]
    public void Selected_colored_geometry_fails_fast_before_omitting_unproved_color_feedback(ushort triangles, ushort quads)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicActorAnimation2C.RequireSelectedPrimitiveGroups([7, 11, triangles, quads]));
    }

    private static NativeBattleCinematicActorAnimation2CServices Services(byte[] skeleton, Action capture, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleCinematicActorCloneDraw> draw)
    {
        byte[] animations = new byte[4 + 29 * 4 + 65];
        BinaryPrimitives.WriteInt32LittleEndian(animations, 29);
        for (int index = 0; index < 29; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(animations.AsSpan(4 + index * 4), 120);
        }
        animations[120] = 2;
        NativeBattleCinematicModelServices model = new((_, _) => throw new InvalidOperationException("No embedded shape is selected."), (_, address) => address == AnimationAddress ? animations : throw new InvalidDataException());
        return new(actor =>
        {
            capture();
            Assert.Equal(0x1D97494u, actor);
            NativeBattleEffectArena resources = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x1939018u)).Write(4, BitConverter.GetBytes(0x193F000u)).Write(8, BitConverter.GetBytes(AnimationAddress)).Write(12, BitConverter.GetBytes(0x193F800u));
            return new(NativeBattleEffectArena.Clear().MarkUnknown(0, 12), resources, skeleton, 0x00102030, uint.MaxValue);
        }, frame =>
        {
            byte[] basis = new byte[32];
            for (int index = 0; index < 3; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(basis.AsSpan(index * 8), 4096);
            }
            BinaryPrimitives.WriteInt32LittleEndian(basis.AsSpan(28), 1000);
            return frame.WriteTemporary(0xE0, basis);
        }, model, draw);
    }

    private static NativeBattleWaterExecutionFrame Fixture(byte animation = 26)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797704, Base + 0x4000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27970D4, 0x1D97494);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Base + 0x3000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x00808080);
        return frame with { Arena = frame.Arena.MarkUnknown(0x4000, 0x80).Write(0x201B, [3]).Write(0x3000, BitConverter.GetBytes((short)animation)) };
    }

    private static byte[] Skeleton()
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        skeleton[6] = 0x5A;
        return skeleton;
    }

    private static uint Dword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(offset, 4));
    private static ushort Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(offset, 2));
}
