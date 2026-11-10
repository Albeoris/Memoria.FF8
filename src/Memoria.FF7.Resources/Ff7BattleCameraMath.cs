namespace Memoria.FF7.Resources;

public sealed record Ff7BattleCameraMath(float DegreesPerAngleUnit, double RadiansPerDegree, double DegreesPerRadian, float AngleUnitsPerDegree)
{
    public int Sine(int angle) => (int)(Math.Sin((double)(float)(angle * DegreesPerAngleUnit) * RadiansPerDegree) * 4096);
    public int Cosine(int angle) => (int)(Math.Cos((double)(float)(angle * DegreesPerAngleUnit) * RadiansPerDegree) * 4096);
    public int Angle(int x, int z) => (int)((double)(float)Math.Atan2(x, z) * DegreesPerRadian * AngleUnitsPerDegree);
}
