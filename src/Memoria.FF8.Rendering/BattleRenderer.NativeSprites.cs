using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    private void DrawNativePrimitiveSprite(NativeBattlePrimitiveFrame frame, NativeBattleLogicalResolvedCommand resolved, NativeBattleEffectTextureState? textures, NativeBattlePrimitiveCapabilities? capabilities, NativeBattlePcTextureWindowCache? textureWindowCache, IReadOnlyList<NativeBattleTextureReplacement>? replacements, NativeBattleEffectTextureState? previousTextureOwner, bool sameReplacements)
    {
        NativeBattleLogicalCommand command = resolved.Command;
        bool semi = (command.Opcode & 2) != 0;
        if (resolved.Pass == 1 && semi || resolved.Pass == 2 && !semi)
        {
            return;
        }
        ImmutableArray<NativeBattleSprite64Corner> corners = NativeBattleSprite64Geometry.Expand(command.Words, command.Flags20);
        NativeBattlePrimitiveDepthState depthState = frame.LastDepthState ?? throw new NotSupportedException("Native sprite rendering requires its retained depth owner.");
        NativeBattlePrimitiveColorState colorState = frame.LastColorState ?? throw new NotSupportedException("Native sprite rendering requires its retained diffuse color owner.");
        bool perspective = !command.VertexPayload4.IsEmpty && command.VertexPayload4[0] != 0 && (frame.LastDescriptorPerspective ?? throw new NotSupportedException("Native sprite rendering requires its executed descriptor depth mode."));
        ImmutableArray<int> depths = command.VertexPayload4.IsEmpty ? [] : [command.VertexPayload4[0], command.VertexPayload4[0], command.VertexPayload4[0], command.VertexPayload4[0]];
        ImmutableArray<NativeBattlePrimitiveDepthVertex> projected = depthState.Project461220(command.OrderingBucket ?? throw new InvalidDataException("Native sprite lacks its original OT bucket."), perspective, command.Flags20, depths);
        int mode = resolved.DrawMode.BlendMode ?? throw new NotSupportedException("Native sprite diffuse color requires its executed ABR.");
        NativeBattlePrimitiveColor diffuse = colorState.Textured(command.Words[0], semi, mode, false);
        PixelImage texture = ResolveNativePrimitiveTexture(frame, resolved, textures, capabilities, textureWindowCache, replacements, colorState, previousTextureOwner, sameReplacements);
        Submit(0, semi ? mode : -1);
        if (!semi)
        {
            uint filter = resolved.DrawMode.SpriteFilter?.Current ?? throw new NotSupportedException("Native opaque sprite requires its retained filter control.");
            if (filter != 0)
            {
                NativeSoftwareNormalViewportDerived viewport = resolved.DrawMode.NormalViewport ?? throw new NotSupportedException("Native filtered sprite requires its executed normal viewport.");
                if (viewport.ScaleFlags != 0)
                {
                    Submit(4, 0);
                }
            }
        }

        void Submit(int offset, int blend)
        {
            PrimitiveVertex a = At(0);
            PrimitiveVertex b = At(1);
            PrimitiveVertex c = At(2);
            QueueNativePrimitive(a, b, c, texture, blend, resolved.Pass);
            QueueNativePrimitive(b, At(3), c, texture, blend, resolved.Pass);

            PrimitiveVertex At(int index)
            {
                NativeBattleSprite64Corner corner = corners[index];
                Vector2 position = NativePrimitivePosition(unchecked(corner.X + offset), unchecked(corner.Y + offset), command.Flags20, resolved.DrawMode);
                return new(position.X, position.Y, projected[index].Depth, projected[index].ReciprocalW, new(corner.TextureU, corner.TextureV), diffuse.Rgb, 127);
            }
        }
    }
}
