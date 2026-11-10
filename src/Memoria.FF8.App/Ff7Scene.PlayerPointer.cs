using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private PixelImage? playerPointer;

    private Ff7FieldPointerVisual? PlayerPointer()
    {
        if (Session is null || error is not null || !Ff7PlayerPointer.IsRequested(Session.Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent3Byte, 30), Session.Host.Process))
        {
            return null;
        }
        Ff7Actor? actor = Session.Host.Process.Actors.SingleOrDefault(item => item.PlayerControlled);
        if (actor is null)
        {
            return null;
        }
        PcFieldCamera camera = CurrentMovieCamera() ?? Session.Field.Cameras[0];
        Ff7PlayerPointerBounds? placed = Ff7PlayerPointer.Project(actor, Session.Field.Scripts.Scale, camera, FieldCameraOffset(Session.Host.Process, CurrentMovieCamera()));
        if (placed is not { } point)
        {
            return null;
        }
        if (playerPointer is null)
        {
            using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
            IReadOnlyList<LgpEntry> matches = archive.Find("hand.tex");
            if (matches.Count != 1)
            {
                throw new InvalidDataException("FFVII player-pointer presentation requires its unique original field texture.");
            }
            playerPointer = Ff7FieldRenderer.DecodeTexture(PcTexture.Read(archive.ReadEntry(matches[0])));
            if (playerPointer.Width != 128 || playerPointer.Height != 16)
            {
                throw new NotSupportedException("The selected original player-pointer texture has unsupported dimensions.");
            }
        }
        return new(playerPointer, new System.Numerics.Vector2(point.X, point.Y), point.FlipHorizontal, point.FlipVertical);
    }
}
