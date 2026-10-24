using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect63Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect63Lower;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect63LowerTests
{
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(11, 12, 2)]
    public void Sprite_draw_and_release_precede_phase_and_terminal_motion(int phase, int next, int expectedResult)
    {
        Fixture f = new();
        f.Task(task => task.WriteByte(27, unchecked((byte)phase)).WriteWord(12, 10).WriteWord(14, 20).WriteWord(16, 2000).WriteWord(18, unchecked((ushort)-10)).WriteWord(20, 5).WriteWord(22, 2).WriteByte(24, unchecked((byte)-2)).WriteByte(25, 3).WriteByte(26, 4));
        NativeBattleEffect63Step step = Advance(f.Frame, Fixture.Common, f.Resources, f.Inputs);
        NativeBattleEffect63DrawBatch draw = Assert.Single(f.Draws);
        Assert.Equal(CursorGlobal, draw.CursorGlobal);
        Assert.Equal(phase, Word63(f.Temporary(step.Frame, 180), 4));
        Assert.Equal(expectedResult, step.Result);
        NativeBattleModulePoolTask result = Read(step.Frame, Fixture.Common);
        Assert.Equal(next, result.ReadByte(27));
        Assert.Equal(expectedResult == 2 ? 10 : 0, Signed(result, 12));
        Assert.Equal(expectedResult == 2 ? 20 : 25, Signed(result, 14));
        Assert.Equal(expectedResult == 2 ? -10 : -12, Signed(result, 18));
        Assert.Equal(f.Frame.Random, step.Frame.Random);
        Assert.Equal(f.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        Assert.Equal(0, Word63(f.Temporary(step.Frame, 180), 36));
    }

    [Theory]
    [InlineData(0, 4096)]
    [InlineData(1, 2731)]
    [InlineData(2, 1366)]
    [InlineData(3, 0)]
    public void Envelope_uses_native_zero_rise_and_private_packet_cursor(int age, int factor)
    {
        Fixture f = new();
        NativeBattleEffect63Frame result = Envelope6E7A30(f.Frame, age, 255, 255, 255, 0, 1, 3, f.Inputs);
        NativeBattleEffect63DrawBatch draw = Assert.Single(f.Draws);
        Assert.Equal(CursorGlobal, draw.CursorGlobal);
        Assert.Equal(24, draw.Batch.AllocationBytes);
        Assert.Equal((byte)(255 * factor >> 12), (byte)draw.Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Same(f.Frame.Memory, result.Memory);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 0, 1000)]
    [InlineData(1000, 1000, -1000)]
    public void Direction_keeps_native_angle_words_and_accepts_zero_endpoints(int x, int y, int z)
    {
        NativeBattleVector direction = Direction6E7920(x, y, z);
        Assert.Equal(0, direction.Z);
        Assert.Equal(unchecked((short)-NativeWorldScriptedPathMath.NativeAngle(x, z)), direction.Y);
        Assert.InRange(direction.X, (short)0, (short)4095);
    }

    [Theory]
    [InlineData(0, 0x30u)]
    [InlineData(1024, 0xF0u)]
    public void Beam_native_raw_GT4_flags_depth_and_window_use_exact_188_byte_temporary(int fade, uint flags)
    {
        Fixture f = new();
        NativeBattleEffect63Frame result = DrawBeam6E7790(f.Frame, Fixture.Point(0, 0, 2000), Fixture.Point(0, 0, 3000), 128, 0, 0, 4096, fade, f.Resources, f.Inputs);
        NativeBattleEffectArena physical = f.Temporary(result, 188);
        Assert.Equal(4096u, Dword63(physical, 0));
        Assert.Equal(1000u, Dword63(physical, 8));
        Assert.Equal(BeamAddress, Dword63(physical, 0x50));
        Assert.Equal(flags, Dword63(physical, 0x6C));
        Assert.Equal(-128, unchecked((int)Dword63(physical, 0x68)));
        Assert.Equal(0, Word63(physical, 0x72));
        Assert.Equal(128, Word63(physical, 0x82));
        Assert.Equal(f.Frame.Random, result.Random);
        Assert.Equal(f.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        NativeBattleEffect63DrawBatch draw = Assert.Single(f.Draws);
        NativeBattleGeometryPrimitive primitive = Assert.Single(draw.Batch.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!;
        Assert.Equal(12u, primitive.Words[0] >> 24);
        Assert.Equal(result.Memory.GeometryRegisters!.Sz.Select(value => unchecked((int)value!.Value)), primitive.VertexPayload4);
        Assert.Equal(CursorGlobal, draw.CursorGlobal);
        Assert.False(physical.ReadSnapshot(0x22, 2).Known[0]);
    }

    public sealed class Fixture
    {
        public const uint Common = 0x20DFAB8;
        public NativeBattleEffect63Frame Frame;
        public NativeBattleEffect63Resources Resources;
        public NativeBattleEffect63Inputs Inputs;
        public List<NativeBattleEffect63DrawBatch> Draws = [];

        public Fixture()
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, Common, SpriteStride, 30);
            (pool, int slot) = pool.Append(SpriteCallback);
            pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteBytes(12, new byte[16]).WriteWord(16, 2000)) };
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(memory, [pool]);
            Frame = new(memory, new(63, 63, new(63, Initializer, Entry), null, null) { Pools = [pool] }, new(1), NativeBattleEffect63State.FromColdProcess().WriteDword(WorkGlobal, Common).WriteDword(CursorGlobal, Common + FirstPacketOffset)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, Enumerable.Repeat((byte)0xA5, 532).ToArray()) };
            byte[] sprite = new byte[64];
            sprite[0] = 36;
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 12);
            for (int phase = 0; phase < 12; phase++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10 + phase * 2), 36);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(34), ushort.MaxValue);
            BinaryPrimitives.WriteUInt32LittleEndian(sprite.AsSpan(36), 1);
            sprite[40] = 4;
            sprite[41] = 128;
            sprite[42] = 4;
            sprite[43] = 0x2E;
            Resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(BeamAddress, Mesh(BeamAddress, false, 7)), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(SpriteAddress, new(63, SpriteAddress, (uint)sprite.Length, 0, sprite.ToImmutableArray())), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(FirstSelectorsAddress, ImmutableArray.Create<byte>(0x30, 0x30)).Add(SecondSelectorsAddress, ImmutableArray.Create<byte>(0x31, 0x30, 0x30, 0x30, 0x30, 0x30)));
            Inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => null!, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(identity, null), _ => new(null, 100, 160, 108), _ => 0, null!, null!, (frame, operation) =>
            {
                Draws.Add(Assert.IsType<NativeBattleEffect63DrawBatch>(operation));
                return frame;
            });
        }

        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change) => Frame = Write(Frame, Common, change(Read(Frame, Common)));
        public NativeBattleEffectArena Temporary(NativeBattleEffect63Frame frame, int length) => NativeBattleTemporaryMemory.ReadView(frame.Staging!, NativeBattleTemporaryMemory.Address, length);
        public static NativeBattleEffectArena Point(short x, short y, short z) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
        public static NativeBattleStaticMeshResource Mesh(uint address, bool shapes, int group, bool polygon = true)
        {
            int start = shapes ? 12 : 8;
            int shapeCount = shapes ? 2 : 1;
            int polygonOffset = start + shapeCount * 32;
            int faceLength = group == 7 ? 36 : 24;
            byte[] geometry = new byte[polygonOffset + 32 + (polygon ? faceLength : 0)];
            BinaryPrimitives.WriteInt32LittleEndian(geometry, polygonOffset);
            BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(4), 4);
            if (shapes)
            {
                BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(8), shapeCount);
            }
            short[] xyz = [-100, -100, 1000, 100, -100, 1000, -100, 100, 1000, 100, 100, 1000];
            for (int shape = 0; shape < shapeCount; shape++)
            {
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        BinaryPrimitives.WriteInt16LittleEndian(geometry.AsSpan(start + (shape * 4 + vertex) * 8 + axis * 2), unchecked((short)(xyz[vertex * 3 + axis] + (axis == 0 ? shape * 200 : 0))));
                    }
                }
            }
            if (polygon)
            {
                int countOffset = polygonOffset + group * 4;
                BinaryPrimitives.WriteInt32LittleEndian(geometry.AsSpan(countOffset), 1);
                int face = countOffset + 4;
                BinaryPrimitives.WriteUInt32LittleEndian(geometry.AsSpan(face), group == 7 ? 0x3C808080u : 0x2C808080u);
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(face + 4 + vertex * 2), (ushort)(vertex * 2));
                }
                if (group == 7)
                {
                    for (int index = 0; index < 3; index++)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(geometry.AsSpan(face + 12 + index * 4), 0x00808080);
                    }
                }
            }
            return new(0, address, geometry.ToImmutableArray());
        }
    }
}
