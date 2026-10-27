using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect319Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect319LowerTests
{
    [Fact]
    public void Factory_resolves_actual_bone_record_and_preserves_native_argument_order()
    {
        Fixture fixture = new(0x65F2B0, false);
        fixture.Frame = NativeBattleEffect319Lower.Birth65E4B0(fixture.Frame, 25, 16, 1024, 512, fixture.Inputs());
        NativeBattleModulePoolTask task = Read(fixture.Frame, fixture.Address);
        Assert.Equal(25, fixture.ResolvedBone);
        Assert.Equal(0x1234510u, task.ReadDword(12));
        Assert.Equal(16, task.ReadWord(16));
        Assert.Equal(1024, task.ReadWord(20));
        Assert.Equal(512, task.ReadWord(24));
        Assert.Equal(0, task.ReadWord(18));
        Assert.Equal(0, task.ReadWord(22));
    }

    [Fact]
    public void Positive_delay_still_applies_current_zero_scales_without_integrating()
    {
        Fixture fixture = new(0x65F2B0);
        fixture.Task(task => task.WriteDword(12, 0x1234510).WriteWord(16, 8).WriteWord(20, 512).WriteWord(24, 1024));
        NativeBattleEffect319Step step = NativeBattleEffect319Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, step.Result);
        Assert.Equal(7, Read(step.Frame, fixture.Address).ReadWord(16));
        Assert.Equal(new NativeBattleEffect319ScaleBone56BEF0(0x1234510, 0, 0), Assert.Single(fixture.Operations));
    }

    [Fact]
    public void Delay_expiry_integrates_then_applies_XY_XY_Z_to_the_same_current_pointer()
    {
        Fixture fixture = new(0x65F2B0);
        fixture.Task(task => task.WriteDword(12, 0x1234510).WriteWord(16, 1).WriteWord(20, 512).WriteWord(24, 1024));
        NativeBattleEffect319Step step = NativeBattleEffect319Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, step.Result);
        Assert.Equal(0, Read(step.Frame, fixture.Address).ReadWord(16));
        Assert.Equal(new NativeBattleEffect319ScaleBone56BEF0(0x1234510, 1024, 512), Assert.Single(fixture.Operations));
    }

    [Fact]
    public void Terminal_clamps_are_published_but_full_scale_is_never_applied()
    {
        Fixture fixture = new(0x65F2B0);
        fixture.Task(task => task.WriteDword(12, 0x1234510).WriteWord(18, 4000).WriteWord(20, 512).WriteWord(22, 4000).WriteWord(24, 1024));
        NativeBattleEffect319Step step = NativeBattleEffect319Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, step.Result);
        Assert.Equal(4096, Read(step.Frame, fixture.Address).ReadWord(18));
        Assert.Equal(4096, Read(step.Frame, fixture.Address).ReadWord(22));
        Assert.Empty(fixture.Operations);
    }

    [Fact]
    public void Signed_WORD_overflow_precedes_upper_clamp_and_survives_json()
    {
        Fixture fixture = new(0x65F2B0);
        fixture.Task(task => task.WriteDword(12, 0x1234510).WriteWord(18, 32760).WriteWord(20, 512).WriteWord(22, 1).WriteWord(24, 1024));
        fixture.Frame = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect319Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Frame)))!);
        NativeBattleEffect319Step step = NativeBattleEffect319Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(new NativeBattleEffect319ScaleBone56BEF0(0x1234510, 1025, unchecked((short)(32760 + 512))), Assert.Single(fixture.Operations));
        Assert.Equal(0, step.Result);
    }

    [Theory]
    [InlineData(8, 0, 9)]
    [InlineData(9, 2, 10)]
    public void Sprite_draws_current_phase_before_increment_and_clears_actual_flags_WORD(int age, int expectedResult, int nextAge)
    {
        Fixture fixture = new(0x65E430);
        fixture.Task(task => task.WriteWord(12, (ushort)age).WriteWord(20, 4000).WriteWord(22, 0xCAFE));
        fixture.Frame = fixture.Frame with { Staging = fixture.Frame.Staging!.Write(NativeBattleTemporaryMemory.Address + 0x24, [0xFF, 0xFF]).MarkUnknown(NativeBattleTemporaryMemory.Address + 179, 1) };
        NativeBattleEffect319Step step = NativeBattleEffect319Lower.Advance(fixture.Frame, fixture.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(expectedResult, step.Result);
        Assert.Equal(nextAge, Read(step.Frame, fixture.Address).ReadWord(12));
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(step.Frame.Staging!, NativeBattleTemporaryMemory.Address, 180);
        Assert.Equal(age, NativeBattleEffect319Lower.Word319(temporary, 4));
        Assert.Equal(new byte[2], temporary.Read(0x24, 2));
        Assert.Equal(new byte[4], temporary.Read(0xB0, 4));
        Assert.IsType<NativeBattleEffect319DrawBatch>(Assert.Single(fixture.Operations));
        Assert.Equal(2976, step.Frame.Memory.ModuleSpriteBillboard21DFED0!.ReadMatrix().Z);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    internal sealed class Fixture
    {
        public const uint Arena = 0x20DFAB8;
        public NativeBattleEffect319Frame Frame;
        public uint Address;
        public NativeBattleEffect319Resources Resources;
        public List<NativeBattleEffect319Operation> Operations = [];
        public int ResolvedBone = -1;

        public Fixture(uint callback, bool append = true)
        {
            bool sprite = callback == 0x65E430;
            Address = Arena + (sprite ? 0x924u : 0x898u);
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(sprite ? 0x25053B0u : 0x25053C0u, Address, sprite ? (ushort)24 : (ushort)28, sprite ? (ushort)16 : (ushort)5);
            if (append)
            {
                (pool, int slot) = pool.Append(callback);
                pool = pool.WriteSlot(slot, pool.Slots[slot].WriteBytes(12, new byte[pool.Stride - 12]));
            }
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, [pool]);
            NativeBattleEffect319State state = NativeBattleEffect319State.FromColdProcess().WriteDword(0x2505410, Arena).WriteDword(0x25053AC, 0x1D97494);
            Frame = new(memory, new(319, 318, new(319, 0x65DB30, 0x65DB40), null, null) { Pools = [pool] }, new(42), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()).Write(NativeBattleTemporaryMemory.Address, new byte[532]) };
            byte[] spriteData = new byte[38];
            BinaryPrimitives.WriteUInt16LittleEndian(spriteData.AsSpan(8), 10);
            for (int index = 0; index < 10; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(spriteData.AsSpan(10 + index * 2), 34);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(spriteData.AsSpan(30), 0xFFFF);
            Resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty.Add(0xFF12E0, new(5, 0xFF12E0, 38, 0, spriteData.ToImmutableArray())), ImmutableDictionary<int, NativeBattleEffectFileAsset>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
        }

        public void Task(Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> change) => Frame = Write(Frame, Address, change(Read(Frame, Address)));
        public NativeBattleEffect319Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, (_, _) => NativeBattleEffectArena.Clear(), _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 512, 160, 108), _ => 0, null!, null!, (_, _, bone) =>
        {
            ResolvedBone = bone;
            return 0x1234510;
        }, null!, (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
