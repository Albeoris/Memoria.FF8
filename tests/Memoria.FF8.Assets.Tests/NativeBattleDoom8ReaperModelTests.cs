using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDoom8ReaperModelTests
{
    [Theory]
    [InlineData(256, 60)]
    [InlineData(3840, 4)]
    public void Original_reaper_normal_body_keeps_nonzero_native_fade_and_cold_next(short coefficient, byte expectedAlpha)
    {
        using GameAssets game = OpenGame();
        Component component = new(game, 4);
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(component.Frame, component.Address);
        task = NativeBattleTonberry90Creature.Recolor7678D0(task.WriteByte(0x29, 9).WriteWord(0x13A, unchecked((ushort)coefficient)).WriteWord(0x13C, unchecked((ushort)coefficient)), coefficient, 3, component.Inputs.Colors.Transition);
        NativeBattleDoom8Frame frame = NativeBattleDoom8Controller.Write(component.Frame, component.Address, task);
        Assert.Equal(expectedAlpha, task.ReadByte(0x37));
        NativeBattleDoom8ModelDrawStep drawn = component.Normal(frame);
        NativeBattleGeometryPrimitive[] bodies = BodyPackets(drawn.Batch);
        Assert.NotEmpty(bodies);
        Assert.True(drawn.Batch.AllocationBytes > 32);
        Assert.All(bodies, primitive =>
        {
            Assert.Equal(task.ReadDword(0x58) & 0xFFFFFF, primitive.Words[1] & 0xFFFFFF);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, primitive.PhysicalWriteMask!.Value[30]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, primitive.PhysicalWriteMask.Value[31]);
        });
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(drawn.Staging, NativeBattleTemporaryMemory.Address, 0x4C);
        Assert.Equal(new[] { expectedAlpha, expectedAlpha, expectedAlpha }, temporary.Read(0x48, 3));
        Assert.Throws<NotSupportedException>(() => temporary.Read(0x4B, 1));
        Assert.Equal(NativeBattleDoom8ModelDrawing.ProjectedAddress, Dword(temporary.Read(0x24, 4)));
        Assert.All(drawn.Frame.State.Globals.ReadSnapshot(checked((int)(NativeBattleDoom8ModelDrawing.ProjectedAddress - NativeBattleDoom8State.BaseAddress)), 302 * 8).Known, value => Assert.True(value));
        AssertLocalBones(drawn.Staging, component.Resource, NativeBattleDoom8Models.Read(drawn.Frame, component.Address));
        AssertReleased(drawn);
        NativeBattleDoom8Frame resumed = Cold(drawn.Frame);
        NativeBattleDoom8ModelDrawStep next = component.AdvanceNormal(drawn.Frame);
        NativeBattleDoom8ModelDrawStep coldNext = component.AdvanceNormal(resumed);
        Assert.Equal(NativeBattleSnapshotJson.Serialize(next), NativeBattleSnapshotJson.Serialize(coldNext));
        Assert.Equal(expectedAlpha, NativeBattleDoom8Controller.Read(next.Frame, component.Address).ReadByte(0x37));
        AssertReleased(next);
        Assert.NotEmpty(BodyPackets(next.Batch));
    }

    [Fact]
    public void Original_reaper_appearance_runs_native_threshold_and_hold_inputs_with_live_JSON_continuation()
    {
        using GameAssets game = OpenGame();
        Component component = new(game, 0);
        NativeBattleDoom8ModelDrawStep bootstrap = component.Normal(component.Frame);
        NativeBattleDoom8Frame frame = NativeBattleDoom8ReaperAppearance.Initialize8CD020(bootstrap.Frame, component.Address + 0x30);
        frame = frame with { State = frame.State.WriteWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x54, 0).WriteWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5A, 256) };
        int customPackets = 0;
        int greatestAge = 0;
        for (int callback = 0; callback < 33; callback++)
        {
            bool entering = callback < 18;
            NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, component.Address);
            task = task.WriteByte(0x29, entering ? (byte)1 : (byte)2).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 8));
            frame = NativeBattleDoom8Controller.Write(frame, component.Address, task);
            ushort angle = (ushort)((frame.State.ReadWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x58) + 256) & 0xFFF);
            frame = frame with { State = frame.State.WriteWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x58, angle) };
            if (entering)
            {
                frame = frame with { State = frame.State.WriteWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x54, unchecked((ushort)(frame.State.ReadWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x54) - 72))) };
            }
            NativeBattleDoom8Frame? resumed = callback is 0 or 17 or 18 or 32 ? Cold(frame) : null;
            NativeBattleDoom8ModelDrawStep drawn = component.AdvanceAppearance(frame);
            if (resumed is not null)
            {
                NativeBattleDoom8ModelDrawStep coldNext = component.AdvanceAppearance(resumed);
                Assert.Equal(NativeBattleSnapshotJson.Serialize(drawn), NativeBattleSnapshotJson.Serialize(coldNext));
            }
            customPackets += BodyPackets(drawn.Batch).Length;
            byte[] ages = drawn.Frame.State.Read(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5D, 526);
            greatestAge = Math.Max(greatestAge, ages.Max(value => (int)value));
            Assert.All(ages, age => Assert.InRange(age, (byte)0, (byte)19));
            AssertReleased(drawn);
            AssertLocalBones(drawn.Staging, component.Resource, NativeBattleDoom8Models.Read(drawn.Frame, component.Address));
            Assert.NotNull(drawn.Frame.Memory.ScriptedLighting!.LightMatrix);
            Assert.NotNull(drawn.Frame.Memory.ScriptedLighting.BackColor);
            frame = drawn.Frame;
        }
        Assert.True(customPackets > 0, "The original appearance model must emit custom FT3/FT4 packets at reached visible ages.");
        Assert.Equal(19, greatestAge);
        Assert.Equal(unchecked((ushort)-1296), frame.State.ReadWord(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x54));
        Assert.True(frame.State.ReaperSourceLoaded);
        Assert.Single(frame.State.Models!.Value);
        Assert.Equal(0x24808080u, frame.State.ReadDword(NativeBattleDoom8ReaperAppearance.TrianglePalette + 19 * 4));
        Assert.Equal(0x2C808080u, frame.State.ReadDword(NativeBattleDoom8ReaperAppearance.QuadPalette + 19 * 4));
        (byte[] _, bool[] known) = frame.State.Globals.ReadSnapshot(checked((int)(NativeBattleDoom8ReaperAppearance.VertexAddress - NativeBattleDoom8State.BaseAddress)), 302 * 8);
        for (int vertex = 0; vertex < 302; vertex++)
        {
            Assert.All(known.Skip(vertex * 8).Take(6), value => Assert.True(value));
            Assert.Equal(known[6], known[vertex * 8 + 6]);
            Assert.Equal(known[7], known[vertex * 8 + 7]);
        }
    }

    private sealed class Component
    {
        public uint Address { get; }
        public NativeBattleDoom8Frame Frame { get; }
        public NativeBattleEffectOwnedModelResource Resource { get; }
        public NativeBattleDoom8ModelDrawInputs Inputs { get; }

        public Component(GameAssets game, short animation)
        {
            NativeBattleDoom8Resources resources = game.ReadNativeDoom8Resources();
            NativeBattleGraphicsRules rules = game.ReadNativeBattleGraphicsRules();
            Resource = resources.ReaperModel;
            Assert.Equal(28, Resource.PoseSource.BoneCount);
            Assert.Equal(302, Assert.Single(Resource.Geometry).Vertices.Length);
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(8, NativeBattleDoom8Reaper.Callback, 320);
            Address = appended.Address;
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
            memory = memory with { ScriptedGraphics = memory.ScriptedGraphics!.InitializeGeom56BAA0() };
            NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = appended.Hierarchy.Pools };
            NativeBattleDoom8Frame frame = new(memory, effect, new(1), NativeBattleDoom8State.FromColdProcess(resources.InitialIoScript.Data).WriteDword(NativeBattleDoom8ReaperAppearance.CursorAddress, 0x2200000).WriteDword(NativeBattleDoom8ModelDrawing.FarInputAddress, 0)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            NativeBattleSummonColors colors = rules.SummonColors ?? throw new InvalidDataException("Original reaper component requires its actual model color constants.");
            frame = NativeBattleDoom8Models.Initialize(frame, Address, Resource, 3, colors.Normal);
            frame = NativeBattleDoom8Models.Select(frame, Address, Resource, animation);
            NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, Address).WriteDword(0x60, Address + 0x114).WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
            Frame = NativeBattleDoom8Controller.Write(frame, Address, task);
            NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770() with { Y = 2048, Z = 16384 };
            Inputs = new(NativeBattlePackedMatrix32.FromMatrix(camera, null), new(camera, 256, 160, 108), rules.ShadowSource ?? throw new InvalidDataException("Original reaper component requires its actual shadow template."), NativeBattlePrimitiveCapabilities.PrimaryRgba8(), colors);
        }

        public NativeBattleDoom8ModelDrawStep Normal(NativeBattleDoom8Frame frame)
        {
            return NativeBattleDoom8ModelDrawing.Draw8CC4D0(frame, frame.Staging ?? throw new InvalidDataException("Component staging must remain current."), Address, NativeBattleDoom8Models.Read(frame, Address), Resource, Inputs);
        }

        public NativeBattleDoom8ModelDrawStep AdvanceNormal(NativeBattleDoom8Frame frame)
        {
            return Normal(NativeBattleDoom8Models.Advance(frame, Address, Resource, true).Frame);
        }

        public NativeBattleDoom8ModelDrawStep AdvanceAppearance(NativeBattleDoom8Frame frame)
        {
            frame = NativeBattleDoom8Models.Advance(frame, Address, Resource, true).Frame;
            return NativeBattleDoom8ModelDrawing.Draw8CC6B0(frame, frame.Staging ?? throw new InvalidDataException("Component staging must remain current."), Address, NativeBattleDoom8Models.Read(frame, Address), Resource, Inputs);
        }
    }

    private static NativeBattleGeometryPrimitive[] BodyPackets(NativeBattleGfGeometryBatch batch)
    {
        return batch.Attempts.Where(attempt => attempt.Primitive is { } primitive && (primitive.Words[1] >> 24 is 0x24 or 0x26 or 0x2C or 0x2E)).Select(attempt => attempt.Primitive!).ToArray();
    }

    private static void AssertLocalBones(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelResource resource, NativeBattleEffectOwnedModelState state)
    {
        ImmutableArray<NativeBattleActionMatrix> expected = NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource, state.DecodedPose!);
        uint skeleton = resource.Address + (uint)resource.Section(0);
        for (int index = 0; index < expected.Length; index++)
        {
            (byte[] bytes, bool[] known) = staging.ReadSnapshot(skeleton + 32u + (uint)(index * 48), 32);
            NativeBattlePackedMatrix32 packed = new(bytes.ToImmutableArray(), known.ToImmutableArray());
            Assert.Equal(NativeBattleSnapshotJson.Serialize(expected[index]), NativeBattleSnapshotJson.Serialize(packed.ReadMatrix()));
            short parent = BinaryPrimitives.ReadInt16LittleEndian(resource.PoseSource.Skeleton.AsSpan().Slice(16 + index * 48));
            Assert.Equal(parent < 0 && !state.DecodedPose!.HasScale, known[18]);
            Assert.Equal(known[18], known[19]);
        }
    }

    private static void AssertReleased(NativeBattleDoom8ModelDrawStep step)
    {
        Assert.Equal(NativeBattleTemporaryMemory.Address, Dword(step.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(NativeBattleSnapshotJson.Serialize(step.Staging), NativeBattleSnapshotJson.Serialize(step.Frame.Staging));
        Assert.NotNull(step.Frame.Memory.GeometryRegisters!.Sxy[2]);
    }

    private static NativeBattleDoom8Frame Cold(NativeBattleDoom8Frame frame)
    {
        NativeBattleDoom8Frame restored = NativeBattleSnapshotJson.Deserialize<NativeBattleDoom8Frame>(NativeBattleSnapshotJson.Serialize(frame))!;
        restored.State.Validate();
        return restored;
    }

    private static uint Dword(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);

    private static GameAssets OpenGame()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources to check the reaper model component.");
        return new(directory!);
    }
}
