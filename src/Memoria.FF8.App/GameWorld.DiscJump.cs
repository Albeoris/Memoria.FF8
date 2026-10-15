using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private void ValidateDiscJumpArrival(FieldTransition? arrival, bool allocating)
    {
        if (arrival?.DiscJump is not NativeDiscJumpTransition request)
        {
            return;
        }
        NativeDiscProcessState disc = session.DiscProcess ?? throw new NotSupportedException("DISCJUMP destination entry requires its actual disc process owner.");
        request.Validate(disc.SavedDisc204);
        if (assets.NativeRuntimeProfile is not { IsVerified: true } profile || request.ExecutableHash != profile.ExecutableHash || request.SourceFieldId != session.State.PreviousFieldId || arrival.Direction != unchecked((byte)request.NativeFacingWord))
        {
            throw new InvalidDataException("DISCJUMP destination entry does not match its native executable or departing field.");
        }
        if (!allocating)
        {
            return;
        }
        //4706B0 kind9 reloads wm2field before selecting entry kind0. The Steam
        //1001F290 callback selects the requested marker and forces phase6.
        NativeSteamDiscMount actual = NativeSteamDiscMountReader.Read(assets.GameDirectory, disc.SavedDisc204);
        if (actual != request.Mount)
        {
            throw new InvalidDataException("Steam DISCJUMP marker or reloaded wm2field changed after the issuing request.");
        }
    }
}
