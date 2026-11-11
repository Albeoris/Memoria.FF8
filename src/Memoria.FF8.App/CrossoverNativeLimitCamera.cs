using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverNativeLimitCamera(NativeBattleActionMatrix RootMatrix, NativeBattleActionMatrix BodyMatrix, NativeBattleCameraState? Native = null, NativeBattleCameraPose? RenderPose = null, NativeBattleCameraProjection? RenderProjection = null)
{
    public static CrossoverNativeLimitCamera Capture(NativeBattleGraphicsModel source, NativeBattlePoseSource skeleton, CrossoverGunbladeHostPlacement placement, Vector3 targetRoot, Vector3 targetImpact)
    {
        Matrix4x4 inverse = Invert(placement.Matrix);
        NativeBattleModelTransform transform = source.Transform ?? throw new InvalidDataException("The guest Limit camera lacks its original source transform.");
        NativeBattleVector root = transform.Position28 with { Y = transform.Angle36 };
        NativeBattleVector bone = NativeBattlePoseMatrices.ReadBone(source, skeleton, 240, 2048);
        NativeBattleVector fallback = new(source.Rotation12!.Value, unchecked((short)(source.Rotation14!.Value + 2048)), source.Rotation16!.Value);
        NativeBattleActionMatrix rootMatrix = NativeBattleActionGeometryRules.CaptureMatrix(root, ToNative(Vector3.Transform(targetRoot, inverse)), fallback) with { Scale = 4096 };
        NativeBattleActionMatrix bodyMatrix = NativeBattleActionGeometryRules.CaptureMatrix(bone, ToNative(Vector3.Transform(targetImpact, inverse)), fallback);
        return new(rootMatrix, bodyMatrix);
    }

    public (NativeBattleCameraProjection Projection, short Roll) ForEffects(SceneCameraPlacement host, CrossoverGunbladeHostPlacement placement)
    {
        return Native is { Projection: { } native } owner ? (Centered(native), owner.Roll) : (CrossoverNativeSpriteRenderer.Camera(host, placement.Matrix), (short)0);
    }

    public CrossoverNativeLimitCamera Advance(NativeBattleEffectAnimationRegisters animation, NativeBattleGraphicsModel source, NativeBattlePoseSource skeleton, SceneCameraPlacement host, CrossoverGunbladeHostPlacement placement)
    {
        NativeBattleCameraState owner = Native ?? Initialize(host, placement);
        NativeBattleCameraPose renderedPose = owner.PublishedPresentationPose ?? owner.CurrentPose ?? throw new InvalidDataException("The guest Limit camera lost its preceding pose.");
        NativeBattleCameraProjection renderedProjection = Centered(owner.Projection ?? throw new InvalidDataException("The guest Limit camera lost its preceding projection."));
        if (owner.ControlResource is not { } current || current.Source.Identity != animation.Source.Identity || current.StreamOffset != animation.StreamOffset || current.ModelTableOffset != animation.ModelTableOffset)
        {
            owner = owner.RegisterControlResource(animation);
        }
        if (owner.ControlCursor is not null)
        {
            ImmutableArray<byte> sourceBytes = animation.Source.Data;
            NativeBattleCameraBank bank = new(sourceBytes[animation.ModelTableOffset..]);
            owner = owner.AdvanceControl(bank, sourceBytes[animation.StreamOffset..], new()
            {
                // Foreign targets have no FFVIII information BYTE246. The ordinary profile uses BYTE246=0, which initializes BYTE145=3.
                ReadExternal = variable => variable == 21 ? 3 : throw new NotSupportedException($"The guest Limit camera queried source variable {variable}.")
            });
        }
        owner = owner.Tick(4, new() { TransformReference = TransformReference }).PublishProjection(4);
        return this with { Native = owner, RenderPose = renderedPose, RenderProjection = renderedProjection };

        NativeBattleCameraPoint TransformReference(byte reference, NativeBattleCameraPoint point)
        {
            NativeBattleVector local = new(point.X, point.Y, point.Z);
            NativeBattleVector transformed = reference switch
            {
                0x90 => NativeBattlePoseMatrices.TransformCameraPoint(source, skeleton, 0, local, mirror: false),
                0xF2 => NativeBattlePoseMatrices.TransformCameraPoint(RootMatrix, local, mirror: false),
                0xF5 => NativeBattlePoseMatrices.TransformCameraPoint(BodyMatrix, local, mirror: false),
                _ => throw new NotSupportedException($"The guest Limit camera queried source matrix {reference:X2}.")
            };
            return Point(transformed);
        }
    }

    public SceneCameraPlacement? ForHost(CrossoverGunbladeHostPlacement placement)
    {
        if (RenderPose is not { } pose || RenderProjection is not { View: { } view, FocalLength: ushort focal })
        {
            return null;
        }
        Matrix4x4 matrix = placement.Matrix;
        Vector3 eye = Vector3.Transform(new Vector3(pose.Eye.X, pose.Eye.Y, pose.Eye.Z), matrix);
        Vector3 focus = Vector3.Transform(new Vector3(pose.Target.X, pose.Target.Y, pose.Target.Z), matrix);
        Vector3 right = Direction(0);
        Vector3 up = -Direction(3);
        Vector3 forward = Direction(6);
        return new(eye, focus) { Basis = new(eye, right, up, forward), FocalLength = focal };

        Vector3 Direction(int row) => Vector3.Normalize(Vector3.TransformNormal(new(view.Rotation[row], view.Rotation[row + 1], view.Rotation[row + 2]), matrix));
    }

    public void Validate()
    {
        RootMatrix.Validate();
        BodyMatrix.Validate();
        Native?.Validate();
        RenderProjection?.View?.Validate();
        if ((Native is null) != (RenderPose is null) || (Native is null) != (RenderProjection is null) || RenderProjection is { View: null } or { FocalLength: null or 0 })
        {
            throw new InvalidDataException("The saved guest Limit camera lacks its matching render frame.");
        }
    }

    private static NativeBattleCameraState Initialize(SceneCameraPlacement host, CrossoverGunbladeHostPlacement placement)
    {
        Matrix4x4 inverse = Invert(placement.Matrix);
        NativeBattleCameraPose pose = new(Point(ToNative(Vector3.Transform(host.Position, inverse))), Point(ToNative(Vector3.Transform(host.Focus, inverse))), 512, 0);
        return NativeBattleCameraState.FromColdProcess().Initialize500520() with { CurrentPose = pose, CapturedPose = pose, Projection = CrossoverNativeSpriteRenderer.Camera(host, placement.Matrix) };
    }

    private static NativeBattleCameraProjection Centered(NativeBattleCameraProjection projection) => projection with { OffsetX = 160, OffsetY = 120 };
    private static Matrix4x4 Invert(Matrix4x4 placement) => Matrix4x4.Invert(placement, out Matrix4x4 inverse) ? inverse : throw new InvalidDataException("The guest Limit camera requires an invertible origin placement.");
    private static NativeBattleCameraPoint Point(NativeBattleVector value) => new(value.X, value.Y, value.Z);
    private static NativeBattleVector ToNative(Vector3 value) => new(Word(value.X), Word(value.Y), Word(value.Z));
    private static short Word(float value) => checked((short)NativePrecision53.RoundInteger(value));
}
