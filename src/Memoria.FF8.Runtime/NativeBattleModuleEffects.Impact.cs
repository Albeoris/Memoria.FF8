using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModuleState
{
    private NativeBattleModuleCallback AdvanceImpactSeed5707A0(int slot, NativeBattleModuleInputs inputs)
    {
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, []);
        }
        NativeBattleModuleTask task = Slots[slot];
        uint modelAddress = U32(task.Payload, 12);
        if (modelAddress == 0)
        {
            return new(this, true, []);
        }
        (NativeBattleModuleState state, int child) = Append(NativeBattleModuleKind.Impact570860);
        byte[] payload = state.Slots[child].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload, 0);
        task.Payload.AsSpan().Slice(4, 8).CopyTo(payload.AsSpan(4));
        short rotation = (inputs.ReadModelRotation14 ?? throw new NotSupportedException("Native impact requires its current model rotationWORD14."))(ModelIndex(modelAddress));
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(12), rotation);
        NativeCrtRandomState random = inputs.CrtRandom ?? throw new NotSupportedException("Native impact requires its current CRT thread random owner.");
        random = random.Draw(out ushort roll);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(14), unchecked((short)(roll % 4096)));
        random = random.Draw(out ushort scaleXY);
        int intensity = (int)((U32(task.Payload, 16) >> 16) & 127) << 8;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(16), unchecked((short)(unchecked(intensity * (scaleXY % 768 + 1280)) >> 12)));
        random = random.Draw(out ushort scaleZ);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(20), unchecked((short)(scaleZ % 896 + 1536)));
        state = state with { Slots = state.Slots.SetItem(child, state.Slots[child] with { Payload = payload.ToImmutableArray() }) };
        return new(state, true, []) { CrtRandomAfter = random };
    }

    private NativeBattleModuleCallback AdvanceImpact570860(int slot, NativeBattleModuleInputs inputs)
    {
        NativeBattleModuleTask task = Slots[slot];
        short age = S16(task.Payload, 0);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Impact570860, 0) { Anchor = ReadAnchor(task.Payload, 4), Phase = unchecked((ushort)age), Rotation = S16(task.Payload, 12), Orientation = S16(task.Payload, 14), Scale = S16(task.Payload, 16), ImpactScaleZ = S16(task.Payload, 20) };
        if ((inputs.GraphicsFlags & 1) != 0)
        {
            return new(this, false, [draw]);
        }
        byte[] payload = task.Payload.ToArray();
        short next = unchecked((short)(age + 1));
        BinaryPrimitives.WriteInt16LittleEndian(payload, next);
        return new(this with { Slots = Slots.SetItem(slot, task with { Payload = payload.ToImmutableArray() }) }, next >= 4, [draw]);
    }
}
