using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private bool AcknowledgeDigitalDiscMedia(byte _)
    {
        byte disc = Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent3Byte, 0);
        if (disc is < 1 or > 3)
        {
            throw new NotSupportedException("The native disc-change operand does not identify one of the three supported media identities.");
        }
        if (installation.Layout != Ff7InstallationLayout.SteamWorkingDirectory || !Directory.Exists(installation.GetRequired(Ff7ResourceRole.Movies).Path))
        {
            throw new NotSupportedException("Disc change requires the selected unified digital media backend or an explicit physical-media implementation.");
        }
        return true;
    }
}
