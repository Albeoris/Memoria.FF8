using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalCommandViewTests
{
    [Fact]
    public void Raw_view_exposes_the_same_command_and_survives_ordering_mutation()
    {
        NativeBattleModelTextureOwner material = new("party-10");
        NativeBattleSceneCapture capture = new(BattleSceneVisibleArea.Native, NativeBattleSceneCaptureKind.Image);
        NativeBattlePilotVisualIdentity identity = new(0, 0x502D40, 11, 3, 4);
        NativeBattleModelPrimitiveBatch.Builder batch = new();
        int emission = batch.BeginEmission(new("party-10", material, capture, identity, true, false));
        NativeBattlePilotGeometryReference pilot = batch.AddPilot(new(new(0, 0, 50), new(8, 0, 50), new(0, 8, 50), default, 7, emission, 3));
        batch.AddRaw(41, [0x04000000, 0x20010203, 0, 8, 8 << 16], [50, 60, 70, 0], [new(0, 0), new(8, 0), new(0, 8)], [], pilot, emission);

        NativeBattlePrimitiveReference packet = new("off_B6D070", 0, 20);
        NativeBattlePlacedModelPrimitives placement = new(batch.Freeze(), [packet], 19, "source-bank");
        using NativeBattleLogicalOrdering.Builder writer = new(NativeBattleLogicalOrdering.Clear());
        writer.PrependPlacedModel(placement, 0);
        NativeBattleLogicalOrdering ordering = writer.ToImmutable();
        ImmutableArray<byte> lengths = new byte[256].ToImmutableArray().SetItem(0x20, 4);
        NativeBattleLogicalCommandReference reference = ordering.TraverseReferences(lengths)[0];
        Assert.True(reference.HasModelPlacement);
        NativeBattleLogicalCommandView view = reference.Resolve();
        AssertMatches(reference, view);
        Assert.True(view.HasProjectedCoordinates);
        Assert.Same(material, view.ModelTextureOwner);
        Assert.Same(capture, view.SceneCapture);
        Assert.Equal(identity with { Primitive = 7 }, view.Pilot.Identity);

        using NativeBattleLogicalOrdering.Builder overwrite = new(ordering);
        overwrite.InvalidatePacketRange(packet, 20);
        overwrite.ToImmutable();
        Assert.Same(packet, view.Packet);
        Assert.Equal(0x20010203u, view.WordSpan[0]);
        Assert.Equal(60, view.VertexPayloadSpan[1]);
        Assert.Equal(new BattleSceneScreenPoint(0, 8), view.ProjectedCoordinateSpan[2]);
        Assert.Equal("party-10", view.PresentationActorId);
    }

    [Fact]
    public void Authored_view_preserves_literal_fields_and_defers_missing_depth_failure()
    {
        NativeBattleLogicalCommand literal = new(new("native", 1, 40), 2, 0x68, [0x68121314, 0x00020001], 5, [], 30)
        {
            OrderingBucket = 4000,
            ScreenCoverage = new(0, 0, 1, 1),
            ProjectedCoordinates = []
        };
        NativeBattleLogicalCommandView view = ((NativeBattleLogicalCommandReference)literal).Resolve();
        AssertMatches(literal, view);
        Assert.True(view.HasProjectedCoordinates);
        Assert.True(view.ProjectedCoordinateSpan.IsEmpty);
        Assert.True(view.VertexPayloadSpan.IsEmpty);

        NativeBattleLogicalCommandReference stateCommand = literal with { Opcode = 0xE2, Words = [0xE2000000], VertexPayload4 = default, ProjectedCoordinates = null };
        NativeBattleLogicalCommandView state = stateCommand.Resolve();
        Assert.Equal((byte)0xE2, state.Opcode);
        Assert.False(state.HasProjectedCoordinates);
        Assert.Equal("Native command has no original proxy depth fields.", Assert.Throws<InvalidDataException>(() => ReadDepthCount(stateCommand)).Message);

        static void ReadDepthCount(NativeBattleLogicalCommandReference command)
        {
            _ = command.Resolve().VertexPayloadSpan.Length;
        }
    }

    [Fact]
    public void Repeated_raw_and_authored_resolution_allocates_no_managed_storage()
    {
        NativeBattleModelPrimitiveBatch.Builder batch = new();
        int emission = batch.BeginEmission(new("ally", null, null, default, false, false));
        batch.AddRaw(30, [0x04000000, 0x200000FF, 0, 8, 8 << 16], [80, 90, 100, 0], [], [], default, emission);
        NativeBattlePlacedModelPrimitives placement = new(batch.Freeze(), [new("off_B6D070", 0, 0)], 19, "replacement");
        NativeBattleLogicalCommandReference raw = new(placement, 0, 30);
        NativeBattleLogicalCommandReference authored = raw.ToCommand();
        _ = ResolveMany(raw, authored);
        long start = GC.GetAllocatedBytesForCurrentThread();
        uint observed = ResolveMany(raw, authored);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;

        Assert.NotEqual(0u, observed);
        Assert.Equal(0, allocated);
    }

    private static uint ResolveMany(NativeBattleLogicalCommandReference raw, NativeBattleLogicalCommandReference authored)
    {
        uint result = 0;
        for (int index = 0; index < 2048; index++)
        {
            NativeBattleLogicalCommandView view = ((index & 1) == 0 ? raw : authored).Resolve();
            result += view.WordSpan[index & 3] + (uint)view.VertexPayloadSpan[0] + view.Opcode;
        }
        return result;
    }

    private static void AssertMatches(NativeBattleLogicalCommandReference command, in NativeBattleLogicalCommandView view)
    {
        Assert.Same(command.Packet, view.Packet);
        Assert.Equal(command.WordOffset, view.WordOffset);
        Assert.Equal(command.Opcode, view.Opcode);
        Assert.Equal(command.Flags20, view.Flags20);
        Assert.Equal(command.ModuleDraw, view.ModuleDraw);
        Assert.Equal(command.OrderingBucket, view.OrderingBucket);
        Assert.Equal(command.TextureSourceOwner, view.TextureSourceOwner);
        Assert.Equal(command.PresentationActorId, view.PresentationActorId);
        Assert.Same(command.ModelTextureOwner, view.ModelTextureOwner);
        Assert.Same(command.SceneCapture, view.SceneCapture);
        Assert.Same(command.ScreenCoverage, view.ScreenCoverage);
        Assert.Equal(command.Pilot, view.Pilot);
        Assert.Equal(command.HasProjectedCoordinates, view.HasProjectedCoordinates);
        Assert.True(command.WordSpan.SequenceEqual(view.WordSpan));
        Assert.True(command.VertexPayloadSpan.SequenceEqual(view.VertexPayloadSpan));
        Assert.True(command.ProjectedCoordinateSpan.SequenceEqual(view.ProjectedCoordinateSpan));
    }
}
