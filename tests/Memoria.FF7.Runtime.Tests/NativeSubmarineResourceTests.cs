using System.Buffers.Binary;
using Memoria.FF7.Resources;

internal static class NativeSubmarineResourceTests
{
    public static void Run()
    {
        byte[] terrain = Enumerable.Repeat((byte)'0', 4096).ToArray();
        byte[] routes = new byte[4096];
        byte[] fleet = new byte[576];
        byte[] player = new byte[40];
        byte[] speeds = new byte[76];
        byte[] types = new byte[280];
        byte[] motion = new byte[66];
        byte[] roots = new byte[512];
        byte[] reciprocals = new byte[512];
        I32(routes, 0, -27);
        I32(routes, 0x10, 128);
        I32(routes, 0x14, 21000);
        I32(routes, 0x18, 127);
        I32(fleet, 0, -28);
        I32(fleet, 0x10, 6000);
        I16(fleet, 0x14, -123);
        I16(fleet, 0x16, 456);
        I16(fleet, 0x18, short.MinValue);
        I32(fleet, 0x1C, 300);
        I32(fleet, 0x20, 14000);
        I32(fleet, 0x24, 1);
        I32(fleet, 0x28, 2);
        I32(fleet, 0x2C, 98);
        I32(player, 0, -27);
        I32(player, 4, 8);
        I32(player, 8, -27);
        I32(player, 0x10, 16384);
        I32(player, 0x20, 1);
        I32(player, 0x24, 3);
        I32(speeds, 72, -100);
        I16(motion, 0, -32768);
        I16(motion, 32, 16);
        I16(motion, 64, 32767);
        I16(roots, 128, 4096);
        I16(reciprocals, 510, 2052);
        types[279] = 0xAB;
        byte[][] sources = [terrain, routes, fleet, player, speeds, types, motion, roots, reciprocals];
        byte[][] before = sources.Select(value => (byte[])value.Clone()).ToArray();
        Dictionary<int, int> initialGlobals = new() { [0x987028] = 17, [0x9873D8] = -2, [0x9884F8] = 36000 };
        Ff7SubmarineResources result = Ff7SubmarineResources.Read(terrain, routes, fleet, player, speeds, types, motion, motion, motion, motion, roots, reciprocals, initialGlobals);
        Check(result.RouteNodes[0].Radius == 128 && result.RouteNodes[0].TargetSpeed == 21000, "Authored route defaults no longer use their supplied source controls.");
        byte[] routeDefaults = new byte[4096];
        I32(routeDefaults, 0, -6);
        I32(routeDefaults, 0x10, 200);
        I32(routeDefaults, 0x14, 9830);
        Ff7SubmarineResources retainedControls = Ff7SubmarineResources.Read(terrain, routes, fleet, player, routeDefaults: routeDefaults);
        Array.Clear(routeDefaults);
        Check(retainedControls.RouteNodes[0].X == -27 && retainedControls.RouteNodes[0].Radius == 200 && retainedControls.RouteNodes[0].TargetSpeed == 9830 && BinaryPrimitives.ReadInt32LittleEndian(retainedControls.RouteNodes[0].Data.Span[0x14..]) == 21000, "Native route initialization replaced retained controls or source evidence.");
        Check(initialGlobals.Count == 3 && result.InitialGlobals[0x987028] == 17 && result.InitialGlobals[0x9873D8] == -2, "Initial native globals changed during loading.");
        initialGlobals.Clear();
        Check(result.InitialGlobals.Count == 3 && result.InitialGlobals[0x9884F8] == 36000, "Initial globals retained a mutable source dictionary.");
        Check(result.RouteNodes.Count == 128 && result.RouteNodes[0].X == -27 && result.RouteNodes[0].NextIndex == 127 && result.RouteNodes[127].Data.Length == 32, "Route source stride or signed values changed.");
        Ff7SubmarineFleetEntry ship = result.Fleet[0];
        Check(result.Fleet.Count == 12 && ship.X == -28 && ship.HitPoints == 6000 && ship.Pitch == -123 && ship.Yaw == 456 && ship.Roll == short.MinValue && ship.Speed == 300 && ship.TargetSpeed == 14000 && ship.Flags == 1 && ship.BehaviorIndex == 2 && ship.RouteIndex == 98, "Fleet native field offsets changed.");
        Check(result.Player.X == -27 && result.Player.Y == 8 && result.Player.Z == -27 && result.Player.HitPoints == 16384 && result.Player.ActionType == 1 && result.Player.BehaviorIndex == 3 && result.Player.Data.Length == 40, "Player final DWORD or source units changed.");
        Check(result.SpeedTable.Count == 19 && result.SpeedTable[18] == -100 && result.PlayerTypes.Count == 5 && result.PlayerTypes[4].Span[55] == 0xAB, "Physics table widths changed.");
        Check(result.MotionTables.Count == 4 && result.MotionTables.All(table => table.Count == 33 && table[0] == short.MinValue && table[16] == 16 && table[32] == short.MaxValue), "Centered signed motion tables changed.");
        Check(result.NativeRoots[64] == 4096 && result.NativeReciprocals[255] == 2052, "Unsigned lookup indexing changed.");
        for (int index = 0; index < sources.Length; index++)
        {
            Check(sources[index].SequenceEqual(before[index]), "Loader mutated source data.");
            Array.Clear(sources[index]);
        }
        Check(result.TerrainAscii[0] == (byte)'0' && result.RouteNodes[0].Data.Span[0] == 0xE5 && result.Fleet[0].HitPoints == 6000 && result.Player.Data.Span[0] == 0xE5 && result.PlayerTypes[4].Span[55] == 0xAB && result.NativeRoots[64] == 4096, "Loader retained writable source aliases.");
        Ff7SubmarineResources minimal = Ff7SubmarineResources.Read(terrain, routes, fleet, player);
        Check(minimal.NativeRoots.Count == 0 && minimal.SpeedTable.Count == 0, "Optional authored physics tables acquired invented data.");
        Reject(() => Ff7SubmarineResources.Read(new byte[4095], routes, fleet, player));
        Reject(() => Ff7SubmarineResources.Read(terrain, new byte[4097], fleet, player));
        Reject(() => Ff7SubmarineResources.Read(terrain, routes, new byte[575], player));
        Reject(() => Ff7SubmarineResources.Read(terrain, routes, fleet, new byte[36]));
        Reject(() => Ff7SubmarineResources.Read(terrain, routes, fleet, player, new byte[75]));
        Reject(() => Ff7SubmarineResources.Read(terrain, routes, fleet, player, types: new byte[279]));
        Reject(() => Ff7SubmarineResources.Read(terrain, routes, fleet, player, motion2: new byte[65]));
        Reject(() => Ff7SubmarineResources.Read(terrain, routes, fleet, player, roots: new byte[511]));
        Reject(() => Ff7SubmarineResources.Read(terrain, routes, fleet, player, reciprocals: new byte[513]));
        Reject(() => Ff7SubmarineResources.Read(terrain, routes, fleet, player, routeDefaults: new byte[4095]));
    }

    private static void I32(byte[] data, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
    private static void I16(byte[] data, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), value);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Reject(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("Malformed native submarine table was accepted.");
    }
}
