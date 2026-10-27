using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect301Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect301Lower;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect301LowerTests
{
    [Fact]
    public void New_body_record_keeps_reverse_map_uninserted_and_refreshes_actual_hint()
    {
        Fixture fixture = new();
        NativeBattleEffect301Frame result = NativeBattleEffect301Records.Birth66E930(fixture.Frame, 17, -23, 9);
        Assert.Equal(0xFF, ReadBytes(result, Fixture.Main + 0x4AC + 9, 1).Read(0, 1)[0]);
        Assert.Equal(Fixture.Main + 0xB4, Dword301(ReadBytes(result, Fixture.Main + 0xE0C, 4), 0));
        NativeBattleEffectArena row = ReadBytes(result, Fixture.Main + 0xAC, 8);
        Assert.Equal(17, Word301(row, 0));
        Assert.Equal(-23, Word301(row, 2));
        Assert.Equal(9, Word301(row, 4));
        Assert.Equal(12, Word301(row, 6));
    }

    [Fact]
    public void Full_record_bank_fails_without_consuming_point_or_current_GTE()
    {
        Fixture fixture = new();
        for (int index = 0; index < 128; index++)
        {
            fixture.Frame = WriteBytes(fixture.Frame, Fixture.Main + 0xB2 + (uint)index * 8, PutWord301(NativeBattleEffectArena.Clear(), 0, 1), 2);
        }
        NativeBattleEffect301Frame result = NativeBattleEffect301Records.Birth66E930(fixture.Frame, 1, 2, 3);
        Assert.Same(fixture.Frame, result);
    }

    [Fact]
    public void Record_draw_loads_X_zero_Z_and_life_death_updates_only_map_BYTE()
    {
        Fixture fixture = new();
        NativeBattleEffectArena row = PutWord301(PutWord301(PutWord301(PutWord301(NativeBattleEffectArena.Clear(), 0, 100), 2, 3000), 4, 7), 6, 1);
        fixture.Frame = WriteBytes(fixture.Frame, Fixture.Main + 0xAC, row, 8);
        NativeBattleEffect301Frame result = NativeBattleEffect301Records.Advance66DF20(fixture.Frame, Fixture.Main, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(100u, Dword301(temporary, 0x98));
        Assert.Equal(0u, Dword301(temporary, 0x9C));
        Assert.Equal(3000u, Dword301(temporary, 0xA0));
        Assert.Equal(0, Word301(ReadBytes(result, Fixture.Main + 0xB2, 2), 0));
        Assert.Equal(0xFF, ReadBytes(result, Fixture.Main + 0x4AC + 7, 1).Read(0, 1)[0]);
        Assert.IsType<NativeBattleEffect301DrawBatch>(Assert.Single(fixture.Operations));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Script_birth_uses_current_cursor_exact_partial_bytes_and_no_CRT(bool mesh)
    {
        Fixture fixture = new(mesh ? 0x66EC50u : 0x66EE60u, false);
        byte[] script = new byte[24];
        BinaryPrimitives.WriteInt16LittleEndian(script, 1);
        short[] fields = mesh ? [11, -12, 13, -1, -2, -3, 7, 8, -9, -10] : [11, -12, 13, -1, -2, -3];
        for (int index = 0; index < fields.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(script.AsSpan(2 + index * 2), fields[index]);
        }
        fixture.Resources = fixture.Resources with { StaticFronts = fixture.Resources.StaticFronts.SetItem(0x103BD00, script.ToImmutableArray()) };
        NativeBattleEffect301Frame result = mesh ? NativeBattleEffect301Particles.Birth66EB10(fixture.Frame, fixture.Resources, fixture.Inputs()) : NativeBattleEffect301Particles.Birth66ED70(fixture.Frame, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask task = Read(result, fixture.Address);
        Assert.Equal(11, Signed(task, 12));
        Assert.Equal(-12, Signed(task, 14));
        Assert.Equal(13, Signed(task, 16));
        Assert.Equal(0, Signed(task, 18));
        Assert.Equal(new byte[] { 255, 254, 253, 2 }, task.ReadBytes(20, 4));
        Assert.Equal(0x103BD00u + 2u + (uint)fields.Length * 2u, result.State.ReadDword(0x103DC9C));
        Assert.Equal(fixture.Frame.Random, result.Random);
    }

    [Fact]
    public void Mesh_callback_bounces_with_NEG_before_SAR_and_draws_before_motion()
    {
        Fixture fixture = new(0x66EC50);
        fixture.Task(task => task.WriteWord(14, unchecked((ushort)-4)).WriteWord(16, 3000).WriteBytes(20, [255, 3, 2, 2, 7, 8, 1, 2]));
        NativeBattleEffect301Step result = NativeBattleEffect301Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask task = Read(result.Frame, fixture.Address);
        Assert.Equal(-16, Signed(task, 12));
        Assert.Equal(8, Signed(task, 14));
        Assert.Equal(3032, Signed(task, 16));
        Assert.Equal(0, task.ReadBytes(21, 1)[0]);
        Assert.Equal(new byte[] { 8, 10 }, task.ReadBytes(24, 2));
        Assert.Equal(-4, result.Frame.Memory.GeometryRegisters!.Translation[1]);
        Assert.Single(fixture.Operations);
    }

    [Fact]
    public void Sprite_terminal_phase_still_moves_and_ignores_stored_gravity()
    {
        Fixture fixture = new(0x66EE60);
        fixture.Task(task => task.WriteWord(16, 3000).WriteWord(18, 15).WriteBytes(20, [255, 3, 2, 99]));
        NativeBattleEffect301Step result = NativeBattleEffect301Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, result.Result);
        NativeBattleModulePoolTask task = Read(result.Frame, fixture.Address);
        Assert.Equal(-1, Signed(task, 12));
        Assert.Equal(3, Signed(task, 14));
        Assert.Equal(3002, Signed(task, 16));
        Assert.Equal(3, task.ReadBytes(21, 1)[0]);
        Assert.Equal(16, task.ReadWord(18));
    }

    [Fact]
    public void Warm_script_keeps_current_pointer_and_faults_at_the_33rd_mandatory_append()
    {
        Fixture fixture = new(0x66EC50, false);
        byte[] current = new byte[642];
        BinaryPrimitives.WriteInt16LittleEndian(current, 1316);
        fixture.Resources = fixture.Resources with { StaticFronts = fixture.Resources.StaticFronts.Add(0x103CFB8, current.ToImmutableArray()) };
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State with { ScriptSourcePending = false, ScriptCursor4 = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x103CFB8u)) } };
        int reads = 0;
        NativeBattleEffect301Inputs inputs = fixture.Inputs() with { ReadModelArena9C = (_, _) =>
        {
            reads++;
            return NativeBattleEffectArena.Clear();
        } };
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => NativeBattleEffect301Particles.Birth66EB10(fixture.Frame, fixture.Resources, inputs));
        Assert.Contains("0x0103D23A", exception.Message);
        Assert.Equal(32 * 3, reads);
    }
    internal sealed class Fixture
    {
        public const uint Main = 0x20DFAB8;
        public NativeBattleEffect301Frame Frame;
        public uint Address;
        public NativeBattleEffect301Resources Resources;
        public List<NativeBattleEffect301Operation> Operations = [];
        public Fixture(uint callback = 0x66DBD0, bool append = true)
        {
            bool main = callback == 0x66DBD0;
            bool mesh = callback == 0x66EC50;
            ushort stride = main ? (ushort)0xE14 : mesh ? (ushort)28 : (ushort)24;
            uint descriptor = main ? 0x2507708u : mesh ? 0x25076F8u : 0x25076E8u;
            Address = Main + (main ? 0u : mesh ? 0xE14u : 0x1194u);
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, Address, stride, main ? (ushort)1 : (ushort)32);
            if (append)
            {
                (pool, int slot) = pool.Append(callback);
                pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[stride - 12]));
            }
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, [pool]);
            Frame = new(memory, new(301, 300, new(301, 0x66D8E0, 0x66D8F0), null, null) { Pools = [pool] }, new(42), NativeBattleEffect301State.FromColdProcess().WriteDword(0x2507754, Main).WriteDword(0x25076E0, 0x1D97494)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            byte[] normal = new byte[48];
            BinaryPrimitives.WriteUInt16LittleEndian(normal.AsSpan(8), 16);
            for (int index = 0; index < 16; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(normal.AsSpan(10 + index * 2), 44);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(normal.AsSpan(42), 0xFFFF);
            byte[] projected = new byte[68];
            BinaryPrimitives.WriteUInt16LittleEndian(projected.AsSpan(8), 13);
            for (int index = 0; index < 13; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(projected.AsSpan(10 + index * 2), 40);
            }
            byte[] empty = new byte[40];
            BinaryPrimitives.WriteInt32LittleEndian(empty, 8);
            Resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(0x103D314, new(0, 0x103D314, empty.ToImmutableArray())), ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(0x103D118, new(5, 0x103D118, 48, 0, normal.ToImmutableArray())).Add(0x103CFB8, new(5, 0x103CFB8, 68, 0, projected.ToImmutableArray())), null!, BitConverter.GetBytes(0x103BD00u).ToImmutableArray(), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
            if (main && append)
            {
                Frame = WriteBytes(Frame, Main + 0x4AC, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)255, 2400).ToArray()), 2400);
                Frame = WriteBytes(Frame, Main + 0xE0C, Put301(NativeBattleEffectArena.Clear(), 0, Main + 0xAC), 4);
            }
        }
        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update) => Frame = Write(Frame, Address, update(Read(Frame, Address)));
        public NativeBattleEffect301Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), _ => 0, null!, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
