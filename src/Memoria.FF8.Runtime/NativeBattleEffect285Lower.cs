using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect285Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect285Lower
{
    public static NativeBattleEffect285Step Advance(NativeBattleEffect285Frame frame, uint address, NativeBattleEffect285Resources resources, NativeBattleEffect285Inputs inputs)
    {
        if (Read(frame, address).Callback != 0x67B720)
        {
            throw new NotSupportedException("The effect requires its selected lower callback.");
        }
        return NativeBattleEffect285Sprite.Advance(frame, address, resources, inputs);
    }

    public static NativeBattleEffect285PointStep BonePoint67B560(NativeBattleEffect285Frame frame, NativeBattleEffectArena local8, NativeBattleEffectArena retained8, NativeBattleEffect285Inputs inputs) => inputs.BonePoint67B560(frame, frame.State.ReadDword(0x2507D10), local8, retained8);

    public static NativeBattleEffect285Frame Camera67B5C0(NativeBattleEffect285Frame frame, int firstX, int secondY, int ratio, NativeBattleEffect285Resources resources, NativeBattleEffect285Inputs inputs)
    {
        NativeBattleEffect285CameraSnapshot camera = inputs.ReadCamera(frame);
        NativeBattleActionMatrix rotation = NativeBattlePandemona291Restoration.Rotation701270(unchecked((short)secondY));
        if (firstX != 0)
        {
            rotation = NativeBattleGfMatrices.Multiply56C090(rotation, NativeBattlePandemona291Restoration.Rotation701220(unchecked((short)firstX)));
        }
        short targetY = Word285(camera.Target8, 2);
        short intermediateY = unchecked((short)(targetY + (unchecked((Word285(camera.Eye8, 2) - targetY) * ratio) >> 12)));
        NativeBattleVector local = new(unchecked((short)(unchecked((Word285(camera.Eye8, 0) - Word285(camera.Target8, 0)) * ratio) >> 12)), unchecked((short)(unchecked((intermediateY - targetY) * ratio) >> 12)), unchecked((short)(unchecked((Word285(camera.Eye8, 4) - Word285(camera.Target8, 4)) * ratio) >> 12)));
        NativeBattleVector transformed = NativeBattleGfMatrices.RotateShort56C4F0(rotation, local);
        NativeBattleEffectArena eye = PutWord285(PutWord285(PutWord285(camera.Eye8, 0, transformed.X + Word285(camera.Target8, 0)), 2, transformed.Y + targetY), 4, transformed.Z + Word285(camera.Target8, 4));
        return inputs.Execute(frame, new NativeBattleEffect285CameraPoint(true, eye));
    }

    public static NativeBattleEffect285Frame Visibility67B6C0(NativeBattleEffect285Frame frame, int argument, NativeBattleEffect285Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect285StageFlags(0xFD, unchecked((byte)((argument & 1) << 1))));
        for (int actor = 6; actor >= 0; actor--)
        {
            uint model = checked(0x1D972C0u + (uint)actor * 0x9C);
            if (argument == 0)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect285ModelFlags(model, 1, 0xFF, 4));
            }
            else
            {
                byte saved = frame.State.Read(0x2507D90 + (uint)(actor * 4), 1)[0];
                frame = inputs.Execute(frame, new NativeBattleEffect285ModelFlags(model, 2, 0xFFFB, (ushort)(saved & 4)));
            }
        }
        return frame;
    }

    public static uint Dword285(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Read(offset, 4));
    public static short Word285(NativeBattleEffectArena bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.Read(offset, 2));
    public static NativeBattleEffectArena Put285(NativeBattleEffectArena bytes, int offset, uint value) => bytes.Write(offset, BitConverter.GetBytes(value));
    public static NativeBattleEffectArena PutWord285(NativeBattleEffectArena bytes, int offset, int value) => bytes.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
}
