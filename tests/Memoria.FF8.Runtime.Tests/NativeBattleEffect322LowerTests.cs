using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect322Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect322LowerTests
{
    [Theory]
    [InlineData(10, 0, 11)]
    [InlineData(11, 2, 12)]
    public void Puff_draws_current_phase_then_retires_after_twelve(int phase, int result, int next)
    {
        Fixture fixture = new(0x65B600);
        fixture.Task(task => task.WriteWord(0x12, (ushort)phase).WriteWord(0x14, 4000));
        NativeBattleEffect322Step step = NativeBattleEffect322Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(result, step.Result);
        Assert.Equal(next, Read(step.Frame, fixture.Address).ReadWord(0x12));
        NativeBattleEffectArena temp = NativeBattleTemporaryMemory.ReadView(step.Frame.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(phase, NativeBattleEffect322Lower.Word322(temp, 4));
        Assert.Equal(0u, (uint)temp.Read(0x24, 2)[0]);
        Assert.Single(fixture.Operations);
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
    }

    [Theory]
    [InlineData(6, 0, 110, 13)]
    [InlineData(7, 2, 100, 10)]
    public void Oriented_sprite_moves_by_old_velocity_and_terminal_draw_skips_motion(int phase, int result, int x, int velocity)
    {
        Fixture fixture = new(0x65B680);
        fixture.Task(task => task.WriteWord(12, 100).WriteWord(16, 4000).WriteWord(0x12, (ushort)phase).WriteWord(0x14, 10).WriteWord(0x1C, 3));
        NativeBattleEffect322Step step = NativeBattleEffect322Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask current = Read(step.Frame, fixture.Address);
        Assert.Equal(result, step.Result);
        Assert.Equal(x, current.ReadWord(12));
        Assert.Equal(velocity, current.ReadWord(0x14));
        NativeBattleEffectArena temp = NativeBattleTemporaryMemory.ReadView(step.Frame.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(1, NativeBattleEffect322Lower.Word322(temp, 0x24));
        Assert.Single(fixture.Operations);
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Actual_572200_mesh_resets_depth_and_draws_an_actual_quad()
    {
        Fixture fixture = new(0x65BB20);
        fixture.Task(task => task.WriteBytes(0x10, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null).Bytes.AsSpan()).WriteWord(14, 0));
        NativeBattleStaticMeshResource mesh = new(0, 0xFE5A50, NativeBattleActDepthRoutingTests.Geometry(false));
        fixture.Resources = fixture.Resources with { Meshes = fixture.Resources.Meshes.Add(mesh.Address, mesh) };
        NativeBattleEffect322Step step = NativeBattleEffect322Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        NativeBattleEffect322DrawBatch delivery = Assert.IsType<NativeBattleEffect322DrawBatch>(Assert.Single(fixture.Operations));
        Assert.Equal(17 + ((int)step.Frame.Memory.GeometryRegisters!.Otz!.Value >> 2), Assert.Single(delivery.Batch.Attempts).Primitive!.Bucket);
        NativeBattleEffectArena temp = NativeBattleTemporaryMemory.ReadView(step.Frame.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(0u, NativeBattleEffect322Lower.Dword322(temp, 0x18));
        Assert.Equal(1, Read(step.Frame, fixture.Address).ReadWord(12));
    }

    internal sealed class Fixture
    {
        public const uint Arena = 0x20DFAB8;
        public NativeBattleEffect322Frame Frame;
        public uint Address;
        public NativeBattleEffect322Resources Resources;
        public List<NativeBattleEffect322Operation> Operations = [];


        public Fixture(uint callback, bool append = true)
        {
            ushort stride = callback == 0x65B680 ? (ushort)44 : callback == 0x65BB20 ? (ushort)48 : (ushort)24;
            Address = Arena + 0x9A4;
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25052C0, Address, stride, 1);
            if (append)
            {
                (pool, int slot) = pool.Append(callback);
                pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[pool.Stride - 12]));
            }
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, [pool]);
            NativeBattleEffect322State state = NativeBattleEffect322State.FromColdProcess().WriteDword(0x2505340, Arena);
            Frame = new(memory, new(322, 321, new(322, 0x65AB70, 0x65AB80), null, null) { Pools = [pool] }, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            ImmutableDictionary<uint, NativeBattleModuleResource> sprites = ImmutableDictionary<uint, NativeBattleModuleResource>.Empty;
            foreach ((uint source, int count) in new (uint, int)[] { (0xFE4384, 12), (0xFE42A8, 8) })
            {
                int end = 10 + (count + 1) * 2;
                byte[] spriteData = new byte[end + 4];
                BinaryPrimitives.WriteUInt16LittleEndian(spriteData.AsSpan(8), (ushort)count);
                for (int index = 0; index < count; index++)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(spriteData.AsSpan(10 + index * 2), (ushort)end);
                }
                BinaryPrimitives.WriteUInt16LittleEndian(spriteData.AsSpan(10 + count * 2), 0xFFFF);
                sprites = sprites.Add(source, new(5, source, (uint)spriteData.Length, 0, spriteData.ToImmutableArray()));
            }
            Resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, sprites, ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);

        }

        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change) => Frame = Write(Frame, Address, change(Read(Frame, Address)));
        public NativeBattleEffect322Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, null!, null!, null!, null!, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
