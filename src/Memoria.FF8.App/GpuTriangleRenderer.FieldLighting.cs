using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

public sealed partial class GpuTriangleRenderer
{
    private const int FieldShadowMapSize = 1024;
    private readonly Vector4[] fieldLightPositions = new Vector4[8];
    private readonly Vector4[] fieldLightDirections = new Vector4[8];
    private readonly Vector4[] fieldLightColors = new Vector4[8];
    private readonly Vector4[] fieldLightCones = new Vector4[8];
    private FieldLightingScene? fieldLightingScene;
    private RenderTarget2D? fieldShadowTarget;
    private bool fieldShadowPass;
    private int fieldPrimaryIndex = -1;

    public bool FieldLightingSupported => effect.Parameters["FieldLightCount"] is not null && effect.Parameters["FieldShadowMatrix"] is not null && effect.Techniques["FieldLightingTechnique"] is not null && effect.Techniques["FieldShadowTechnique"] is not null;
    public string? FieldLightingUnavailableReason => FieldLightingSupported ? null : "This graphics effect has no field spotlight shader.";

    public void SetFieldLighting(FieldLightingScene? scene)
    {
        if (!active)
        {
            throw new InvalidOperationException("Field lighting must be configured inside an active scene.");
        }
        Flush();
        fieldLightingScene = scene;
        fieldPrimaryIndex = -1;
        int count = 0;
        Array.Clear(fieldLightPositions);
        Array.Clear(fieldLightDirections);
        Array.Clear(fieldLightColors);
        Array.Clear(fieldLightCones);
        if (scene is not null)
        {
            foreach (FieldSpotLight light in scene.Lights.Where(light => light.Enabled))
            {
                if (count >= fieldLightPositions.Length)
                {
                    throw new InvalidDataException("Field lighting supports at most eight enabled spotlights.");
                }
                if (light.Id == scene.PrimaryLightId)
                {
                    fieldPrimaryIndex = count;
                }
                System.Numerics.Vector3 color = FieldLightingMath.LinearColor(light.Color);
                fieldLightPositions[count] = new(light.Position.X, light.Position.Y, light.Position.Z, light.Range);
                fieldLightDirections[count] = new(light.Direction.X, light.Direction.Y, light.Direction.Z, light.Intensity);
                fieldLightColors[count] = new(color.X, color.Y, color.Z, 0);
                fieldLightCones[count] = new(MathF.Cos(light.InnerConeDegrees * MathF.PI / 180), MathF.Cos(light.OuterConeDegrees * MathF.PI / 180), 0, 0);
                count++;
            }
        }
        effect.Parameters["FieldLightCount"].SetValue(count);
        effect.Parameters["FieldLightPositionRange"].SetValue(fieldLightPositions);
        effect.Parameters["FieldLightDirectionIntensity"].SetValue(fieldLightDirections);
        effect.Parameters["FieldLightColor"].SetValue(fieldLightColors);
        effect.Parameters["FieldLightCone"].SetValue(fieldLightCones);
        effect.Parameters["FieldPrimaryIndex"].SetValue(fieldPrimaryIndex);
        effect.Parameters["FieldShadowParameters"].SetValue(Vector4.Zero);
        effect.Parameters["FieldLightingPass"].SetValue(0f);
        effect.Parameters["FieldShadowTexture"].SetValue((Texture2D?)null);
        device.Textures[1] = null;
    }

    public bool PrepareFieldShadow(IReadOnlyList<FieldShadowTriangle> casters, out string? reason)
    {
        reason = null;
        if (fieldLightingScene is null || fieldPrimaryIndex < 0 || casters.Count == 0)
        {
            return true;
        }
        Flush();
        FieldSpotLight light = fieldLightingScene.Lights.First(light => light.Id == fieldLightingScene.PrimaryLightId);
        RenderTargetBinding[] savedTargets = device.GetRenderTargets();
        Viewport savedViewport = device.Viewport;
        try
        {
            if (fieldShadowTarget is null)
            {
                device.Adapter.QueryRenderTargetFormat(device.GraphicsProfile, SurfaceFormat.Vector2, DepthFormat.Depth24, 0, out SurfaceFormat colorFormat, out DepthFormat depthFormat, out int samples);
                if (colorFormat != SurfaceFormat.Vector2 || depthFormat != DepthFormat.Depth24 || samples != 0)
                {
                    reason = "The graphics backend does not support the required single-sample floating-point shadow color target and depth attachment.";
                    return false;
                }
                fieldShadowTarget = new RenderTarget2D(device, FieldShadowMapSize, FieldShadowMapSize, false, SurfaceFormat.Vector2, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
            }
            System.Numerics.Matrix4x4 matrix = FieldLightingMath.ShadowViewProjection(light);
            effect.Parameters["FieldShadowMatrix"].SetValue(new Matrix(matrix.M11, matrix.M12, matrix.M13, matrix.M14, matrix.M21, matrix.M22, matrix.M23, matrix.M24, matrix.M31, matrix.M32, matrix.M33, matrix.M34, matrix.M41, matrix.M42, matrix.M43, matrix.M44));
            effect.Parameters["FieldShadowTexture"].SetValue((Texture2D?)null);
            device.Textures[1] = null;
            device.SetRenderTarget(fieldShadowTarget);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(0f, 0, 0, 1), 1, 0);
            device.RasterizerState = RasterizerState.CullNone;
            fieldShadowPass = true;
            foreach (FieldShadowTriangle caster in casters)
            {
                // The floating-point depth/level target contains no scene alpha and requires an opaque write, without blending.
                TriangleMaterial material = caster.Material with { Blend = TriangleBlend.Replace, PreserveTargetAlpha = false, DepthWrite = true, ColorWrite = true, DepthComparison = TriangleDepthComparison.LessEqual };
                Draw(caster.A, caster.B, caster.C, material);
            }
            Flush();
            effect.Parameters["FieldShadowParameters"].SetValue(new Vector4(1, fieldLightingScene.GroundShadow.Opacity, fieldLightingScene.GroundShadow.Softness / FieldShadowMapSize, 1f / FieldShadowMapSize));
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
        {
            vertexCount = 0;
            reason = $"Spotlight shadow target unavailable: {exception.Message}";
            effect.Parameters["FieldShadowParameters"].SetValue(Vector4.Zero);
            return false;
        }
        finally
        {
            fieldShadowPass = false;
            device.SetRenderTargets(savedTargets);
            device.Viewport = savedViewport;
        }
        effect.Parameters["FieldShadowTexture"].SetValue(fieldShadowTarget);
        return true;
    }
}
