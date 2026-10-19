namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLionHeart165WindowedMeshDraw(uint Address, NativeBattleActionMatrix Matrix, NativeBattleHoly175WindowedMeshContext Context, bool Common) : NativeBattleLionHeart165Operation;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep AdvanceAlignedMesh(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        short counter = Word(task, 12);
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        if (callback == 0x5EDC10 && Word(task, 14) > 0)
        {
            if (!paused)
            {
                task = task.WriteWord(14, unchecked((ushort)(Word(task, 14) - 1)));
            }
            return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, 0);
        }
        NativeBattleActionMatrix matrix;
        uint mesh;
        int? fade;
        if (callback == 0x5EDC10)
        {
            if (counter == 0)
            {
                NativeBattleRenzokuken160WeaponTip tip = inputs.ReadWeaponTip(frame.Parameters.Actor78);
                NativeBattleVector point = new(unchecked((short)(tip.Position.X + ((380 * tip.DirectionX) >> 12))), unchecked((short)(tip.Position.Y + ((380 * tip.DirectionY) >> 12))), unchecked((short)(tip.Position.Z + ((380 * tip.DirectionZ) >> 12))));
                frame = frame with { Staging = Point(frame.Staging, 0x23C6930, point) };
            }
            NativeBattleModuleProjectedVertex first = NativeBattleModuleProjection.Project45FAA0(view, new(Word(task, 16), Word(task, 18), Word(task, 20)), projection, 0, 0);
            NativeBattleModuleProjectedVertex second = NativeBattleModuleProjection.Project45FAA0(view, new(ReadWord(frame.Staging, 0x23C6930), ReadWord(frame.Staging, 0x23C6932), ReadWord(frame.Staging, 0x23C6934)), projection, 0, 0);
            int x = unchecked((short)second.Screen) / 8;
            int y = unchecked((short)(second.Screen >> 16)) / 8;
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(unchecked((short)first.Screen) / 8 - x, unchecked((short)(first.Screen >> 16)) / 8 - y, 0);
            NativeBattleModuleAxisAngle aligned = NativeBattleModuleAlignment.Between571480(4096, 0, 0, direction.X, direction.Y, direction.Z);
            matrix = new(NativeBattleModuleAlignment.Rotation5714F0(aligned.Angle, aligned.Axis), x - 160, y - 108, projection.FocalLength ?? throw new NotSupportedException("Native Lion Heart screen-aligned mesh requires its original H WORD."), 4096);
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, Word(task, 28), Word(task, 32), Word(task, 32));
            mesh = 0xD81B34;
            fade = counter >= 32 ? (counter - 32) << 9 : null;
        }
        else if (callback is 0x5ECB40 or 0x5ECCD0)
        {
            uint source = unchecked(0x23C32F8u + (uint)(Word(task, 14) * 28));
            NativeBattleModuleAxisAngle aligned = NativeBattleModuleAlignment.Between571480(0, 0, -4096, ReadDword(frame.Staging, source + 8), ReadDword(frame.Staging, source + 12), ReadDword(frame.Staging, source + 16));
            NativeBattleActionMatrix basis = new(NativeBattleModuleAlignment.Rotation5714F0(aligned.Angle, aligned.Axis), 0, 0, 0, 4096);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(basis, NativeBattleGfMatrices.Euler56CE30(new(0, 0, Word(task, 24))));
            local = local with { X = ReadWord(frame.Staging, source), Y = ReadWord(frame.Staging, source + 2), Z = ReadWord(frame.Staging, source + 4) };
            int scale = Word(task, 28);
            matrix = NativeBattlePoseMatrices.Compose56C2F0(view, NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale));
            mesh = callback == 0x5ECB40 ? 0xD823E4u : 0xD82B5Cu;
            fade = callback == 0x5ECB40 ? counter >= 4 ? 682 * (counter - 4) : null : counter >= 2 ? (counter - 2) << 9 : null;
        }
        else
        {
            throw new NotSupportedException("Native Lion Heart aligned mesh callback is unsupported.");
        }
        NativeBattleTemporaryAllocation context = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 88);
        uint flags = fade.HasValue ? 0xF3u : 0x33u;
        NativeBattleFileStaging staging = WriteDword(WriteDword(WriteDword(context.Staging, context.Address, mesh), context.Address + 8, 0), context.Address + 28, flags);
        if (fade.HasValue)
        {
            staging = WriteDword(staging, context.Address + 12, unchecked((uint)fade.Value));
        }
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleLionHeart165MeshDraw(mesh, matrix, new(flags, 0, fade, null, null, null, 17, 2), false));
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 88) };
        if (paused)
        {
            return new(frame, pool, 0);
        }
        if (callback == 0x5EDC10)
        {
            int scale = counter > 15 ? 128 : (unchecked(NativeBattleCameraMath.Sine((counter << 10) / 15 + 1024) << 13) >> 12) + 128;
            task = task.WriteWord(32, unchecked((ushort)scale));
        }
        else
        {
            short speed = Word(task, 30);
            task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) + speed))).WriteWord(30, unchecked((ushort)(speed - speed / (callback == 0x5ECB40 ? 4 : 24))));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, task.ReadWord(12) >= (callback == 0x5EDC10 ? 40 : 10) ? 2 : 0);
    }

    public static NativeBattleLionHeart165ChildStep AdvanceWindowedMesh(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        short counter = Word(task, 12);
        if (callback is not (0x5EA8E0 or 0x5ED590))
        {
            throw new NotSupportedException("Native Lion Heart windowed mesh callback is unsupported.");
        }
        bool common = callback == 0x5ED590;
        NativeBattleVector angles = common ? new(Word(task, 24), 0, Word(task, 26)) : new(0, Word(task, 24), 0);
        int scale = Word(task, 28);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(angles), scale, common ? scale : Word(task, 32), scale);
        matrix = common ? matrix with { X = 0, Y = 0, Z = projection.FocalLength ?? throw new NotSupportedException("Native Lion Heart windowed mesh requires its actual H WORD.") } : NativeBattlePoseMatrices.Compose56C2F0(view, matrix with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) });
        uint mesh = common ? 0xD7B668u : 0xD879ECu;
        int scroll = counter * (common ? Word(task, 32) : 16);
        int? fade = common ? counter < 8 ? 4096 - 384 * counter : counter < 20 ? 1024 : 384 * counter - 6656 : counter < 8 ? (8 - counter) << 9 : null;
        uint color = common ? unchecked((uint)ReadDword(frame.Staging, unchecked(0xD88808u + (uint)(counter % 18) * 4))) : 0x808080;
        uint flags = fade.HasValue ? 0xF3u : 0x33u;
        NativeBattleTemporaryAllocation context = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 144);
        NativeBattleFileStaging staging = WriteDword(WriteDword(WriteDword(WriteDword(WriteDword(context.Staging, context.Address, mesh), context.Address + 8, 0), context.Address + 16, 0), context.Address + 20, unchecked((uint)scroll)), context.Address + 32, flags);
        staging = WriteWord(WriteWord(WriteWord(WriteWord(staging, context.Address + 24, common ? (ushort)0 : (ushort)64), context.Address + 26, 0), context.Address + 28, 64), context.Address + 30, 128);
        staging = WriteDword(staging, context.Address + 36, color);
        if (fade.HasValue)
        {
            staging = WriteDword(staging, context.Address + 12, unchecked((uint)fade.Value));
        }
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleLionHeart165WindowedMeshDraw(mesh, matrix, new(flags, 0, fade, color, 0, scroll, new(common ? (short)0 : (short)64, 0, 64, 128), common ? 4122 : 17, common ? (byte)6 : (byte)2), common));
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 144) };
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        if (!common)
        {
            short speed = Word(task, 34);
            task = task.WriteWord(32, unchecked((ushort)(Word(task, 32) + speed))).WriteWord(34, unchecked((ushort)(speed - speed / 32)));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, task.ReadWord(12) >= (common ? 28 : 60) ? 2 : 0);
    }
}
