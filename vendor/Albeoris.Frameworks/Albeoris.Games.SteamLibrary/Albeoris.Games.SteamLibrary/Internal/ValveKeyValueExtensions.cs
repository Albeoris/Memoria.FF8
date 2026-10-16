using System.Globalization;
using ValveKeyValue;

namespace Albeoris.Games.SteamLibrary.Internal;

internal static class ValveKeyValueExtensions
{
    public static IFormatProvider FormatProvider => CultureInfo.InvariantCulture;

    public static IEnumerable<KVObject> EnumerateChildren(this KVObject self, String childName)
    {
        KVValue? value = self.FindValue(childName);
        if (value is null)
            yield break;

        foreach (KVObject child in (IEnumerable<KVObject>)value)
            yield return child;
    }

    public static String GetString(this KVObject self, String childName) =>
        self.GetValue(childName).ToString(FormatProvider).Replace(@"\\", @"\");

    public static String? FindString(this KVObject self, String childName) =>
        self.FindValue(childName)?.ToString(FormatProvider).Replace(@"\\", @"\");

    public static UInt32 GetUInt32(this KVObject self, String childName) =>
        self.GetValue(childName).ToUInt32(FormatProvider);

    public static UInt64 GetUInt64(this KVObject self, String childName) =>
        self.GetValue(childName).ToUInt64(FormatProvider);

    public static UInt64? FindUInt64(this KVObject self, String childName) =>
        self.FindValue(childName)?.ToUInt64(FormatProvider);

    public static Int64? FindInt64(this KVObject self, String childName) =>
        self.FindValue(childName)?.ToInt64(FormatProvider);

    public static KVValue? FindValue(this KVObject self, String childName)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(childName);

        KVValue? value = self[childName];
        if (value is not null)
            return value;

        return self.Children
            .FirstOrDefault(child => String.Equals(child.Name, childName, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    public static KVValue GetValue(this KVObject self, String childName)
    {
        return self.FindValue(childName) ?? throw new InvalidDataException(
            $"Required Valve KeyValues field '{childName}' is missing from '{self.Name}'.");
    }
}
