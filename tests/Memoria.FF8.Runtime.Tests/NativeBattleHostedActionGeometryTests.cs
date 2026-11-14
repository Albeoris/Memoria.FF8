using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedActionGeometryTests
{
    [Theory]
    [InlineData(0, 0x08, 0x08)]
    [InlineData(3, 0x45, 0x45)]
    [InlineData(2, 0x04, 0x04)]
    [InlineData(2, 0x4004, 0x04)]
    public void Equivalent_seven_row_geometry_preserves_native_json_and_all_matrices(int actor, int sourceMask, int targetMask)
    {
        ImmutableArray<NativeBattleGraphicsActor?> rows = Enumerable.Range(0, 7).Select(row => (NativeBattleGraphicsActor?)new NativeBattleGraphicsActor($"actor-{row}", row < 3)).ToImmutableArray();
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(row =>
        {
            NativeBattleVector position = Position(row);
            return new NativeBattleGraphicsModel(row, 0)
            {
                Flags2 = 4,
                Rotation12 = 100,
                Rotation14 = 200,
                Rotation16 = 300,
                Transform = new(position, position, 0, position.Y, 4096)
            };
        }).ToImmutableArray();
        NativeBattleGraphicsState initial = new(models, rows.Select(row => row?.ActorId).ToImmutableArray(), 0, 0);
        NativeBattleGraphicsState native = NativeBattleActionGeometryRules.PrepareGeometry50D300(initial, actor, (ushort)sourceMask, (ushort)targetMask, NativeBattleActionGeometryGlobals.FromColdProcess(), ReadBone);
        NativeBattleGraphicsActorSet hostedSource = sourceMask == 0x4004 ? Rows(2, 3) : NativeBattleGraphicsActorSet.FromNative((ushort)sourceMask);
        NativeBattleGraphicsState hosted = NativeBattleActionGeometryRules.PrepareGeometry50D300(initial with { PresentationRoster = new(rows) }, actor, hostedSource, NativeBattleGraphicsActorSet.FromNative((ushort)targetMask), NativeBattleActionGeometryGlobals.FromColdProcess(), ReadBone);
        NativeBattleActionGeometry legacyGeometry = native.ActionContext!;
        NativeBattleActionGeometry hostedGeometry = hosted.ActionContext!;

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(legacyGeometry));
        Assert.False(json.RootElement.TryGetProperty(nameof(NativeBattleActionGeometry.PresentationTargets), out _));
        Assert.False(json.RootElement.TryGetProperty(nameof(NativeBattleActionGeometry.PresentationSourceTargets), out _));
        Assert.Equal((ushort)sourceMask, legacyGeometry.SourceTargetMask);
        Assert.Equal(sourceMask == 1 << actor, legacyGeometry.IsSelfTarget);
        Assert.Equal(legacyGeometry.IsSelfTarget, hostedGeometry.IsSelfTarget);
        Assert.Equal(legacyGeometry.UnadjustedDistance, hostedGeometry.UnadjustedDistance);
        Assert.Equal(legacyGeometry.ContactDistance, hostedGeometry.ContactDistance);
        Assert.Equal(legacyGeometry.TargetYaw, hostedGeometry.TargetYaw);
        Assert.Equal(legacyGeometry.TargetIndex, hostedGeometry.TargetIndex);
        Assert.Equal(legacyGeometry.PhysicalTargetMask, hostedGeometry.PhysicalTargetMask);
        Assert.Equal(JsonSerializer.Serialize(legacyGeometry.Matrices), JsonSerializer.Serialize(hostedGeometry.Matrices));
        Assert.Equal(JsonSerializer.Serialize(legacyGeometry.TargetMatrix), JsonSerializer.Serialize(hostedGeometry.TargetMatrix));
        Assert.Equal((native.Models[actor].Rotation12, native.Models[actor].Rotation14, native.Models[actor].Rotation16),
            (hosted.Models[actor].Rotation12, hosted.Models[actor].Rotation14, hosted.Models[actor].Rotation16));

        static NativeBattleVector ReadBone(NativeBattleGraphicsState graphics, int row, byte bone, short fraction)
        {
            NativeBattleVector position = graphics.Models[row].Transform!.Position28;
            return new((short)(position.X + bone), (short)(position.Y + fraction / 4), position.Z);
        }
    }

    [Fact]
    public void Hosted_group_uses_all_selected_rows_and_explicit_roles()
    {
        NativeBattleGraphicsState initial = CreateGraphics();
        NativeBattleGraphicsActorSet targets = Rows(7, 16, 24);
        NativeBattleGraphicsState graphics = Prepare(initial, 0, Rows(0, 7), targets);
        NativeBattleActionGeometry context = graphics.ActionContext!;

        graphics.Validate();
        Assert.Equal(targets, context.TargetActors);
        Assert.Equal((ushort)1, context.SourceTargetMask);
        Assert.Equal((ushort)0, context.PhysicalTargetMask);
        Assert.Equal((byte)24, context.TargetIndex);
        Assert.Equal((short)2048, graphics.Models[0].Rotation14);
        Assert.Equal((ushort)4, graphics.Models[0].Flags2);
        foreach (int row in targets.EnumerateRows())
        {
            Assert.Equal((ushort)0x14, graphics.Models[row].Flags2);
        }
        NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter([Position(7), Position(16), Position(24)]);
        Assert.Equal(center, NativeBattlePoseMatrices.TransformCameraPoint(context.TargetMatrix!, default, false));
        Assert.Equal(NativeBattlePoseMatrices.RotationWords(new(0, 0, 0)).ToArray(), context.TargetMatrix!.Rotation.ToArray());

        NativeBattleGraphicsState partySource = Prepare(initial, 7, default, Rows(0, 24));
        Assert.Equal((short)0, partySource.Models[7].Rotation14);
        Assert.Equal(NativeBattlePoseMatrices.RotationWords(new(0, 2048, 0)).ToArray(), partySource.ActionContext!.TargetMatrix!.Rotation.ToArray());
    }

    [Fact]
    public void Hosted_self_target_round_trips_when_the_native_mask_is_zero()
    {
        NativeBattleGraphicsState graphics = Prepare(CreateGraphics(), 24, Rows(24), Rows(24));
        NativeBattleActionGeometry context = graphics.ActionContext!;
        Assert.True(context.IsSelfTarget);
        Assert.Equal(1000, context.UnadjustedDistance);
        Assert.Equal(1000, context.ContactDistance);
        Assert.Equal((ushort)0, context.SourceTargetMask);
        Assert.Equal((ushort)0, context.PhysicalTargetMask);
        Assert.False((context with { PresentationSourceTargets = Rows(16, 24) }).IsSelfTarget);
        Assert.True(new NativeBattleActionGeometry(2, 4).IsSelfTarget);
        Assert.False(new NativeBattleActionGeometry(2, 0x4004).IsSelfTarget);

        string json = JsonSerializer.Serialize(graphics);
        NativeBattleGraphicsState restored = JsonSerializer.Deserialize<NativeBattleGraphicsState>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(Rows(24), restored.ActionContext!.TargetActors);
        Assert.True(restored.ActionContext.IsSelfTarget);
        Assert.Equal(5, restored.ActionContext.Matrices!.Value.Length);
    }

    [Fact]
    public void Hosted_camera_counts_the_explicit_party_and_all_selected_targets()
    {
        NativeBattleGraphicsState graphics = Prepare(CreateGraphics(), 0, Rows(0), Rows(0, 7, 16, 24));
        Assert.Equal(2, NativeBattleCameraGraphics.ReadVariable(graphics, 19));
        Assert.Equal(1, NativeBattleCameraGraphics.ReadVariable(graphics, 27));
        Assert.Equal(4, NativeBattleCameraGraphics.ReadVariable(graphics, 26));
        Assert.Equal(24, NativeBattleCameraGraphics.ReadVariable(graphics, 25));
        Assert.Equal(0x20, NativeBattleCameraGraphics.ReadVariable(graphics, 22));

        graphics = Prepare(graphics, 0, Rows(0), Rows(16));
        Assert.Equal(0x21, NativeBattleCameraGraphics.ReadVariable(graphics, 22));
    }

    [Fact]
    public void Hosted_geometry_rejects_unbound_rows_and_inconsistent_native_projections()
    {
        NativeBattleGraphicsState initial = CreateGraphics();
        NativeBattleGraphicsState graphics = Prepare(initial, 0, Rows(0), Rows(7, 24));
        NativeBattleActionGeometry context = graphics.ActionContext!;
        Assert.Throws<InvalidDataException>((graphics with { PresentationRoster = null }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionContext = context with { PhysicalTargetMask = 1 } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionContext = context with { SourceTargetMask = 0 } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionContext = context with { PresentationSourceTargets = null } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionContext = context with { PresentationTargets = default(NativeBattleGraphicsActorSet) } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionContext = context with { TargetIndex = 16 } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionContext = context with { ActorIndex = 17 } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionContext = context with { PresentationTargets = Rows(17, 24) } }).Validate);
        Assert.Throws<InvalidDataException>((graphics with { ActionContext = context with { PresentationSourceTargets = Rows(0, 17) } }).Validate);
        Assert.Throws<InvalidDataException>(() => Prepare(initial, 0, Rows(0), Rows(17)));
        Assert.Throws<InvalidDataException>(() => Prepare(initial, 0, Rows(17), Rows(7)));
        Assert.Throws<InvalidDataException>(() => Prepare(initial, 17, Rows(0), Rows(7)));
        Assert.Throws<InvalidDataException>(() => Prepare(initial, 0, Rows(0), default));

        NativeBattleGraphicsState shortBank = CreateGraphics(8);
        Assert.Throws<InvalidDataException>(() => Prepare(shortBank, 0, Rows(0), Rows(24)));
        Assert.Throws<InvalidDataException>(() => Prepare(shortBank with { PresentationRoster = null }, 0, Rows(0), Rows(7)));
    }

    private static NativeBattleGraphicsState Prepare(NativeBattleGraphicsState graphics, int actor, NativeBattleGraphicsActorSet source, NativeBattleGraphicsActorSet targets)
    {
        return NativeBattleActionGeometryRules.PrepareGeometry50D300(graphics, actor, source, targets, NativeBattleActionGeometryGlobals.FromColdProcess(), (state, row, _, _) => state.Models[row].Transform!.Position28);
    }

    private static NativeBattleGraphicsActorSet Rows(params int[] rows)
    {
        NativeBattleGraphicsActorSet set = default;
        foreach (int row in rows)
        {
            set = set.Add(row);
        }
        return set;
    }

    private static NativeBattleGraphicsState CreateGraphics(int count = 25)
    {
        NativeBattleGraphicsActor?[] rows = new NativeBattleGraphicsActor?[count];
        rows[0] = new("enemy", false);
        rows[7] = new("ally-7", true);
        if (count == 25)
        {
            rows[16] = new("ally-16", true);
            rows[24] = new("enemy-24", false);
        }
        NativeBattleGraphicsRoster roster = new(rows.ToImmutableArray());
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearPresentationBank(roster);
        foreach (int row in roster.PartyRows.Concat(roster.EnemyRows))
        {
            NativeBattleVector position = Position(row);
            models[row] = models[row] with
            {
                Flags0 = 2,
                Flags2 = 4,
                Flags8 = row is 16 or 24 ? 0x20u : 0u,
                ModelId4 = roster[row].IsParty ? (byte)1 : (byte)20,
                ResourceFlags44 = 1,
                Rotation12 = 0,
                Rotation14 = 0,
                Rotation16 = 0,
                SequenceState = new(),
                Transform = new(position, position, 0, position.Y, 4096)
            };
        }
        return new(models.ToImmutableArray(), rows.Select(row => row?.ActorId).ToImmutableArray(), 0, 0) { PresentationRoster = roster };
    }

    private static NativeBattleVector Position(int row) => new((short)(row * 300), (short)(row * 10), (short)(row * -100));
}
