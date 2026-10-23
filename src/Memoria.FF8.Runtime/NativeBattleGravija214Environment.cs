using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleGravija214Environment
{
    public static NativeBattleEffectMemory Clear45BEE0(NativeBattleEffectMemory memory, NativeSoftwareDrawingRectangle rectangle, byte red, byte green, byte blue)
    {
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Gravija requires its initialized VRAM owner.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Gravija requires its current primitive process.");
        NativeBattlePcTextureWindowCache cache = process.TextureWindowCache ?? throw new NotSupportedException("Gravija requires its initialized texture cache.");
        if (rectangle.X < 0 || rectangle.Y < 0 || rectangle.Width < 0 || rectangle.Height < 0 || rectangle.X + rectangle.Width > 1024 || rectangle.Y + rectangle.Height > 512)
        {
            throw new NotSupportedException("Gravija clear exceeds its bounded native VRAM rectangle.");
        }
        ushort color = unchecked((ushort)(((red >> 3) << 10) | ((green >> 3) << 5) | (blue >> 3)));
        byte[] words = new byte[checked(rectangle.Width * rectangle.Height * 2)];
        for (int offset = 0; offset < words.Length; offset += 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(words.AsSpan(offset), color);
        }
        NativeBattleEffectTextureState updated = textures.Upload45BD30(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, words);
        cache = cache.Invalidate464850(rectangle.X, rectangle.Y, rectangle.X + rectangle.Width - 1, rectangle.Y + rectangle.Height - 1, textures);
        return memory with { Textures = updated, PrimitiveProcess = process with { TextureWindowCache = cache } };
    }

    public static NativeBattleEffectMemory SetOrigin45B5E0(NativeBattleEffectMemory memory, int x, int y)
    {
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Gravija requires its current primitive process.");
        NativeBattleSoftwarePrimitiveState software = process.Software ?? throw new NotSupportedException("Gravija requires its current drawing environment.");
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironment.SetOrigin45B5E0(software.Environment, x, y);
        return memory with { PrimitiveProcess = process with { Software = software with { Environment = environment } } };
    }
}
