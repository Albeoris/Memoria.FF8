using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDuel212Controller
{
    public static NativeBattleDuel212Frame SpawnLate6A1660(NativeBattleDuel212Frame frame, NativeBattleVector origin, bool resetFragments)
    {
        NativeBattleEffectTaskPool fragments = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == Fragments);
        if (resetFragments)
        {
            NativeBattleEffectTaskPool cleared = NativeBattleEffectTaskPool.Initialize(Fragments, fragments.BankAddress, fragments.Stride, checked((ushort)fragments.Slots.Length));
            fragments = cleared with { Slots = cleared.Slots.Select((task, index) => task with { Payload = fragments.Slots[index].Payload, ResourcePointers = fragments.Slots[index].ResourcePointers, UnknownPayloadRanges = fragments.Slots[index].UnknownPayloadRanges }).ToImmutableArray() };
        }
        for (int index = 0; index < 2; index++)
        {
            uint callback = ((3 * Rand()) >> 15) switch { 0 => 0x6A3280u, 1 => 0x6A33C0u, 2 => 0x6A33E0u, _ => throw new InvalidDataException("Native Burning Rave debris selector left its original three callbacks.") };
            if (fragments.Slots.All(task => (task.Flags & 1) != 0))
            {
                break;
            }
            (fragments, int slot) = fragments.Append(callback);
            NativeBattleModulePoolTask task = fragments.Slots[slot].WriteDword(24, 0).WriteByte(25, 50).WriteByte(27, 15);
            task = task.WriteWord(32, unchecked((ushort)((Rand() & 0x7FB) + 1024)));
            task = task.WriteWord(12, unchecked((ushort)(origin.X + (Rand() & 0xFFF) - 2048))).WriteWord(14, 1024).WriteWord(16, unchecked((ushort)(origin.Z - 5160)));
            task = task.WriteWord(18, 0).WriteWord(20, unchecked((ushort)(-512 - (Rand() & 0x1FF)))).WriteWord(22, 0);
            task = task.WriteByte(28, unchecked((byte)Rand())).WriteByte(29, unchecked((byte)Rand())).WriteByte(30, unchecked((byte)Rand())).WriteByte(31, unchecked((byte)Rand()));
            fragments = fragments with { Slots = fragments.Slots.SetItem(slot, task) };
        }
        NativeBattleEffectTaskPool sprites = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == Sprites);
        Sprite(0x6A3800, 16);
        Sprite(0x6A38B0, 12);
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(pool => pool.DescriptorAddress == Fragments ? fragments : pool.DescriptorAddress == Sprites ? sprites : pool).ToImmutableArray() } };

        void Sprite(uint callback, byte life)
        {
            if (sprites.Slots.All(task => (task.Flags & 1) != 0))
            {
                return;
            }
            (sprites, int slot) = sprites.Append(callback);
            NativeBattleModulePoolTask task = sprites.Slots[slot].WriteWord(12, unchecked((ushort)(origin.X + 4700 * Rand() / 32768 - 2350))).WriteWord(14, 0).WriteWord(16, unchecked((ushort)(origin.Z - 4136)));
            task = task.WriteWord(18, unchecked((ushort)(((Rand() << 11) >> 15) + 4096))).WriteByte(20, 0).WriteByte(21, unchecked((byte)-127)).WriteByte(22, 0).WriteByte(23, life);
            sprites = sprites with { Slots = sprites.Slots.SetItem(slot, task) };
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
