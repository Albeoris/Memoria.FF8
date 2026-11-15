using System.Buffers.Binary;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed class Ff7ChocoboPresentation
{
    private const float NativeScale = 640 / 4096f * 30.4309f;
    private static readonly Vector3[] OpponentColors = [new(0, 80, 0), new(0, 0, 80), new(80, 20, 40), new(80, -40, -40), new(40, -40, 80), new(80, 80, 80)];
    private static readonly Vector3[] PlayerColors = [new(80, 80, 0), new(0, 80, 0), new(0, 80, 80), new(-80, -80, -80), new(99, 47, -117)];
    private static readonly TriangleMaterial Solid = new(null, DepthComparison: TriangleDepthComparison.GreaterEqual) { DepthMapping = TriangleDepthMapping.Positive, DepthScale = 1 };
    private static readonly TriangleMaterial Transparent = Solid with { Blend = TriangleBlend.Add, DepthWrite = false };
    private readonly Ff7ChocoboPresentationData[] courses;
    private readonly Model[][] models;
    private readonly TriangleMaterial[][] sprites;

    public Ff7ChocoboPresentation(Ff7Installation installation)
    {
        string archiveName = installation.Language switch
        {
            "en" => "chocobo.lgp",
            "de" => "gchocobo.lgp",
            "fr" => "fchocobo.lgp",
            "es" => "schocobo.lgp",
            _ => throw new NotSupportedException($"FFVII Chocobo archive for language '{installation.Language}' is unsupported.")
        };
        using LgpArchive archive = LgpArchive.Open(Path.Combine(installation.DataDirectory, "minigame", archiveName));
        courses = [Ff7PcExecutableData.LoadChocoboPresentationData(installation, false), Ff7PcExecutableData.LoadChocoboPresentationData(installation, true)];
        Dictionary<string, Model> loaded = new(StringComparer.OrdinalIgnoreCase);
        models = courses.Select(course => course.Models.Select(descriptor =>
        {
            string key = descriptor.Hrc + ":" + descriptor.Transparent;
            if (!loaded.TryGetValue(key, out Model? model))
            {
                model = Load(archive, descriptor);
                loaded.Add(key, model);
            }
            return model;
        }).ToArray()).ToArray();
        sprites = courses.Select((course, courseIndex) => course.SpriteTextures.Select((name, index) =>
        {
            PcTexture texture = PcTexture.Read(archive.ReadEntry(archive.Find(name).Single()));
            bool transparent = courseIndex == 0 ? index is 3 or 6 or 0x21 : index == 0;
            return (transparent ? Transparent : Solid) with { Texture = Ff7FieldRenderer.DecodeTexture(texture), Sampler = TriangleSampler.PointClamp };
        }).ToArray()).ToArray();
    }

    public void Render(ITriangleRenderer renderer, Ff7ChocoboRuntime race, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        PcChocoboCourse course = race.Course;
        int courseIndex = course.Track.Count == 0x3B1 ? 1 : 0;
        Ff7ChocoboPresentationData resources = courses[courseIndex];
        IReadOnlyList<Ff7ChocoboRacerState> racers = race.Racers;
        Matrix4x4 staticView = race.Camera.StaticView;
        Matrix4x4 animatedView = race.Camera.AnimatedView;
        SortedDictionary<int, List<Submission>> batches = new();
        renderer.Begin(width, height, new(0, 0, 0, 1), 0);
        try
        {
            int playerPath = racers[race.Camera.TargetRacer].Path;
            byte layer = (byte)(course.Track[playerPath].Color >> 24);
            bool shortDome = course.Track.Count == 0x3B1 && playerPath > 0x35 && playerPath < 0x9B;
            if ((layer & 1) != 0 || shortDome)
            {
                for (int offset = 0; offset < course.DomeTriangles.Length; offset += 36)
                {
                    ReadOnlySpan<byte> data = course.DomeTriangles.Span.Slice(offset, 36);
                    DrawPrimitive([Point(data), Point(data[8..]), Point(data[16..])], [Color(data[24..]), Color(data[28..]), Color(data[32..])], Solid, 0);
                }
            }
            if ((layer & 2) != 0 && !shortDome)
            {
                uint trackColor = course.Track[playerPath].Color;
                Vector4 color = new((trackColor & 255) / 255f, ((trackColor >> 8) & 255) / 255f, ((trackColor >> 16) & 255) / 255f, 128 / 255f);
                TriangleMaterial overlay = new(null, TriangleBlend.Alpha, TriangleDepthComparison.Always, false);
                TriangleVertex a = new(new(0, 0), 0, 1, Vector2.Zero, color);
                TriangleVertex b = new(new(width, 0), 0, 1, Vector2.Zero, color);
                TriangleVertex c = new(new(0, height * 232 / 240f), 0, 1, Vector2.Zero, color);
                TriangleVertex d = new(new(width, height * 232 / 240f), 0, 1, Vector2.Zero, color);
                Submit(a, b, c, overlay, 150);
                Submit(c, b, d, overlay, 150);
            }
            int start = Mod(race.Camera.CullStart, course.Track.Count);
            int end = Mod(race.Camera.CullEnd, course.Track.Count);
            if (start > end)
            {
                Visit(0, end);
                Visit(start, course.Track.Count);
            }
            else
            {
                Visit(start, end);
            }
            foreach (Ff7ChocoboSceneryPose pose in race.SceneryPoses)
            {
                if (pose.Model < 16 || pose.Model >= resources.Models.Count)
                {
                    throw new InvalidDataException("FFVII Chocobo scenery pose references an invalid native model slot.");
                }
                Ff7ChocoboPresentationModel descriptor = resources.Models[pose.Model];
                Matrix4x4 rotation = Matrix4x4.CreateRotationZ(-MathF.PI) * Matrix4x4.CreateRotationY(-pose.RotationY * MathF.Tau / 250 + (descriptor.PatchYaw ? MathF.PI : 0));
                Vector3 position = new(pose.Position.X, pose.Position.Y, pose.Position.Z);
                DrawModel(models[courseIndex][pose.Model], 0, pose.Frame, position, rotation, Vector3.Zero, (descriptor.Transparent ? 200 : 50) + pose.Model);
            }
            for (int index = 0; index < racers.Count; index++)
            {
                if (!race.Camera.Visible[index])
                {
                    continue;
                }
                Ff7ChocoboRacerState racer = racers[index];
                Ff7ChocoboPoint drawPosition = race.Camera.DrawPositions[index];
                Vector3 position = new(drawPosition.X, drawPosition.Y, drawPosition.Z);
                if (racer.Model < 6 || racer.Model > 15 || racer.Jockey < 0 || racer.Jockey >= 6)
                {
                    throw new NotSupportedException("FFVII Chocobo racer model slots exceed the original bird and rider table.");
                }
                Vector3 offset = racer.Jockey == 0 ? PlayerColors[race.PlayerColor] : racer.Model == 9 ? new(-100) : OpponentColors[racer.Jockey];
                byte yaw = unchecked((byte)(256 - (race.Camera.DrawYaws[index] >> 4)));
                Matrix4x4 rotation = Matrix4x4.CreateRotationZ(-MathF.PI) * Matrix4x4.CreateRotationY(-yaw * MathF.Tau / 250);
                DrawModel(models[courseIndex][racer.Jockey], racer.Animation, racer.AnimationCounter >> 8, position, rotation, offset / 256, 50 + racer.Jockey);
                Model rider = models[courseIndex][racer.Model];
                int animation = race.RiderAnimations[racer.Jockey];
                if (animation >= rider.Frames.Length)
                {
                    throw new NotSupportedException("FFVII Chocobo rider animation requires its retained native clip binding.");
                }
                DrawModel(rider, animation, racer.AnimationCounter >> 8, position - Vector3.UnitY * 10, rotation, Vector3.Zero, 50 + racer.Model);
            }
            foreach (List<Submission> batch in batches.Values)
            {
                foreach (Submission submission in batch)
                {
                    renderer.Draw(submission.A, submission.B, submission.C, submission.Material);
                }
            }
        }
        finally
        {
            renderer.End();
        }

        void Visit(int first, int last)
        {
            int remaining = last - first;
            int pointer = course.MapPointers[first];
            if (pointer < 0 || pointer >= course.MapTable.Count)
            {
                throw new InvalidDataException("FFVII Chocobo map pointer lies outside its native primitive table.");
            }
            for (; pointer < course.MapTable.Count; pointer++)
            {
                int entry = unchecked((int)course.MapTable[pointer]);
                if (entry == -1)
                {
                    return;
                }
                if (entry < 0 && --remaining < 0)
                {
                    return;
                }
                int index = unchecked((short)entry);
                switch ((entry >> 16) & 255)
                {
                    case 0:
                        ReadOnlySpan<byte> triangle = course.Triangles.Span.Slice(checked(index * 28), 28);
                        DrawPrimitive([Point(triangle), Point(triangle[8..]), Point(triangle[16..])], [Color(triangle[24..]), Rgb(triangle[6], triangle[7], triangle[14]), Rgb(triangle[22], triangle[23], triangle[15])], triangle[27] == 0x30 ? Solid : Transparent, triangle[27] == 0x30 ? 1 : 100);
                        break;
                    case 1:
                        ReadOnlySpan<byte> quad = course.Quads.Span.Slice(checked(index * 40), 40);
                        DrawPrimitive([Point(quad), Point(quad[8..]), Point(quad[16..]), Point(quad[24..])], [Color(quad[36..]), Rgb(quad[22], quad[23], quad[15]), Rgb(quad[30], quad[31], quad[35]), Color(quad[32..])], quad[39] == 0x38 ? Solid : Transparent, quad[39] == 0x38 ? 2 : 101);
                        break;
                    case 0x21:
                        DrawSprite(index);
                        break;
                }
            }
            throw new InvalidDataException("FFVII Chocobo primitive visibility list has no native terminator.");
        }

        void DrawSprite(int index)
        {
            ReadOnlySpan<byte> sprite = course.Sprites.Span.Slice(checked(index * 22), 22);
            Vector3 bottom = Point(sprite);
            Vector3 camera = Transform(bottom, staticView);
            int distance = (int)camera.Z >> 2;
            if (distance <= 0 || distance > 4000)
            {
                return;
            }
            int x = unchecked((short)(160 + (double)camera.X * 270 / camera.Z));
            int y = unchecked((short)(120 + (double)camera.Y * 270 / camera.Z));
            if (x < -40 || y < -40 || x > 350 || y > 270)
            {
                return;
            }
            int spriteWidth = BinaryPrimitives.ReadInt16LittleEndian(sprite[10..]) * 270 / distance;
            int left = unchecked((short)(x - spriteWidth / 2));
            int right = unchecked((short)(left + spriteWidth));
            bottom.Y = unchecked((short)((short)bottom.Y - BinaryPrimitives.ReadInt16LittleEndian(sprite[12..]) * 4));
            Vector3 top = Transform(bottom, staticView);
            if (top.Z <= 0)
            {
                return;
            }
            int topY = unchecked((short)(120 + (double)top.Y * 270 / top.Z));
            bool flip = courseIndex == 1 ? index + 1 != 0x49 : index + 1 is >= 0x66 and <= 0x73 or >= 0x86 and <= 0x9B or >= 0xBF and <= 0xCF;
            float vBottom = flip ? 0.94f : 0.03f;
            float vTop = flip ? 0.03f : 0.94f;
            Vector4 color = new(1, 1, 1, 160 / 255f);
            TriangleVertex a = Vertex(left, y, new(0.03f, vBottom));
            TriangleVertex b = Vertex(left, topY, new(0.03f, vTop));
            TriangleVertex c = Vertex(right, y, new(0.94f, vBottom));
            TriangleVertex d = Vertex(right, topY, new(0.94f, vTop));
            int texture = resources.SpriteMap[index];
            TriangleMaterial material = sprites[courseIndex][texture];
            int batch = (material.Blend == TriangleBlend.Replace ? 3 : 102) + texture;
            Submit(a, b, c, material, batch);
            Submit(c, b, d, material, batch);

            TriangleVertex Vertex(int screenX, int screenY, Vector2 uv)
            {
                return new(new(screenX * width / 320f, screenY * height / 240f), 1 / top.Z, 1, uv, color);
            }
        }

        void DrawPrimitive(Vector3[] positions, Vector4[] colors, TriangleMaterial material, int batch)
        {
            TriangleVertex[] vertices = new TriangleVertex[positions.Length];
            for (int index = 0; index < positions.Length; index++)
            {
                Vector3 camera = Transform(positions[index], staticView);
                if (camera.Z <= 0)
                {
                    return;
                }
                vertices[index] = Project(camera, Vector2.Zero, colors[index]);
            }
            Submit(vertices[0], vertices[1], vertices[2], material, batch);
            if (vertices.Length == 4)
            {
                Submit(vertices[2], vertices[1], vertices[3], material, batch);
            }
        }

        void DrawModel(Model model, int animation, int frame, Vector3 position, Matrix4x4 rotation, Vector3 colorOffset, int batch)
        {
            Matrix4x4 basis = Matrix4x4.CreateScale(NativeScale) * rotation * Matrix4x4.CreateTranslation(position);
            foreach (Ff7WorldModelTriangle triangle in model.Adapter.CreateOriginal(animation, Math.Clamp(frame, 0, model.Frames[animation] - 1), NativeScale, basis, rotation))
            {
                Vector3 a = Transform(triangle.A.Position, animatedView);
                Vector3 b = Transform(triangle.B.Position, animatedView);
                Vector3 c = Transform(triangle.C.Position, animatedView);
                if (a.Z <= 0 || b.Z <= 0 || c.Z <= 0)
                {
                    continue;
                }
                TriangleVertex first = Project(a, triangle.A.Uv, AdjustColor(triangle.A.Color, colorOffset));
                TriangleVertex second = Project(b, triangle.B.Uv, AdjustColor(triangle.B.Color, colorOffset));
                TriangleVertex third = Project(c, triangle.C.Uv, AdjustColor(triangle.C.Color, colorOffset));
                Vector2 ab = second.Position - first.Position;
                Vector2 ac = third.Position - first.Position;
                if (triangle.Cull && ab.X * ac.Y - ab.Y * ac.X >= 0)
                {
                    continue;
                }
                Submit(first, second, third, triangle.Material, batch);
            }
        }

        TriangleVertex Project(Vector3 camera, Vector2 uv, Vector4 color)
        {
            float reciprocal = 1 / camera.Z;
            return new(new((160 + camera.X * reciprocal * 270) * width / 320, (120 + camera.Y * reciprocal * 270) * height / 240), reciprocal, reciprocal, uv, color);
        }

        void Submit(TriangleVertex a, TriangleVertex b, TriangleVertex c, TriangleMaterial material, int batch)
        {
            if (!batches.TryGetValue(batch, out List<Submission>? submissions))
            {
                submissions = new();
                batches.Add(batch, submissions);
            }
            submissions.Add(new(a, b, c, material));
        }
    }

    private static Model Load(LgpArchive archive, Ff7ChocoboPresentationModel source)
    {
        string name = source.Hrc;
        string[] clips = source.Animations.ToArray();
        PcFieldModelDescriptor descriptor = new(name, 0, name, "640", [], 0, clips);
        Ff7FieldModelResources model = Ff7FieldModelResources.Load(archive, descriptor);
        return new(new(new(name, 640, clips) { Transparent = source.Transparent }, model), clips.Select(clip => model.Animations[clip].Frames.Count).ToArray());
    }

    private static Vector3 Point(ReadOnlySpan<byte> data)
    {
        return new(BinaryPrimitives.ReadInt16LittleEndian(data), BinaryPrimitives.ReadInt16LittleEndian(data[2..]), BinaryPrimitives.ReadInt16LittleEndian(data[4..]));
    }

    private static Vector3 Transform(Vector3 point, Matrix4x4 view)
    {
        return new(
            (float)((float)((double)point.X * view.M11 + (double)point.Y * view.M21 + (double)point.Z * view.M31) + (double)view.M41),
            (float)((float)((double)point.X * view.M12 + (double)point.Y * view.M22 + (double)point.Z * view.M32) + (double)view.M42),
            (float)((float)((double)point.X * view.M13 + (double)point.Y * view.M23 + (double)point.Z * view.M33) + (double)view.M43));
    }

    private static Vector4 Color(ReadOnlySpan<byte> data)
    {
        return Rgb(data[0], data[1], data[2]);
    }

    private static Vector4 Rgb(byte red, byte green, byte blue)
    {
        return new(red / 255f, green / 255f, blue / 255f, 1);
    }

    private static Vector4 AdjustColor(Vector4 color, Vector3 offset)
    {
        return new(Vector3.Clamp(new(color.X + offset.X, color.Y + offset.Y, color.Z + offset.Z), Vector3.Zero, Vector3.One), color.W);
    }

    private static int Mod(int value, int length)
    {
        return (value % length + length) % length;
    }

    private sealed record Model(Ff7WorldModelAdapter Adapter, int[] Frames);
    private sealed record Submission(TriangleVertex A, TriangleVertex B, TriangleVertex C, TriangleMaterial Material);
}
