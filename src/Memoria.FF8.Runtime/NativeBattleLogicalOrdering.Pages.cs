using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleLogicalOrdering
{
    private readonly record struct PacketSlot(NativeBattlePrimitiveReference Reference, NativeBattleImmutableSlice<uint> Words, NativeBattlePrimitiveLink Next, long Generation)
    {
        internal byte? NextTableBuffer { get; init; }
        internal bool RequiresPhysicalRead { get; init; }
        internal uint? AddressedNextAddress { get; init; }
        internal NativeBattleImmutableSlice<BattleSceneScreenPoint>? ProjectedCoordinates { get; init; }
        internal NativeBattlePilotGeometryReference Pilot { get; init; }
        internal string? PresentationActorId { get; init; }
        internal string? TextureSourceOwner { get; init; }
        internal NativeBattleModelTextureOwner? ModelTextureOwner { get; init; }
        internal NativeBattleScreenCoverage? ScreenCoverage { get; init; }
        internal NativeBattleSceneCapture? SceneCapture { get; init; }
        internal PacketObservation? Observation { get; init; }

        internal PacketSlot(NativeBattleLogicalPacket packet) : this(packet.Reference, packet.Words, packet.Next, 0)
        {
            NextTableBuffer = packet.NextTableBuffer;
            RequiresPhysicalRead = packet.RequiresPhysicalRead;
            AddressedNextAddress = packet.AddressedNextAddress;
            ProjectedCoordinates = packet.ProjectedCoordinates;
            Pilot = packet.Pilot;
            PresentationActorId = packet.PresentationActorId;
            TextureSourceOwner = packet.TextureSourceOwner;
            ModelTextureOwner = packet.ModelTextureOwner;
            ScreenCoverage = packet.ScreenCoverage;
            SceneCapture = packet.SceneCapture;
        }

        internal bool ReadsPhysicalBytes(long generation)
        {
            return RequiresPhysicalRead || Generation != generation;
        }

        internal NativeBattleLogicalPacket Capture(long generation)
        {
            bool reset = !RequiresPhysicalRead && Generation != generation;
            return new(Reference, Words.ToImmutableArray(), Next)
            {
                NextTableBuffer = NextTableBuffer,
                RequiresPhysicalRead = RequiresPhysicalRead || reset,
                AddressedNextAddress = reset ? null : AddressedNextAddress,
                ProjectedCoordinates = ProjectedCoordinates?.ToImmutableArray(),
                Pilot = Pilot,
                PresentationActorId = PresentationActorId,
                TextureSourceOwner = TextureSourceOwner,
                ModelTextureOwner = ModelTextureOwner,
                ScreenCoverage = ScreenCoverage,
                SceneCapture = SceneCapture
            };
        }
    }

    // A diagnostics borrow still preserves record identity while its effective row is unchanged.
    private sealed class PacketObservation
    {
        internal NativeBattleLogicalPacket? Authored;
        internal NativeBattleLogicalPacket? AfterDraw;
    }

    private readonly record struct ProxySlot(NativeBattlePrimitiveReference Packet, ushort Flags20, NativeBattleImmutableSlice<int> VertexPayload4, int? ModuleDraw, byte? NextHigh22)
    {
        internal ProxySlot(NativeBattleLogicalProxy proxy) : this(proxy.Packet, proxy.Flags20, proxy.VertexPayload4, proxy.ModuleDraw, proxy.NextHigh22)
        {
        }

        internal NativeBattleLogicalProxy? Observation { get; init; }

        internal NativeBattleLogicalProxy Capture()
        {
            return new(Packet, Flags20, VertexPayload4.ToImmutableArray(), ModuleDraw) { NextHigh22 = NextHigh22 };
        }
    }

    // Pages keep insertions and undo suffixes from copying the contents of earlier pages on growth.
    private sealed class OrderingPages<T>
    {
        private const int PageSize = 128;
        private readonly List<T[]> pages = [];

        internal int Count { get; private set; }
        internal T this[int index]
        {
            get => Slot(index);
            set => Slot(index) = value;
        }

        internal ref T Slot(int index)
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return ref pages[index / PageSize][index % PageSize];
        }

        internal void Add(in T value)
        {
            int page = Count / PageSize;
            if (page == pages.Count)
            {
                pages.Add(new T[PageSize]);
            }
            pages[page][Count % PageSize] = value;
            Count++;
        }

        internal void Trim(int count)
        {
            if (count >= Count)
            {
                return;
            }
            for (int index = count; index < Count;)
            {
                int page = index / PageSize;
                int offset = index % PageSize;
                int length = Math.Min(PageSize - offset, Count - index);
                Array.Clear(pages[page], offset, length);
                index += length;
            }
            Count = count;
        }

        internal void Clear() => Trim(0);
    }

    private sealed class ProjectedRows<T>(Func<int> count, Func<int, T> read) : IReadOnlyList<T>
    {
        public int Count => count();
        public T this[int index] => read(index);

        public IEnumerator<T> GetEnumerator()
        {
            for (int index = 0; index < Count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class PacketBank
    {
        internal readonly OrderingPages<PacketSlot> Rows = new();
        internal readonly Dictionary<NativeBattlePrimitiveReference, int> Indices = [];
        internal readonly Dictionary<(string Bank, byte Buffer), PacketRangeIndex> Ranges = [];

        internal void Clear()
        {
            Rows.Clear();
            Indices.Clear();
            Ranges.Clear();
        }
    }
}
