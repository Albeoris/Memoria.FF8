using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectNodeContextExtension([property: JsonRequired] NativeBattleEffectArena Prefix2793DA4, [property: JsonRequired] NativeBattleEffectArena Tail2793E38)
{
    public static NativeBattleEffectNodeContextExtension FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());
    public void Validate()
    {
        Check(Prefix2793DA4, 100);
        Check(Tail2793E38, 32);
    }
    private static void Check(NativeBattleEffectArena arena, int length)
    {
        arena.Validate();
        arena.ValidateExtent(length);
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectNodeContextExtension? NodeContextExtension { get; init; }
}

public static class NativeBattleEffectNodeContextMemory
{
    public const uint Address = 0x2793DA4;
    public const int Length = 180;

    public static NativeBattleEffectArena Capture(NativeBattleEffectMemory memory)
    {
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().MarkUnknown(0, Length);
        if (memory.NodeContextExtension is { } extension)
        {
            extension.Validate();
            view = Copy(extension.Prefix2793DA4, 0, view, 0, 100);
            view = Copy(extension.Tail2793E38, 0, view, 148, 32);
        }
        if (memory.NodeScratch2793E30 is { } scratch)
        {
            scratch.Validate();
            if (scratch.Prefix2793E08 is { } prefix)
            {
                view = Copy(prefix, 0, view, 100, 40);
            }
            view = view.Write(140, scratch.Bytes.AsSpan());
            for (int index = 0; index < 8; index++)
            {
                if ((scratch.KnownMask & (1 << index)) == 0)
                {
                    view = view.MarkUnknown(140 + index, 1);
                }
            }
        }
        return view;
    }

    public static NativeBattleEffectMemory Publish(NativeBattleEffectMemory memory, NativeBattleEffectArena view)
    {
        view.Validate();
        (byte[] bytes, bool[] known) = view.ReadSnapshot(140, 8);
        byte mask = 0;
        for (int index = 0; index < 8; index++)
        {
            if (known[index])
            {
                mask |= (byte)(1 << index);
            }
        }
        NativeBattleEffectNodeScratch scratch = new(bytes.ToImmutableArray(), mask) { Prefix2793E08 = Slice(view, 100, 40) };
        return memory with { NodeScratch2793E30 = scratch, NodeContextExtension = new(Slice(view, 0, 100), Slice(view, 148, 32)) };
    }

    public static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int sourceOffset, NativeBattleEffectArena destination, int destinationOffset, int count)
    {
        (byte[] data, bool[] known) = source.ReadSnapshot(sourceOffset, count);
        destination = destination.Write(destinationOffset, data);
        int offset = 0;
        while (offset < count)
        {
            if (known[offset])
            {
                offset++;
                continue;
            }
            int first = offset;
            while (offset < count && !known[offset])
            {
                offset++;
            }
            destination = destination.MarkUnknown(destinationOffset + first, offset - first);
        }
        return destination;
    }

    private static NativeBattleEffectArena Slice(NativeBattleEffectArena source, int offset, int count) => Copy(source, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, count), 0, count);
}
