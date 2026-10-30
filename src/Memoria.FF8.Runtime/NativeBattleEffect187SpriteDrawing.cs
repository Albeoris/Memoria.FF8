using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect187SpriteDrawing
{
    public static NativeBattleModuleSpriteDraw Draw571C80(NativeBattleEffectMemory memory, NativeBattleEffectArena context, Func<uint, NativeBattleModuleResource> readResource, NativeBattleCameraProjection projection, short cameraRoll, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(context.Read(0x24, 2));
        if ((flags & ~0x100) == 0x200)
        {
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(memory, context.Write(0x24, BitConverter.GetBytes((ushort)(flags & ~0x200))), readResource, projection, 0, 17, 2, pilotIdentity: pilotIdentity);
            ushort resultFlags = BinaryPrimitives.ReadUInt16LittleEndian(drawn.Temporary180.Read(0x24, 2));
            return drawn with { Temporary180 = drawn.Temporary180.Write(0x24, BitConverter.GetBytes((ushort)(resultFlags | 0x200))) };
        }
        if ((flags & 0x200) != 0)
        {
            throw new NotSupportedException("Effect187 sprite selected an unsupported rotation flag combination.");
        }
        return NativeBattleModuleSpriteDrawing.Draw571C80(memory, context, readResource, projection, cameraRoll, 17, 2, allowCallerScale: (flags & 2) != 0, pilotIdentity: pilotIdentity);
    }
}
