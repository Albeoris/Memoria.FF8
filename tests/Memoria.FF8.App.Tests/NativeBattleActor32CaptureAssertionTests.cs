using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleActor32CaptureAssertionTests
{
    [Fact]
    public void Two_physical_primitives_replayed_in_two_passes_keep_two_distinct_visual_identities()
    {
        NativeBattleLogicalCommand first = Command(0, 7);
        NativeBattleLogicalCommand second = Command(40, 8);
        NativeBattleActor32CaptureAssertions.AssertPhysicalIdentities([first, second, first with { }, second with { }]);
    }

    [Fact]
    public void Buffer_one_and_its_normalized_reference_identify_the_same_physical_command()
    {
        NativeBattleLogicalCommand first = Command(0, 7) with { Packet = new("off_B6D070", 1, 0) };
        NativeBattleLogicalCommand replay = first with { Packet = new("off_B6D070", 0, 114688) };
        NativeBattleActor32CaptureAssertions.AssertPhysicalIdentities([first, replay]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Different_physical_identity_collisions_and_diverging_replays_are_rejected(int change)
    {
        NativeBattleLogicalCommand first = Command(0, 7);
        NativeBattleLogicalCommand other = change switch
        {
            0 => first with { Packet = new("20DFAB8", 0, 40) },
            1 => first with { PilotGeometry = first.PilotGeometry! with { Identity = first.PilotGeometry!.Identity with { Primitive = 8 } } },
            2 => first with { Words = first.Words.SetItem(1, 0x12345678) },
            3 => first with { VertexPayload4 = first.VertexPayload4.SetItem(3, 1) },
            4 => first with { Opcode = 0x24 },
            _ => first with { WordOffset = 2 }
        };
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => NativeBattleActor32CaptureAssertions.AssertPhysicalIdentities([first, other]));
    }

    private static NativeBattleLogicalCommand Command(int offset, int primitive)
    {
        NativeBattlePilotVisualIdentity identity = new(286, 0x2507CE8, 0, 1, primitive) { ResourceGeneration = 29 };
        return new(new("20DFAB8", 0, offset), 1, 0x20, [0x200000FF, 0, 10, 10u << 16], 0, [2000, 2000, 2000, 0], null)
        {
            PilotGeometry = new(identity, new(0, 0, 2000), new(10, 0, 2000), new(0, 10, 2000), default, 3) { SourceDepths = new(2000, 2000, 2000, 0), UseSourceDepths = true }
        };
    }
}
