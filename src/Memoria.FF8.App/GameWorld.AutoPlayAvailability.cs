using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal string ReadAutoPlayFieldProgramHash(int fieldId)
    {
        return assets.ReadFieldProgramHash(fieldId);
    }

    internal bool VerifyAutoPlayGatewayDestination(int sourceField, int index, int actualField)
    {
        if (sourceField is < 72 or >= 0x7FFF || actualField is < 72 or >= 0x7FFF || index < 0 || session.State.Scene != SceneKind.Field || session.State.FieldId != actualField || !HasCurrentFieldAllocation || Events.FieldId != actualField || Events.Scene.PreviousFieldWord84 != sourceField)
        {
            return false;
        }
        FieldInformation? information;
        try
        {
            information = assets.LoadField(sourceField).Information;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        return information is not null && VerifyAutoPlayGatewayDestination(information, index, actualField);
    }

    internal static bool VerifyAutoPlayGatewayDestination(FieldInformation information, int index, int actualField)
    {
        if (index < 0 || actualField is < 72 or >= 0x7FFF)
        {
            return false;
        }
        // Match the exact imported list used by the native movement observer,
        // including world entries that occupy indices but are not field proofs.
        FieldGateway[] gateways = information.Gateways.Where(gateway => gateway.Defined || gateway.DestinationField < 72).ToArray();
        if ((uint)index >= gateways.Length)
        {
            return false;
        }
        FieldGateway gateway = gateways[index];
        return gateway.DestinationField == actualField && gateway.DestinationTriangle is >= 0 and < 0x7FFF && gateway.DestinationX != short.MaxValue && gateway.DestinationY != short.MaxValue;
    }
}
