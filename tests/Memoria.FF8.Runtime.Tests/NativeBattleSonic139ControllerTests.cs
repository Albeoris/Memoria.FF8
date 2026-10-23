using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSonic139Controller;
using static Memoria.FF8.Runtime.NativeBattleSonic139Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSonic139ControllerTests
{
    private const uint MainAddress = NativeBattleEffectNodeMemory.ArenaAddress + MainOffset;
    private static readonly NativeBattleSonic139Resources Resources = new(null!, null!, new(MainActAddress, []), new(TargetActAddress, []), null!, [], [], [], [0x4040FF, 0xFF40FF, 0xFF4040, 0x3167616D], 0x641C8);

    [Fact]
    public void Completion_waits_for_lower_queue_then_requests_common_once_and_observes_actual_byte()
    {
        NativeBattleSonic139Frame frame = Frame(70);
        NativeBattleEffectTaskPool lower = frame.Effect.Pools[2];
        (lower, _) = lower.Append(TargetCallback);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, lower) } };
        int requests = 0;
        NativeBattleSonic139Inputs inputs = Inputs((current, operation) =>
        {
            var request = Assert.IsType<NativeBattleSonic139RequestCommonCompletion>(operation);
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + FirstPacketOffset, request.SourceAddress);
            Assert.Equal(MainAddress + 0x13, request.FlagAddress);
            requests++;
            return current;
        });
        var waiting = AdvanceMain(frame, MainAddress, Resources, inputs);
        Assert.Equal(0, requests);
        Assert.Equal((ushort)70, Read(waiting.Frame, MainAddress).ReadWord(12));
        lower = NativeBattleEffectTaskPool.Initialize(LowerDescriptor, NativeBattleEffectNodeMemory.ArenaAddress, 0x298, 4);
        frame = waiting.Frame with { Effect = waiting.Frame.Effect with { Pools = waiting.Frame.Effect.Pools.SetItem(2, lower) } };
        var requested = AdvanceMain(frame, MainAddress, Resources, inputs);
        Assert.Equal(1, requests);
        Assert.Equal((ushort)71, Read(requested.Frame, MainAddress).ReadWord(12));
        Assert.Equal(0, requested.Result);
        var stillWaiting = AdvanceMain(requested.Frame, MainAddress, Resources, inputs);
        Assert.Equal(1, requests);
        Assert.Equal(0, stillWaiting.Result);
        frame = stillWaiting.Frame with { Effect = WriteCompletionByte(stillWaiting.Frame.Effect, MainAddress + 0x13, 0xFF) };
        Assert.Equal(2, AdvanceMain(frame, MainAddress, Resources, inputs).Result);
        Assert.Throws<InvalidDataException>(() => WriteCompletionByte(frame.Effect, MainAddress + 0x12, 0xFF));
    }

    [Fact]
    public void Target_completion_restores_raw_color_clears_only_tint_flag_and_contacts_after_act()
    {
        NativeBattleSonic139Frame frame = Target(18, out uint address);
        List<string> order = [];
        NativeBattleSonic139Inputs inputs = Inputs((current, operation) =>
        {
            switch (operation)
            {
                case NativeBattleSonic139WriteActorColor color:
                    Assert.Equal(0x1D972C0u, color.ActorAddress);
                    Assert.Equal(0x9A123456u, color.Color);
                    order.Add("restore");
                    break;
                case NativeBattleSonic139SetActorFlags flags:
                    Assert.Equal((ushort)0, flags.Set);
                    Assert.Equal((ushort)0x800, flags.Clear);
                    order.Add("clear");
                    break;
                case NativeBattleSonic139ContactForActor contact:
                    Assert.Equal(0x1D972C0u, contact.ActorAddress);
                    order.Add("contact");
                    break;
                default:
                    throw new InvalidOperationException();
            }
            return current;
        }) with
        {
            AdvanceAct = (current, workspace, source, context) =>
            {
                Assert.Equal(address + 32, workspace);
                Assert.Equal(TargetActAddress, source.Address);
                Assert.Equal(0, context.DepthBias);
                Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset, context.MorphAddress);
                Assert.Equal(new int?[] { 777, 888, 999 }, current.Memory.GeometryRegisters!.Translation);
                Assert.Equal(10, context.Matrix.ReadMatrix().X);
                order.Add("act");
                return new(current, 0);
            }
        };
        var completed = AdvanceTarget(frame, address, Resources, inputs);
        Assert.Equal(2, completed.Result);
        Assert.Equal(new[] { "act", "restore", "clear", "contact" }, order);
        Assert.Equal((ushort)18, Read(completed.Frame, address).ReadWord(12));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Screen_bounds_ignore_nonpositive_signed_depth(short depth)
    {
        Assert.Equal(NativeBattleSonic139Bounds.Initial, IncludePoint6662C0(NativeBattleSonic139Bounds.Initial, -300, 400, depth));
    }

    [Fact]
    public void Screen_bounds_include_signed_coordinates_and_minimum_positive_depth()
    {
        NativeBattleSonic139Bounds bounds = IncludePoint6662C0(NativeBattleSonic139Bounds.Initial, -100, 300, 2000);
        bounds = IncludePoint6662C0(bounds, 200, -50, 1000);
        Assert.Equal(new(-100, -50, 200, 300, 1000, true), bounds);
    }

    [Fact]
    public void Target_bounds_consume_45E250_quarter_depth_while_GTE_SZ3_retains_raw_projection()
    {
        NativeBattleSonic139Frame frame = Frame(70);
        frame = frame with { Memory = frame.Memory with { ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() } };
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleGraphicsModel model = new(0, 3) { Transform = new(new(0, 0, 0), new(0, 0, 8000), 0, 0, 0) };
        NativeBattleSonic139Inputs inputs = Inputs((current, _) => current) with { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(identity, null), ReadProjection = _ => new(identity, 256, 160, 108) };
        var observed = IncludeActor6A9AD0(frame, model, NativeBattleSonic139Bounds.Initial, inputs);
        Assert.Equal(8000u, observed.Frame.Memory.GeometryRegisters!.Sz[3]);
        Assert.Equal(2000, observed.Bounds.MinimumDepth);
        Assert.Equal(12, observed.Bounds.MaxX - observed.Bounds.MinX);
    }

    [Fact]
    public void Byte_copy_preserves_unknown_padding_in_the_single_task_owner()
    {
        NativeBattleSonic139Frame frame = Frame(70);
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, new byte[] { 1, 2, 3, 4, 5, 6 }).MarkUnknown(6, 2);
        frame = WriteBytes(frame, MainAddress + 0x20, source, 8);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, Read(frame, MainAddress).ReadBytes(0x20, 6));
        Assert.Throws<NotSupportedException>(() => Read(frame, MainAddress).ReadWord(0x26));
        Assert.Throws<NotSupportedException>(() => ReadBytes(frame, MainAddress + 0x20, 8).Read(6, 2));
        Assert.Throws<InvalidDataException>(() => WriteBytes(frame, MainAddress + 8, source, 8));
    }

    private static NativeBattleSonic139Frame Target(short age, out uint address)
    {
        NativeBattleSonic139Frame frame = Frame(70);
        NativeBattleEffectTaskPool lower = frame.Effect.Pools[2];
        (lower, int slot) = lower.Append(TargetCallback);
        address = checked(lower.BankAddress + (uint)(slot * lower.Stride));
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, lower) } };
        return Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)age)).WriteDword(16, 0x1D972C0).WriteDword(20, 0x9A123456));
    }

    private static NativeBattleSonic139Frame Frame(short age)
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (roots, _) = roots.Append(RootCallback);
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, 0x4DC, 1);
        (main, _) = main.Append(MainCallback);
        main = main with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteWord(12, unchecked((ushort)age))) };
        NativeBattleEffectTaskPool lower = NativeBattleEffectTaskPool.Initialize(LowerDescriptor, NativeBattleEffectNodeMemory.ArenaAddress, 0x298, 4);
        NativeBattleOriginalEffectState effect = new(139, 138, new(139, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = [roots, main, lower] };
        NativeBattleGeometryRegisters geometry = NativeBattleGeometryRegisters.Unknown() with { Translation = [777, 888, 999] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = geometry, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
        NativeBattleSonic139State state = NativeBattleSonic139State.FromColdProcess().WriteDword(WorkingGlobal, NativeBattleEffectNodeMemory.ArenaAddress) with { RootBankStorage = null };
        return new(memory, effect, new(55), state);
    }

    private static NativeBattleSonic139Inputs Inputs(Func<NativeBattleSonic139Frame, NativeBattleSonic139Operation, NativeBattleSonic139Frame> execute) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 30 }, null), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), execute);
}
