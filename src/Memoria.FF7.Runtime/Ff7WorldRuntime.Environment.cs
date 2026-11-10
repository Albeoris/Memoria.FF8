namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldSkyState(int CloudPhase = 0, int CloudHeading = 0);

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldSkyState sky = new();
    private int worldMapState;

    public int WorldMapState => worldMapState;

    private void AdvanceSky()
    {
        if (mainState < 1 || Resources.World != 0)
        {
            return;
        }
        int heading = camera.Heading;
        if (heading - sky.CloudHeading > 0x800)
        {
            heading -= 0x1000;
        }
        else if (sky.CloudHeading - heading > 0x800)
        {
            heading += 0x1000;
        }
        int delta = ((heading - sky.CloudHeading) * 5000) / 10000;
        int phase = sky.CloudPhase - delta;
        if (phase < 0)
        {
            phase += 256;
        }
        else if (phase > 255)
        {
            phase -= 256;
        }
        sky = new(phase, (sky.CloudHeading + delta * 10000 / 5000) & 4095);
    }
}
