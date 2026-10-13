namespace Memoria.FF8.Runtime;

public sealed record NativeScriptBattleFlagMigrationProof(int Version, string SourceCheckpointSha256, string SourceSemanticSha256, string WriterManifestSha256)
{
    public const string RetainedArrivalCheckpointSha256 = "42A1D4A20EE147A04454FF83F77E9F11162FEC94DA2F3849E0268BE46BCE3675";
    public const string RetainedArrivalSemanticSha256 = "5BAC2084855DAEA3AEDFB233563542A6A7FBACD624E1497E760D66E68CD2A3CF";
    public const string RetainedRouteWriterManifestSha256 = "7DA82A6FD0CFD3F6C8C4A1CFC5CB04CD109FC96A1AE6FF22B854A6F158B01A22";

    public void Validate()
    {
        if (Version != 1 || SourceCheckpointSha256 != RetainedArrivalCheckpointSha256 || SourceSemanticSha256 != RetainedArrivalSemanticSha256 || WriterManifestSha256 != RetainedRouteWriterManifestSha256)
        {
            throw new InvalidDataException("Unknown native script-battle flag migration authority.");
        }
    }
}
