using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModuleState
{
    private NativeBattleModuleCallback AdvancePaletteSprite570AC0(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        NativeBattleModuleResource resource = inputs.Resources.SingleOrDefault(resource => resource.Id == 5) ?? throw new NotSupportedException("Native570AC0 requires its actual sprite table slot5.");
        resource.Validate();
        if (resource.Kind != 0)
        {
            resource = inputs.Resources.SingleOrDefault(resource => resource.Id == 0) ?? throw new NotSupportedException("Native570AC0 requires its actual fallback sprite table slot0.");
            resource.Validate();
        }
        ushort phase = unchecked((ushort)S16(task.Payload, 0));
        NativeBattleModuleSpriteFrame frame = resource.SpriteFrame(phase);
        NativeBattleModuleAnchor anchor = ReadAnchor(task.Payload, 4);
        uint packed = U32(task.Payload, 16);
        int scale = (int)((packed >> 16) & 0x7F) << 8;
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Anchor = anchor,
            Phase = phase,
            Scale = scale,
            DepthOffset = -(anchor.W == 0 ? scale >> 4 : anchor.W),
            Rotation = unchecked((short)(((packed >> 8) & 255) << 4)),
            Direction = (packed & 0x800000) != 0 ? -4096 : 4096,
            PackedParameters = packed,
            Resource = resource,
            SpriteFrame = frame,
            SpriteFlags = 0x13,
            SpritePaletteOffsets = [0x11, 0, 0, 0]
        };
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = task.Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, unchecked((short)(phase + 1)));
        NativeBattleModuleTask advanced = task with { Payload = payload.ToImmutableArray() };
        return new(this with { Slots = Slots.SetItem(slot, advanced) }, frame.NextOffset < 0, [draw]);
    }
}
