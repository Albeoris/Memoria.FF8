using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBlueCinematicResourcesTests
{
    [Theory]
    [InlineData(229, 10084, "7CB21FA5484916162EF5AAB85BF473606AE9EB0D427BA8C88BA22C1281AA4337", 152672, "89B4FF5C74C813CBA21CEC053C88515F78B6B24352B9D66C68894DBD1C8921DA", 0x188062Cu)]
    [InlineData(230, 23792, "8F73018AADD60644C538AF5AB4A87E3E9B34F2E79ABF64DC021F3A0375618AA9", 204392, "E2A371BB8FFD2F54588B2E27A0BBDB7145CFB02D1393DB273F63C719293ED9C4", 0x187EAB8u)]
    public void Original_profile_preserves_initial_files_directories_and_actor_resource_bounds(int effect, int firstLength, string firstHash, int secondLength, string secondHash, uint actorDirectory)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeExecutableMemory executable = new(File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe")));
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effect);
        Assert.True(NativeBattleCinematicProfile.IsSupported(effect));
        Assert.Equal(profile, NativeBattleCinematicProfile.ForInitializationAddress(profile.InitializationAddress));
        Assert.False(profile.Lit);
        Assert.False(profile.HasVibrationDirectory);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([checked((ushort)effect)])[effect - 1];
        Assert.Equal(profile.Entry, binding.Entry);
        Assert.Equal(profile.Initializer, binding.Initializer);
        NativeBattleEffectInitializerAsset[] initial = binding.InitializerAssets!.Value.ToArray();
        Assert.Equal(2, initial.Length);
        Assert.Equal(profile.InitialFile, initial[0].Name);
        Assert.Equal(profile.InitialFile[..^1] + "1", initial[1].Name);
        Assert.Equal(firstLength, initial[0].Data.Length);
        Assert.Equal(firstHash, Convert.ToHexString(SHA256.HashData(initial[0].Data.AsSpan())));
        Assert.Equal(secondLength, initial[1].Data.Length);
        Assert.Equal(secondHash, Convert.ToHexString(SHA256.HashData(initial[1].Data.AsSpan())));
        Assert.Equal(profile.File0Publication, binding.InitializerResources!.Value[0].GlobalAddress);
        Assert.Equal(profile.File1Publication, binding.InitializerResources!.Value[1].GlobalAddress);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(initial[0].Data.AsSpan().Slice(0x24)));
        NativeBattleCinematicResources resources = assets.ReadNativeCinematicResources(effect);
        resources.Validate();
        Assert.Null(resources.Vibration);
        Assert.Same(resources, assets.ReadNativeCinematicResources(effect));
        Assert.Equal(executable.Read(profile.InitializationAddress, profile.StaticLength), resources.StaticData.Data.ToArray());
        foreach ((string table, uint address, int count) in new[]
        {
            ("bone_table", profile.BoneTable, profile.BoneCount),
            ("prim_table", profile.PrimitiveTable, 60),
            ("spr_table", profile.SpriteTable, 11),
            ("ptype_table", profile.PlacementTable, 5),
            ("pop_table", profile.ParticleInstructionTable, 24),
            ("draw_table", profile.DrawTable, 73),
            ("vm_table", profile.InstructionTable, 0x147)
        })
        {
            Assert.Equal(executable.Read(address, count * 4), resources.ReadStatic(address, count * 4).ToArray());
            for (int index = 0; index < count; index++)
            {
                uint handler = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(address + (uint)index * 4, 4));
                uint codeEnd = effect == 229 ? 0xA85590u : 0xA7A9F0u;
                if (handler < profile.Initializer || handler >= codeEnd)
                {
                    Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, table, index, handler));
                    continue;
                }
                uint selected = NativeBattleCinematicDispatch.RequireSlot(effect, table, index, handler);
                if (NativeBattleCinematicDispatch.IsNoOp(effect, table, index, selected))
                {
                    Assert.Equal((byte)0xC3, executable.Read(handler, 1)[0]);
                }
                Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(effect, table, index, handler + 1));
            }
        }
        int sections = BinaryPrimitives.ReadInt32LittleEndian(executable.Read(actorDirectory, 4));
        Assert.Equal(3, sections);
        int length = BinaryPrimitives.ReadInt32LittleEndian(executable.Read(actorDirectory + 4u + 4u * (uint)sections, 4));
        Assert.Equal(7028, length);
        Assert.Equal(executable.Read(actorDirectory, length), resources.AdditionalStaticData[actorDirectory].ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(actorDirectory + (uint)length, 1).ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(profile.InitializationAddress - 1, 1).ToArray());
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(profile.InitializationAddress + (uint)profile.StaticLength, 1).ToArray());
    }
}
