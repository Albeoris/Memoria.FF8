using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRayBomb307BeamTests
{
    [Theory]
    [InlineData(1, 0x33, -1)]
    [InlineData(5, 0xF3, 1364)]
    public void Actual_88_draw_precedes_pause_and_retains_early_RGB_or_writes_exact_fade(ushort age, uint flags, int cue)
    {
        Fixture fixture = new(age);
        NativeBattleRayBomb307TaskStep step = fixture.Advance(fixture.Frame, 0x201);
        Assert.Equal(0, step.Result);
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
        Assert.Equal(age, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(12));
        Assert.Equal((ushort)70, NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank).ReadWord(24));
        NativeBattleEffectArena temporary = Assert.Single(fixture.Contexts);
        Assert.Equal(flags, Dword(temporary, 0x1C));
        Assert.Throws<NotSupportedException>(() => temporary.Read(11, 1));
        if (cue < 0)
        {
            Assert.Equal(new byte[] { 0x12, 0x34, 0x56 }, temporary.Read(8, 3));
            Assert.Throws<NotSupportedException>(() => temporary.Read(12, 4));
        }
        else
        {
            Assert.Equal(new byte[3], temporary.Read(8, 3));
            Assert.Equal((uint)cue, Dword(temporary, 12));
            Assert.Equal((uint)cue, step.Frame.Memory.ScriptedColorRegisters!.Ir0);
        }
        Assert.NotEmpty(Assert.Single(fixture.Batches).Attempts);
        Assert.NotNull(Assert.Single(fixture.Batches[0].Attempts).Primitive);
        Assert.NotNull(step.Frame.Memory.ScriptedColorRegisters!.NormalInput);
        Assert.Null(step.Frame.Memory.GeometryRegisters!.Rotation20[18]);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Throws<NotSupportedException>(() => step.Frame.State.ReadView(0x2242580, 8).Read(6, 2));
        Assert.Single(step.Frame.Effect.Pools[1].Slots.Where(task => (task.Flags & 1) != 0));
    }

    [Fact]
    public void Age2_births_use_count_draw_then_five_draws_each_and_preserve_parent_link_updates()
    {
        Fixture fixture = new(2);
        NativeBattleRayBomb307Frame frame = fixture.Frame;
        NativeCrtRandomState expected = frame.Random.Draw(out ushort countDraw);
        int count = countDraw % 5 + 3;
        NativeBattleRayBomb307TaskStep step = fixture.Advance(frame);
        NativeBattleRayBomb10BeamGeometry geometry = Fixture.Beam(2);
        NativeBattleEffectTaskPool pool = step.Frame.Effect.Pools[1];
        Assert.Equal(count + 1, pool.Slots.Count(task => (task.Flags & 1) != 0));
        Assert.Equal(1, pool.Slots[0].Next);
        for (int index = 0; index < count; index++)
        {
            expected = expected.Draw(out ushort delay);
            expected = expected.Draw(out ushort x);
            expected = expected.Draw(out ushort z);
            expected = expected.Draw(out ushort speed);
            expected = expected.Draw(out ushort scale);
            NativeBattleModulePoolTask child = pool.Slots[index + 1];
            Assert.Equal(0x5A2340u, child.Callback);
            Assert.Equal((ushort)0, child.ReadWord(12));
            Assert.Equal((ushort)((delay & 7) + 8), child.ReadWord(14));
            Assert.Equal(unchecked((ushort)(geometry.End.X + x % 1000 - 500)), child.ReadWord(16));
            Assert.Equal((ushort)0, child.ReadWord(18));
            Assert.Equal(unchecked((ushort)(geometry.End.Z + z % 900 - 400)), child.ReadWord(20));
            Assert.Equal(unchecked((ushort)(-10 - speed % 20)), child.ReadWord(22));
            Assert.Equal((ushort)(scale % 0x700 + 0xC00), child.ReadWord(28));
            Assert.Equal(index + 1 == count ? null : (int?)(index + 2), child.Next);
        }
        Assert.Equal(expected, step.Frame.Random);
        Assert.Equal((ushort)3, pool.Slots[0].ReadWord(12));
        Assert.Equal(unchecked((ushort)(70 + 160)), pool.Slots[0].ReadWord(24));
        Assert.Equal((ushort)170, pool.Slots[0].ReadWord(26));
    }

    [Fact]
    public void Old_age9_draws_and_births_before_retiring_new_age10_with_signed_angular_damping()
    {
        Fixture fixture = new(9);
        NativeBattleModulePoolTask task = NativeBattleRayBomb307Controller.Read(fixture.Frame, ChildBank).WriteWord(24, 32760).WriteWord(26, unchecked((ushort)-17));
        NativeBattleRayBomb307Frame input = NativeBattleRayBomb307Controller.Write(fixture.Frame, ChildBank, task);
        NativeBattleRayBomb307TaskStep step = fixture.Advance(input);
        NativeBattleModulePoolTask current = NativeBattleRayBomb307Controller.Read(step.Frame, ChildBank);
        Assert.Equal(2, step.Result);
        Assert.Equal((ushort)10, current.ReadWord(12));
        Assert.Equal((ushort)32743, current.ReadWord(24));
        Assert.Equal(unchecked((ushort)-19), current.ReadWord(26));
        Assert.Equal(4092u, Dword(Assert.Single(fixture.Contexts), 12));
        Assert.True(step.Frame.Effect.Pools[1].Slots.Count(slot => (slot.Flags & 1) != 0) >= 4);
        Assert.NotEqual(input.Random, step.Frame.Random);
    }

    [Fact]
    public void Unknown_origin_W_is_not_consumed_and_JSON_next_retains_physical_temporary_and_geometry()
    {
        Fixture first = new(2);
        NativeBattleRayBomb307TaskStep step = first.Advance(first.Frame);
        NativeBattleRayBomb307Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleRayBomb307Frame>(NativeBattleTestSnapshots.Json(step.Frame))!;
        Fixture second = new(2);
        Assert.Equal(NativeBattleTestSnapshots.Json(first.Advance(step.Frame).Frame), NativeBattleTestSnapshots.Json(second.Advance(restored).Frame));
        Assert.Throws<NotSupportedException>(() => step.Frame.State.ReadView(0x2242580, 8).Read(6, 2));
        foreach (NativeBattleModulePoolTask child in step.Frame.Effect.Pools[1].Slots.Skip(1).Where(task => (task.Flags & 1) != 0))
        {
            Assert.InRange(unchecked((short)child.ReadWord(22)), (short)-29, (short)-10);
        }
    }

    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private sealed class Fixture
    {
        public NativeBattleRayBomb307Frame Frame { get; }
        public List<NativeBattleEffectArena> Contexts { get; } = [];
        public List<NativeBattleGfGeometryBatch> Batches { get; } = [];
        private static NativeBattleActionMatrix View => NativeBattleCuragaMatrices.Identity8DD770() with { Z = 10000 };
        private static readonly NativeBattleCameraProjection Projection = new(null, 256, 160, 108);
        private static readonly NativeBattleVector Eye = new(0, 0, -10000);
        public Fixture(ushort age)
        {
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 100);
            (children, int slot) = children.Append(0x5A1EE0);
            children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(slot, children.Slots[slot].WriteWord(12, age).WriteWord(16, 2000).WriteWord(18, unchecked((ushort)-500)).WriteWord(20, 4000).WriteWord(24, 70).WriteWord(26, 160).WriteWord(32, 2048)) });
            NativeBattleOriginalEffectState effect = new(307, 307, new(307, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 2), children] };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            NativeBattleEffectArena origin = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes((short)1000)).Write(2, BitConverter.GetBytes((short)-1000)).Write(4, BitConverter.GetBytes((short)1000));
            Frame = new(memory, effect, new(123), NativeBattleRayBomb307State.FromColdProcess().WriteView(0x2242580, origin, 8)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address + 8, [0x12, 0x34, 0x56]) };
        }
        public static NativeBattleRayBomb10BeamGeometry Beam(short age) => NativeBattleRayBomb10Geometry.Beam628B00(new(2000, -500, 4000), new(1000, -1000, 1000, 0), Eye, 70, 2048, age);
        public NativeBattleRayBomb307TaskStep Advance(NativeBattleRayBomb307Frame frame, uint flags = 0) => NativeBattleRayBomb307Controller.AdvanceBeam(frame, ChildBank, null!, new(NativeBattleActionPacketsState.Fresh(), flags, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), Execute) { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(View, null), ReadCameraEye = _ => Eye });
        private NativeBattleRayBomb307Frame Execute(NativeBattleRayBomb307Frame frame, NativeBattleRayBomb307Operation operation)
        {
            NativeBattleRayBomb307DrawMesh88 draw = Assert.IsType<NativeBattleRayBomb307DrawMesh88>(operation);
            Assert.Equal(0xCE3BB4u, draw.SourceAddress);
            Assert.Equal(NativeBattleTemporaryMemory.Address + 88, BitConverter.ToUInt32(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(frame.Staging, draw.TemporaryAddress, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
            Contexts.Add(temporary);
            NativeBattlePackedMatrix32 matrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(frame.Memory.GeometryRegisters!);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.Draw(frame.Memory, Mesh(), matrix.ReadMatrix(), Projection, temporary, NativeBattleGteColorInterpolation.CodeConstants45F270);
            Batches.Add(drawn.Batch);
            return frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging, draw.TemporaryAddress, drawn.Context, 88) };
        }
        private static NativeBattleStaticMeshResource Mesh()
        {
            byte[] bytes = new byte[96];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
            short[] xy = [-1000, -1000, 1000, -1000, -1000, 1000, 1000, 1000];
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + corner * 8), xy[corner * 2]);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + corner * 8), xy[corner * 2 + 1]);
            }
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(60), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 0x38112233);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(68 + corner * 2), (ushort)(corner * 2));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), 0x30224466);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80), 0x306688AA);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(84), 0x30AACCEE);
            return new(0, 0xCE3BB4, bytes.ToImmutableArray());
        }
    }
}
