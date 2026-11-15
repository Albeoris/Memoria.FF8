using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed class Ff7ChocoboHud
{
    private readonly Ff7ChocoboHudData data;
    private readonly PcTexture[] textures;
    private readonly Dictionary<(int Texture, int Palette), PixelImage> images = new();

    public Ff7ChocoboHud(Ff7Installation installation)
    {
        data = Ff7PcExecutableData.LoadChocoboHudData(installation);
        string archiveName = installation.Language switch
        {
            "en" => "chocobo.lgp",
            "de" => "gchocobo.lgp",
            "fr" => "fchocobo.lgp",
            "es" => "schocobo.lgp",
            _ => throw new NotSupportedException($"FFVII Chocobo HUD language '{installation.Language}' is unsupported.")
        };
        using LgpArchive archive = LgpArchive.Open(Path.Combine(installation.DataDirectory, "minigame", archiveName));
        textures = data.Textures.Select(name => PcTexture.Read(archive.ReadEntry(archive.Find(name).Single()))).ToArray();
    }

    public void Render(ITriangleRenderer renderer, Ff7ChocoboRuntime race, int width, int height)
    {
        if (race.Phase is Ff7ChocoboPhase.Results or Ff7ChocoboPhase.Completed)
        {
            return;
        }
        IReadOnlyList<Ff7ChocoboRacerState> racers = race.Racers;
        Ff7ChocoboRacerState[] ranking = race.HudRacers.OrderBy(racer => racer.Finished != 0 ? racer.Placement : racer.Rank + 10).ToArray();
        List<UiSubmission> submissions = new();
        Sprite(data.Controls[racers[0].Automatic != 0 ? 0 : 1]);
        for (int rank = 0; rank < racers.Count; rank++)
        {
            if (race.SecondFinished)
            {
                int palette = (race.FrameCounter & 8) != 0 ? 0 : 1;
                Sprite(data.Crowns[0] with { Palette = palette });
                Sprite(data.Crowns[1] with { Palette = palette });
            }
            if (!race.Started)
            {
                continue;
            }
            Ff7ChocoboRacerState racer = ranking[rank];
            int y = rank == 0 ? 18 : rank == 1 ? 26 : 34;
            Sprite(new(16, rank * 40 + 16, y, 24, 24, racer.Jockey % 3 * 48 / 256f, racer.Jockey < 3 ? 0 : 48 / 128f, 48 / 256f, 48 / 128f, 0, 0));
            int selector = racer.Finished != 0 ? data.FaceSelectors[5 + racer.Placement] : data.FaceSelectors[racer.Animation];
            int facePalette = racer.Jockey switch
            {
                0 => race.PlayerColor switch { 0 => 1, 1 => 2, 2 => 3, 3 => 4, 4 => 1, _ => throw new InvalidDataException("FFVII Chocobo player palette exceeds its native color table.") },
                1 => racer.Model == 9 ? 4 : 3,
                2 => 8,
                3 => 5,
                4 => 6,
                5 => 7,
                _ => throw new InvalidDataException("FFVII Chocobo HUD references an invalid bird slot.")
            };
            Sprite(new(17, rank * 40 + 8, y + 22, 31, 32, selector % 4 * 64 / 256f, selector < 4 ? 0 : 64 / 128f, 64 / 256f, 64 / 128f, facePalette, 0));
        }
        Ff7ChocoboRacerState player = racers[0];
        Sprite(new(player.Dash != 0 && (race.FrameCounter & 4) != 0 ? 23 : 22, 20, 120, 16, 96, 0, 0, 0.94f, 0.75f, 0, 0));
        if (player.MaxStamina <= 0)
        {
            throw new InvalidDataException("FFVII Chocobo stamina HUD requires a positive native maximum.");
        }
        int staminaY = Math.Min(209, 208 - unchecked(player.Stamina * 68) / player.MaxStamina);
        Plain(24, 25, staminaY, 5, 209 - staminaY, new(1, 20 / 255f, 20 / 255f, 128 / 255f), TriangleBlend.Replace);
        Sprite(new(19, 252, 24, 64, 32, 0, 0, 0.98f, 0.97f, 0, 0), TriangleBlend.Alpha);
        for (int index = 0; index < racers.Count; index++)
        {
            Ff7ChocoboRacerState racer = racers[index];
            if (racer.Finished != 0)
            {
                continue;
            }
            uint packed = index == 0 ? data.PlayerMarkerColors[race.PlayerColor] : index == 1 && racer.Model == 9 ? 0x808080u : data.MarkerColors[index];
            bool blink = index == race.Camera.TargetRacer && (race.FrameCounter & 8) != 0;
            int divisor = blink ? 1 : 2;
            Vector4 color = new((packed & 255) / divisor / 255f, ((packed >> 8) & 255) / divisor / 255f, ((packed >> 16) & 255) / divisor / 255f, 128 / 255f);
            Plain(25, racer.Position.X / 650 + 282, racer.Position.Z / 610 + 40, 3, 3, color, TriangleBlend.Alpha);
        }
        if (race.DisplayedFadeAlpha != 0)
        {
            Ff7ChocoboHudSprite fade = data.Fade;
            byte alpha = unchecked((byte)race.DisplayedFadeAlpha);
            Plain(fade.Slot, fade.X, fade.Y, fade.Width, fade.Height, new(0, 0, 0, alpha / 255f), TriangleBlend.Alpha);
        }
        foreach (UiSubmission submission in submissions.OrderBy(submission => submission.Material.Blend != TriangleBlend.Replace).ThenBy(submission => submission.Slot))
        {
            Quad(submission);
        }

        void Sprite(Ff7ChocoboHudSprite sprite, TriangleBlend blend = TriangleBlend.Replace)
        {
            int texture = sprite.Slot switch
            {
                >= 14 and <= 17 => sprite.Slot - 14,
                19 => race.Course.Track.Count == 0x3B1 ? 6 : 5,
                >= 20 and <= 23 => sprite.Slot - 13,
                _ => throw new NotSupportedException("FFVII Chocobo HUD sprite references a non-race material slot.")
            };
            if (!images.TryGetValue((texture, sprite.Palette), out PixelImage? image))
            {
                image = Ff7FieldRenderer.DecodeTexture(textures[texture], paletteOverride: sprite.Palette);
                images.Add((texture, sprite.Palette), image);
            }
            TriangleMaterial material = new(image, blend, TriangleDepthComparison.Always, false, Sampler: TriangleSampler.PointClamp);
            submissions.Add(new(sprite.Slot, sprite.X, sprite.Y, sprite.Width, sprite.Height, new(sprite.U, sprite.V), new(sprite.TextureWidth, sprite.TextureHeight), new(1, 1, 1, 128 / 255f), material));
        }

        void Plain(int slot, int x, int y, int rectangleWidth, int rectangleHeight, Vector4 color, TriangleBlend blend)
        {
            submissions.Add(new(slot, x, y, rectangleWidth, rectangleHeight, Vector2.Zero, Vector2.Zero, color, new(null, blend, TriangleDepthComparison.Always, false)));
        }

        void Quad(UiSubmission submission)
        {
            TriangleVertex Vertex(int x, int y, Vector2 uv)
            {
                return new(new(x * width / 320f, y * height / 240f), 0, 1, uv, submission.Color);
            }
            TriangleVertex a = Vertex(submission.X, submission.Y, submission.Uv);
            TriangleVertex b = Vertex(submission.X, submission.Y + submission.Height, submission.Uv + new Vector2(0, submission.Size.Y));
            TriangleVertex c = Vertex(submission.X + submission.Width, submission.Y, submission.Uv + new Vector2(submission.Size.X, 0));
            TriangleVertex d = Vertex(submission.X + submission.Width, submission.Y + submission.Height, submission.Uv + submission.Size);
            renderer.Draw(a, b, c, submission.Material);
            renderer.Draw(c, b, d, submission.Material);
        }
    }

    private sealed record UiSubmission(int Slot, int X, int Y, int Width, int Height, Vector2 Uv, Vector2 Size, Vector4 Color, TriangleMaterial Material);
}
