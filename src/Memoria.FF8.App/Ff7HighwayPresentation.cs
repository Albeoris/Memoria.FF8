using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed class Ff7HighwayPresentation
{
    private const float ActorScale = 2.3f;
    private static readonly string[] RoadTextures = ["fpaa.tex", "ptaa.tex", "qtaa.tex", "rtaa.tex", "epaa.tex", "staa.tex", "ttaa.tex", "utaa.tex", "vtaa.tex", "wtaa.tex", "xtaa.tex"];
    private static readonly string[] WallTextures = ["ytaa.tex", "ztaa.tex", "auaa.tex", "buaa.tex", "cuaa.tex", "duaa.tex", "euaa.tex", "fuaa.tex"];
    private readonly TriangleMaterial[] roadMaterials;
    private readonly TriangleMaterial[] wallMaterials;
    private readonly Model cloud;
    private readonly Model hardyDaytona;
    private readonly Model truck;
    private readonly Model[] bikes;
    private readonly Model rider;
    private readonly Model motorball;

    public Ff7HighwayPresentation(Ff7Installation installation)
    {
        using LgpArchive archive = Ff7HighwayResources.Open(installation);
        roadMaterials = RoadTextures.Select(LoadMaterial).ToArray();
        wallMaterials = WallTextures.Select(LoadMaterial).ToArray();
        cloud = Load(archive, "caaa.hrc", "dgaa.a", "egaa.a", "fgaa.a", "ggaa.a");
        hardyDaytona = Load(archive, "pbaa.hrc", "lgaa.a");
        truck = Load(archive, "ybaa.hrc", "qgaa.a");
        rider = Load(archive, "ydaa.hrc", "vgaa.a");
        bikes = [Load(archive, "veaa.hrc", "bhaa.a"), Load(archive, "efaa.hrc", "ghaa.a"), Load(archive, "nfaa.hrc", "lhaa.a")];
        motorball = Load(archive, "zcaa.hrc", "ugaa.a");

        TriangleMaterial LoadMaterial(string name)
        {
            return Material(Ff7FieldRenderer.DecodeTexture(PcTexture.Read(archive.ReadEntry(archive.Find(name).Single()))));
        }
    }

    public void Render(ITriangleRenderer renderer, Ff7HighwayRuntime highway, int width, int height)
    {
        // TODO(TD-027): Course props, overlay effects and original actor pose commands need their own submissions.
        int track = highway.TrackPosition;
        Ff7HighwayRider player = highway.Player;
        Vector3 eye;
        Vector3 target = highway.SampleRoad(track + (player.Z >> 8), player.X >> 8);
        if (highway.FixedCameraPosition is int fixedPosition)
        {
            eye = highway.SampleRoad(fixedPosition, 90) - Vector3.UnitY * 180;
        }
        else
        {
            Vector3 pathOffset = highway.CameraPath is int path ? highway.SamplePath(path, highway.CameraPathPosition) : Vector3.Zero;
            eye = highway.SampleRoad(track + (player.Z >> 9) + (int)pathOffset.Z, -(player.X >> 9) * 4 / 3 + (int)pathOffset.X);
            eye.Y -= 85 + pathOffset.Y;
            target.Y += 2 * Math.Max(0, (player.Z - 350000) >> 11) - 120;
        }
        Vector3 cameraForward = Vector3.Normalize(target - eye);
        Vector3 lateral = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, cameraForward));
        Vector3 cameraUp = Vector3.Cross(lateral, cameraForward);
        float focal = 300f * height / 240f;
        Vector4 sky = new(0.055f, 0.065f, 0.135f, 1);
        renderer.Begin(width, height, sky, 0);
        try
        {
            IReadOnlyList<Ff7HighwayRoadSegment> road = highway.Road;
            for (int index = 1; index < road.Count; index++)
            {
                Ff7HighwayRoadSegment first = road[index - 1];
                Ff7HighwayRoadSegment second = road[index];
                if (second.BeyondBrokenRoad)
                {
                    continue;
                }
                Vector3 firstCenter = new(first.X, first.Y, first.Z);
                Vector3 secondCenter = new(second.X, second.Y, second.Z);
                Matrix4x4 firstRotation = RoadRotation(first);
                Matrix4x4 secondRotation = RoadRotation(second);
                Vector3 firstAcross = Vector3.TransformNormal(Vector3.UnitX, firstRotation);
                Vector3 secondAcross = Vector3.TransformNormal(Vector3.UnitX, secondRotation);
                Vector3 firstLeft = firstCenter - firstAcross * (first.Width / 2f - 30);
                Vector3 firstRight = firstCenter + firstAcross * (first.Width / 2f - 30);
                Vector3 secondLeft = secondCenter - secondAcross * (second.Width / 2f - 30);
                Vector3 secondRight = secondCenter + secondAcross * (second.Width / 2f - 30);
                DrawQuad(firstLeft, firstRight, secondRight, secondLeft, roadMaterials[second.Surface]);
                Vector3 firstRise = Vector3.TransformNormal(-Vector3.UnitY * 90, firstRotation);
                Vector3 secondRise = Vector3.TransformNormal(-Vector3.UnitY * 90, secondRotation);
                Vector3 firstLeftWall = firstCenter - firstAcross * (first.Width / 2f) + firstRise;
                Vector3 firstRightWall = firstCenter + firstAcross * (first.Width / 2f) + firstRise;
                Vector3 secondLeftWall = secondCenter - secondAcross * (second.Width / 2f) + secondRise;
                Vector3 secondRightWall = secondCenter + secondAcross * (second.Width / 2f) + secondRise;
                DrawQuad(firstLeftWall, secondLeftWall, secondLeft, firstLeft, wallMaterials[second.Stripe]);
                DrawQuad(secondRightWall, firstRightWall, firstRight, secondRight, wallMaterials[second.Stripe]);
            }
            foreach (Ff7HighwayRider vehicle in highway.Riders)
            {
                DrawRider(vehicle);
            }
        }
        finally
        {
            renderer.End();
        }

        void DrawRider(Ff7HighwayRider vehicle)
        {
            if (!vehicle.Active)
            {
                return;
            }
            int at = track + (vehicle.Z >> 8);
            int offset = vehicle.X >> 8;
            Vector3 onRoad = highway.SampleRoad(at, offset);
            Vector3 direction = highway.SampleRoad(at + 128, offset) - onRoad;
            float heading = MathF.Atan2(direction.X, direction.Z);
            float lean = Math.Clamp(vehicle.Lean / 65536f, -0.8f, 0.8f);
            Matrix4x4 rotation = Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateScale(1, -1, 1) * Matrix4x4.CreateRotationZ(lean) * Matrix4x4.CreateRotationY(heading);
            Matrix4x4 basis = Matrix4x4.CreateScale(ActorScale) * rotation * Matrix4x4.CreateTranslation(onRoad);
            switch (vehicle.Kind)
            {
                case 0:
                    DrawModel(hardyDaytona, 0, 0, basis, rotation);
                    int attack = vehicle.AttackTimer;
                    int animation = attack == 0 ? (lean < 0 ? 0 : 1) : attack < 0 ? 2 : 3;
                    int frame = attack == 0 ? (int)(MathF.Abs(lean) * 8) : 29 - Math.Abs(attack);
                    DrawModel(cloud, animation, frame, basis, rotation);
                    break;
                case 1:
                    DrawModel(truck, 0, 0, basis, rotation);
                    break;
                case 2:
                    DrawModel(bikes[vehicle.Variant], 0, (int)MathF.Abs(lean * 8), basis, rotation);
                    DrawModel(rider, 0, (int)MathF.Abs(lean * 8), basis, rotation);
                    break;
                case 10:
                    DrawModel(motorball, 0, (int)highway.Tick, basis, rotation);
                    break;
            }
        }

        void DrawModel(Model model, int animation, int frame, Matrix4x4 basis, Matrix4x4 rotation)
        {
            foreach (Ff7WorldModelTriangle triangle in model.Adapter.CreateOriginal(animation, Math.Clamp(frame, 0, model.Frames[animation] - 1), ActorScale, basis, rotation))
            {
                Draw(Vertex(triangle.A.Position, triangle.A.Uv, triangle.A.Color), Vertex(triangle.B.Position, triangle.B.Uv, triangle.B.Color), Vertex(triangle.C.Position, triangle.C.Uv, triangle.C.Color), triangle.Material);
            }
        }

        CameraSpaceVertex Vertex(Vector3 point, Vector2 uv, Vector4 color)
        {
            Vector3 relative = point - eye;
            return new(new(Vector3.Dot(relative, lateral), Vector3.Dot(relative, cameraUp), Vector3.Dot(relative, cameraForward)), uv, color);
        }

        void Draw(CameraSpaceVertex a, CameraSpaceVertex b, CameraSpaceVertex c, TriangleMaterial material)
        {
            CameraSpaceTriangleSubmission.Draw(renderer, a, b, c, width, height, focal, material);
        }

        void DrawQuad(Vector3 first, Vector3 second, Vector3 third, Vector3 fourth, TriangleMaterial material)
        {
            CameraSpaceVertex a = Vertex(first, new(0, 0), Vector4.One);
            CameraSpaceVertex b = Vertex(second, new(1, 0), Vector4.One);
            CameraSpaceVertex c = Vertex(third, new(1, 1), Vector4.One);
            CameraSpaceVertex d = Vertex(fourth, new(0, 1), Vector4.One);
            Draw(a, b, c, material);
            Draw(a, c, d, material);
        }
    }

    private static Matrix4x4 RoadRotation(Ff7HighwayRoadSegment segment)
    {
        const float radians = MathF.Tau / 4096f;
        return Matrix4x4.CreateRotationZ(segment.Roll * radians) * Matrix4x4.CreateRotationX(segment.Pitch * radians) * Matrix4x4.CreateRotationY(segment.Yaw * radians);
    }

    private static Model Load(LgpArchive archive, string name, params string[] clips)
    {
        PcFieldModelDescriptor descriptor = new(name, 0, name, "1", [], 0, clips);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(archive, descriptor);
        return new(new(new(name, 512, clips), model), clips.Select(clip => model.Animations[clip].Frames.Count).ToArray());
    }

    private static TriangleMaterial Material(PixelImage image)
    {
        return new(image, DepthComparison: TriangleDepthComparison.GreaterEqual) { DepthMapping = TriangleDepthMapping.Positive, DepthScale = 1 };
    }

    private sealed record Model(Ff7WorldModelAdapter Adapter, int[] Frames);
}
