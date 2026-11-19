using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7CustomLightingConsumerTests
{
    [Fact]
    public void Required_normal_consumer_fails_before_default_lighting()
    {
        Ff7ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0) { RequiresNativeCustomLighting = true };
        Assert.Throws<NotSupportedException>(() => Ff7FieldRenderer.RequireCustomLightingSupport(actor, [Model()]));
    }

    [Fact]
    public void Ordinary_hidden_and_absent_normal_consumers_keep_existing_skips()
    {
        Ff7ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0);
        Ff7FieldRenderer.RequireCustomLightingSupport(actor, [Model()]);
        actor = actor with { RequiresNativeCustomLighting = true };
        Ff7FieldRenderer.RequireCustomLightingSupport(actor with { Visible = false }, [Model()]);
        Ff7FieldRenderer.RequireCustomLightingSupport(actor, [null]);
        Ff7FieldRenderer.RequireCustomLightingSupport(actor, [Model() with { Normals = [] }]);
        Ff7FieldRenderer.RequireCustomLightingSupport(actor, [Model() with { Groups = [] }]);
    }

    private static PcPolygonModel Model()
    {
        PcModelGroup group = new(0, 0, 1, 0, 3, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        PcModelPolygon polygon = new(0, 0, 1, 2, 0, 0, 0, ReadOnlyMemory<byte>.Empty);
        return new([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], [Vector3.UnitZ], [], [], [], [polygon], [], [group], ReadOnlyMemory<byte>.Empty);
    }
}
