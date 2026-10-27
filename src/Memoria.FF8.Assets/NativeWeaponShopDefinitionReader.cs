using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeWeaponShopDefinitionReader
{
    public static NativeWeaponRecipes ReadNewGameRecipes(byte[] initial)
    {
        NativeInitialData.Validate(initial);
        NativeWeaponRecipes result = new(Convert.ToHexString(SHA256.HashData(initial)), BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(2728)));
        result.Validate();
        return result;
    }

    public static NativeWeaponShopDefinitions Read(byte[] executable, byte[] kernel, byte[] recipes, byte[] messages)
    {
        return Read(new NativeExecutableMemory(executable), kernel, recipes, messages);
    }

    public static NativeWeaponShopDefinitions Read(IGameNativeMemorySource memory, byte[] kernel, byte[] recipes, byte[] messages)
    {
        string executableHash = memory.SourceHash;
        string kernelHash = Convert.ToHexString(SHA256.HashData(kernel));
        string recipeHash = Convert.ToHexString(SHA256.HashData(recipes));
        string messageHash = Convert.ToHexString(SHA256.HashData(messages));
        if (!NativeFieldRuntimeProfile.IsSupportedImage(executableHash))
        {
            throw new NotSupportedException("Native weapon shop requires the authenticated original executable, recipes and message words.");
        }
        NativeMenuDataReader.ValidateWeaponRecipes(recipes);
        NativeMenuDataReader.ValidateWeaponMessages(messages);
        int[] sections = NativeKernelDirectory.Read(kernel);
        int weaponStart = sections[4];
        int weaponEnd = sections[5];
        int nameStart = sections[35];
        int nameEnd = sections[36];
        if (weaponEnd - weaponStart != 33 * 12 || nameStart == nameEnd)
        {
            throw new InvalidDataException("Native weapon kernel rows changed their original thirty-three-record layout.");
        }
        ImmutableArray<NativeWeaponRecipe>.Builder rows = ImmutableArray.CreateBuilder<NativeWeaponRecipe>(33);
        for (int id = 0; id < 33; id++)
        {
            ReadOnlySpan<byte> weapon = kernel.AsSpan(weaponStart + id * 12, 12);
            int position = checked(nameStart + BinaryPrimitives.ReadUInt16LittleEndian(weapon));
            int zero = position < nameEnd ? Array.IndexOf(kernel, (byte)0, position, nameEnd - position) : -1;
            if (zero < 0)
            {
                throw new InvalidDataException("Native weapon name lies outside its original kernel text section.");
            }
            string name = FF8Encoding.CreateEuropean().GetString(kernel[position..zero]);
            ImmutableArray<NativeItemStock> materials = Enumerable.Range(0, 4).Select(slot => new NativeItemStock(recipes[id * 12 + 4 + 2 * slot], recipes[id * 12 + 5 + 2 * slot])).ToImmutableArray();
            rows.Add(new NativeWeaponRecipe(name, weapon[4], recipes[id * 12 + 3], materials, ImmutableArray.CreateRange(weapon.ToArray()), ImmutableArray.CreateRange(recipes.AsSpan(id * 12, 12).ToArray())));
        }
        NativeWeaponShopDefinitions result = new(executableHash, kernelHash, recipeHash, messageHash, rows.MoveToImmutable(), ImmutableArray.CreateRange(messages), ImmutableArray.CreateRange(memory.Read(0xB88918, 256)));
        result.Validate();
        return result;
    }
}
