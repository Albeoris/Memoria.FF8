namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattlePcTextureWindowCache
{
    public NativeBattlePcTextureWindowCache PreprocessSprite463120(byte opcode, ushort texturePage, ushort clut)
    {
        RequireSpriteBaseCache(opcode, texturePage);
        return Preprocess462F90(texturePage, (ushort)(clut & 0x7FFF));
    }

    public NativeBattlePcTextureWindowResolve ResolveSprite462DF0(byte opcode, ushort texturePage, ushort clut, uint windowBits)
    {
        RequireSpriteBaseCache(opcode, texturePage);
        return Resolve465CE0(texturePage, (ushort)(clut & 0x7FFF), windowBits);
    }

    private void RequireSpriteBaseCache(byte opcode, ushort texturePage)
    {
        if (opcode is not (0x64 or 0x66))
        {
            throw new NotSupportedException("Native sprite texture preparation requires its selected variable-size opcode.");
        }
        int depth = texturePage >> 7 & 3;
        if (depth == 2)
        {
            return;
        }
        uint control = SpriteTileCacheControl1CCFD90 ?? throw new NotSupportedException("Native indexed sprite preparation requires its retained tile-cache control.");
        if (control != 0)
        {
            throw new NotSupportedException("Native indexed sprite preparation selected its separate tile-cache owner.");
        }
    }
}
