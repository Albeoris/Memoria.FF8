using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBadBreath131Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath131TargetsTests
{
    [Theory]
    [InlineData(0, 0xAB102030u)]
    [InlineData(7, 0xABC6BAAEu)]
    [InlineData(8, 0x11E0D0C0u)]
    [InlineData(30, 0xABE0D0C0u)]
    [InlineData(38, 0xAB102030u)]
    [InlineData(39, 0x77102030u)]
    public void Target_color_branches_use_actual_weighted_registers_and_full_DWORD_direct_stores(int age, uint expected)
    {
        Fixture fixture = new(age);
        var actual = NativeBattleBadBreath131Controller.AdvanceTarget(fixture.Frame, fixture.TargetAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(expected, Assert.Single(fixture.Operations.OfType<NativeBattleBadBreath131ActorColor>()).Color);
        Assert.Equal((ushort)(age + 1), NativeBattleBadBreath131Controller.Read(actual.Frame, fixture.TargetAddress).ReadWord(12));
        Assert.Equal(0, actual.Result);
        Assert.IsType<NativeBattleBadBreath131ActorFlags>(fixture.Operations[0]);
        Assert.Equal(0x12345678u, actual.Frame.State.ReadDword(BlackGlobal));
        Assert.Equal(0x1C, fixture.ActOffset);
        Assert.Equal(0x128, fixture.ActSize);
        Assert.Equal((uint)(ArenaAddress + ScratchOffset), BinaryPrimitives.ReadUInt32LittleEndian(fixture.Context!.Read(8, 4)));
        Assert.Throws<NotSupportedException>(() => fixture.Context!.Read(6, 2));
        if (age is 0 or 7 or 30 or 38)
        {
            Assert.NotNull(actual.Frame.Memory.ScriptedColorRegisters!.Ir0);
            Assert.NotEqual(NativeBattleTestSnapshots.Json(fixture.Frame.Memory.ScriptedColorRegisters), NativeBattleTestSnapshots.Json(actual.Frame.Memory.ScriptedColorRegisters));
        }
    }

    [Fact]
    public void Target_birth_uses_two_CRT_draws_only_after_successful_particle_allocation()
    {
        Fixture fixture = new(16);
        var actual = NativeBattleBadBreath131Controller.AdvanceTarget(fixture.Frame, fixture.TargetAddress, fixture.Resources, fixture.Inputs());
        NativeCrtRandomState expected = fixture.Frame.Random.Draw(out ushort selector).Draw(out ushort scale);
        Assert.Equal(expected, actual.Frame.Random);
        Assert.Equal((short)(selector * 28 >> 15), Assert.Single(fixture.SelectedBones));
        NativeBattleModulePoolTask child = actual.Frame.Effect.Pools[3].Slots[0];
        Assert.Equal(ParticleCallback, child.Callback);
        Assert.Equal(new byte[] { 10, 0, 20, 0, 30, 0 }, child.ReadBytes(16, 6));
        Assert.Equal((ushort)((scale & 511) + 1024), child.ReadWord(22));
        Assert.Equal((ushort)0, child.ReadWord(12));
        NativeBattleBadBreath131Frame full = fixture.Frame;
        for (int index = 0; index < ParticleCount; index++)
        {
            (full, _) = NativeBattleBadBreath131Controller.Append(full, 3, ParticleCallback);
        }
        fixture.SelectedBones.Clear();
        var rejected = NativeBattleBadBreath131Controller.AdvanceTarget(full, fixture.TargetAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(full.Random, rejected.Frame.Random);
        Assert.Empty(fixture.SelectedBones);
    }

    [Theory]
    [InlineData(39, 0, 0)]
    [InlineData(40, 1, 0)]
    [InlineData(45, 0, 0)]
    [InlineData(46, 0, 2)]
    public void Contact_is_old40_relative_index_and_terminal_clear_is_new47(int age, int contacts, int result)
    {
        Fixture fixture = new(age);
        var actual = NativeBattleBadBreath131Controller.AdvanceTarget(fixture.Frame, fixture.TargetAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(result, actual.Result);
        Assert.Equal(contacts, fixture.Operations.OfType<NativeBattleBadBreath131Contact>().Count());
        if (contacts != 0)
        {
            Assert.Equal(2, Assert.Single(fixture.Operations.OfType<NativeBattleBadBreath131Contact>()).Index);
        }
        Assert.Equal(result == 2 ? 1 : 0, fixture.Operations.OfType<NativeBattleBadBreath131ActorFlags>().Count(operation => operation.ClearMask == 0x800));
        Assert.Equal(fixture.Frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Middle_color_does_not_read_unreached_saved_color_and_ACT_remaining_is_not_callback_lifetime()
    {
        Fixture fixture = new(20);
        NativeBattleModulePoolTask task = NativeBattleBadBreath131Controller.Read(fixture.Frame, fixture.TargetAddress).MarkPayloadUnknown(24, 4);
        NativeBattleBadBreath131Frame frame = NativeBattleBadBreath131Controller.Write(fixture.Frame, fixture.TargetAddress, task);
        var actual = NativeBattleBadBreath131Controller.AdvanceTarget(frame, fixture.TargetAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, actual.Result);
        Assert.Equal(fixture.Resources.ActorColor, Assert.Single(fixture.Operations.OfType<NativeBattleBadBreath131ActorColor>()).Color);
        Assert.Throws<NotSupportedException>(() => NativeBattleBadBreath131Controller.Read(actual.Frame, fixture.TargetAddress).ReadDword(24));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(16, 0)]
    [InlineData(17, 2)]
    public void Particle_draws_physical180_before_motion_and_releases_allocation_before_native_terminal(int age, int result)
    {
        Fixture fixture = new(20);
        var (frame, address) = NativeBattleBadBreath131Controller.Append(fixture.Frame, 3, ParticleCallback);
        frame = NativeBattleBadBreath131Controller.Write(frame, address, NativeBattleBadBreath131Controller.Read(frame, address).WriteWord(12, (ushort)age).WriteWord(16, 10).WriteWord(18, 20).WriteWord(20, 30).WriteWord(22, 1024));
        var actual = NativeBattleBadBreath131Controller.AdvanceParticle(frame, address, fixture.Resources, fixture.Inputs());
        Assert.Equal(result, actual.Result);
        NativeBattleBadBreath131DrawSprite180 draw = Assert.Single(fixture.Operations.OfType<NativeBattleBadBreath131DrawSprite180>());
        Assert.Equal(NativeBattleTemporaryMemory.Address, draw.TemporaryAddress);
        Assert.Equal(ParticleSpriteAddress, draw.SourceAddress);
        Assert.Equal((ushort)age, BinaryPrimitives.ReadUInt16LittleEndian(actual.Frame.Staging!.Read(draw.TemporaryAddress + 4, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(actual.Frame.Staging.Read(draw.TemporaryAddress + 36, 2)));
        Assert.Equal((ushort)0xFFFF, BinaryPrimitives.ReadUInt16LittleEndian(actual.Frame.Staging.Read(draw.TemporaryAddress + 40, 2)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(actual.Frame.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        NativeBattleModulePoolTask task = NativeBattleBadBreath131Controller.Read(actual.Frame, address);
        Assert.Equal((ushort)4, task.ReadWord(18));
        Assert.Equal((ushort)(age + 1), task.ReadWord(12));
        Assert.Equal((ushort)1024, task.ReadWord(22));
        Assert.Equal(10, actual.Frame.Memory.ScriptedColorRegisters!.Mac[0]);
        Assert.Equal(frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void JSON_target_continuation_preserves_shared_owners_and_unknown_context_W()
    {
        Fixture fixture = new(40);
        NativeBattleBadBreath131Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBadBreath131Frame>(NativeBattleTestSnapshots.Json(fixture.Frame))!;
        var expected = NativeBattleBadBreath131Controller.AdvanceTarget(fixture.Frame, fixture.TargetAddress, fixture.Resources, fixture.Inputs());
        var actual = NativeBattleBadBreath131Controller.AdvanceTarget(restored, fixture.TargetAddress, fixture.Resources, fixture.Inputs());
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Frame), NativeBattleTestSnapshots.Json(actual.Frame));
    }

    private sealed class Fixture
    {
        public uint TargetAddress => ArenaAddress + 0x600;
        public NativeBattleBadBreath131Frame Frame { get; }
        public List<NativeBattleBadBreath131Operation> Operations { get; } = [];
        public List<short> SelectedBones { get; } = [];
        public NativeBattleEffectArena? Context { get; private set; }
        public int ActOffset { get; private set; }
        public int ActSize { get; private set; }
        public NativeBattleBadBreath131Resources Resources { get; } = new(new(TargetActAddress, []), new(0, ParticleSpriteAddress, 0, 0, []), new("unused", []), [], [], [], [], 0x11E0D0C0, 0x64194);
        public Fixture(int age)
        {
            NativeBattleEffectTaskPool[] pools =
            [
                NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1),
                NativeBattleEffectTaskPool.Initialize(MainDescriptor, ArenaAddress + MainOffset, MainStride, 1),
                NativeBattleEffectTaskPool.Initialize(TargetDescriptor, TargetAddress, TargetStride, TargetCount),
                NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, ArenaAddress, ParticleStride, ParticleCount)
            ];
            NativeBattleOriginalEffectState effect = new(131, 131, new(131, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutableArray() };
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Sxy = [1, 2, 3], Sz = [4, 5, 6, 7] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Rgb = [8, 9, 10] }, ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools);
            NativeBattleBadBreath131Frame frame = new(memory, effect, new(1), NativeBattleBadBreath131State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            (frame, _) = NativeBattleBadBreath131Controller.Append(frame, 2, TargetCallback);
            NativeBattleModulePoolTask task = NativeBattleBadBreath131Controller.Read(frame, TargetAddress).WriteBytes(12, new byte[312]).WriteWord(12, (ushort)age).WriteByte(14, 2).WriteByte(15, 0).WriteWord(16, 100).WriteWord(18, 200).WriteWord(20, 300).MarkPayloadUnknown(22, 2).WriteDword(24, 0x77102030);
            Frame = NativeBattleBadBreath131Controller.Write(frame, TargetAddress, task);
        }
        public NativeBattleBadBreath131Inputs Inputs() => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, actor => new(actor, 1) { Color40 = 0xAB010203 }, _ => 28, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (_, _) => throw new InvalidOperationException(), (frame, _, bone, _) =>
        {
            SelectedBones.Add(bone);
            return new(frame, new(10, 20, 30));
        }, (frame, _, _, offset, size, context) =>
        {
            ActOffset = offset;
            ActSize = size;
            Context = context;
            return new(frame with { State = frame.State.WriteDword(BlackGlobal, 0x12345678) }, 0);
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            if (operation is NativeBattleBadBreath131DrawSprite180 draw)
            {
                frame = frame with { Staging = frame.Staging!.Write(draw.TemporaryAddress + 40, BitConverter.GetBytes((ushort)0xFFFF)) };
            }
            return frame;
        });
    }
}
