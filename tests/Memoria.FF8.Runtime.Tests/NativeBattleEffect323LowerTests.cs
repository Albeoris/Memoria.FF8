using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect323Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect323LowerTests
{
    [Theory]
    [InlineData(8, 0, 110, 13)]
    [InlineData(9, 2, 100, 10)]
    public void Sprite_draws_then_moves_by_old_velocity_or_retires_before_motion(int phase, int result, int x, int velocity)
    {
        Fixture fixture = new(0x65A660);
        fixture.Task(task => task.WriteWord(12, 100).WriteWord(16, 4000).WriteWord(0x12, (ushort)phase).WriteWord(0x14, 10).WriteWord(0x1C, 3));
        NativeBattleEffect323Step step = NativeBattleEffect323Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask current = Read(step.Frame, fixture.Address);
        Assert.Equal(result, step.Result);
        Assert.Equal(x, current.ReadWord(12));
        Assert.Equal(velocity, current.ReadWord(0x14));
        Assert.Equal(phase + 1, current.ReadWord(0x12));
        NativeBattleEffectArena temp = NativeBattleTemporaryMemory.ReadView(step.Frame.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(phase, NativeBattleEffect323Lower.Word323(temp, 4));
        Assert.Equal(0, NativeBattleEffect323Lower.Word323(temp, 0x24));
        Assert.Single(fixture.Operations);
        Assert.Equal(fixture.Frame.Random, step.Frame.Random);
    }

    [Fact]
    public void Actual_572200_mesh_resets_depth_and_draws_an_actual_quad()
    {
        Fixture fixture = new(0x65AA80);
        fixture.Task(task => task.WriteBytes(0x10, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null).Bytes.AsSpan()).WriteWord(14, 0));
        NativeBattleStaticMeshResource mesh = new(0, 0xFE2974, NativeBattleActDepthRoutingTests.Geometry(false));
        fixture.Resources = fixture.Resources with { Meshes = fixture.Resources.Meshes.Add(mesh.Address, mesh) };
        NativeBattleEffect323Step step = NativeBattleEffect323Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        NativeBattleEffect323DrawBatch delivery = Assert.IsType<NativeBattleEffect323DrawBatch>(Assert.Single(fixture.Operations));
        Assert.Equal(17 + ((int)step.Frame.Memory.GeometryRegisters!.Otz!.Value >> 2), Assert.Single(delivery.Batch.Attempts).Primitive!.Bucket);
        NativeBattleEffectArena temp = NativeBattleTemporaryMemory.ReadView(step.Frame.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(0u, NativeBattleEffect323Lower.Dword323(temp, 0x18));
        Assert.Equal(1, Read(step.Frame, fixture.Address).ReadWord(12));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Contact_walk_reloads_current_header_and_keeps_duplicate_matching_rows(bool shrinkAfterFirst)
    {
        Fixture fixture = new(0x65A660);
        fixture.Frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Parameters = new(4, 0, 1, 0, 0, 0) } };
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(4, 8, 13, 0, 215, 323, null));
        foreach (byte actor in new byte[] { 0, 4, 4, 3 })
        {
            (packets, _) = packets.AppendContact(new(new(actor, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        (packets, _) = packets.Publish(4);
        List<int> delivered = [];
        NativeBattleEffect323Inputs inputs = fixture.Inputs() with
        {
            ReadPackets = _ => packets,
            Execute = (frame, operation) =>
            {
                delivered.Add(Assert.IsType<NativeBattleEffect323Contact506690>(operation).ContactIndex);
                if (shrinkAfterFirst)
                {
                    NativeBattleActionHeaderSlot header = packets.Headers[0];
                    packets = packets with { Headers = packets.Headers.SetItem(0, header with { Payload = header.Payload.SetItem(16, (byte)2) }) };
                }
                return frame with { Random = frame.Random with { Seed = frame.Random.Seed + 1 } };
            }
        };
        NativeBattleEffect323Frame result = NativeBattleEffect323Lower.AllMatchingContacts65A5F0(fixture.Frame, 0x1D972C0 + 4 * 156, inputs);
        Assert.Equal(shrinkAfterFirst ? new[] { 1 } : new[] { 1, 2 }, delivered);
        Assert.Equal(fixture.Frame.Random.Seed + (uint)delivered.Count, result.Random.Seed);
    }

    internal sealed class Fixture
    {
        public const uint Arena = 0x20DFAB8;
        public NativeBattleEffect323Frame Frame;
        public uint Address;
        public NativeBattleEffect323Resources Resources;
        public List<NativeBattleEffect323Operation> Operations = [];


        public Fixture(uint callback, bool append = true)
        {
            ushort stride = callback == 0x65AA80 ? (ushort)48 : (ushort)36;
            Address = Arena + 0xE54;
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2505240, Address, stride, 1);
            if (append)
            {
                (pool, int slot) = pool.Append(callback);
                pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteBytes(12, new byte[pool.Stride - 12])) };
            }
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(memory, [pool]);
            NativeBattleEffect323State state = NativeBattleEffect323State.FromColdProcess().WriteDword(0x25052A8, Arena);
            Frame = new(memory, new(323, 322, new(323, 0x659A80, 0x659A90), null, null) { Pools = [pool] }, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            ImmutableDictionary<uint, NativeBattleModuleResource> sprites = ImmutableDictionary<uint, NativeBattleModuleResource>.Empty;
            foreach ((uint source, int count) in new (uint, int)[] { (0xFE12B4, 10) })
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
            Resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, sprites, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);

        }

        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change) => Frame = Write(Frame, Address, change(Read(Frame, Address)));
        public NativeBattleEffect323Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, null!, null!, null!, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
