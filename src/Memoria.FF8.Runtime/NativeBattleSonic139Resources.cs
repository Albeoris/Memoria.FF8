using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSonic139Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSonic139Resources(NativeBattleEffectHeapAllocation EntryCopySeed, NativeBattleEffectHeapAllocation FramesSeed, NativeBattleGfAnimationSource MainAct, NativeBattleGfAnimationSource TargetAct, NativeBattleEffectHeapAllocation Camera, ImmutableArray<byte> Anchor, ImmutableArray<ushort> SqrtWords256, ImmutableArray<short> SineWords4096, ImmutableArray<uint> Colors, uint Sound)
{
    public void Validate()
    {
        if (EntryCopySeed.Identity != "FF8_EN:11D4E90" || EntryCopySeed.Data.IsDefault || EntryCopySeed.Data.Length != EntryCopyLength || FramesSeed.Identity != "FF8_EN:11D7900" || FramesSeed.Data.IsDefault || FramesSeed.Data.Length != MutableFramesLength || MainAct.Address != MainActAddress || MainAct.Data.IsDefault || MainAct.Data.Length != 0x26AC || TargetAct.Address != TargetActAddress || TargetAct.Data.IsDefault || TargetAct.Data.Length != 0x1F80 || Camera.Identity != "FF8_EN:11DD21C" || Camera.Data.IsDefault || Camera.Data.Length != 0x11C || BinaryPrimitives.ReadUInt16LittleEndian(Camera.Data.AsSpan().Slice(6)) != 0x11C || Anchor.IsDefault || Anchor.Length != 8 || SqrtWords256.IsDefault || SqrtWords256.Length != 256 || SineWords4096.IsDefault || SineWords4096.Length != 4096 || Colors.IsDefault || !Colors.AsSpan().SequenceEqual(new uint[] { 0x4040FF, 0xFF40FF, 0xFF4040, 0x3167616D }) || Sound != 0x641C8)
        {
            throw new InvalidDataException("Sonic Wave requires its complete original addressed source, ACT, camera, lookup, anchor, color and sound fronts.");
        }
        foreach (int offset in new[] { 0, 0x2420, 0x2A70, 0x30C0, 0x3710 })
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(EntryCopySeed.Data.AsSpan().Slice(offset)) != FrameLength || BinaryPrimitives.ReadInt32LittleEndian(EntryCopySeed.Data.AsSpan().Slice(offset + 4)) != VertexCount)
            {
                throw new InvalidDataException("Sonic Wave requires its five original201-vertex source headers.");
            }
        }
        if (!FramesSeed.Data.AsSpan().SequenceEqual(EntryCopySeed.Data.AsSpan().Slice(checked((int)(MutableFramesAddress - SourceMeshAddress)), MutableFramesLength)))
        {
            throw new InvalidDataException("Sonic Wave cold frame seeds disagree with their actual source bytes.");
        }
    }

    public ImmutableArray<byte> Read(uint address, int length)
    {
        if (length < 0 || address < MutableFramesAddress + MutableFramesLength && (ulong)address + (uint)length > MutableFramesAddress)
        {
            throw new NotSupportedException("Sonic Wave original mutable frame bytes require their retained physical state owner.");
        }
        foreach ((uint origin, ImmutableArray<byte> bytes) in new (uint, ImmutableArray<byte>)[] { (SourceMeshAddress, EntryCopySeed.Data.Slice(0, checked((int)(MutableFramesAddress - SourceMeshAddress)))), (TargetActAddress, TargetAct.Data), (MainActAddress, MainAct.Data), (CameraAddress, Camera.Data), (AnchorAddress, Anchor) })
        {
            if (address >= origin && (ulong)address + (uint)length <= (ulong)origin + (uint)bytes.Length)
            {
                return bytes.Slice(checked((int)(address - origin)), length);
            }
        }
        if (address >= SqrtLookupAddress && (ulong)address + (uint)length <= SqrtLookupAddress + 512)
        {
            return Words(SqrtWords256.Select(value => unchecked((short)value))).Slice(checked((int)(address - SqrtLookupAddress)), length);
        }
        if (address >= SineAddress && (ulong)address + (uint)length <= SineAddress + 8192)
        {
            return Words(SineWords4096).Slice(checked((int)(address - SineAddress)), length);
        }
        if (address >= ColorsAddress && (ulong)address + (uint)length <= ColorsAddress + 16)
        {
            return Colors.SelectMany(BitConverter.GetBytes).ToImmutableArray().Slice(checked((int)(address - ColorsAddress)), length);
        }
        throw new NotSupportedException("Sonic Wave selected bytes outside its bounded original immutable fronts.");
    }
    public NativeBattleEffectArena ReadView(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, Read(address, length).AsSpan());
    private static ImmutableArray<byte> Words(IEnumerable<short> words) => words.SelectMany(BitConverter.GetBytes).ToImmutableArray();
}
