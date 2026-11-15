using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDirectEntryTests
{
    [Fact]
    public void Read40RetainsTheActualReturnedTaskAcrossModuleSelectionAndIgnoredDirectCalls()
    {
        NativeBattleEffectBinding first = new(3, 0x700C10, 0);
        NativeBattleOriginalEffectState task = NativeBattleEffect3.InitializePools(new(3, 2, first, new(OriginalOwner(0), 0, BattleModelTargets.Empty, new BattleActionHeaderReference(0), 0, 0), null));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().AssignReturnedTask(task);
        Assert.Equal(1, memory.ReadReturnedTask5044B0());
        memory = memory.RetainReturnedTaskOnModuleLoad(task).PublishEntry50AF20(new(2, 0x6298A0, 0x629880), false).SetDirectParameters(0x50BEE0, 0xED, 0, 0);
        Assert.Equal(task.ReturnedDescriptor, memory.ReturnedTaskPointer1D96AAC);
        Assert.Same(task, memory.DetachedReturnedTask);
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        restored.Validate();
        Assert.Equal(1, restored.ReadReturnedTask5044B0());
        Assert.Equal(task.ReturnedDescriptor, restored.DetachedReturnedTask!.ReturnedDescriptor);
        Assert.Equal(0, restored.AssignReturnedTask(task with { ReturnedDescriptor = 0 }).ReadReturnedTask5044B0());
        Assert.Equal(0, restored.InitializeReturnedTask500870().ReadReturnedTask5044B0());
        Assert.Throws<NotSupportedException>(() => (memory with { ReturnedTaskPointer1D96AAC = null, DetachedReturnedTask = null }).ReadReturnedTask5044B0());
    }

    [Fact]
    public void CallbackDestinationsRemainIndependentAcrossLoadsAndJson()
    {
        NativeBattleEffectBinding first = new(1, 0x8D6A00, 0);
        NativeBattleEffectBinding second = new(2, 0x6298A0, 0x629880);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().PublishEntry50AF20(first, false).CopyEntry50B830().PublishEntry50AF20(second, true);
        Assert.Equal(first, memory.Entry21DFEC4);
        Assert.Equal(first, memory.Entry21DFEC8);
        Assert.Equal(second, memory.Entry21DFEC0);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress, memory.MagicTextureBufferAddress1D99A88);
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        Assert.Equal(first.Entry, restored.Entry21DFEC4!.Entry);
        Assert.Equal(second.Entry, restored.Entry21DFEC0!.Entry);
        Assert.Equal(first.Entry, restored.Entry21DFEC8!.Entry);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress, restored.MagicTextureBufferAddress1D99A88);
        NativeBattleOriginalEffectState current = new(2, 1, second, null, 0);
        Assert.Throws<NotSupportedException>(() => restored.RequireDirectEntry21DFEC4(current));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectMemory.FromColdProcess().RequireDirectEntry21DFEC4(current));
    }

    [Theory]
    [InlineData(0x50BEE0u, 0xED, 0, 0, 2, 99)]
    [InlineData(0x50BEE0u, 0xEE, 0, 0, 1, 99)]
    [InlineData(0x50BDC0u, 0xF1, 0, 0, 2, 99)]
    [InlineData(0x50BDC0u, 0xF1, 5, 0, 7, 99)]
    [InlineData(0x50BDC0u, 0xF1, 6, 0, 77, 99)]
    [InlineData(0x50BDC0u, 0xF1, 65532, 0, 8, 0)]
    [InlineData(0x50BDC0u, 0xF1, 65530, 3, 8, 254)]
    public void DirectParametersPreserveSkippedOwnersAndByteWrapping(uint callback, byte command, ushort action, byte effectLowByte, byte expected80, byte expected82)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Parameter80 = 77, Parameter82 = 99 };
        NativeBattleEffectMemory result = memory.SetDirectParameters(callback, command, action, effectLowByte);
        Assert.Equal(expected80, result.Parameter80);
        Assert.Equal(expected82, result.Parameter82);
    }

    [Theory]
    [InlineData(0x50BEE0u, 0xED, 0, 1, true, 0x10)]
    [InlineData(0x50BEE0u, 0xED, 0, 2, false, 0x10)]
    [InlineData(0x50BEE0u, 0xEE, 0, 0, true, 0)]
    [InlineData(0x50BDC0u, 0xF1, 65532, 0, true, 0)]
    [InlineData(0x50BDC0u, 0xF1, 65530, 0, true, 0x10)]
    [InlineData(0x50BDC0u, 0xF1, 0, 1, true, 0x10)]
    public void DirectCompletionUsesExactReadyByteAndBranchSpecificFlagCleanup(uint callback, byte command, ushort action, byte ready, bool complete, uint flags)
    {
        var fixture = Fixture(callback, command, action);
        NativeBattleGraphicsState graphics = fixture.Graphics with { OriginalEffect = fixture.Graphics.OriginalEffect! with { Parameters = fixture.Graphics.OriginalEffect.Parameters! with { Parameter81 = ready } } };
        var result = Poll(fixture.Session, graphics, 1);
        Assert.Equal(complete, result.Complete);
        Assert.Equal(flags, result.Graphics.Flags);
        Assert.Equal(1, fixture.Session.NativeEffectMemory!.Active);
    }

    [Fact]
    public void DirectInvocationReusesTheLoadedEntryWithoutLoadingAndIgnoresItsReturnedDescriptor()
    {
        var fixture = Fixture(0x50BEE0, 0xED, 0, 188);
        NativeBattleEffectBinding binding = new(188, 0x5BE340, 0x5BE320);
        NativeBattleEffectMemory memory = fixture.Session.NativeEffectMemory! with { Shot188 = NativeBattleShot188State.FromColdProcess() };
        NativeBattleModelAliases aliases = fixture.Session.NativeActionPackets.ModelAliases;
        NativeBattleOriginalEffectState selected = new(188, 187, binding, new(aliases.DecodeLiteral(0), 0, BattleModelTargets.From([aliases.DecodeLiteral(3)]), new BattleActionHeaderReference(0), 0, 0) { Parameter80 = 0 }, null);
        NativeBattleShot188Frame opened = NativeBattleShot188.Enter(new(memory, selected, new(1)), fixture.Session.NativeActionPackets);
        memory = opened.Memory.PublishEntry50AF20(binding, false);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(fixture.Session, memory);
        typeof(BattleSession).GetField("nativeCrtRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Session, opened.Random);
        NativeBattleGraphicsState graphics = fixture.Graphics with { OriginalEffect = opened.Effect };
        var result = Poll(fixture.Session, graphics, 0);
        Assert.Equal((byte)1, result.Phase);
        Assert.False(result.Complete);
        Assert.Equal(opened.Effect.ReturnedDescriptor, result.Graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.Equal(3, result.Graphics.OriginalEffect.Pools.Length);
        Assert.Equal((byte?)2, result.Graphics.OriginalEffect.Parameters!.Parameter80);
        Assert.Equal(0x5BE4D0u, result.Graphics.OriginalEffect.Pools[1].Slots[1].Callback);
        Assert.Equal(1, fixture.Session.NativeEffectMemory!.Shot188!.ShotSerial);
        Assert.Equal(memory.ReturnedTaskPointer1D96AAC, fixture.Session.NativeEffectMemory.ReturnedTaskPointer1D96AAC);
        Assert.Equal(binding, fixture.Session.NativeEffectMemory.Entry21DFEC4);
        Assert.Equal(1, fixture.Session.NativeEffectMemory.Active);
        Assert.Equal(0, fixture.Session.NativeEffectMemory.Cursor);
        Assert.Null(fixture.Session.NativeEffectMemory.Entry21DFEC0);
    }

    [Theory]
    [InlineData(0x50A9A0u, 11, 65531, 0, 0, false)]
    [InlineData(0x50A9A0u, 11, 65531, 1, 1, true)]
    [InlineData(0x50A9A0u, 14, 0, 1, 0, true)]
    [InlineData(0x50BDC0u, 241, 0, 0, 0, false)]
    [InlineData(0x50BDC0u, 241, 0, 1, 0, true)]
    [InlineData(0x50BDC0u, 241, 0, 1, 1, false)]
    [InlineData(0x50BDC0u, 241, 65532, 0, 0, true)]
    [InlineData(0x50BEE0u, 237, 0, 1, 0, true)]
    [InlineData(0x50BEE0u, 237, 0, 2, 0, false)]
    [InlineData(0x50BEE0u, 238, 0, 0, 0, true)]
    public void Presentation_service_readiness_uses_the_current_native_header_and_branch_specific_handshake(uint callback, byte command, ushort action, byte ready, byte cameraMask, bool expected)
    {
        var fixture = Fixture(callback, command, action);
        NativeBattleActionPacketsState packets = fixture.Session.NativeActionPackets;
        if (callback == 0x50A9A0)
        {
            packets = packets.WriteSequence(new BattleActionHeaderReference(0), 2);
            typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Session, packets);
        }
        NativeBattleGraphicsState graphics = fixture.Graphics with { OriginalEffect = fixture.Graphics.OriginalEffect! with { Parameters = fixture.Graphics.OriginalEffect.Parameters! with { Parameter81 = ready } } };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(fixture.Session, graphics);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeCamera))!.SetValue(fixture.Session, fixture.Session.NativeCamera! with { Mask = cameraMask });
        typeof(BattleSession).GetProperty("EffectPresentationInvoked", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Session, true);
        bool actual = (bool)typeof(BattleSession).GetMethod("ReadEffectPresentationServiceComplete", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fixture.Session, [])!;
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Publishing_the_advanced_source_keeps_its_pose_and_resource_fields_without_advancing_native_owners()
    {
        var fixture = Fixture(0x50BDC0, 241, 0);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(fixture.Session, fixture.Graphics);
        typeof(BattleSession).GetField("presentationForeignActors", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Session, new Dictionary<BattleModelOwner, NativeBattleForeignActor>());
        NativeCrtRandomState random = new(123);
        typeof(BattleSession).GetField("nativeCrtRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Session, random);
        string memory = NativeBattleTestSnapshots.Json(fixture.Session.NativeEffectMemory);
        NativeBattleOriginalEffectState original = fixture.Graphics.OriginalEffect!;
        NativeBattleGraphicsModel source = ReadOriginalModelCell(fixture.Graphics, 0) with { Sequence = 3, SequenceFrame = 4, ResourceFlags44 = 0x240, ActiveResourceRow100 = 11, OriginalResourceRow104 = 11, SequenceState = new() { Offset46 = 5, Pose = 2, PoseFrame114 = 3 } };
        typeof(BattleSession).GetMethod("PublishEffectPresentationSource", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fixture.Session, [source]);
        Assert.Same(source, ReadOriginalModelCell(fixture.Session.NativeGraphics!, 0));
        Assert.Equal(fixture.Graphics.Callback, fixture.Session.NativeGraphics!.Callback);
        Assert.Same(original, fixture.Session.NativeGraphics!.OriginalEffect);
        Assert.Equal(memory, NativeBattleTestSnapshots.Json(fixture.Session.NativeEffectMemory));
        Assert.Same(random, typeof(BattleSession).GetField("nativeCrtRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Session));
    }

    private static (BattleSession Session, NativeBattleGraphicsState Graphics) Fixture(uint callback, byte command, ushort action, ushort effect = 2)
    {
        NativeBattleEffectBinding binding = new(2, 0x6298A0, 0x629880);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().PublishEntry50AF20(binding, false) with { Active = 1 };
        BattleSession session = new(new BattleDefinition { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess() with { EffectMemory = memory }, Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeCamera))!.SetValue(session, NativeBattleCameraState.FromColdProcess());
        NativeBattleActionPacketsState packets = session.NativeActionPackets;
        BattleModelOwner source = packets.ModelAliases.DecodeLiteral(0);
        BattleModelOwner target = packets.ModelAliases.DecodeLiteral(3);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(source, command, 0, 0, action, effect, null));
        (packets, _) = packets.AppendContact(new(new(target, 0, 0, 0, 0, 0, 0), null));
        packets = packets.WritePlayerRepeatMode(header, 1);
        (packets, _) = packets.Publish(1);
        typeof(BattleSession).GetField("nativeActionPackets", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, packets);
        NativeBattleActionGraphicsState context = NativeBattleActionGraphics.Normalize(packets, header) with { Callback = callback };
        NativeBattleGraphicsState graphics = OriginalGraphics([(0, source, Model(0)), (3, target, Model(3))], flags: 0x10) with
        {
            ActionGraphics = context,
            OriginalEffect = new(2, 1, binding, new(source, 1, context.TargetActors.Resolve(packets.ModelAliases), header, 0, 0), 0)
        };
        typeof(BattleSession).GetField("presentationSourceOwner", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, source);
        typeof(BattleSession).GetField("presentationInvocationHeader", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, header);
        return (session, graphics);
    }

    private static NativeBattleGraphicsModel Model(int slot)
    {
        return new(0)
        {
            Flags2 = slot == 3 ? (ushort)0 : null,
            Transform = new(new(0, 0, 0), new((short)(slot * 100), 0, 0), 0, 0, 4096),
            HasLinkedModelRing140 = false,
            Rotation12 = 0,
            Rotation14 = 0,
            Rotation16 = 0
        };
    }

    private static (NativeBattleGraphicsState Graphics, byte Phase, bool Complete) Poll(BattleSession session, NativeBattleGraphicsState graphics, byte phase)
    {
        return ((NativeBattleGraphicsState, byte, bool))typeof(BattleSession).GetMethod("PollNativeActionGraphics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [graphics, phase])!;
    }
}
