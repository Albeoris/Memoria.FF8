using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectricDischarge126CaptureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Both_ribbon_projections_capture_actual_centers_and_native_edge_offsets_without_native_writes(bool bone)
    {
        Fixture fixture = new(bone);
        var native = fixture.Draw(false);
        string nativeFrame = NativeBattleTestSnapshots.Json(native.Frame);
        string nativeBatch = NativeBattleTestSnapshots.Json(native.Batch);
        var captured = fixture.Draw(true);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(nativeBatch, NativeBattleTestSnapshots.Json(captured.Batch));
        Assert.Equal(2, fixture.World.Count);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(captured.Batch.Attempts);
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(attempt.Primitive!.PilotGeometry);
        Assert.Equal(new Vector3(0, 0, 4096), geometry.A);
        Assert.Equal(new Vector3(512, 0, 4096), geometry.C);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(Vector4.Zero, geometry.SourceDepths);
        Assert.Equal(fixture.Bank, geometry.Identity.Bank);
        Assert.Equal(0, geometry.Identity.Slot);
        Assert.Equal(1, geometry.Identity.Primitive);
        Assert.Equal(fixture.Identity(0).Generation, geometry.Identity.Generation);
        Assert.Equal(fixture.Identity(1).Generation, geometry.Identity.ResourceGeneration);
        int[] words = [8, 20, 32, 44];
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 projected = NativeBattlePilotVisualGeometry.Project(geometry.Corner(corner), fixture.Projection);
            Vector2 sampled = new Vector2(projected.X, projected.Y) * geometry.ScreenScale + geometry.ScreenOffsets[corner];
            uint packet = BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(words[corner]));
            Assert.Equal((float)unchecked((short)packet), sampled.X);
            Assert.Equal((float)unchecked((short)(packet >> 16)), sampled.Y);
        }
        NativeBattlePilotVisualGeometry later = Assert.Single(fixture.Draw(true, 128).Batch.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(geometry.Identity, later.Identity);
        Assert.Equal(geometry.A + new Vector3(64, 0, 0), NativeBattlePilotVisualGeometry.SampleCorner(geometry, later, 0, 0.5f));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public void Recycled_either_endpoint_changes_segment_identity_and_stops_cross_birth_sampling(bool bone, int endpoint)
    {
        Fixture fixture = new(bone);
        NativeBattlePilotVisualGeometry before = Assert.Single(fixture.Draw(true).Batch.Attempts).Primitive!.PilotGeometry!;
        long first = fixture.Identity(0).Generation;
        long second = fixture.Identity(1).Generation;
        fixture.Recycle(endpoint);
        NativeBattlePilotVisualGeometry after = Assert.Single(fixture.Draw(true, 128).Batch.Attempts).Primitive!.PilotGeometry!;
        Assert.NotEqual(before.Identity, after.Identity);
        Assert.Equal(endpoint == 0, fixture.Identity(0).Generation != first);
        Assert.Equal(endpoint == 1, fixture.Identity(1).Generation != second);
        Assert.Equal(before.A, NativeBattlePilotVisualGeometry.SampleCorner(before, after, 0, 0.5f));
    }

    private sealed class Fixture
    {
        private readonly bool bone;
        private NativeBattleCpuTrailFrame frame;
        private readonly NativeBattleCpuTrailLayout layout = new(ArenaAddress + WorldPointsOffset, ArenaAddress + WorldFreePointerOffset, ArenaAddress + BonePointsOffset, ArenaAddress + BoneFreePointerOffset, PointCount, 0, WorldTrailCallback, 0, BoneTrailCallback);
        private readonly NativeBattleElectricDischarge126Resources resources = new(new(0, []), new(0, 0, 0, 0, []), new(0, 0, 0, 0, []), new("unused", []), Enumerable.Repeat((byte)16, 16384).ToImmutableArray(), [], [], [], 0, 0, 0);
        public uint Bank => bone ? layout.BoneBankAddress : layout.WorldBankAddress;
        private uint FreePointer => bone ? layout.BoneFreePointerAddress : layout.WorldFreePointerAddress;
        public Dictionary<uint, Vector3> World { get; } = [];
        private static NativeBattleActionMatrix View => NativeBattleCuragaMatrices.Identity8DD770();
        public NativeBattleCameraProjection Projection { get; } = new(View, 256, 160, 108);

        public Fixture(bool bone)
        {
            this.bone = bone;
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters!, View, true, true, null) };
            frame = new(memory, new(126, 126, new(126, Entry, Initializer), null, null));
            Allocate(0);
            Allocate(1);
            Link();
        }

        private void Allocate(int index)
        {
            uint pointer = Bank + (uint)(index * PointStride);
            Store(FreePointer, BitConverter.GetBytes(pointer));
            NativeBattleCpuTrailAddressStep allocated = bone
                ? NativeBattleCpuTrails.AllocateBonePoint(frame, layout, 0, (short)(index * 4), 4096, false, 2048, 0)
                : NativeBattleCpuTrails.AllocateWorldPoint(frame, layout, new((short)(index * 512), 0, 4096), 4096);
            Assert.Equal(pointer, allocated.Address);
            frame = allocated.Frame;
        }

        public void Recycle(int index)
        {
            Store(Bank + (uint)(index * PointStride + (bone ? 0 : 6)), BitConverter.GetBytes((short)-1));
            Allocate(index);
            Link();
        }

        public NativeBattlePilotVisualIdentity Identity(int row) => NativeBattlePilotVisualLifetimes.Identity(126, frame.Memory.Arena!, Bank, row);
        private void Link() => Store(Bank + 0x24, BitConverter.GetBytes(Bank + PointStride));
        private void Store(uint address, byte[] bytes) => frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(checked((int)(address - ArenaAddress)), bytes) } };

        public (NativeBattleElectricDischarge126Frame Frame, NativeBattleGfGeometryBatch Batch) Draw(bool capture, short delta = 0)
        {
            World.Clear();
            Action<uint, Vector3>? receipt = capture ? (address, world) => World[address] = world : null;
            NativeBattleEffectMemory projected;
            if (bone)
            {
                projected = NativeBattleElectrocute275BoneProjection.ProjectChain(frame.Memory, Bank, Bank, PointCount * PointStride, _ => NativeBattlePackedMatrix32.FromMatrix(View, null), (offset, count) =>
                {
                    Assert.Equal(6, count);
                    byte[] xyz = new byte[6];
                    BinaryPrimitives.WriteInt16LittleEndian(xyz, (short)((offset == 0 ? 0 : 512) + delta));
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), 4096);
                    return xyz;
                }, NativeBattlePackedMatrix32.FromMatrix(View, null), Projection, frame.Memory.ScriptedGraphics!, captureWorld: receipt);
            }
            else
            {
                for (int row = 0; row < 2; row++)
                {
                    Store(Bank + (uint)(row * PointStride), BitConverter.GetBytes((short)(row * 512 + delta)));
                }
                projected = NativeBattleRibbonProjection.ProjectCurrent(frame.Memory, Bank, new(Bank, checked((int)(Bank - ArenaAddress)), PointCount), Projection, frame.Memory.ScriptedGraphics!, receipt);
            }
            NativeBattleElectricDischarge126Frame visual = new(projected, frame.Effect, new(1), NativeBattleElectricDischarge126State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress));
            NativeBattlePilotVisualIdentity? identity = capture ? new(126, Bank, 0, 1, 0) : null;
            return bone ? NativeBattleElectricDischarge126Ribbons.DrawBoneProjected(visual, Bank, 2, resources, identity, capture ? World : null) : NativeBattleElectricDischarge126Ribbons.DrawProjected(visual, Bank, 2, 0, resources, 0, identity, capture ? World : null);
        }
    }
}
