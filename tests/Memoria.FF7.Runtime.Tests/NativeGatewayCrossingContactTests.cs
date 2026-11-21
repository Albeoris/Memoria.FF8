using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeGatewayCrossingContactTests
{
    internal static void Run()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, new(0, 0, 0), new(0, 100, 0), new(100, 0, 0), -1, 1, -1),
            new(1, new(100, 0, 0), new(0, 100, 0), new(100, 100, 0), 0, 2, -1),
            new(2, new(0, 100, 0), new(0, 200, 0), new(100, 100, 0), 5, 3, 1),
            new(3, new(100, 100, 0), new(0, 200, 0), new(100, 200, 0), 2, -1, -1),
            new(4, new(-100, 100, 0), new(-100, 200, 0), new(0, 100, 0), -1, 5, -1),
            new(5, new(0, 100, 0), new(-100, 200, 0), new(0, 200, 0), 4, -1, 2)
        ];
        short[] vectors = new short[512];
        byte[] angles = new byte[129];
        for (int heading = 0; heading < 256; heading++)
        {
            vectors[heading * 2] = (short)Math.Round(Math.Sin(heading * Math.PI / 128) * 4096);
            vectors[heading * 2 + 1] = (short)Math.Round(Math.Cos(heading * Math.PI / 128) * 4096);
        }
        for (int index = 0; index < angles.Length; index++)
        {
            angles[index] = (byte)Math.Round(Math.Asin(index / 128.0) * 128 / Math.PI);
        }
        Ff7NativeMovement movement = new(mesh, vectors, angles);
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, Triangle = 0, X4096 = 20 << 12, Y4096 = 20 << 12, CollisionRadius = 30, MoveSpeed = 4096 };
        PcFieldGateway gateway = new(new(0, 0, 0), new(0, 200, 0), 0, 0, 0, 100, 0, 0);
        byte[] blocked = [0];
        string before = JsonSerializer.Serialize(new { mesh, player, gateway, blocked, vectors, angles });
        Vector3 naive = Ff7AutoplayDriver.ReadGatewayCrossingTarget(gateway, new(20, 20, 0), new(0, 20, 0), player.CollisionRadius);
        Check(!mesh.Any(triangle => Ff7AutoplayDriver.Contains(triangle, naive, out _)), "The authored nearest normal accidentally remained inside the source floor.");
        var contacts = Ff7AutoplayDriver.ReadGatewayCrossingContacts(mesh, blocked, player, gateway, 0, movement, [player], false, (_, _) => true);
        Check(contacts.Count > 0 && contacts.Any(contact => contact.Point.Y > 100), "The long gateway lost its farther interior native crossing witnesses.");
        foreach (var contact in contacts)
        {
            Check(contact.Triangle >= 0 && contact.Triangle < mesh.Length && Ff7AutoplayDriver.Contains(mesh[contact.Triangle], contact.Point, out _), "A crossing contact installed an off-floor predecessor.");
            Ff7Actor predecessor = player with { X4096 = (int)(contact.Point.X * 4096), Y4096 = (int)(contact.Point.Y * 4096), Z4096 = (int)(contact.Point.Z * 4096), Triangle = contact.Triangle };
            bool crossing = false;
            foreach (byte heading in new byte[] { 0, 32, 64, 96, 128, 160, 192, 224 })
            {
                Ff7NativeMoveResult step = movement.StepDirection(predecessor, heading, [player], blocked, false);
                crossing |= step.Moved && Ff7Session.CrossesGateway(gateway, predecessor, step.Actor);
            }
            Check(crossing, "A returned predecessor lacked a real native one-step gateway crossing.");
        }
        Check(Ff7AutoplayDriver.ReadGatewayCrossingContacts(mesh, blocked, player, gateway, 0, movement, [player], false, (_, _) => false).Count == 0, "A witnessed crossing bypassed the caller's protected-movement guard.");
        Check(Ff7AutoplayDriver.ReadGatewayCrossingContacts(mesh, blocked, player, gateway with { DestinationField = short.MaxValue }, 0, movement, [player], false, (_, _) => true).Count == 0, "An absent native gateway acquired crossing contacts.");
        Check(Ff7AutoplayDriver.ReadGatewayCrossingContacts(mesh, blocked, player, gateway with { A = new(0, 0, 300), B = new(0, 200, 300) }, 0, movement, [player], false, (_, _) => true).Count == 0, "A height-incompatible native gateway acquired crossing contacts.");
        Check(before == JsonSerializer.Serialize(new { mesh, player, gateway, blocked, vectors, angles }), "Crossing witness discovery changed actual actor state, geometry or native movement tables.");
        Console.WriteLine("Gateway crossing contacts retain native guarded predecessor witnesses beyond an invalid nearest normal.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
