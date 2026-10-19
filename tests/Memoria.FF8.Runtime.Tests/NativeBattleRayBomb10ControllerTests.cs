using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRayBomb10ControllerTests
{
    [Fact]
    public void BeamCrossProductWrapsBeforeShiftAndSquareRootPreservesItsSignedDwordContract()
    {
        Assert.Equal((0, 0, 4096), NativeBattleRayBomb10Geometry.Cross56BBF0((4096, 0, 0), (0, 4096, 0)));
        Assert.Equal((unchecked(int.MaxValue * int.MaxValue) >> 12, 0, 0), NativeBattleRayBomb10Geometry.Cross56BBF0((0, int.MaxValue, 0), (0, 0, int.MaxValue)));
        Assert.Equal(4096, NativeBattleRayBomb10Geometry.Sqrt56BEC0(16785408));
        Assert.Equal(0, NativeBattleRayBomb10Geometry.Sqrt56BEC0(-1));
        Assert.Equal(46340, NativeBattleRayBomb10Geometry.Sqrt56BEC0(int.MaxValue));
    }

    [Fact]
    public void RestoreRunsWhilePausedAndWaitsForItsActualPhysicalCompletionWordAcrossIndependentJsonLoads()
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x24C91F8, 0x24C91D8, 16, 2);
        (roots, int index) = roots.Append(0x6296F0);
        roots = roots with { Slots = roots.Slots.SetItem(index, roots.Slots[index].WriteWord(12, 0).WriteWord(14, 0xFFFF)) };
        List<NativeBattleRayBomb10Operation> operations = [];
        NativeBattleRayBomb10Frame frame = Frame(roots);
        NativeBattleRayBomb10Frame first = NativeBattleRayBomb10Controller.Advance629730(frame, Resources(), Inputs(0x201, operations));
        NativeBattleRayBomb10Restore restore = Assert.IsType<NativeBattleRayBomb10Restore>(Assert.Single(operations));
        Assert.Equal(0x20F1AB8u, restore.Destination);
        Assert.Equal(0x24C91E6u, restore.Completion);
        Assert.Equal((ushort)1, first.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal((ushort)0, first.Effect.Pools[0].Slots[0].ReadWord(14));
        Assert.Equal(0x24C91F8u, first.Effect.ReturnedDescriptor);
        first = first with { Effect = NativeBattleRayBomb10Controller.WriteCompletionByte(first.Effect, restore.Completion, 255) };
        string json = JsonSerializer.Serialize(first);
        operations.Clear();
        NativeBattleRayBomb10Frame left = NativeBattleRayBomb10Controller.Advance629730(JsonSerializer.Deserialize<NativeBattleRayBomb10Frame>(json)!, Resources(), Inputs(0x201, operations));
        NativeBattleRayBomb10Frame right = NativeBattleRayBomb10Controller.Advance629730(JsonSerializer.Deserialize<NativeBattleRayBomb10Frame>(json)!, Resources(), Inputs(0x201, operations));
        Assert.Empty(operations);
        Assert.Equal(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right));
        Assert.Equal(0u, left.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)2, left.Effect.Pools[0].Slots[0].ReadWord(12));
    }

    [Fact]
    public void DirectorFinishesItsActual161TickBoundaryWithoutChangingRandomOrRetainedParticleTails()
    {
        NativeBattleEffectTaskPool roots = NativeBattleEffectTaskPool.Initialize(0x24C91F8, 0x24C91D8, 16, 2);
        (roots, _) = roots.Append(0x629730);
        NativeBattleRayBomb10Frame frame = Frame(roots);
        NativeBattleEffectTaskPool children = frame.Effect.Pools[1];
        (children, int index) = children.Append(0x627F10);
        children = children with { Slots = children.Slots.SetItem(index, children.Slots[index].WriteWord(12, 160)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children) } };
        NativeBattleRayBomb10State state = Assert.IsType<NativeBattleRayBomb10State>(frame.Memory.RayBomb10);
        state = state with { Particles = state.Particles.SetItem(23, 0xD7) };
        frame = frame with { Memory = frame.Memory with { RayBomb10 = state } };
        List<NativeBattleRayBomb10Operation> operations = [];
        NativeBattleRayBomb10Frame result = NativeBattleRayBomb10Controller.Advance629730(frame, Resources(), Inputs(0, operations));
        Assert.Equal(2, operations.Count);
        Assert.All(operations, operation => Assert.Equal(0, Assert.IsType<NativeBattleRayBomb10Flash>(operation).Intensity));
        Assert.Equal(0u, result.Effect.ReturnedDescriptor);
        Assert.Equal(frame.Random, result.Random);
        Assert.Equal((byte)0xD7, result.Memory.RayBomb10!.Particles[23]);
        Assert.Equal((ushort)161, result.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    private static NativeBattleRayBomb10Resources Resources() => new(new("camera", []), [], [], [], [], [], [], [], [], 0, 0);
    private static NativeBattleRayBomb10Inputs Inputs(uint flags, List<NativeBattleRayBomb10Operation> operations)
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        return new(flags, NativeBattleActionPacketsState.Fresh(), new(identity, 512, 160, 104), 0, new(0, 0, 0), actor => new(actor, 0), _ => identity, (_, _, _) => throw new InvalidOperationException("This boundary reads no bone."), _ => throw new InvalidOperationException("This boundary reads no target centre."), _ => throw new InvalidOperationException("This boundary reads no actor anchor."), (frame, operation) =>
        {
            operations.Add(operation);
            return frame;
        });
    }

    private static NativeBattleRayBomb10Frame Frame(NativeBattleEffectTaskPool roots)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(9) with { RayBomb10 = NativeBattleRayBomb10State.FromColdProcess() with { ArenaBase = 0x20E1AB8 } };
        NativeBattleOriginalEffectState effect = new(10, 9, new(10, 0x627E60, 0x627E40), new(3, 0, 1, 0, 0, 0), null) { ReturnedDescriptor = 0x24C91F8, Pools = [roots, NativeBattleEffectTaskPool.Initialize(0x24C9988, 0x24C9998, 36, 100)] };
        return new(memory, effect, new(1));
    }
}

