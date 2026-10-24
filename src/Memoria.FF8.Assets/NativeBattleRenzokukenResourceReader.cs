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
        (ushort Magic, uint Service, uint Resource)[] bindings = [(160, 0x5FF080, 0xEA1BA0), (141, 0x61DE70, 0xEA8AB4), (259, 0x5A6C20, 0xE9264C), (332, 0x58B930, 0xE7E368), (333, 0x589BD0, 0xE7B0C0), (159, 0x600BC0, 0xEA3A28), (161, 0x5FD100, 0xE9B324), (334, 0x587790, 0xE71864), (335, 0x5856B0, 0xE6CC48), (336, 0x583040, 0xE640FC), (337, 0x580990, 0xE5DF7C), (339, 0x57E5D0, 0xE567AC), (340, 0x57BC00, 0xE5110C), (341, 0x579A80, 0xE4F048), (342, 0x5776D0, 0xE47704), (343, 0x575650, 0xE426D0)];
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
            return new(magic, service, resource, raw, frames.MoveToImmutable(), graphics, abOffset, block.ShowBar, block.Segments, block.Timeline) { TargetResources = ReadTargets(magic, resource) };
        }

        ImmutableArray<BattleNativeRenzokukenTargetResource> ReadTargets(ushort magic, uint actorResource)
        {
            uint outer = magic switch
            {
                159 => 0xEA3A18,
                161 => 0xE9B314,
                334 => 0xE71854,
                335 => 0xE6CC38,
                336 => 0xE640EC,
                337 => 0xE5DF6C,
                339 => 0xE56798,
                340 => 0xE510FC,
                341 => 0xE4F038,
                342 => 0xE476F4,
                343 => 0xE426C0,
                _ => 0
            };
            if (outer == 0)
            {
                return [];
            }
            uint count = U32(outer);
            uint expected = magic == 339 ? 3u : 2u;
            uint totalLength = U32(outer + 4 * (count + 1));
            if (count != expected || outer + U32(outer + 4) != actorResource)
            {
                throw new InvalidDataException("Native Renzokuken paired resource directory differs from its original binding.");
            }
            ImmutableArray<BattleNativeRenzokukenTargetResource>.Builder targets = ImmutableArray.CreateBuilder<BattleNativeRenzokukenTargetResource>(checked((int)count - 1));
            for (uint index = 1; index < count; index++)
            {
                uint offset = U32(outer + 4 * (index + 1));
                uint end = U32(outer + 4 * (index + 2));
                uint target = outer + offset;
                uint animations = U32(target + 4);
                uint sequences = U32(target + 8);
                uint length = U32(target + 12);
                if (offset >= end || end > totalLength || U32(target) != 2 || animations < 16 || sequences <= animations || sequences >= length || length != end - offset)
                {
                    throw new InvalidDataException("Native Renzokuken target resource exceeds its paired directory.");
                }
                ImmutableArray<byte> raw = Bytes(target, checked((int)length)).ToArray().ToImmutableArray();
                uint animationCount = U32(target + animations);
                if (animationCount is 0 or > 128 || 4 * (animationCount + 1) > sequences - animations)
                {
                    throw new InvalidDataException("Native Renzokuken target animation directory exceeds its section.");
                }
                ImmutableArray<byte>.Builder frames = ImmutableArray.CreateBuilder<byte>(checked((int)animationCount));
                for (uint animation = 0; animation < animationCount; animation++)
                {
                    uint frameOffset = U32(target + animations + 4 * (animation + 1));
                    if (frameOffset < 4 * (animationCount + 1) || frameOffset >= sequences - animations)
                    {
                        throw new InvalidDataException("Native Renzokuken target animation exceeds its original section.");
                    }
                    frames.Add(Bytes(target + animations + frameOffset, 1)[0]);
                }
                NativeBattleGraphicsResource graphics = new(Bytes(target + sequences, checked((int)(length - sequences))).ToArray().ToImmutableArray()) { AnimationFrames = frames.ToImmutable() };
                graphics.Validate();
                targets.Add(new(target, raw, frames.MoveToImmutable(), graphics));
            }
            return targets.MoveToImmutable();
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
            nativeRenzokukenResources = NativeBattleRenzokukenResourceReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
        }
        return nativeRenzokukenResources;
    }
}
