using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect54Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect54Lower
{
    public static NativeBattleEffect54Frame DrawSprites80F1F0(NativeBattleEffect54Frame frame, uint taskAddress, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        uint node = Read32(frame, frame.State.ReadDword(CurrentContextGlobal) + 0x2C);
        while (node != 0)
        {
            if (Read16(frame, node + 8) == 1)
            {
                RequireSelected(Descriptor(frame, node, resources));
                if (Read32(frame, node + 0x170) != 0)
                {
                    int count = unchecked((sbyte)ReadByte(frame, node + 0x1D8));
                    for (int instance = 0; instance < count; instance++)
                    {
                        if (count != 1)
                        {
                            for (int axis = 0; axis < 3; axis++)
                            {
                                Store32(ref frame, node + 0xC0 + (uint)(axis * 4), unchecked((uint)(int)Read16(frame, node + 0x19C + (uint)(instance * 8 + axis * 2))));
                            }
                        }
                        frame = DrawSprite80F2B0(frame, taskAddress, node, resources, inputs);
                    }
                }
            }
            node = Read32(frame, node + 4);
        }
        return frame;
    }

    public static NativeBattleEffect54Frame DrawSprite80F2B0(NativeBattleEffect54Frame frame, uint taskAddress, uint node, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        uint scratch = unchecked(frame.State.ReadDword(ScratchPointerGlobal) - 180);
        if (scratch != NativeBattleEffectNodeContextMemory.Address)
        {
            throw new NotSupportedException("Effect54 sprite scratch requires its current canonical shared placement.");
        }
        frame = frame with { State = frame.State.WriteDword(ScratchPointerGlobal, scratch) };
        uint source = Read32(frame, node + 0x170);
        ushort phase = unchecked((ushort)(short)(sbyte)ReadByte(frame, node + 0x1D0));
        ImmutableArray<byte> sprite = resources.ReadResource(RequireStaging(frame), source);
        int first = checked((int)(frame.State.ReadDword(EmitterRecordsGlobal) - 0x20DFAB8));
        int second = checked((int)(frame.State.ReadDword(DrawRecordsGlobal) - 0x20DFAB8));
        NativeBattleEffectNodeMemory physical = new(frame.Memory, frame.Effect, taskAddress, first, EmitterStride * EmitterCount, second, DrawStride * DrawCount, true);
        NativeBattleActionMatrix matrix = Packed(ReadBytes(frame, node + 0xAC, 32)).ReadMatrix();
        NativeBattleCameraProjection projection = inputs.ReadProjection(frame);
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(sprite, phase, matrix, projection, inputs.ReadCameraRoll(frame), 17, 2, geometry: physical.ProjectedSpriteGeometry(checked((int)(node - 0x20DFAB8)), source, sprite, phase, projection));
        frame = frame with { Memory = physical.CaptureProcessMemory() };
        frame = inputs.Execute(frame, new NativeBattleEffect54DrawBatch(batch, PrimaryCursorGlobal));
        return frame with { State = frame.State.WriteDword(ScratchPointerGlobal, unchecked(frame.State.ReadDword(ScratchPointerGlobal) + 180)) };
    }
}
