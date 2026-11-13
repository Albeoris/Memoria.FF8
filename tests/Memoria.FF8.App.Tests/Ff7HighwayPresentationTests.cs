using System.Numerics;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using GameTime = Microsoft.Xna.Framework.GameTime;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7HighwayPresentationTests
{
    [Fact]
    public void InstalledStoryRaceSubmitsOriginalRoadAndRiderGeometryThroughFinish()
    {
        using Ff7Session session = OpenRace(out Ff7HighwayPresentation presentation);
        Ff7HighwayRuntime highway = Assert.IsType<Ff7HighwayRuntime>(session.Highway);
        bool sawEnemy = false;
        bool sawMotorball = false;
        bool sawAttack = false;
        bool sawCourseCamera = false;
        bool sawFinish = false;
        for (int tick = 0; tick < 4500 && session.Highway is not null; tick++)
        {
            bool newEnemy = !sawEnemy && highway.Riders.Any(rider => rider.Kind == 2 && rider.Active);
            bool newMotorball = !sawMotorball && highway.Riders.Any(rider => rider.Kind == 10 && rider.Active);
            bool newAttack = !sawAttack && highway.Player.AttackTimer != 0;
            bool newCourseCamera = !sawCourseCamera && (highway.FixedCameraPosition is not null || highway.CameraPath is not null);
            bool newFinish = !sawFinish && highway.FadingOut;
            if (tick == 70 || newEnemy || newMotorball || newAttack || newCourseCamera || newFinish)
            {
                Capture capture = new();
                presentation.Render(capture, highway, 320, 240);
                Assert.True(capture.RoadTriangles > 10, $"Only {capture.RoadTriangles} road triangles at highway tick {tick}, camera {highway.FixedCameraPosition}/{highway.CameraPath}, surfaces {string.Join(",", highway.Road.Select(segment => segment.Surface).Distinct())}.");
                Assert.True(capture.VisibleModelTriangles > 30, $"No visible models at highway tick {tick}.");
            }
            sawEnemy |= newEnemy;
            sawMotorball |= newMotorball;
            sawAttack |= newAttack;
            sawCourseCamera |= newCourseCamera;
            sawFinish |= newFinish;
            ushort direction = tick % 180 < 70 ? (ushort)0x2000 : (ushort)0x8000;
            ushort attack = tick % 100 == 70 ? (ushort)0x20 : tick % 100 == 25 ? (ushort)0x80 : (ushort)0;
            session.SetHighwayInput(direction, attack);
            session.AdvanceTick();
        }
        Assert.True(sawEnemy && sawMotorball && sawAttack && sawCourseCamera && sawFinish);
        Assert.Null(session.Highway);
        Assert.Equal("blackbg9", session.Field.Name);
    }

    [Fact]
    public void InstalledStoryRaceRendersRoadAndVehiclesIntoTheFramebuffer()
    {
        Ff7Installation installation = FindInstallation();
        using GpuTestDevice device = new();
        using Ff7Scene scene = new(device.Device, installation, "", new(50, 32000), _ => throw new InvalidOperationException("The highway fixture contains no movie."), audioEnabled: false);
        scene.Continue(new CombinedSessionSave { ActiveGame = SessionGame.Ff7, Ff7 = OpenDeparture(installation) });
        scene.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromMilliseconds(34)), default);
        Ff7HighwayRuntime highway = Assert.IsType<Ff7HighwayRuntime>(scene.Session!.Highway);
        for (int tick = 0; tick < 70; tick++)
        {
            scene.Update(new GameTime(TimeSpan.FromMilliseconds((tick + 1) * 34), TimeSpan.FromMilliseconds(34)), default);
        }
        int startingX = highway.Player.X;
        scene.Update(new GameTime(TimeSpan.FromMilliseconds(71 * 34), TimeSpan.FromMilliseconds(34)), new KeyboardState(Keys.Right, Keys.X));
        Assert.True(highway.Player.X > startingX);
        Assert.True(highway.Player.AttackTimer < 0);
        using SpriteBatch batch = new(device.Device);
        using RenderTarget2D target = device.CreateTarget(320, 240);
        device.Device.SetRenderTarget(target);
        scene.Draw(batch, new Rectangle(0, 0, 320, 240));
        Assert.Null(scene.Error);
        var pixels = GpuTestDevice.Read(target);
        Assert.True(pixels.Distinct().Count() > 60);
        Assert.True(pixels.Count(pixel => pixel != pixels[0]) > 5000);
    }

    private static Ff7Session OpenRace(out Ff7HighwayPresentation presentation)
    {
        Ff7Installation installation = FindInstallation();
        presentation = new(installation);
        Ff7Session session = Ff7Session.Restore(installation, OpenDeparture(installation));
        try
        {
            session.AdvanceTick();
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    private static Ff7Installation FindInstallation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select an original FFVII installation.");
        return Ff7Installation.Discover(directory!);
    }

    private static Ff7SessionSave OpenDeparture(Ff7Installation installation)
    {
        using Ff7Session initial = Ff7Session.NewGame(installation);
        FieldVariables variables = new(initial.Machine.Variables.CopyPersistent(), new byte[256]);
        variables.WriteWord(FieldBank.Persistent0Word, 0, 326);
        Ff7NativeSaveSlot slot = Ff7NativeSaveCodec.ExportField(initial.Progress, variables.CopyPersistent(), 101, 0, 0, 3, 0);
        using Ff7Session imported = Ff7Session.Restore(installation, Ff7NativeSaveCodec.Import(slot, installation));
        imported.AdvanceTick();
        return imported.Capture();
    }

    private sealed class Capture : ITriangleRenderer
    {
        public int RoadTriangles { get; private set; }
        public int VisibleModelTriangles { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Assert.True(float.IsFinite(a.Position.X) && float.IsFinite(a.Position.Y));
            Assert.True(float.IsFinite(b.Position.X) && float.IsFinite(b.Position.Y));
            Assert.True(float.IsFinite(c.Position.X) && float.IsFinite(c.Position.Y));
            if (material.Texture is not null && a.Color == Vector4.One && b.Color == Vector4.One && c.Color == Vector4.One)
            {
                RoadTriangles++;
            }
            else if (Onscreen(a) || Onscreen(b) || Onscreen(c))
            {
                VisibleModelTriangles++;
            }
        }

        public void End()
        {
        }

        private static bool Onscreen(TriangleVertex vertex)
        {
            return vertex.Position.X >= 0 && vertex.Position.X < 320 && vertex.Position.Y >= 0 && vertex.Position.Y < 240 && vertex.Color.W > 0;
        }
    }
}
