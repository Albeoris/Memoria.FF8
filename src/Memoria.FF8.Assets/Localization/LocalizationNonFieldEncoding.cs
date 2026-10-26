using System.Security.Cryptography;
using System.Text;
using Albeoris.Games.FF8.TextEncoding;

namespace Memoria.FF8.Assets.Localization;

/// <summary>Retains undecodable Japanese source slots while typed readers enumerate their original identities.</summary>
internal sealed class LocalizationNonFieldEncoding : Encoding
{
    private readonly FF8Encoding japanese = FF8Encoding.CreateJapanese();
    private readonly Dictionary<string, string> failures = new(StringComparer.Ordinal);

    public bool TryGetFailure(string value, out string? failure) => failures.TryGetValue(value, out failure);

    private string Decode(byte[] bytes, int index, int count)
    {
        byte[] bounded = bytes.AsSpan(index, count).ToArray();
        try
        {
            return japanese.GetString(bounded, 0, bounded.Length);
        }
        catch (Exception error) when (error is FormatException or InvalidOperationException or IndexOutOfRangeException or ArgumentException)
        {
            // Noncharacters cannot be produced by the game's character tables. The private
            // identity token is consumed before a source record is published.
            string token = "\uFDD0" + System.Convert.ToHexString(SHA256.HashData(bounded)) + "\uFDD1";
            failures.TryAdd(token, error.GetType().Name + ": " + error.Message);
            return token;
        }
    }

    public override string GetString(byte[] bytes, int index, int count) => Decode(bytes, index, count);
    public override int GetCharCount(byte[] bytes, int index, int count) => Decode(bytes, index, count).Length;
    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        string value = Decode(bytes, byteIndex, byteCount);
        value.CopyTo(0, chars, charIndex, value.Length);
        return value.Length;
    }
    public override int GetByteCount(char[] chars, int index, int count) => japanese.GetByteCount(chars, index, count);
    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) => japanese.GetBytes(chars, charIndex, charCount, bytes, byteIndex);
    public override int GetMaxByteCount(int charCount) => japanese.GetMaxByteCount(charCount);
    public override int GetMaxCharCount(int byteCount) => Math.Max(66, japanese.GetMaxCharCount(byteCount));
}
