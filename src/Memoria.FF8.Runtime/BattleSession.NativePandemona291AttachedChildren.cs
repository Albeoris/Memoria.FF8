using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePandemona291ChildStep Step, NativeBattleGraphicsState Graphics) AdvanceNativePandemona291AttachedChild(NativeBattlePandemona291Frame frame, uint address, NativeBattleModulePoolTask task, NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona attached programme requires its actual arena.");
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x10);
        uint rowPointer = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(start + 0xC, 4));
        int row = NativeBattleWaterExecutionFields.ArenaOffset(rowPointer, 32);
        uint actorPointer = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(row, 4));
        uint relative = unchecked(actorPointer - 0x1D972C0);
        if (relative % 156 != 0 || relative / 156 >= graphics.Models.Length)
        {
            throw new NotSupportedException("Native Pandemona attached programme requires its actual physical actor pointer.");
        }
        NativeBattleGraphicsModel actor = graphics.Models[checked((int)(relative / 156))];
        NativeBattleModelTransform transform = actor.Transform ?? throw new NotSupportedException("Native Pandemona attached programme requires its current actor position and scale.");
        short height = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(row + 4, 2));
        NativeBattlePandemona291Resources resources = frame.Effect.Binding.Pandemona291Resources ?? throw new NotSupportedException("Native Pandemona attached programme requires its current resources.");
        short scale = unchecked((short)(transform.Scale38 + 1024));
        NativeBattleActionMatrix local = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], transform.Position28.X, unchecked(transform.Position28.Y + height), transform.Position28.Z, 4096);
        NativeBattleActionMatrix view = NativeCamera?.Projection?.View ?? throw new NotSupportedException("Native Pandemona attached programme requires its actual view.");
        NativeBattleActionMatrix parent = NativeBattleGfMatrices.Compose56C2F0(view, local);
        int length = task.Callback == 0x6F0D90 ? 0x180 : 0x8C;
        uint sourceAddress = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(start + 0x10, 4));
        uint buffer = frame.Memory.Pandemona291?.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona attached programme requires its current buffer.");
        NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == 726);
        uint resourceStart = unchecked(buffer + 0x23220);
        long offset = (long)sourceAddress - resourceStart;
        if (offset < 0 || offset >= file.Data.Length)
        {
            throw new InvalidDataException("Native Pandemona attached programme leaves its delivered source owner.");
        }
        int sourceLength = checked(file.Data.Length - (int)offset);
        NativeBattleGfAnimationSource source = NativeBattleGfAnimationSource.Borrow(sourceAddress, arena, NativeBattleWaterExecutionFields.ArenaOffset(sourceAddress, sourceLength), sourceLength);
        NativeBattlePandemona291SurfaceInputs inputs = new(source, parent, null, 0, -128, length, 0x10);
        var drawn = DrawNativePandemona291Surface(frame, address, 0, graphics, inputs);
        return (new(drawn.Frame, task, drawn.Remaining == 0 ? 2 : 0), drawn.Graphics);
    }
}
