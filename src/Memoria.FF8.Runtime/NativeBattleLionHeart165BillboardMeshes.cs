namespace Memoria.FF8.Runtime;

public abstract record NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165MeshDraw(uint Address, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context, bool Common) : NativeBattleLionHeart165Operation;
public sealed record NativeBattleLionHeart165Inputs(uint GraphicsFlags, Func<int> Pending508500, Func<byte, NativeBattleGraphicsModel> ReadModel, Func<byte, NativeBattleModuleAnchor> ReadPosition, Func<byte, byte, short, NativeBattleVector> ReadBone, Func<byte, NativeBattleRenzokuken160WeaponTip> ReadWeaponTip, Func<NativeBattleLionHeart165Frame, NativeBattleLionHeart165Operation, NativeBattleLionHeart165Frame> Execute);

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep AdvanceBillboardMesh(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs, NativeBattleActionMatrix view)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        uint callback = task.Callback;
        NativeBattleVector angles = callback switch
        {
            0x5EB790 => new(0, 0, 0),
            0x5ED760 => new(0, 0, Word(task, 24)),
            0x5EE850 => new(256, 0, 256),
            _ => throw new NotSupportedException("Native Lion Heart billboard mesh callback is unsupported.")
        };
        short scale = Word(task, 28);
        NativeBattleActionMatrix rotation = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(angles), scale, scale, scale);
        int depth = callback == 0x5EE850 ? 0 : -(scale >> 3);
        NativeBattleModuleDraw setup = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = new(Word(task, 16), Word(task, 18), Word(task, 20), 0), Scale = callback == 0x5EB790 ? 0 : 4096, DepthOffset = depth, SpriteFlags = 0 };
        NativeBattleActionMatrix camera = NativeBattlePoseMatrices.ModuleSpriteCamera(setup, view, 0);
        NativeBattleActionMatrix matrix = rotation with { X = camera.X, Y = camera.Y, Z = camera.Z };
        uint address = callback == 0x5EB790 ? 0xD7F960u : callback == 0x5ED760 ? 0xD813B0u : 0xD83E3Cu;
        uint flags = 0x33;
        int? fade = null;
        if (callback == 0x5ED760 && (counter < 8 || counter >= 32))
        {
            flags = 0xF3;
            fade = counter < 8 ? (8 - counter) << 9 : (counter - 32) << 9;
        }
        else if (callback == 0x5EE850)
        {
            frame = frame with { Staging = WriteWord(frame.Staging, 0x23C6936, unchecked((ushort)((850 * scale) >> 12))) };
            if (counter >= 16)
            {
                flags = 0xF3;
                fade = (counter - 16) << 9;
            }
        }
        frame = inputs.Execute(frame, new NativeBattleLionHeart165MeshDraw(address, matrix, new(flags, 0, fade, null, null, null, 17, callback == 0x5EE850 ? (byte)1 : (byte)2), callback == 0x5EE850));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        if (callback == 0x5ED760)
        {
            task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + Word(task, 26))));
        }
        if (callback != 0x5EB790 || counter < 16)
        {
            short speed = Word(task, 30);
            task = task.WriteWord(28, unchecked((ushort)(scale + speed))).WriteWord(30, unchecked((ushort)(speed - speed / (callback == 0x5EE850 ? 8 : 32))));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        bool released = callback == 0x5EB790 ? Word(task, 12) >= 48 : task.ReadWord(12) >= (callback == 0x5ED760 ? 40 : 24);
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, released ? 2 : 0);
    }
}
