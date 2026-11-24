using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class BattleLoadoutModelPreview
{
    private const int Width = 300;
    private const int Height = 350;
    private readonly BattleRenderer renderer = new(Width * BattleRenderer.ModelPreviewSampleScale, Height * BattleRenderer.ModelPreviewSampleScale);
    private readonly CrossoverFf8BattleActorVisual[] idle;
    private readonly BattleCamera camera;
    private readonly PixelImage image = new(Width, Height);
    private int renderedFrame = -1;

    private BattleLoadoutModelPreview(CrossoverFf8BattleActorVisual[] idle)
    {
        this.idle = idle;
        Vector3 front = Vector3.Normalize(new Vector3(0.24f, 0.12f, 1));
        Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, front));
        Vector3 up = Vector3.Cross(front, right);
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        foreach (CrossoverFf8BattleActorVisual pose in idle)
        {
            foreach (Vector3 point in BattleRenderer.MeasureModelPreview(pose))
            {
                Vector3 viewPoint = new(Vector3.Dot(point, right), Vector3.Dot(point, up), Vector3.Dot(point, front));
                minimum = Vector3.Min(minimum, viewPoint);
                maximum = Vector3.Max(maximum, viewPoint);
            }
        }
        Vector3 extents = (maximum - minimum) * 0.5f;
        if (!float.IsFinite(extents.LengthSquared()) || extents.LengthSquared() <= 0)
        {
            throw new InvalidDataException("The selected battle model has no visible idle geometry.");
        }
        Vector3 midpoint = (minimum + maximum) * 0.5f;
        Vector3 target = right * midpoint.X + up * midpoint.Y + front * midpoint.Z;
        const float fieldOfView = 0.48f;
        const float fill = 0.84f;
        float distance = extents.Z + MathF.Max(extents.X * Height / Width, extents.Y) / (fill * MathF.Tan(fieldOfView / 2));
        camera = new(target + front * distance, target, fieldOfView);
    }

    internal static BattleLoadoutModelPreview Prepare(BattleAssets resources, int character, int costume, int weaponId)
    {
        BattleModel body = resources.LoadPlayerForWeapon(character, costume, weaponId);
        BattleModel? weapon = resources.ResolvePlayerRenderWeapon(character, weaponId, body);
        bool independentWeapon = weapon?.NativeWeaponUsesBodySkeleton == false;
        NativeBattlePoseSource source = body.NativePoseSource ?? throw new InvalidDataException("The selected battle model lacks its native idle animation bank.");
        NativeBattleGraphicsResource resource = resources.LoadPlayerSequences(character, weaponId, body).NativeResource with
        {
            PoseSource = source,
            AnimationFrames = FrameCounts(body),
            LinkedPoseSource = independentWeapon ? weapon!.NativePoseSource : null,
            LinkedAnimationFrames = independentWeapon ? FrameCounts(weapon!) : []
        };
        BattlePartyActorModel owner = new(new("loadout-preview"));
        NativeGlobalRandomState random = new(0);
        NativeBattleGraphicsModel model = new(0)
        {
            Transform = new(default, default, 0, 0, 4096),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() },
            LinkedTransform = independentWeapon ? new(default, default) : null
        };
        model = NativeBattleGraphicsModels.CompleteLoad(model, checked((byte)character), 0, true, ref random);
        model = NativeBattleGraphicsModels.SelectSequence(model, NativeBattleGraphicsModels.PlayerStandingSequence(0));
        var inputs = new NativeBattleSequenceInputs(0, 0);
        model = NativeBattleSequenceMachine.Advance(owner, model, resource, inputs).Model;
        byte frameCount = model.SequenceState?.PoseLimit115 ?? throw new InvalidDataException("The standing sequence did not initialize its idle clip.");
        body = CapturePreviewModel(body);
        weapon = weapon is null ? null : CapturePreviewModel(weapon);
        Matrix4x4 placement = Matrix4x4.CreateRotationX(MathF.PI) * Matrix4x4.CreateScale(0.01f);
        List<CrossoverFf8BattleActorVisual> idle = [];
        for (int frame = 0; frame < frameCount; frame++)
        {
            model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, source) };
            idle.Add(new(body, weapon, model, placement));
            if (frame + 1 < frameCount)
            {
                model = NativeBattleSequenceMachine.Advance(owner, model, resource, inputs).Model;
            }
        }
        return new(idle.ToArray());
    }

    private static ImmutableArray<byte> FrameCounts(BattleModel model)
    {
        return model.Animations.Select(animation => checked((byte)animation.Frames.Count)).ToImmutableArray();
    }

    private static BattleModel CapturePreviewModel(BattleModel model)
    {
        if (model.EditableOverride is not null && model.Meshes.All(mesh => mesh.Vertices.All(vertex => vertex.NativePosition.HasValue)))
        {
            // Animation-only overrides are already represented by the rewritten native pose source.
            model = model with { EditableOverride = null };
        }
        return model.Textures is ResourceTextureList textures ? model with { Textures = textures.Snapshot() } : model;
    }

    internal PixelImage Render(float seconds)
    {
        int frame = (int)(seconds * ModelExchange.FramesPerSecond) % idle.Length;
        if (frame != renderedFrame)
        {
            renderer.RenderModelPreview(idle[frame], camera, image);
            renderedFrame = frame;
        }
        return image;
    }

    internal int CurrentFrame => renderedFrame;
}
