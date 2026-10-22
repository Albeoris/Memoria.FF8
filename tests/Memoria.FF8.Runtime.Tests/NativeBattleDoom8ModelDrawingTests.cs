using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoom8ModelDrawingTests
{
    [Fact]
    public void Normal_body_publishes_physical_context_FAR_bones_scratch_and_private_cursors()
    {
        Fixture fixture = new();
        NativeBattleDoom8ModelDrawStep result = fixture.Draw();
        Assert.Equal(32, result.Batch.AllocationBytes);
        Assert.Single(result.Batch.Attempts);
        Assert.Equal(0x2000020u, result.Frame.State.ReadDword(NativeBattleDoom8ReaperAppearance.CursorAddress));
        NativeBattleModelRendererRegisters registers = result.Frame.Memory.PrimitiveProcess!.ModelRendererRegisters!;
        Assert.Equal((uint?)0x800, registers.FarRed);
        Assert.Equal((uint?)0x800, registers.FarGreen);
        Assert.Equal((uint?)0x800, registers.FarBlue);
        Assert.Equal((uint?)0x2000020, registers.PacketCursor);
        Assert.Equal((uint?)(fixture.Resource.Address + 148), registers.SourceCursor);
        Assert.Equal(JsonSerializer.Serialize(fixture.Frame.Memory.ScriptedColorRegisters), JsonSerializer.Serialize(result.Frame.Memory.ScriptedColorRegisters));
        Assert.Equal(new int?[] { 0, 0, 2000 }, result.Frame.Memory.GeometryRegisters!.Translation);
        Assert.Null(result.Frame.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8Controller.Read(result.Frame, fixture.Address).ReadWord(0x82));
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging, NativeBattleTemporaryMemory.Address, 0x4C);
        Assert.Equal(fixture.Resource.Address + 88 + 44, U32(temporary.Read(0x20, 4)));
        Assert.Equal(NativeBattleDoom8ModelDrawing.ProjectedAddress, U32(temporary.Read(0x24, 4)));
        Assert.Equal(0x11223344u, U32(temporary.Read(0x30, 4)));
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }, temporary.Read(0x28, 8));
        Assert.Equal(new byte[] { 60, 60, 60 }, temporary.Read(0x48, 3));
        Assert.Throws<NotSupportedException>(() => temporary.Read(0x4B, 1));
        Assert.Throws<NotSupportedException>(() => temporary.Read(18, 2));
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        uint bone = fixture.Resource.Address + 24 + 32;
        Assert.Equal(0, I32(result.Staging.Read(bone + 28, 4)));
        Assert.Equal(new byte[] { 0, 0 }, result.Staging.Read(bone + 18, 2));
        Assert.NotEqual(new byte[24], result.Frame.State.Read(NativeBattleDoom8ModelDrawing.ProjectedAddress, 24));
        Assert.Equal(JsonSerializer.Serialize(result.Staging), JsonSerializer.Serialize(result.Frame.Staging));
    }

    [Fact]
    public void Nonzero_secondary_color_retains_actual_temporary_bytes_without_a_second_packet_pass()
    {
        Fixture fixture = new();
        NativeBattleDoom8ModelDrawStep faded = fixture.Draw();
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(fixture.Frame, fixture.Address).WriteByte(0x37, 0);
        NativeBattleDoom8Frame unfaded = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, task);
        NativeBattleDoom8ModelDrawStep ordinary = fixture.Draw(unfaded);
        Assert.Equal(JsonSerializer.Serialize(ordinary.Batch), JsonSerializer.Serialize(faded.Batch));
        Assert.Equal(JsonSerializer.Serialize(ordinary.Frame.Memory), JsonSerializer.Serialize(faded.Frame.Memory));
        Assert.Equal(new byte[] { 0, 0, 0 }, NativeBattleTemporaryMemory.ReadView(ordinary.Staging, NativeBattleTemporaryMemory.Address, 0x4C).Read(0x48, 3));
        Assert.Equal(new byte[] { 60, 60, 60 }, NativeBattleTemporaryMemory.ReadView(faded.Staging, NativeBattleTemporaryMemory.Address, 0x4C).Read(0x48, 3));
    }

    [Fact]
    public void Masked_body_still_publishes_primary_FAR_and_restores_local_bones_without_reading_projected_bytes()
    {
        Fixture fixture = new();
        NativeBattleDoom8Frame frame = NativeBattleDoom8Controller.Write(fixture.Frame, fixture.Address, NativeBattleDoom8Controller.Read(fixture.Frame, fixture.Address).WriteDword(0xAC, 0));
        frame = frame with { State = frame.State with { Globals = frame.State.Globals.MarkUnknown(checked((int)(NativeBattleDoom8ModelDrawing.ProjectedAddress - NativeBattleDoom8State.BaseAddress)), NativeBattleDoom8ModelDrawing.ProjectedLength) } };
        NativeBattleDoom8ModelDrawStep result = fixture.Draw(frame);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal((uint?)0x800, result.Frame.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        Assert.Throws<NotSupportedException>(() => result.Frame.State.Read(NativeBattleDoom8ModelDrawing.ProjectedAddress, 1));
        Assert.Equal(JsonSerializer.Serialize(frame.Memory.GeometryRegisters), JsonSerializer.Serialize(result.Frame.Memory.GeometryRegisters));
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Body_shadow_is_after_body_and_uses_the_same_private_cursor_with_real_nested_temporary()
    {
        Fixture fixture = new();
        NativeBattleDoom8Frame frame = fixture.WithShadow(fixture.Frame);
        NativeBattleDoom8ModelDrawStep result = fixture.Draw(frame);
        Assert.True(result.Batch.Attempts.Length > 1);
        Assert.Equal(32, result.Batch.Attempts[1].ByteOffsetRelative);
        Assert.All(result.Batch.Attempts.Skip(1).Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(4121, attempt.Primitive!.Bucket));
        Assert.Equal(0x2000000u + (uint)result.Batch.AllocationBytes, result.Frame.State.ReadDword(NativeBattleDoom8ReaperAppearance.CursorAddress));
        Assert.Equal((uint?)0x2000020, result.Frame.Memory.PrimitiveProcess!.ModelRendererRegisters!.PacketCursor);
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(0u, U32(result.Staging.Read(0xB8B9E4, 4)));
        Assert.Equal((uint?)0, result.Frame.Memory.ScriptedColorRegisters!.Flags);
    }

    [Fact]
    public void Appearance_orders_shadow_before_custom_draw_and_restores_local_source_bones_afterward()
    {
        Fixture fixture = new();
        NativeBattleDoom8ModelDrawStep normal = fixture.Draw(fixture.WithShadow(fixture.Frame));
        NativeBattleDoom8Frame frame = NativeBattleDoom8ReaperAppearance.Initialize8CD020(normal.Frame, fixture.Address + 0x30);
        frame = frame with { State = frame.State.Write(NativeBattleDoom8ReaperAppearance.ScratchAddress + 0x5D, [19]) };
        NativeBattleDoom8ModelDrawStep result = NativeBattleDoom8ModelDrawing.Draw8CC6B0(frame, normal.Staging, fixture.Address, fixture.Model, fixture.Resource, fixture.Inputs);
        Assert.True(result.Batch.Attempts.Length > 1);
        Assert.Equal(4112, result.Batch.Attempts[0].Primitive!.Bucket);
        Assert.Equal(0x24808080u, result.Batch.Attempts[^1].Primitive!.Words[1]);
        Assert.Equal(0, I32(result.Staging.Read(fixture.Resource.Address + 24 + 32 + 28, 4)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleDoom8ModelDrawing.Draw8CC6B0(JsonSerializer.Deserialize<NativeBattleDoom8Frame>(JsonSerializer.Serialize(frame))!, normal.Staging, fixture.Address, fixture.Model, fixture.Resource, fixture.Inputs)));
    }

    [Fact]
    public void Source_and_pose_are_required_and_draw_never_seeds_or_hydrates_unknown_resource_bytes()
    {
        Fixture fixture = new();
        Assert.Throws<NotSupportedException>(() => NativeBattleDoom8ModelDrawing.Draw8CC4D0(fixture.Frame, NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), fixture.Address, fixture.Model, fixture.Resource, fixture.Inputs));
        NativeBattleEffectOwnedModelState changed = fixture.Model with { DecodedPose = fixture.Model.DecodedPose! with { Translation = new(1, 2, 3) } };
        Assert.Throws<InvalidDataException>(() => NativeBattleDoom8ModelDrawing.Draw8CC4D0(fixture.Frame, fixture.Staging, fixture.Address, changed, fixture.Resource, fixture.Inputs));
        NativeBattleFileStaging published = NativeBattleDoom8ModelDrawing.PublishDecodedPose(fixture.Staging, changed, fixture.Resource);
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(published.Read(fixture.Resource.Address + 24 + 8, 2)));
        NativeBattleFileStaging restored = NativeBattleDoom8ModelDrawing.PrepareLocalBones508C90(published, changed, fixture.Resource);
        Assert.Equal(16, I32(restored.Read(fixture.Resource.Address + 24 + 32 + 20, 4)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, U32(restored.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Neutral_shadow_overload_matches_the_existing_actor_contract_without_a_model_adapter()
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleEffectLocalBounds bounds = new(new(-100, 0, -100), new(100, 0, 100));
        NativeBattleGraphicsModel actor = new(3, 3) { GeometryWorldMatrix = identity, Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096), GeometryBounds52 = bounds, Color44 = 0x32808080 };
        NativeBattleShadowSource source = new(identity, new(4096, 0, 4096), Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 10).ToImmutableArray());
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleCameraProjection projection = new(identity with { Z = 2000 }, 100, 160, 108);
        NativeBattleShadowDrawStep existing = NativeBattleShadowDrawing.Prepare5088A0(staging, registers, actor, projection, NativeBattleScriptedGraphicsMemory.FromColdProcess(), source, true, 4121, 16);
        NativeBattleShadowDrawStep neutral = NativeBattleShadowDrawing.Prepare5088A0(staging, registers, identity, 0, bounds, 0x32808080, projection, NativeBattleScriptedGraphicsMemory.FromColdProcess(), source, true, 4121, 16);
        Assert.Equal(JsonSerializer.Serialize(existing), JsonSerializer.Serialize(neutral));
    }

    private sealed class Fixture
    {
        public uint Address { get; }
        public NativeBattleDoom8Frame Frame { get; }
        public NativeBattleFileStaging Staging { get; }
        public NativeBattleEffectOwnedModelResource Resource { get; }
        public NativeBattleEffectOwnedModelState Model { get; }
        public NativeBattleDoom8ModelDrawInputs Inputs { get; }

        public Fixture()
        {
            byte[] bytes = new byte[156];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
            int[] sections = [24, 88, 148, 152, 156];
            for (int index = 0; index < sections.Length; index++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), sections[index]);
            }
            bytes[24] = 1;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -1);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(88), 1);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(92), 8);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(96), 1);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(100), 3);
            short[] positions = [-100, -100, 0, 100, -100, 0, -100, 100, 0];
            for (int index = 0; index < positions.Length; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(102 + index * 2), positions[index]);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(120), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(132), 0x8000);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(134), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(136), 2);
            NativeBattlePoseSource source = new(bytes.AsSpan(24, 64).ToArray().ToImmutableArray(), [1]);
            Resource = new(0x1636CE8, bytes.ToImmutableArray(), source, NativeBattleRawGeometryReader.Read(bytes.AsSpan(88, 60).ToArray(), 1));
            NativeBattleEffectHierarchy hierarchy = new(NativeBattleDoom8State.Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(8, 0x8CC3A0, 320);
            Address = appended.Address;
            NativeBattleEffectOwnedModelStep initialized = NativeBattleEffectOwnedModel.Initialize8DD0F0(appended.Hierarchy.Read(Address), Address, Resource, 3, 0x32808080);
            NativeBattleModulePoolTask task = initialized.Task.WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096).WriteByte(0x30, (byte)(initialized.Task.ReadByte(0x30) | 0x20)).WriteByte(0x37, 60);
            hierarchy = appended.Hierarchy.Write(Address, task);
            Model = initialized.State with { DecodedPose = new(new(0, 0, 0), [new(new(0, 0, 0), new(1024, 1024, 1024))], false) };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
            memory = memory with { ScriptedGraphics = memory.ScriptedGraphics!.InitializeGeom56BAA0() };
            NativeBattleOriginalEffectState effect = new(8, 8, new(8, NativeBattleDoom8Controller.Entry, 0x8C4960), null, null) { Pools = hierarchy.Pools };
            Frame = new(memory, effect, new(1), NativeBattleDoom8State.FromColdProcess(new byte[112].ToImmutableArray()).WriteDword(NativeBattleDoom8ReaperAppearance.CursorAddress, 0x2000000).WriteDword(NativeBattleDoom8ModelDrawing.FarInputAddress, 0x11223344));
            Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(Resource.Address, Resource.Bytes.AsSpan());
            Staging = NativeBattleDoom8ModelDrawing.PublishDecodedPose(Staging, Model, Resource);
            Frame = Frame with { Staging = Staging };
            NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
            NativeBattleShadowSource shadow = new(NativeBattleCuragaMatrices.Identity8DD770(), new(4096, 0, 4096), Enumerable.Repeat(new NativeBattleVector(0, 0, 0), 10).ToImmutableArray());
            Inputs = new(NativeBattlePackedMatrix32.FromMatrix(view, null), new(view, 100, 160, 108), shadow, new(true) { HardwareEnabled = true }, NativeBattleGteColorInterpolation.CodeConstants45F270);
        }

        public NativeBattleDoom8Frame WithShadow(NativeBattleDoom8Frame frame)
        {
            NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, Address);
            return NativeBattleDoom8Controller.Write(frame, Address, task.WriteByte(0x30, (byte)(task.ReadByte(0x30) & ~0x20)));
        }

        public NativeBattleDoom8ModelDrawStep Draw(NativeBattleDoom8Frame? frame = null)
        {
            return NativeBattleDoom8ModelDrawing.Draw8CC4D0(frame ?? Frame, Staging, Address, Model, Resource, Inputs);
        }
    }

    private static uint U32(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    private static int I32(byte[] bytes) => BinaryPrimitives.ReadInt32LittleEndian(bytes);
}
