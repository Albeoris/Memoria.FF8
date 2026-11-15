using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectParametersTests
{
    private static readonly BattleModelOwner[] HostedOwners =
    [
        new BattleForeignPartyModel(new BattleForeignPresentationKey("extra-ally")),
        new BattleForeignEnemyModel(new BattleForeignPresentationKey("extra-enemy-a")),
        new BattleForeignEnemyModel(new BattleForeignPresentationKey("extra-enemy-b"))
    ];

    [Fact]
    public void Original_parameter_window_projects_the_complete_native_selection()
    {
        NativeBattleEffectParameters parameters = Parameters([OriginalOwner(0), OriginalOwner(6)]);

        Assert.Equal(new[] { OriginalOwner(0), OriginalOwner(6) }, parameters.TargetActors.Enumerate());
        Assert.Equal(new byte[] { 3, 8, 0x41, 0 }, NativeBattleCinematicParameterBytes.Read(parameters, 0, 4));
    }

    [Fact]
    public void Hosted_parameter_window_preserves_full_selection_and_marks_raw_word_unknown()
    {
        BattleModelOwner[] owners = [OriginalOwner(0), OriginalOwner(6), .. HostedOwners];
        NativeBattleEffectParameters parameters = Parameters(owners);

        Assert.Equal(owners, parameters.TargetActors.Enumerate());
        Assert.Equal(new byte[] { 3, 8 }, NativeBattleCinematicParameterBytes.Read(parameters, 0, 2));
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicParameterBytes.Read(parameters, 2, 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Effect312_centroid_uses_the_full_typed_selection(bool hosted)
    {
        BattleModelOwner[] expected = hosted ? HostedOwners : [OriginalOwner(0), OriginalOwner(6)];
        NativeBattleEffectParameters parameters = Parameters(expected);
        int centroidCalls = 0;
        NativeBattleEffect312Inputs inputs = Inputs() with
        {
            TargetCenter5020A0 = (frame, targets, output) =>
            {
                centroidCalls++;
                Assert.Equal(expected, targets.Enumerate());
                return new(frame, output.Write(0, new byte[] { 123, 0 }));
            }
        };

        NativeBattleEffect312Step step = NativeBattleEffect312Controller.AdvanceMain59CC60(Frame(parameters), ChildBank, Resources(), inputs);

        Assert.Equal(1, centroidCalls);
        Assert.Equal(123, step.Frame.State.ReadWord(TargetPointAddress));
    }

    private static NativeBattleEffectParameters Parameters(IEnumerable<BattleModelOwner> owners)
    {
        return new NativeBattleEffectParameters(OriginalOwner(3), 8, BattleModelTargets.From(owners), new BattleActionHeaderReference(0), 0, 0).CaptureOriginalWindow(OriginalAliases);
    }

    private static NativeBattleEffect312Frame Frame(NativeBattleEffectParameters parameters)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 1);
        (pool, _) = pool.Append(MainCallback);
        NativeBattleEffect312State state = NativeBattleEffect312State.FromColdProcess().WriteModel(CasterGlobal, parameters.Source, OriginalAliases);
        NativeBattleOriginalEffectState effect = new(312, 311, new(312, Entry, Initializer), parameters, null) { Pools = [pool] };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(123), state);
    }

    private static NativeBattleEffect312Resources Resources()
    {
        byte[] axis = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(axis.AsSpan(8), -4096);
        return new(null!, null!, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty,
            ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(CenterOffsetAddress, new byte[6].ToImmutableArray()).Add(AlignmentAddress, axis.ToImmutableArray()));
    }

    private static NativeBattleEffect312Inputs Inputs()
    {
        NativeBattlePackedMatrix32 world = NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 4096), null);
        NativeBattlePackedMatrix32 bone = NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096), null);
        return new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => 0, null!, null!, (_, _, _) => throw new InvalidOperationException(), null!,
            (_, _) => world, (_, _, _) => bone, (_, _, _) => throw new InvalidOperationException(), null!, null!, null!, null!, null!, (frame, operation) =>
            {
                Assert.IsType<NativeBattleEffect312Stage5713E0>(operation);
                return frame;
            });
    }
}
