namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWeightedColorStep(NativeBattleEffectMemory Memory, uint Color);

public static class NativeBattleWeightedColor56CC00
{
    public static NativeBattleWeightedColorStep Blend(NativeBattleEffectMemory memory, uint first, uint second, short firstFactor, short secondFactor, uint destination, NativeBattleSummonColors constants)
    {
        NativeBattleVector firstRgb = new((byte)first, (byte)(first >> 8), (byte)(first >> 16));
        NativeBattleVector secondRgb = new((byte)second, (byte)(second >> 8), (byte)(second >> 16));
        NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(memory, firstRgb, secondRgb, firstFactor, secondFactor, constants);
        uint color = (destination & 0xFF000000) | (byte)blended.Position.X | (uint)(byte)blended.Position.Y << 8 | (uint)(byte)blended.Position.Z << 16;
        return new(blended.Memory, color);
    }
}
