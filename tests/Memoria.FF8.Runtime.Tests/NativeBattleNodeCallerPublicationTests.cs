using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNodeCallerPublicationTests
{
    [Fact]
    public void Delivery_failure_rolls_back_source_auxiliary_and_task_context_as_one_unpublished_helper()
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaFrame prepared = NativeBattleCuragaNodeCycleTests.EmitOnce(fixture.Frame, 1);
        prepared = NativeBattleCuragaSpawnTests.Task(prepared, fixture.Task, task => task.WriteWord(0x34 + 0x20, 1).WriteWord(0x34 + 0x18, 1));
        prepared = prepared with { Memory = prepared.Memory with { PrimitiveProcess = null } };
        NativeBattleNodeSource source = prepared.Memory.CuragaExecutable!.Data!;
        NativeBattleModulePoolTask originalTask = new NativeBattleEffectHierarchy(prepared.Effect.Pools).Read(fixture.Task);
        NativeBattleEffectNodeCallerPolicy policy = new(NativeBattleCuragaExecutableState.Address, source.Length, originalTask.Callback, 90, 140, false);
        NativeBattleRecover93NodeState state = new(1, source, NativeBattleCuragaSpawnTests.Emitter, NativeBattleCuragaSpawnTests.Node, 0, 0, 0, 0, prepared.State.CopiedCamera, 0, null, null);
        NativeBattleRecover93NodeFrame frame = new(prepared.Memory, prepared.Effect, state, new(1));
        const int auxOffset = NativeBattleCuragaSpawnTests.Node + 0x200;
        var originalAux = prepared.Memory.Arena!.ReadSnapshot(auxOffset, 4);
        byte[] originalContext = new byte[4];
        bool[] originalContextKnown = new bool[4];
        originalTask.ReadWithKnownness(0x34 + 0x18, originalContext, originalContextKnown);
        ushort originalSource = source.ReadUInt16(0x300);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        bool reachedDraw = false;
        using (NativeBattleTaskOperation operation = new(frame.Memory, frame.Effect))
        {
            NotSupportedException failure = Assert.Throws<NotSupportedException>((Action)(() =>
            {
                _ = Run();
            }));
            Assert.Contains("common packet owner", failure.Message);
            Assert.True(reachedDraw);
            // The helper has merged its local work, but no external recipient has accepted it.
            Assert.Equal((ushort)0xBEEF, source.ReadUInt16(0x300));
            Assert.Equal(0x11223344u, prepared.Memory.Arena!.ReadUInt32(auxOffset));
            Assert.Equal(0xAABBCCDDu, new NativeBattleEffectHierarchy(prepared.Effect.Pools).Read(fixture.Task).ReadDword(0x34 + 0x18));
        }
        AssertRestored();
        Assert.Throws<NotSupportedException>((Action)(() =>
        {
            _ = Run();
        }));
        AssertRestored();

        NativeBattleRecover93NodeStep Run() => NativeBattleEffectNodeCaller.Advance74F6E0(policy, frame, fixture.Task, _ => throw new InvalidOperationException("Unexpected actor model read."), new(null, 256, 160, 120), 0, ordering, NativeBattleGteColorInterpolation.CodeConstants45F270, (bank, _, _, _) => bank.WriteUInt16(0x300, 0xBEEF), (memory, node, _) => (_, _) =>
        {
            reachedDraw = true;
            memory.Dword(node + 0x200, 0x11223344);
            memory.ContextDword(0x18, 0xAABBCCDD);
            return new([], 0);
        });

        void AssertRestored()
        {
            Assert.Equal(originalSource, source.ReadUInt16(0x300));
            var actualAux = prepared.Memory.Arena!.ReadSnapshot(auxOffset, 4);
            Assert.Equal(originalAux.Data, actualAux.Data);
            Assert.Equal(originalAux.Known, actualAux.Known);
            byte[] actualContext = new byte[4];
            bool[] actualContextKnown = new bool[4];
            new NativeBattleEffectHierarchy(prepared.Effect.Pools).Read(fixture.Task).ReadWithKnownness(0x34 + 0x18, actualContext, actualContextKnown);
            Assert.Equal(originalContext, actualContext);
            Assert.Equal(originalContextKnown, actualContextKnown);
            Assert.Empty(ordering.Proxies);
        }
    }
}
