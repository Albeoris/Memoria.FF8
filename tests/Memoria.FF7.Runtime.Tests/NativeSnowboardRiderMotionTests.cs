using System.Numerics;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;

internal static class NativeSnowboardRiderMotionTests
{
    internal static void Run()
    {
        CheckHorizontal(0, 65536, -32768, 4096, -2048, 286, 2, 0, 0, 66417, -33208.5f, 0, 290, 0.89442724f);
        CheckHorizontal(0, -65536, 32768, 4096, -2048, 286, 0, 0, 8, -61440, 30720, 286, 268, -0.8944272f);
        CheckHorizontal(12, 65536, -32768, 0, 0, 286, 0, 0, 0, 65528, -32764, 0, 286, 0.8944272f);
        CheckHorizontal(0, 65536, -32768, 4096, -2048, 286, 0, 1, 0, 65536, -32768, 0, 286, 0);
        CheckTurn(0, 512, -32, 65868.7890625f, -31930.091796875f);
        CheckTurn(0, 512, 32, 65116.71875f, -33437.1328125f);
        CheckTurn(12, 512, -32, 65786.59375f, -32139.876953125f);
        CheckTurn(25, 512, -32, 65536, -32768);
        CheckTurn(0, 1280, -128, 65461.9609375f, -29288.189453125f);
        CheckController();

        Ff7SnowboardRiderMotionState rider = new() { Position = new(10, 0, 20), Velocity = new(1, 0, 2), FloorHeight = 10000, Gravity = 4096 };
        if (Ff7SnowboardRiderMotion.AdvanceVertical(rider) || rider.Position.Y != 4096 || rider.AirFrames != 1 || rider.Velocity.Y != 4096)
        {
            throw new InvalidOperationException("The native snowboard airborne integration lost its actual gravity step.");
        }
        if (!Ff7SnowboardRiderMotion.AdvanceVertical(rider) || rider.Position.Y != 10000 || rider.AirFrames != 0 || rider.Velocity.Y != 0 || rider.Gravity != 4096)
        {
            throw new InvalidOperationException("The native snowboard landing did not clamp to the actual sampled floor.");
        }
        rider.Position = new(10, 10000, 20);
        rider.Velocity = Vector3.Zero;
        rider.Gravity = 0;
        if (Ff7SnowboardRiderMotion.AdvanceVertical(rider) || rider.AirFrames != 1)
        {
            throw new InvalidOperationException("The native snowboard strict floor comparison was changed to inclusive contact.");
        }
        Console.WriteLine("Native snowboard horizontal integration vectors, airborne gravity and strict landing passed.");
    }

    private static void CheckController()
    {
        byte[] scripts = new byte[64 * 256];
        new byte[] { 1, 65, 16, 0 }.CopyTo(scripts, 256);
        new byte[] { 5, 0, 15, 0, 1, 24, 1, 0, 1, 25, 24, 2 }.CopyTo(scripts, 16 * 256);
        byte[] before = scripts.ToArray();
        Ff7SnowboardNativeData native = new([], [], [], []) { ActionScripts = scripts, InputMasks = new ushort[] { 16, 64, 128, 32, 4, 1, 8, 2 } };
        Ff7SnowboardRiderController controller = new(native, Vector3.Zero, 1);
        controller.SetAction(1);
        controller.Motion.Speed = 100;
        controller.Motion.Velocity = new(0, 0, -65536);
        for (int tick = 0; tick < 3; tick++)
        {
            controller.Advance(0, 0, tick, 0, 0, 0);
            if (controller.Script.ProgramCounter != 1 || controller.Script.Remaining != 15 - tick || controller.Script.ActionWord != 0x8001 || controller.Motion.Velocity != new Vector3(0, 0, -65536))
            {
                throw new InvalidOperationException("The snowboard controller changed the native timed normal-action callback.");
            }
        }
        controller.Motion.Speed = 512;
        controller.Advance(0x8000, 0, 3, 0, 0, 0);
        if (controller.Script.ActionWord != 17)
        {
            throw new InvalidOperationException("The snowboard native left input did not request its actual next action.");
        }
        controller = new(native, Vector3.Zero, 1);
        controller.Advance(0x1000, 0, 0, 0, 0, 0);
        if (controller.Script.ProgramCounter != 2 || controller.Script.Remaining != 0 || controller.Script.ActionWord != 0x8010)
        {
            throw new InvalidOperationException("The snowboard intro consumed a second animation on its first callback.");
        }
        controller.Advance(0x1000, 0, 1, 0, 0, 0);
        if (controller.Script.ProgramCounter != 3 || controller.Script.Remaining != 23 || !scripts.AsSpan().SequenceEqual(before))
        {
            throw new InvalidOperationException("The snowboard controller changed native intro timing or source action data.");
        }
    }

    private static void CheckTurn(short air, short speed, int amount, float x, float z)
    {
        Ff7SnowboardRiderMotionState rider = new() { AirFrames = air, Speed = speed, Velocity = new(65536, 123, -32768) };
        Ff7SnowboardSteering.Turn(rider, amount);
        if (rider.Velocity != new Vector3(x, 123, z))
        {
            throw new InvalidOperationException($"The native snowboard turn vector disagreed: {rider.Velocity}.");
        }
    }

    private static void CheckHorizontal(short air, float x, float z, float forceX, float forceZ, short speed, int shift, uint flags, byte surface, float expectedX, float expectedZ, float expectedY, short expectedSpeed, float directionX)
    {
        Ff7SnowboardRiderMotionState rider = new() { AirFrames = air, Velocity = new(x, 0, z), Force = new(forceX, 0, forceZ), Speed = speed, ResistanceShift = shift, Flags = flags, Action = 1 };
        Ff7SnowboardRiderMotion.AdvanceHorizontal(rider, surface);
        if (rider.Velocity != new Vector3(expectedX, expectedY, expectedZ) || rider.Position != ((flags & 1) == 0 ? new Vector3(expectedX, 0, expectedZ) : Vector3.Zero) || rider.Speed != expectedSpeed || Math.Abs(rider.Direction.X - directionX) > 0.00000006f)
        {
            throw new InvalidOperationException($"Native snowboard horizontal vector disagreed: {rider}.");
        }
    }
}
