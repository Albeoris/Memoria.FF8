using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeltingBubble43ContextTests
{
    private const int Context = 0x34;
    private const int Phase = Context + 0x20;
    private const int FrameCounter = Context + 0x18;
    private const int AnchorX = Context + 0x1F4;
    private const int AnchorY = Context + 0x1F8;
    private const int AnchorZ = Context + 0x1FC;
    private const int AnchorW = Context + 0x200;

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void Before_context_hook_runs_once_before_phase_dispatch_and_count_publication_including_completion(int phase, bool unknownW)
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture();
        NativeBattleCuragaFrame frame = Prepare(fixture.Frame, fixture.Task, phase, unknownW);
        List<string> calls = [];
        NativeBattleCuragaNodeEngineInputs inputs = new((current, address) =>
        {
            calls.Add("leaves");
            Assert.Equal(fixture.Task, address);
            Assert.Equal(new[] { "hook", "leaves" }, calls);
            Assert.Equal((ushort)phase, Read(current, address).ReadWord(Phase));
            Assert.Equal((ushort)11, Read(current, address).ReadWord(FrameCounter));
            AssertAnchor(current, address, unknownW);
            Assert.Equal(0x1234u, current.Memory.GeometryRegisters!.Otz);
            return current with { Memory = current.Memory with { GeometryRegisters = current.Memory.GeometryRegisters with { Otz = 0x1235 } } };
        })
        {
            BeforeAdvanceContext = (current, address) =>
            {
                calls.Add("hook");
                Assert.Single(calls);
                Assert.Equal(fixture.Task, address);
                Assert.Equal((ushort)phase, Read(current, address).ReadWord(Phase));
                Assert.Equal((ushort)11, Read(current, address).ReadWord(FrameCounter));
                current = Write(current, address, task => task.WriteDword(AnchorX, 0xFFFF0000).WriteDword(AnchorY, 0x00020000).WriteDword(AnchorZ, 0x01030000).WriteWord(Context + 0x14, 2).WriteWord(Context + 0x16, 3));
                return current with { Memory = current.Memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Otz = 0x1234 } } };
            }
        };
        NativeBattleCuragaNodeEngineStep result = NativeBattleCuragaNodeEngine.Advance87EC80(frame, fixture.Task, inputs);
        Assert.Equal(phase == 1 ? new[] { "hook", "leaves" } : new[] { "hook" }, calls);
        Assert.Equal(phase == 2, result.Complete);
        Assert.Equal((ushort)(phase == 0 ? 1 : phase), Read(result.Frame, fixture.Task).ReadWord(Phase));
        Assert.Equal((ushort)(phase == 1 ? 12 : 11), Read(result.Frame, fixture.Task).ReadWord(FrameCounter));
        Assert.Equal((ushort)2, result.Frame.State.PassCount26D82EA);
        Assert.Equal((ushort)3, result.Frame.State.PassCount26D82DA);
        Assert.Equal(phase == 1 ? 0x1235u : 0x1234u, result.Frame.Memory.GeometryRegisters!.Otz);
        AssertAnchor(result.Frame, fixture.Task, unknownW);
        NativeBattleCuragaFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCuragaFrame>(NativeBattleTestSnapshots.Json(result.Frame))!;
        AssertAnchor(restored, fixture.Task, unknownW);
        Assert.Equal(NativeBattleTestSnapshots.Json(result.Frame), NativeBattleTestSnapshots.Json(restored));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Absent_hook_preserves_existing_phase_semantics_and_retained_anchor_bytes(int phase)
    {
        var fixture = NativeBattleCuragaNodeEngineTests.Fixture();
        NativeBattleCuragaFrame frame = Prepare(fixture.Frame, fixture.Task, phase, true);
        frame = Write(frame, fixture.Task, task => task.WriteDword(AnchorX, 0x11223344).WriteDword(AnchorY, 0x55667788).WriteDword(AnchorZ, 0x99AABBCC));
        int calls = 0;
        NativeBattleCuragaNodeEngineInputs inputs = new((current, address) =>
        {
            calls++;
            Assert.Equal((ushort)1, Read(current, address).ReadWord(Phase));
            return current;
        });
        Assert.Null(inputs.BeforeAdvanceContext);
        NativeBattleCuragaNodeEngineStep result = NativeBattleCuragaNodeEngine.Advance87EC80(frame, fixture.Task, inputs);
        Assert.Equal(phase == 1 ? 1 : 0, calls);
        Assert.Equal(phase == 2, result.Complete);
        Assert.Equal((ushort)(phase == 0 ? 1 : phase), Read(result.Frame, fixture.Task).ReadWord(Phase));
        Assert.Equal((ushort)(phase == 1 ? 12 : 11), Read(result.Frame, fixture.Task).ReadWord(FrameCounter));
        Assert.Equal(0x11223344u, Read(result.Frame, fixture.Task).ReadDword(AnchorX));
        Assert.Equal(0x55667788u, Read(result.Frame, fixture.Task).ReadDword(AnchorY));
        Assert.Equal(0x99AABBCCu, Read(result.Frame, fixture.Task).ReadDword(AnchorZ));
        Assert.Throws<NotSupportedException>(() => Read(result.Frame, fixture.Task).ReadDword(AnchorW));
        Assert.Null(result.Frame.Memory.GeometryRegisters);
    }

    private static NativeBattleCuragaFrame Prepare(NativeBattleCuragaFrame frame, uint address, int phase, bool unknownW)
    {
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = null }, State = frame.State with { PassCount26D82EA = 0, PassCount26D82DA = 0 } };
        return Write(frame, address, task =>
        {
            task = task.WriteWord(Phase, (ushort)phase).WriteWord(FrameCounter, 11).WriteWord(Context + 0x1A, 100).WriteWord(Context + 0x14, 0).WriteWord(Context + 0x16, 0);
            return unknownW ? task.MarkPayloadUnknown(AnchorW, 4) : task.WriteDword(AnchorW, 0x89ABCDEF);
        });
    }

    private static void AssertAnchor(NativeBattleCuragaFrame frame, uint address, bool unknownW)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        Assert.Equal(0xFFFF0000u, task.ReadDword(AnchorX));
        Assert.Equal(0x00020000u, task.ReadDword(AnchorY));
        Assert.Equal(0x01030000u, task.ReadDword(AnchorZ));
        if (unknownW)
        {
            Assert.Throws<NotSupportedException>(() => task.ReadDword(AnchorW));
        }
        else
        {
            Assert.Equal(0x89ABCDEFu, task.ReadDword(AnchorW));
        }
    }

    private static NativeBattleModulePoolTask Read(NativeBattleCuragaFrame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleCuragaFrame Write(NativeBattleCuragaFrame frame, uint address, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> update)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, update(hierarchy.Read(address))).Pools } };
    }
}
