using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect287Lower;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect287LowerTests
{
    [Fact]
    public void Attachment_birth_preserves_partial_point_W_and_unwritten_pair_padding()
    {
        Fixture fixture = new(false);
        NativeBattleEffectArena point = PutWord287(PutWord287(PutWord287(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 11), 2, -22), 4, 33).Write(6, [0xA5]);
        NativeBattleEffect287Frame result = NativeBattleEffect287Attachments.Birth6F3D80(fixture.Frame, 0x123456, 0x13FAEB0, point, 0, 12, 7, fixture.Inputs());
        NativeBattleModulePoolTask task = Read(result, Fixture.Task);
        Assert.Equal(0x123456u, task.ReadDword(28));
        Assert.Equal(0x13FAEB0u, task.ReadDword(24));
        Assert.Equal(11, Signed(task, 16));
        Assert.Equal(-22, Signed(task, 18));
        Assert.Equal(33, Signed(task, 20));
        (byte[] bytes, bool[] known) = TaskView(task, 22, 2).ReadSnapshot(0, 2);
        Assert.Equal(0xA5, bytes[0]);
        Assert.True(known[0]);
        Assert.False(known[1]);
        for (int index = 0; index < 8; index++)
        {
            Assert.Equal(128, Signed(task, 32 + index * 4));
            Assert.Equal(0, Signed(task, 34 + index * 4));
        }
        Assert.Equal(fixture.Frame.Random, result.Random);
        Assert.Empty(fixture.Operations);
    }

    [Fact]
    public void Full_attachment_pool_returns_without_consuming_source_or_process_random()
    {
        Fixture fixture = new();
        NativeBattleEffect287Frame result = NativeBattleEffect287Attachments.Birth6F3D80(fixture.Frame, 0, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 12, 7, fixture.Inputs());
        Assert.Same(fixture.Frame, result);
        Assert.Equal(0, fixture.RandomCalls);
    }

    [Fact]
    public void Current_triangle_uses_directory_bones_and_preserves_unknown_W()
    {
        Fixture fixture = new();
        (NativeBattleEffect287Frame frame, NativeBattleEffectArena center, NativeBattleEffectArena normal) = NativeBattleEffect287Attachments.Triangle704130(fixture.Frame, 0x123456, 0x13FAEB0, fixture.Resources, fixture.Inputs());
        Assert.Equal(new[] { 0, 1, 2 }, fixture.Bones);
        Assert.All(fixture.Directories, directory => Assert.Equal(0x123456u, directory));
        Assert.Equal(333, Word287(center, 0));
        Assert.Equal(333, Word287(center, 2));
        Assert.Equal(20999, Word287(center, 4));
        Assert.DoesNotContain(true, center.ReadSnapshot(6, 2).Known);
        Assert.DoesNotContain(true, normal.ReadSnapshot(6, 2).Known);
        Assert.NotNull(frame.Memory.GeometryRegisters!.Lzcr);
        Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6)));
    }

    [Fact]
    public void Attachment_curve_draws_before_taper_retirement_and_uses_eighteen_process_draws()
    {
        Fixture fixture = new();
        NativeBattleModulePoolTask initial = Read(fixture.Frame, Fixture.Task).WriteByte(12, 10);
        for (int index = 0; index < 8; index++)
        {
            initial = initial.WriteWord(32 + index * 4, 0);
        }
        fixture.Frame = Write(fixture.Frame, Fixture.Task, initial);
        NativeBattleEffect287Step result = NativeBattleEffect287Attachments.Advance(fixture.Frame, Fixture.Task, fixture.Resources, fixture.Inputs());
        Assert.Equal(18, fixture.RandomCalls);
        Assert.Equal(fixture.Frame.Random, result.Frame.Random);
        Assert.Equal(2, result.Result);
        Assert.Equal(10, Read(result.Frame, Fixture.Task).ReadByte(12));
        Assert.IsType<NativeBattleEffect287DrawBatch>(Assert.Single(fixture.Operations));
        Assert.Equal(0x1D8E054u, ((NativeBattleEffect287DrawBatch)fixture.Operations[0]).CursorGlobal);
        Assert.Equal(0, Signed(Read(result.Frame, Fixture.Task), 32));
        NativeBattleEffectArena rows = NativeBattleTemporaryMemory.ReadView(result.Frame.Staging!, NativeBattleTemporaryMemory.Address, 128);
        Assert.False(rows.ReadSnapshot(22, 1).Known[0]);
        Assert.NotNull(result.Frame.Memory.GeometryRegisters!.Sxy[2]);
    }

    [Fact]
    public void Zero_taper_counts_as_surviving_and_age_advances_after_draw()
    {
        Fixture fixture = new();
        fixture.Frame = WriteBytes(fixture.Frame, Fixture.Common + 0x1F98, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((short)5120, 8).SelectMany(BitConverter.GetBytes).ToArray()), 16);
        fixture.Frame = Write(fixture.Frame, Fixture.Task, Read(fixture.Frame, Fixture.Task).WriteByte(12, 10).WriteWord(32, 0).WriteWord(36, 0).WriteWord(40, 0).WriteWord(44, 0).WriteWord(48, 0).WriteWord(52, 0).WriteWord(56, 0).WriteWord(60, 0));
        NativeBattleEffect287Step result = NativeBattleEffect287Attachments.Advance(fixture.Frame, Fixture.Task, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, result.Result);
        Assert.Equal(11, Read(result.Frame, Fixture.Task).ReadByte(12));
        Assert.Equal(18, fixture.RandomCalls);
    }

    [Fact]
    public void Debris_integrates_new_gravity_velocity_then_bounces_after_draw()
    {
        Fixture fixture = new();
        fixture.Frame = Write(fixture.Frame, Fixture.Task, Read(fixture.Frame, Fixture.Task).WriteWord(12, 9).WriteWord(16, 10).WriteWord(18, ushort.MaxValue).WriteWord(20, 2000).WriteWord(22, 5).WriteWord(24, 20).WriteWord(26, 30).WriteWord(28, 40).WriteWord(30, 7));
        NativeBattleEffect287Step result = AdvanceDebris(fixture.Frame, Fixture.Task, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask task = Read(result.Frame, Fixture.Task);
        Assert.Equal(2, result.Result);
        Assert.Equal(14, Signed(task, 18));
        Assert.Equal(-15, Signed(task, 22));
        Assert.Equal(2400, Signed(task, 20));
        Assert.Equal(27, Signed(task, 24));
        Assert.Single(fixture.Operations);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Right_diagonal_scale_rounds_signed_half_before_WORD_store(short coefficient)
    {
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [coefficient, 0, 0, 0, 4096, 0, 0, 0, 4096] };
        NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(rotation, new([2048, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096));
        Assert.Equal(NativePrecision53.RoundInteger(coefficient / 2d), scaled.Rotation[0]);
    }

    [Fact]
    public void Presentation_attachment_captures_post_native_rows_before_SXY_overwrite_and_preserves_process_random_FIFO_and_masks()
    {
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        Fixture nativeFixture = new();
        NativeBattleEffect287Step native = NativeBattleEffect287Attachments.Advance(nativeFixture.Frame, Fixture.Task, nativeFixture.Resources, nativeFixture.Inputs() with { ReadProjection = _ => projection });
        string nativeFrame = NativeBattleTestSnapshots.Json(native.Frame);
        string nativeOps = NativeBattleTestSnapshots.Json(nativeFixture.Operations);
        Fixture capturedFixture = new();
        NativeBattleEffect287Inputs inputs = capturedFixture.Inputs() with { ReadProjection = _ => projection, CapturePresentation = true };
        NativeBattleEffect287Step captured = NativeBattleEffect287Attachments.Advance(capturedFixture.Frame, Fixture.Task, capturedFixture.Resources, inputs);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(nativeOps, NativeBattleTestSnapshots.Json(capturedFixture.Operations));
        Assert.Equal(nativeFixture.RandomCalls, capturedFixture.RandomCalls);
        Assert.Equal(18, capturedFixture.RandomCalls);
        Assert.Equal(nativeFixture.Bones, capturedFixture.Bones);
        NativeBattleGfGeometryBatch batch = Assert.IsType<NativeBattleEffect287DrawBatch>(Assert.Single(capturedFixture.Operations)).Batch;
        Assert.NotEmpty(batch.Attempts);
        Assert.All(batch.Attempts, attempt =>
        {
            NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal((ushort)287, geometry.Identity.Effect);
            Assert.True(geometry.Identity.Generation > 0);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), geometry.SourceDepths);
            Assert.Equal(4, geometry.ScreenOffsets.Length);
            Assert.Equal(geometry.A, geometry.B);
            Assert.Equal(geometry.C, geometry.D);
        });
        NativeBattlePilotVisualGeometry first = batch.Attempts[0].Primitive!.PilotGeometry!;
        Assert.Equal(new System.Numerics.Vector3(333, 333, 20999), first.A);
        Assert.Equal(batch.Attempts.Length, batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        string beforeSampling = NativeBattleTestSnapshots.Json(captured.Frame);
        _ = NativeBattlePilotVisualGeometry.Sample(first, first with { A = first.A + System.Numerics.Vector3.One }, 0.5f);
        Assert.Equal(beforeSampling, NativeBattleTestSnapshots.Json(captured.Frame));
    }

    internal sealed class Fixture
    {
        public const uint Common = 0x20DFAB8;
        public const uint Task = Common + 0x21FF4;
        public NativeBattleEffect287Frame Frame;
        public NativeBattleEffect287Resources Resources;
        public List<NativeBattleEffect287Operation> Operations { get; } = [];
        public List<int> Bones { get; } = [];
        public List<uint> Directories { get; } = [];
        public int RandomCalls;

        public Fixture(bool append = true)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleEffect287Profile.MeshDescriptor, Task, 64, 1);
            if (append)
            {
                (pool, int slot) = pool.Append(0x6F3DF0);
                NativeBattleModulePoolTask task = pool.Slots[slot].WriteBytes(12, new byte[52]).WriteByte(14, 12).WriteByte(15, 7).WriteDword(24, 0x13FAEB0).WriteDword(28, 0x123456).WriteWord(22, 1);
                for (int index = 0; index < 8; index++)
                {
                    task = task.WriteWord(32 + index * 4, 128);
                }
                pool = pool.WriteSlot(slot, task);
            }
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, [pool]);
            Frame = new(memory, new(287, 287, new(287, 0x5755B0, 0x6F2E80), null, null) { Pools = [pool] }, new(42), NativeBattleEffect287State.FromColdProcess().WriteDword(NativeBattleEffect287Profile.ArenaGlobal, Common)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            Frame = Frame with { Staging = Frame.Staging.MarkUnknown(NativeBattleTemporaryMemory.Address + 22, 1) };
            byte[] triangle = new byte[24];
            short[] xyz = [0, 0, 1000, 0, 1000, 0, 1000, 1, 0, 1000, 1000, 2];
            for (int index = 0; index < xyz.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(triangle.AsSpan(index * 2), xyz[index]);
            }
            byte[] normalization = new byte[384];
            for (int index = 64; index < 256; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(normalization.AsSpan((index - 64) * 2), (ushort)(Math.Sqrt(index) * 512));
            }
            byte[] emptyMesh = new byte[40];
            BinaryPrimitives.WriteInt32LittleEndian(emptyMesh, 8);
            Resources = new(null!, null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(0x13EF72C, new(0, 0x13EF72C, emptyMesh.ToImmutableArray())), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x13FAEB0, triangle.ToImmutableArray()).Add(NativeBattleEffect287Profile.NormalizationAddress, normalization.ToImmutableArray()).Add(NativeBattleEffect287Profile.DistanceAddress, Enumerable.Repeat((byte)100, 16384).ToImmutableArray()));
        }

        public NativeBattleEffect287Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => null!, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), _ => 0, (_, directory, bone) =>
        {
            Directories.Add(directory);
            Bones.Add(bone);
            return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 20000 }, null);
        }, null!, null!, null!, null!, frame =>
        {
            RandomCalls++;
            return new(frame, 16384);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
