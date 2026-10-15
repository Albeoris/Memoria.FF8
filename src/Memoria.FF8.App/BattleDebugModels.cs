using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8;

/// <summary>An explicit laboratory training prop, separate from the installed enemy corpus.</summary>
internal static class BattleDebugModels
{
    public static BattleModel CreateDummy()
    {
        List<BattleVertex> vertices = [];
        List<BattleFace> faces = [];
        AddBox(new Vector3(-4, 0, -3), new Vector3(4, 2, 3), new Rgba32(90, 70, 45, 255));
        AddBox(new Vector3(-0.8f, 2, -0.8f), new Vector3(0.8f, 10, 0.8f), new Rgba32(130, 100, 60, 255));
        AddBox(new Vector3(-4, 7, -2), new Vector3(4, 15, 2), new Rgba32(190, 155, 95, 255));
        AddBox(new Vector3(-2, 15, -2), new Vector3(2, 19, 2), new Rgba32(220, 190, 130, 255));
        AddBox(new Vector3(-8, 11, -1), new Vector3(8, 13, 1), new Rgba32(160, 120, 70, 255));
        PixelImage texture = new(1, 1, [new Rgba32(128, 128, 128, 255)]);
        BattlePose pose = new(Vector3.Zero, [Vector3.Zero], [Vector3.One]);
        return new BattleModel("Debug training dummy", [new BattleBone(-1, 0)], [new BattleMesh(vertices, faces)], [texture], [new BattleAnimation([pose])], 1);

        void AddBox(Vector3 minimum, Vector3 maximum, Rgba32 color)
        {
            int start = vertices.Count;
            vertices.AddRange(new[]
            {
                new Vector3(minimum.X, minimum.Y, minimum.Z),
                new Vector3(maximum.X, minimum.Y, minimum.Z),
                new Vector3(minimum.X, maximum.Y, minimum.Z),
                new Vector3(maximum.X, maximum.Y, minimum.Z),
                new Vector3(minimum.X, minimum.Y, maximum.Z),
                new Vector3(maximum.X, minimum.Y, maximum.Z),
                new Vector3(minimum.X, maximum.Y, maximum.Z),
                new Vector3(maximum.X, maximum.Y, maximum.Z)
            }.Select(position => new BattleVertex(position, 0)));
            foreach (int[] corners in new[] { new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 4, 0, 6, 2 }, new[] { 1, 5, 3, 7 }, new[] { 2, 3, 6, 7 }, new[] { 4, 5, 0, 1 } })
            {
                faces.Add(new BattleFace(corners.Select(index => start + index).ToArray(), [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, color));
            }
        }
    }
}
