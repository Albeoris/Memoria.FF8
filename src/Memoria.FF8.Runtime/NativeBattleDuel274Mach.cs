namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel274MachInputs(NativeBattleDuel274MoveInputs Moves, Func<NativeBattleDuel274Frame, uint, NativeBattleActionMatrix, uint, int?, uint?, NativeBattleDuel274Frame> Draw572200);

public static partial class NativeBattleDuel274BasicMoves
{
    public static NativeBattleDuel274Frame Advance68B950(NativeBattleDuel274Frame frame, int slot, NativeBattleDuel274Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleDuel274MachInputs supplied)
    {
        NativeBattleDuel274MoveInputs inputs = supplied.Moves;
        NativeBattleModulePoolTask task = Target(frame, slot, 5);
        int actor = Actor(frame);
        int target = NativeBattleDuel274Controller.Index(task.ReadDword(20));
        if (task.ReadWord(12) == 0)
        {
            PositionActor(frame, task, target, actor, inputs, 0);
            uint cursor = Cursor(frame);
            task = task.WriteDword(96, cursor).WriteDword(72, checked(cursor + 616)).WriteDword(80, checked(cursor + 1188));
            frame = SetCursor(Store(frame, slot, task), checked(cursor + 2348));
            frame = NativeBattleDuel274MachGeometry.Backup6D67B0(frame, cursor);
            frame = NativeBattleDuel274Workspaces.Initialize7016B0(frame, resources, task.ReadDword(72), 0x10F10F8, 572);
            frame = NativeBattleDuel274ArenaEcho.Initialize6A1230(frame, task.ReadDword(80), [12, 16, 20]);
        }
        frame = inputs.DrawEcho68A7B0(frame, task.ReadDword(80));
        short counter = Signed(task, 12);
        if (counter is >= 32 and < 46)
        {
            if (counter == 32)
            {
                task = Point(task, 84, inputs.ReadBone502170(actor, 20));
                frame = Store(frame, slot, task);
            }
            frame = Draw(frame, task.ReadDword(72), Yaw(0, Signed(task, 84), Signed(task, 86), Signed(task, 88)), -256, resources, view, projection, colors, inputs);
        }
        uint elapsed = unchecked((uint)(counter - 29));
        if (elapsed < 8)
        {
            if (elapsed < 4)
            {
                task = Point(task, 108, inputs.ReadBone502170(actor, 20));
                frame = Store(frame, slot, task);
            }
            int x = Signed(task, 108) - Signed(task, 100);
            int y = Signed(task, 110) - Signed(task, 102);
            int z = Signed(task, 104) - Signed(task, 112);
            NativeBattleVector position = new(Signed(task, 108), Signed(task, 110), Signed(task, 112));
            NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Basis50CBA0(x, y, z, new(0, 0, 0));
            local = NativeBattlePoseMatrices.Scale56BEF0(local, 512, 512, 512) with { X = position.X, Y = position.Y, Z = position.Z };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, local);
            frame = supplied.Draw572200(frame, 0x10E9530, matrix, elapsed >= 4 ? 0xF0u : 0x30u, elapsed >= 4 ? unchecked((int)(elapsed - 4) << 10) : null, elapsed >= 4 ? 0u : null);
        }
        elapsed = unchecked((uint)(counter - 28));
        if (elapsed < 4)
        {
            if (counter == 28)
            {
                task = Point(task, 100, inputs.ReadBone502170(actor, 20));
                frame = Store(frame, slot, task);
            }
            frame = NativeBattleDuel274MachGeometry.Deform68BCD0(frame, resources, task.ReadDword(96), unchecked((int)(elapsed << 10) + 256), unchecked((short)(elapsed << 10)));
        }
        if (counter == 37)
        {
            frame = NativeBattleDuel274MachGeometry.Restore6D67B0(frame, task.ReadDword(96));
        }
        if (counter is >= 25 and <= 32)
        {
            frame = inputs.CaptureEcho68ADF0(frame, task.ReadDword(80));
        }
        if (counter == 32)
        {
            frame = inputs.Contact506690(frame, task.ReadByte(14));
            frame = inputs.Flash5712C0(frame, true);
        }
        return counter == 27 ? inputs.Sound501330(frame, 0x110BE64) : frame;
    }
}
