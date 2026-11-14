using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class ExpandedBattleSceneFrameBuilder(BattleAssets assets)
{
    private sealed record Layout(int EncounterId, int StageId, int AllyCount, int EnemyCount, bool OriginalSlots, ImmutableSortedDictionary<int, BattleEnemyRosterSource>? Added);
    private readonly record struct ActorLayout(string Id, bool IsPlayer, BattleRosterSlot? Slot, int? SceneSlot, bool IsActive, bool Hidden, int? Form, int Model, int Costume, int? Weapon);
    private sealed record IdleBody(ImmutableArray<Vector3> Vertices, float StandingGround);

    private readonly ConditionalWeakTable<BattleSession, Layout> layouts = new();
    private readonly ConditionalWeakTable<BattleModel, IdleBody> idleBodies = new();
    private BattleModel? debugBody;
    private Layout? previousLayout;
    private ActorLayout[] previousActors = [];
    private ExpandedBattleSceneFrame? previousFrame;

    internal ExpandedBattleSceneFrame ReadSceneFrame(BattleSession owner)
    {
        Layout layout = layouts.GetValue(owner, ReadLayout);
        ActorLayout[] actors = owner.Combatants.Select(actor => new ActorLayout(actor.Id, actor.IsPlayer, actor.Slot, actor.SceneSlot, actor.IsActive, actor.Hidden,
            owner.GetGuardianFormVisual(actor.Id), actor.ModelId, actor.Costume, actor.WeaponId)).ToArray();
        if (previousFrame is not null && layout == previousLayout && actors.AsSpan().SequenceEqual(previousActors))
        {
            return previousFrame;
        }

        BattleEncounter encounter = assets.LoadEncounter(layout.EncounterId);
        BattleStage stage = assets.LoadStage(layout.StageId);
        int cameraId = encounter.PrimaryCamera & 0xF7;
        if (!stage.IntroductionCameras.TryGetValue(cameraId, out BattleStageCamera? camera))
        {
            throw new NotSupportedException($"Stage {stage.Id} camera {cameraId} requires an unsupported native endpoint resolver.");
        }

        var positions = ImmutableDictionary.CreateBuilder<string, NativeBattleVector>(StringComparer.Ordinal);
        List<Vector3> vertices = [];
        foreach (ActorLayout actor in actors)
        {
            NativeBattleVector position = Position(actor, layout, encounter);
            positions.Add(actor.Id, position);
            if (actor.IsPlayer || actor.IsActive && !actor.Hidden)
            {
                AddIdleBody(actor, position, vertices);
            }
        }
        int activeEnemies = actors.Count(actor => !actor.IsPlayer && actor.IsActive);
        ExpandedBattleFrame fitted = ExpandedBattleFraming.Fit(camera, 0, vertices, layout.AllyCount, Math.Max(1, activeEnemies));
        ExpandedBattleSceneFrame frame = new(fitted.Pose, fitted.Projection, positions.ToImmutable());
        previousLayout = layout;
        previousActors = actors;
        previousFrame = frame;
        return frame;
    }

    private static Layout ReadLayout(BattleSession owner)
    {
        BattleDefinition definition = owner.ReadDefinition();
        int encounterId = definition.EncounterId ?? throw new NotSupportedException("Expanded battle framing requires an installed encounter camera.");
        int stageId = definition.StageId ?? throw new NotSupportedException("Expanded battle framing requires its installed stage identity.");
        int allyCount = definition.Combatants.Count(actor => actor.IsPlayer);
        return new(encounterId, stageId, allyCount, definition.Combatants.Length - allyCount, definition.UsesOriginalEnemySlots, definition.AddedEnemySources);
    }

    private static NativeBattleVector Position(ActorLayout actor, Layout layout, BattleEncounter encounter)
    {
        if (!actor.IsPlayer && layout.OriginalSlots)
        {
            if (actor.SceneSlot is not int sceneSlot)
            {
                throw new InvalidDataException("An original enemy requires its authored scene slot.");
            }
            if (layout.Added is { } added && added.ContainsKey(sceneSlot))
            {
                return ExpandedBattleFormation.AddedEnemyPosition(sceneSlot, added.Count);
            }
            return encounter.Slots.SingleOrDefault(slot => slot.Slot == sceneSlot)?.NativePosition
                ?? throw new InvalidDataException($"Original enemy scene slot {sceneSlot} lacks its authored position.");
        }
        int position = actor.Slot?.Position ?? throw new InvalidDataException("Expanded actor has no roster position.");
        return ExpandedBattleFormation.Position(position, actor.IsPlayer ? layout.AllyCount : layout.EnemyCount, actor.IsPlayer);
    }

    private void AddIdleBody(ActorLayout actor, NativeBattleVector position, List<Vector3> vertices)
    {
        BattleModel model = actor.Form is int form ? assets.LoadEnemy(form) : actor.IsPlayer ? assets.LoadPlayer(actor.Model, actor.Costume)
            : actor.Model == -1 ? (debugBody ??= BattleDebugModels.CreateDummy()) : assets.LoadEnemy(actor.Model);
        IdleBody body = idleBodies.GetValue(model, ReadIdleBody);
        Matrix4x4 transform = Matrix4x4.CreateRotationY(actor.IsPlayer ? MathF.PI : 0) * Matrix4x4.CreateTranslation(ScenePosition(position) - Vector3.UnitY * body.StandingGround);
        foreach (Vector3 vertex in body.Vertices)
        {
            vertices.Add(Vector3.Transform(vertex, transform));
        }
    }

    private static IdleBody ReadIdleBody(BattleModel model)
    {
        if (model.Meshes.Count == 0 && model.NativeRenderGeometry is { IsEmpty: true })
        {
            return new([], 0);
        }
        var vertices = ImmutableArray.CreateBuilder<Vector3>();
        vertices.AddRange(model.Pose(0, 0).SelectMany(mesh => mesh));
        if (vertices.Count == 0)
        {
            return new([], 0);
        }
        float ground = vertices.Min(vertex => vertex.Y);
        // Installed poses interpolate between transformed vertex endpoints. With positive depth, their projected extrema occur at the endpoints.
        // Edited models still use BattleModel.Pose for each sample, with their own interpolation between samples.
        for (int frame = 1; frame < model.Animations[0].Frames.Count; frame++)
        {
            vertices.AddRange(model.Pose(0, frame).SelectMany(mesh => mesh));
        }
        return new(vertices.ToImmutable(), ground);
    }

    internal static Vector3 ScenePosition(NativeBattleVector position) => new Vector3(position.X, -position.Y, -position.Z) / 100f;
}
