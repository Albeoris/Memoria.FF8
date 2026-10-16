using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Rendering;

public sealed partial class BattleRenderer
{
    /// <summary>572200's reached FT4/G4 groups used by5753A0 and575250.</summary>
    private void DrawNativeStaticMesh(NativeBattleModuleDraw draw, NativeBattleEffectTextureState? textures, NativeBattleStaticMeshResource resource, int? inheritedBlendMode)
    {
        NativeBattleModuleAnchor anchor = draw.Anchor ?? throw new InvalidDataException("Native572200 mesh requires its original translation.");
        NativeBattleVector rotation = draw.MeshRotation ?? throw new InvalidDataException("Native572200 mesh requires its original rotation.");
        NativeBattleVector scale = draw.MeshScale ?? throw new InvalidDataException("Native572200 mesh requires its original scale.");
        if ((draw.MeshFlags & ~0x2F7u) != 0)
        {
            throw new NotSupportedException("Native572200 flags require an unsupported primitive material override.");
        }
        byte[] bytes = resource.Bytes.ToArray();
        short[] matrix = NativeBattlePoseMatrices.RotationWords(rotation).Select((value, index) => unchecked((short)(value * (index % 3 == 0 ? scale.X : index % 3 == 1 ? scale.Y : scale.Z) >> 12))).ToArray();
        int cursor = checked((int)U32(bytes, 0));
        for (int group = 0; group < 8; group++)
        {
            int count = checked((int)U32(bytes, cursor));
            cursor += 4;
            if (count != 0 && group is not (3 or 5))
            {
                throw new NotSupportedException($"Native static mesh group{group} requires its original572200 primitive contract.");
            }
            for (int face = 0; face < count; face++)
            {
                int start = cursor;
                bool textured = group == 3;
                ushort page = textured ? U16(bytes, start + 18) : (ushort)0;
                ushort clut = textured ? (draw.MeshFlags & 0x200) != 0 ? draw.MeshClut : U16(bytes, start + 14) : (ushort)0;
                PixelImage? texture = textured ? NativeActorTexturePage(textures ?? throw new NotSupportedException("Native textured static mesh requires its retained original VRAM owner."), page, clut) : null;
                byte command = bytes[start + 3];
                if ((draw.MeshFlags & 1) != 0)
                {
                    command |= 2;
                }
                if ((draw.MeshFlags & 4) != 0)
                {
                    command &= 0xFD;
                }
                int blend = (command & 2) == 0 ? -1 : textured ? (page >> 5) & 3 : inheritedBlendMode ?? throw new NotSupportedException("Native untextured static mesh requires its inherited ordering-table draw mode.");
                float fade = (draw.MeshFlags & 0x40) != 0 ? (4096 - Math.Clamp(draw.MeshFade, 0, 4096)) / 4096f : 1;
                EffectVertex At(int corner)
                {
                    int vertex = 8 + U16(bytes, start + 4 + corner * 2) * 4;
                    int x = S16(bytes, vertex);
                    int y = S16(bytes, vertex + 2);
                    int z = S16(bytes, vertex + 4);
                    int Row(int row) => (int)(((long)matrix[row * 3] * x + (long)matrix[row * 3 + 1] * y + (long)matrix[row * 3 + 2] * z) >> 12);
                    Vector3 position = Vector3.Transform(NativeWorld(anchor.X + Row(0), anchor.Y + Row(1), anchor.Z + Row(2)), view);
                    int colorOffset = start + (textured || corner == 0 ? 0 : 8 + corner * 4);
                    Vector3 color = new Vector3(bytes[colorOffset], bytes[colorOffset + 1], bytes[colorOffset + 2]) * fade;
                    if (textured && (command & 1) != 0)
                    {
                        color = new(128);
                    }
                    int uvOffset = start + (corner == 0 ? 12 : corner == 1 ? 16 : corner == 2 ? 20 : 22);
                    return new(position, textured ? new(bytes[uvOffset], bytes[uvOffset + 1]) : Vector2.Zero, color);
                }
                EffectVertex a = At(0);
                EffectVertex b = At(1);
                EffectVertex c = At(2);
                if ((draw.MeshFlags & 0x10) != 0 || Vector3.Dot(Vector3.Cross(b.Position - a.Position, c.Position - a.Position), a.Position) > 0)
                {
                    EffectTriangle(a, b, c, texture, blend, textured);
                    EffectTriangle(b, At(3), c, texture, blend, textured);
                }
                cursor += 24;
            }
        }
    }
}
