using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFlare124Resources(ImmutableArray<byte> Data)
{
    public const uint BaseAddress = 0x1252BE4;
    public NativeBattleGfAnimationSource Burst => new(0x1252C0C, Read(0x1252C0C, 0x99B8));
    public NativeBattleGfAnimationSource Ring => new(0x125C5C4, Read(0x125C5C4, 0xAFC));
    public NativeBattleModuleResource Particle => new(0, BaseAddress, 0, 0, Read(BaseAddress, 0x28));
    public NativeBattleEffectHeapAllocation Camera => new("FF8_EN:125D0C0", Read(0x125D0C0, 0xDDC));
    public uint Sound => Dword(0x125DE9C);
    public uint Red => Dword(0x12652A0);
    public ImmutableArray<byte> Read(uint address, int length) => Data.Slice(checked((int)(address - BaseAddress)), length);
    public uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4).AsSpan());
    public byte Distance(int x, int y) => Read(checked(0x125DEA0u + (uint)(y * 128 + x)), 1)[0];
    public ushort Weight(int step, int index) => BinaryPrimitives.ReadUInt16LittleEndian(Read(checked(0x1261EA0u + (uint)(step * 256 + index * 2)), 2).AsSpan());
    public short Sine(int phase) => BinaryPrimitives.ReadInt16LittleEndian(Read(checked(0x12632A0u + (uint)(phase & 4095) * 2), 2).AsSpan());
}

public sealed record NativeBattleFlare124Execution([property: JsonRequired] int BufferOffset);

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFlare124Execution? Flare124 { get; init; }
}

public static class NativeBattleFlare124Geometry
{
    public static int Flash(short counter) => counter < 8 ? counter << 8 : counter > 76 ? (80 - counter) << 9 : 2048;

    public static uint Color(short counter, uint original, uint red, uint destination)
    {
        if ((uint)(counter - 4) < 32)
        {
            int weight = (counter - 4) << 7;
            return NativeBattleDuelSharedPackets.InterpolateColor56CC00(red, original, weight, 4096 - weight, destination);
        }
        if ((uint)(counter - 72) <= 8)
        {
            int weight = (counter - 72) << 9;
            return NativeBattleDuelSharedPackets.InterpolateColor56CC00(red, original, 4096 - weight, weight, destination);
        }
        return counter >= 4 && counter <= 72 ? red : original;
    }

    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleActionMatrix view, NativeBattleActionMatrix billboard, NativeBattleVector anchor, int scale)
    {
        NativeBattleActionMatrix local = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        NativeBattleVector offset = new(unchecked((short)(unchecked(node.Vector8.X * scale) >> 12)), unchecked((short)(unchecked(node.Vector8.Y * scale) >> 12)), unchecked((short)(unchecked(node.Vector8.Z * scale) >> 12)));
        int x;
        int y;
        int z;
        if ((node.Header & 0x1000) != 0)
        {
            (int ax, int ay, int az) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            (int ox, int oy, int oz) = NativeBattleGfMatrices.Rotate56C3D0(billboard, offset);
            (x, y, z) = (unchecked(ax + ox), unchecked(ay + oy), unchecked(az + oz));
            local = NativeBattleGfMatrices.Multiply56C090(billboard, local);
        }
        else if ((node.Header & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            (x, y, z) = (unchecked(x + offset.X), unchecked(y + offset.Y), unchecked(z + offset.Z));
        }
        else
        {
            NativeBattleVector at = new(unchecked((short)(anchor.X + offset.X)), unchecked((short)(anchor.Y + offset.Y)), unchecked((short)(anchor.Z + offset.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, at);
            local = NativeBattleGfMatrices.Multiply56C090(view, local);
        }
        NativeBattleVector shape = node.Vector24;
        if (shape is not { X: 4096, Y: 4096, Z: 4096 })
        {
            local = (node.Header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(local, new([shape.X, 0, 0, 0, shape.Y, 0, 0, 0, shape.Z], 0, 0, 0, 4096)) : NativeBattlePoseMatrices.Scale56BEF0(local, shape.X, shape.Y, shape.Z);
        }
        return local with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
    }

    public static (NativeBattleModulePoolTask Task, int Mode) PrepareParticle(NativeBattleModulePoolTask task)
    {
        int age = unchecked((short)task.ReadWord(40));
        int grow = unchecked((short)task.ReadWord(14));
        uint elapsed = unchecked((uint)(age - unchecked((short)task.ReadWord(16))));
        if (elapsed <= 8)
        {
            task = task.WriteWord(12, unchecked((ushort)(unchecked((uint)(grow * (8 - (int)elapsed))) >> 3)));
        }
        if (age >= 41)
        {
            task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + (grow >> 2)))).WriteWord(14, unchecked((ushort)(grow > 512 ? grow - (grow >> 2) : 512)));
            if (age >= 56)
            {
                short alpha = unchecked((short)(task.ReadWord(32) - 32));
                task = task.WriteWord(32, unchecked((ushort)alpha));
                return (task, alpha <= 0 ? -2 : 4);
            }
        }
        if (unchecked((short)task.ReadWord(12)) <= 0)
        {
            return (task, -1);
        }
        if (unchecked((short)task.ReadWord(32)) < 128)
        {
            return (task.WriteWord(32, unchecked((ushort)(task.ReadWord(32) + 8))), 4);
        }
        return (task, 0);
    }
}
