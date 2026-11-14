using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedResourceHandleConsumerTests
{
    private static readonly MethodInfo ReadAddress = typeof(BattleSession).GetMethod("NativeModelResourceAddress100", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void Session_row_address_tracks_current_owner_and_preserves_staged_precedence()
    {
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.TurnBased);
        NativeBattleGraphicsModel native = new(0, 0) { ActiveResourceRow100 = 10 };
        NativeBattleGraphicsModel extended = native with { ActiveResourceRow100 = 11 };
        Assert.Equal(NativeBattleModelResourceRowAddress.Encode(10), Address(session, native));
        Assert.Equal(0x910000u, Address(session, extended with { ActiveStagedResourceRow100 = 0x910000 }));
        AssertOutOfExtent();
        Assert.IsType<NotSupportedException>(Assert.Throws<TargetInvocationException>(() => Address(session, native with { ActiveResourceRow100 = null })).InnerException);

        BattleModelOwner?[] rows = new BattleModelOwner?[7];
        for (int row = 0; row < rows.Length; row++)
        {
            rows[row] = new($"party-{row}", true);
        }
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown().WithPresentationRoster(new([.. rows]));
        PropertyInfo owner = typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!;
        owner.SetValue(session, cpu);
        Assert.Equal(NativeBattleModelResourceRowAddress.Encode(11, cpu.Rows.Length), Address(session, extended));
        owner.SetValue(session, cpu with { Rows = cpu.Rows[..11] });
        AssertOutOfExtent();

        void AssertOutOfExtent()
        {
            Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Address(session, extended)).InnerException);
        }
    }

    [Fact]
    public void Breath_body_capture_passes_hosted_alias_to_the_body_publisher()
    {
        const uint actor = 0x1D972C0;
        int capacity = 12;
        uint row = NativeBattleModelResourceRowAddress.Encode(11, capacity);
        NativeBattleBreath129Frame frame = NativeBattleBreath129LowerTests.Frame();
        NativeBattleBreath129Inputs inputs = NativeBattleBreath129LowerTests.Inputs() with
        {
            ReadModel = (_, address) =>
            {
                Assert.Equal(actor, address);
                return new(0, 0) { ActiveResourceRow100 = 11 };
            },
            ReadResourceRowCapacity = () => capacity,
            PublishBody508C90 = (current, handle) =>
            {
                Assert.Equal(row, handle);
                return new(current, new(handle, (offset, length) =>
                {
                    Assert.Equal((0, 4), (offset, length));
                    return NativeBattleEffectArena.Clear().Write(0, new byte[4]);
                }, []));
            }
        };
        NativeBattleBreath129Frame captured = NativeBattleBreath129Body.Capture6B73F0(frame, actor, 0, 0, null!, inputs);
        Assert.Equal(frame.Random, captured.Random);
        capacity = 11;
        Assert.Throws<InvalidDataException>(() => NativeBattleBreath129Body.Capture6B73F0(frame, actor, 0, 0, null!, inputs));
    }

    [Fact]
    public void Mega_spark_reads_the_current_bone_through_the_hosted_alias()
    {
        const int capacity = 37;
        uint expected = NativeBattleModelResourceRowAddress.Encode(36, capacity);
        uint modelAddress = NativeBattleGraphicsModelAddress.Encode(24);
        NativeBattleMegaSpark279Resources resources = NativeBattleMegaSpark279StateTests.Resources();
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() };
        NativeBattleMegaSpark279State state = NativeBattleMegaSpark279State.FromColdProcess().WriteDword(NativeBattleMegaSpark279Profile.CasterGlobal, modelAddress);
        NativeBattleMegaSpark279Frame frame = new(memory, new(279, 278, new(279, NativeBattleMegaSpark279Profile.Entry, NativeBattleMegaSpark279Profile.Initializer), null, 0), new(123), state);
        List<uint> observed = [];
        NativeBattleMegaSpark279Inputs inputs = new(
            NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270,
            _ => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(),
            (_, address) =>
            {
                Assert.Equal(modelAddress, address);
                return new(24, 0) { ActiveResourceRow100 = 36 };
            },
            (_, _) => throw new InvalidOperationException(),
            (_, handle, _) =>
            {
                observed.Add(handle);
                return NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            },
            (_, _, _, _) => throw new InvalidOperationException(),
            (_, _) => throw new InvalidOperationException(),
            _ => throw new InvalidOperationException(),
            (current, _) => current)
        {
            ReadResourceRowCapacity = () => capacity
        };

        var result = NativeBattleMegaSpark279Controller.ReadCasterPoint680C00(frame, NativeBattleMegaSpark279Profile.SourcePointsAddress, resources, inputs);
        Assert.Equal(expected, Assert.Single(observed));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    private static uint Address(BattleSession session, NativeBattleGraphicsModel model)
    {
        return (uint)ReadAddress.Invoke(session, [model])!;
    }
}
