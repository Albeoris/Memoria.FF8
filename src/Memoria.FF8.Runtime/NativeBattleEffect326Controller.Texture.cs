namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Step AdvanceTexture62BB40(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint source = frame.State.ReadDword(0x24F22E4);
        byte[] pixels = NativeBattleEffect326Texture.Warp62BB40(ReadBytes(frame, source, 0x4000).Read(0, 0x4000), Signed(task, 0x18));
        uint destination = checked(source + 0x4000);
        frame = WriteBytes(frame, destination, NativeBattleEffectArena.Clear().Write(0, pixels), pixels.Length);
        frame = inputs.Execute(frame, new NativeBattleEffect326UploadPixels505DF0(0xE196B8, destination));
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = Read(frame, address).WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + 32))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(Write(frame, address, task), Signed(task, 12) > 26 ? 2 : 0);
    }
}
