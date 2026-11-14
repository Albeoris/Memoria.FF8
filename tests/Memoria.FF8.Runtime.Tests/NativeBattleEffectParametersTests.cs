using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectParametersTests
{
    private const uint OriginalParameterAddress = 0x1D99A78;
    private static readonly NativeBattleGraphicsActorSet HostedRows = default(NativeBattleGraphicsActorSet).Add(7).Add(16).Add(24);

    [Fact]
    public void Legacy_json_preserves_native_selection_and_shape()
    {
        const string json = """
            {"Actor78":3,"Flags79":8,"TargetMask7A":65,"HeaderIndex7C":0,"Parameter81":0,"Parameter83":0,"Parameter80":null,"Parameter82":null}
            """;
        NativeBattleEffectParameters parameters = JsonSerializer.Deserialize<NativeBattleEffectParameters>(json)!;
        Assert.Null(parameters.PresentationTargets);
        Assert.Equal(new[] { 0, 6 }, parameters.TargetActors.EnumerateRows());
        using JsonDocument legacy = JsonDocument.Parse(json);
        using JsonDocument saved = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
        Assert.Equal(
            legacy.RootElement.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => (property.Name, property.Value.GetRawText())),
            saved.RootElement.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).Select(property => (property.Name, property.Value.GetRawText())));
    }

    [Fact]
    public void Hosted_json_retains_full_selection_and_original_raw_word()
    {
        NativeBattleEffectParameters parameters = new(3, 8, 0x41, 0, 0, 0) { PresentationTargets = HostedRows.Add(0).Add(6) };
        string json = JsonSerializer.Serialize(parameters);
        NativeBattleEffectParameters restored = JsonSerializer.Deserialize<NativeBattleEffectParameters>(json)!;
        Assert.Equal(new[] { 0, 6, 7, 16, 24 }, restored.TargetActors.EnumerateRows());
        Assert.Equal(new byte[] { 0x41, 0 }, NativeBattleCinematicParameterBytes.Read(restored, 2, 2));
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty(nameof(NativeBattleEffectParameters.TargetActors), out _));
    }

    [Fact]
    public void Mismatched_native_projection_fails_on_target_access()
    {
        NativeBattleEffectParameters parameters = new(3, 8, 1, 0, 0, 0) { PresentationTargets = HostedRows };
        Assert.Throws<InvalidDataException>(() => parameters.TargetActors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Effect312_centroid_uses_the_full_hosted_selection_or_the_native_raw_word(bool hosted)
    {
        NativeBattleEffectParameters parameters = new(3, 8, 0, 0, 0, 0) { PresentationTargets = hosted ? HostedRows : null };
        uint parameterAddress = hosted ? OriginalParameterAddress : 0x1000u;
        int parameterReads = 0;
        int centroidCalls = 0;
        NativeBattleEffect312Inputs inputs = Inputs() with
        {
            ReadPhysical = (_, address, length) =>
            {
                Assert.Equal(parameterAddress, address);
                Assert.Equal(8, length);
                parameterReads++;
                return NativeBattleEffectArena.Clear().Write(2, new byte[] { 0x41, 0 });
            },
            TargetCenter5020A0 = (frame, targets, output) =>
            {
                centroidCalls++;
                Assert.Equal(hosted ? new[] { 7, 16, 24 } : new[] { 0, 6 }, targets.EnumerateRows());
                return new(frame, output.Write(0, new byte[] { 123, 0 }));
            }
        };

        NativeBattleEffect312Step step = NativeBattleEffect312Controller.AdvanceMain59CC60(Frame(parameters, parameterAddress), ChildBank, Resources(), inputs);

        Assert.Equal(hosted ? 0 : 1, parameterReads);
        Assert.Equal(1, centroidCalls);
        Assert.Equal(123, step.Frame.State.ReadWord(TargetPointAddress));
    }

    [Fact]
    public void Effect312_rejects_hosted_selection_for_an_unowned_parameter_pointer()
    {
        NativeBattleEffectParameters parameters = new(3, 8, 0, 0, 0, 0) { PresentationTargets = HostedRows };
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect312Controller.AdvanceMain59CC60(Frame(parameters, OriginalParameterAddress + 2), ChildBank, Resources(), Inputs()));
    }

    private static NativeBattleEffect312Frame Frame(NativeBattleEffectParameters parameters, uint parameterAddress)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ChildBank, 36, 1);
        (pool, _) = pool.Append(MainCallback);
        NativeBattleEffect312State state = NativeBattleEffect312State.FromColdProcess().WriteDword(CasterGlobal, 3).WriteDword(ParametersGlobal, parameterAddress);
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
        return new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => 0, null!, (_, _, _) => throw new InvalidOperationException(), null!,
            (_, _) => world, (_, _, _) => bone, (_, _, _) => throw new InvalidOperationException(), null!, null!, null!, null!, null!, (frame, operation) =>
            {
                Assert.IsType<NativeBattleEffect312Stage5713E0>(operation);
                return frame;
            });
    }
}
