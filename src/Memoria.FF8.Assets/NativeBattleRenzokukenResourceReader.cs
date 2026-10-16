using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRenzokukenResourceReader
{
    public static ImmutableArray<BattleNativeRenzokukenResource> Read(byte[] executable)
    {
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Renzokuken effect resources require the authenticated English executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        (ushort Magic, uint Service, uint Resource)[] bindings = [(160, 0x5FF080, 0xEA1BA0), (141, 0x61DE70, 0xEA8AB4), (259, 0x5A6C20, 0xE9264C), (332, 0x58B930, 0xE7E368), (333, 0x589BD0, 0xE7B0C0)];
        return bindings.Select(binding => ReadResource(binding.Magic, binding.Service, binding.Resource)).ToImmutableArray();

        BattleNativeRenzokukenResource ReadResource(ushort magic, uint service, uint resource)
        {
            uint registered = U32(0xC81774 + 4u * (magic - 1u));
            if (registered != service || U32(resource) != 3)
            {
                throw new InvalidDataException("Native Renzokuken service or embedded resource directory differs from its original producer.");
            }
            uint animations = U32(resource + 4);
            uint sequences = U32(resource + 8);
            uint weaponAnimations = U32(resource + 12);
            uint length = U32(resource + 16);
            if (!(20 <= animations && animations < sequences && sequences < weaponAnimations && weaponAnimations < length))
            {
                throw new InvalidDataException("Native Renzokuken embedded resource sections are invalid.");
            }
            ImmutableArray<byte> raw = Bytes(resource, checked((int)length)).ToArray().ToImmutableArray();
            uint count = U32(resource + animations);
            if (count is 0 or > 128 || 4u * (count + 1) > sequences - animations)
            {
                throw new InvalidDataException("Native Renzokuken animation directory exceeds its section.");
            }
            ImmutableArray<byte>.Builder frames = ImmutableArray.CreateBuilder<byte>(checked((int)count));
            for (uint index = 0; index < count; index++)
            {
                uint offset = U32(resource + animations + 4 * (index + 1));
                if (offset < 4 * (count + 1) || offset >= sequences - animations)
                {
                    throw new InvalidDataException("Native Renzokuken animation lies outside its original section.");
                }
                frames.Add(Bytes(resource + animations + offset, 1)[0]);
            }
            NativeBattleGraphicsResource graphics = new(Bytes(resource + sequences, checked((int)(weaponAnimations - sequences))).ToArray().ToImmutableArray()) { AnimationFrames = frames.ToImmutable() };
            graphics.Validate();
            BattleSequenceSet programs = new(graphics.SequenceSection.ToArray());
            ImmutableArray<byte> program = programs.ReadRawProgram(1);
            int abOffset = -1;
            for (int index = 0; index < program.Length; index++)
            {
                if (program[index] == 0xAB)
                {
                    if (abOffset != -1)
                    {
                        throw new NotSupportedException("Embedded Renzokuken sequence requires branch-owned AB selection.");
                    }
                    abOffset = index;
                }
            }
            if (abOffset < 0)
            {
                throw new InvalidDataException("Native Renzokuken sequence has no timing block.");
            }
            BattleNativeRenzokukenBlock block = programs.ReadRenzokukenBlock(1, abOffset, frames);
            return new(magic, service, resource, raw, frames.MoveToImmutable(), graphics, abOffset, block.ShowBar, block.Segments, block.Timeline);
        }

        uint U32(uint address)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(Bytes(address, 4));
        }

        ReadOnlySpan<byte> Bytes(uint address, int length)
        {
            long rva = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + length <= section.VirtualAddress + section.SizeOfRawData);
            return executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), length);
        }
    }
}

public sealed partial class GameAssets
{
    private ImmutableArray<BattleNativeRenzokukenResource> nativeRenzokukenResources;

    public ImmutableArray<BattleNativeRenzokukenResource> ReadNativeRenzokukenResources()
    {
        if (nativeRenzokukenResources.IsDefault)
        {
            nativeRenzokukenResources = NativeBattleRenzokukenResourceReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
        }
        return nativeRenzokukenResources;
    }
}
