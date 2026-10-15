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
        if (initial.Length != 2812)
        {
            throw new InvalidDataException("Native known-weapon recipes require the complete original New Game template.");
        }
        NativeWeaponRecipes result = new(Convert.ToHexString(SHA256.HashData(initial)), BinaryPrimitives.ReadUInt32LittleEndian(initial.AsSpan(2728)));
        result.Validate();
        return result;
    }

    public static NativeWeaponShopDefinitions Read(byte[] executable, byte[] kernel, byte[] recipes, byte[] messages)
    {
        string executableHash = Convert.ToHexString(SHA256.HashData(executable));
        string kernelHash = Convert.ToHexString(SHA256.HashData(kernel));
        string recipeHash = Convert.ToHexString(SHA256.HashData(recipes));
        string messageHash = Convert.ToHexString(SHA256.HashData(messages));
        if (executableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || kernelHash != "E378FB8F198EDE3DAE858F0DED6670F9BA423AA79ABFFF7237E701DFC7F9E7F6" || recipeHash != "BB9B499DDE640362F8A16A76534E3FFFD695B2F3C58A73CB65EDC16500BD3BA8" || messageHash != "3AA8E9833EA047A2B8E7E9F65CFE0CE26B9FCFDF83114F5B1DA992C980A99B41" || recipes.Length != 396 || messages.Length != 68)
        {
            throw new NotSupportedException("Native weapon shop requires the authenticated original executable, kernel, recipes and message words.");
        }
        int weaponStart = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 4 * 4));
        int weaponEnd = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 5 * 4));
        int nameStart = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 35 * 4));
        int nameEnd = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 36 * 4));
        if (weaponStart != 0x35B8 || weaponEnd - weaponStart != 396)
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
        NativeWeaponShopDefinitions result = new(executableHash, kernelHash, recipeHash, messageHash, rows.MoveToImmutable(), ImmutableArray.CreateRange(messages), ImmutableArray.CreateRange(executable.AsSpan(0x788918, 256).ToArray()));
        result.Validate();
        return result;
    }
}
