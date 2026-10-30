using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect285Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect285Lower;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect285LowerTests
{
    [Fact]
    public void Camera_scales_Y_twice_with_intermediate_WORD_and_retains_eye_padding()
    {
        Fixture fixture = new();
        fixture.Eye = Point(1000, 1234, 2000).MarkUnknown(6, 2);
        fixture.Target = Point(100, -200, 300);
        NativeBattleEffectMemory memory = fixture.Frame.Memory;
        NativeBattleEffect285Frame result = Camera67B5C0(fixture.Frame, 0, 0, 4040, fixture.Resources, fixture.Inputs());
        NativeBattleEffect285CameraPoint operation = Assert.IsType<NativeBattleEffect285CameraPoint>(Assert.Single(fixture.Operations));
        Assert.True(operation.Eye);
        Assert.False(operation.CopyPadding);
        Assert.Equal(new NativeBattleVector(987, 1194, 1976), Vector(operation.Point8));
        Assert.DoesNotContain(true, operation.Point8.ReadSnapshot(6, 2).Known);
        Assert.Same(memory, result.Memory);
        Assert.Equal(new NativeBattleVector(100, -200, 300), Vector(fixture.Target));
    }

    [Fact]
    public void Camera_uses_individual_native_Y_axis_sign_and_full_target_translation()
    {
        Fixture fixture = new();
        fixture.Eye = Point(1000, 1234, 2000);
        fixture.Target = Point(100, -200, 300);
        Camera67B5C0(fixture.Frame, 0, 1024, 4096, fixture.Resources, fixture.Inputs());
        NativeBattleEffect285CameraPoint operation = Assert.IsType<NativeBattleEffect285CameraPoint>(Assert.Single(fixture.Operations));
        Assert.Equal(new NativeBattleVector(1800, 1234, -600), Vector(operation.Point8));
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 0, 2)]
    public void Visibility_preserves_stage_argument_bit_and_descending_model_partial_widths(int argument, byte stageOr, int width)
    {
        Fixture fixture = new();
        for (int actor = 0; actor < 7; actor++)
        {
            fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteView(0x2507D90 + (uint)(actor * 4), NativeBattleEffectArena.Clear().Write(0, [(byte)((actor & 1) * 4)]).MarkUnknown(1, 3), 4) };
        }
        Visibility67B6C0(fixture.Frame, argument, fixture.Inputs());
        Assert.Equal(new NativeBattleEffect285StageFlags(0xFD, stageOr), fixture.Operations[0]);
        Assert.Equal(8, fixture.Operations.Count);
        for (int index = 0; index < 7; index++)
        {
            int actor = 6 - index;
            NativeBattleEffect285ModelFlags operation = Assert.IsType<NativeBattleEffect285ModelFlags>(fixture.Operations[index + 1]);
            Assert.Equal(0x1D972C0u + (uint)(actor * 0x9C), operation.ModelAddress);
            Assert.Equal(width, operation.Width);
            Assert.Equal(argument == 0 ? (ushort)0xFF : (ushort)0xFFFB, operation.AndMask);
            Assert.Equal(argument == 0 ? (ushort)4 : (ushort)((actor & 1) * 4), operation.OrMask);
        }
    }

    [Fact]
    public void Bonepoint_wrapper_uses_current_caster_and_preserves_full_provider_frame()
    {
        Fixture fixture = new();
        NativeBattleEffect285PointStep result = BonePoint67B560(fixture.Frame, Point(1, 2, 3).Write(6, BitConverter.GetBytes((short)10)), Point(4, 5, 6).Write(6, BitConverter.GetBytes((ushort)0xCAFE)), fixture.Inputs());
        Assert.Equal(0x1D97494u, fixture.LastBoneModel);
        Assert.Equal(777u, result.Frame.State.ReadDword(0x2507D64));
        Assert.Equal((ushort)0xCAFE, BinaryPrimitives.ReadUInt16LittleEndian(result.Output8.Read(6, 2)));
        Assert.Equal((short)321, Word285(result.Output8, 0));
    }

    [Theory]
    [InlineData(5, 0, 6)]
    [InlineData(6, 2, 7)]
    public void Sprite_transports_phase_as_input_W_and_draws_before_final_increment(int phase, int resultCode, int next)
    {
        Fixture fixture = new();
        fixture.Task(task => task.WriteWord(16, 4000).WriteWord(18, (ushort)phase));
        NativeBattleEffect285Step result = NativeBattleEffect285Lower.Advance(fixture.Frame, Fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(resultCode, result.Result);
        Assert.Equal(next, Read(result.Frame, Fixture.Address).ReadWord(18));
        Assert.Equal((short)phase, BinaryPrimitives.ReadInt16LittleEndian(result.Frame.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan().Slice(6, 2)));
        Assert.Equal(3488, result.Frame.Memory.ModuleSpriteBillboard21DFED0!.ReadMatrix().Z);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Frame.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(phase, Word285(temporary, 4));
        Assert.Equal(0, Word285(temporary, 0x24));
        Assert.Equal(0x2507D60u, Assert.IsType<NativeBattleEffect285DrawBatch>(Assert.Single(fixture.Operations)).CursorGlobal);
    }

    [Fact]
    public void Presentation_sprite_preserves_native_phase_W_registers_row_and_packet_ledger()
    {
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 512, 160, 108);
        Fixture nativeFixture = new();
        SetPositiveSprite(nativeFixture);
        nativeFixture.Task(task => task.WriteWord(16, 4000).WriteWord(18, 2));
        NativeBattleEffect285Step native = NativeBattleEffect285Sprite.Advance(nativeFixture.Frame, Fixture.Address, nativeFixture.Resources, nativeFixture.Inputs() with { ReadProjection = _ => projection });
        string nativeFrame = NativeBattleTestSnapshots.Json(native.Frame);
        string nativeOperations = NativeBattleTestSnapshots.Json(nativeFixture.Operations);
        Fixture capturedFixture = new();
        SetPositiveSprite(capturedFixture);
        capturedFixture.Task(task => task.WriteWord(16, 4000).WriteWord(18, 2));
        NativeBattleEffect285Step captured = NativeBattleEffect285Sprite.Advance(capturedFixture.Frame, Fixture.Address, capturedFixture.Resources, capturedFixture.Inputs() with { ReadProjection = _ => projection, CapturePresentation = true });
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(nativeOperations, NativeBattleTestSnapshots.Json(capturedFixture.Operations));
        NativeBattleGfGeometryBatch batch = Assert.IsType<NativeBattleEffect285DrawBatch>(Assert.Single(capturedFixture.Operations)).Batch;
        NativeBattleGfGeometryBatch nativeBatch = Assert.IsType<NativeBattleEffect285DrawBatch>(Assert.Single(nativeFixture.Operations)).Batch;
        NativeBattleGeometryPrimitive nativePrimitive = Assert.Single(nativeBatch.Attempts, attempt => attempt.Primitive is not null).Primitive!;
        Assert.Null(nativePrimitive.PilotGeometry);
        Assert.Equal(new[] { 3488, 3488, 3488, 3488 }, nativePrimitive.VertexPayload4);
        Assert.Equal(40, nativeBatch.AllocationBytes);
        Assert.Equal((ushort)3, Read(captured.Frame, Fixture.Address).ReadWord(18));
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(captured.Frame.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal((short)2, Word285(temporary, 4));
        Assert.Equal(1u, Dword285(temporary, 0x30));
        Assert.Equal(0u, Dword285(temporary, 0x40) & 0x60000);
        NativeBattlePilotVisualGeometry geometry = Assert.Single(batch.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!.PilotGeometry!;
        Assert.Equal(new System.Numerics.Vector3(0, 0, 3488), geometry.A);
        Assert.Equal(new System.Numerics.Vector3(16, 0, 3488), geometry.B);
        Assert.Equal(new System.Numerics.Vector3(0, 16, 3488), geometry.C);
        Assert.Equal(new System.Numerics.Vector3(16, 16, 3488), geometry.D);
        Assert.Equal((ushort)285, geometry.Identity.Effect);
        Assert.True(geometry.Identity.Generation > 0);
        Assert.Equal(0x1084AF8, geometry.Identity.ResourceGeneration);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured.Frame));

        static void SetPositiveSprite(Fixture fixture)
        {
            NativeBattleModuleResource original = fixture.Resources.Sprites[0x1084AF8];
            byte[] bytes = new byte[50];
            original.Bytes.AsSpan().CopyTo(bytes);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(26), 1);
            bytes[30] = bytes[32] = 2;
            bytes[31] = 128;
            bytes[33] = 0x2C;
            var sprite = original with { Bytes = bytes.ToImmutableArray() };
            fixture.Resources = fixture.Resources with { Sprites = fixture.Resources.Sprites.SetItem(sprite.Address, sprite) };
        }
    }

    internal static NativeBattleEffectArena Point(short x, short y, short z) => PutWord285(PutWord285(PutWord285(NativeBattleEffectArena.Clear(), 0, x), 2, y), 4, z);

    internal sealed class Fixture
    {
        public const uint Arena = 0x20DFAB8;
        public const uint Address = Arena;
        public NativeBattleEffect285Frame Frame;
        public NativeBattleEffect285Resources Resources;
        public NativeBattleEffectArena Eye = Point(1000, 2000, 3000);
        public NativeBattleEffectArena Target = Point(0, 0, 0);
        public List<NativeBattleEffect285Operation> Operations = [];
        public uint LastBoneModel;
        public NativeBattlePackedMatrix32 View = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);

        public Fixture()
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2507D18, Arena, 20, 64);
            (pool, int slot) = pool.Append(0x67B720);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[8]));
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, [pool]);
            Frame = new(memory, new(285, 284, new(285, 0x67A870, 0x67A860), null, null) { Pools = [pool] }, new(42), NativeBattleEffect285State.FromColdProcess().WriteDword(0x2507D68, Arena).WriteDword(0x2507D10, 0x1D97494)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            byte[] sprite = new byte[30];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 7);
            for (int index = 0; index < 7; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + index * 2), 26);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(24), 0xFFFF);
            Resources = new(null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(0x1084AF8, new(5, 0x1084AF8, 30, 0, sprite.ToImmutableArray())), ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
        }

        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change) => Frame = Write(Frame, Address, change(Read(Frame, Address)));
        public NativeBattleEffect285Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, null!, _ => View, _ => new(null, 512, 160, 108), _ => new(Eye, Target, Eye, Target, 512, 512, 0), (frame, model, _, retained) =>
        {
            LastBoneModel = model;
            return new(frame with { State = frame.State.WriteDword(0x2507D64, 777) }, PutWord285(retained, 0, 321));
        }, null!, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
