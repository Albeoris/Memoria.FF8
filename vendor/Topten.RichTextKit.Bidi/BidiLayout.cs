using System;
using Topten.RichTextKit;
using Topten.RichTextKit.Utils;

namespace Memoria.FF8.Text
{
    /// <summary>Resolves Unicode paragraph embedding levels using the vendored UAX #9 implementation.</summary>
    public sealed class BidiLayout
    {
        private readonly Bidi algorithm = new Bidi();
        private readonly BidiData data = new BidiData();
        public sbyte[] ResolveLevels(int[] codePoints, sbyte paragraphLevel = 0)
        {
            ArgumentNullException.ThrowIfNull(codePoints);
            if (paragraphLevel is < 0 or > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(paragraphLevel));
            }

            if (codePoints.Length == 0)
            {
                return Array.Empty<sbyte>();
            }

            data.Init(new Slice<int>(codePoints), paragraphLevel);
            algorithm.Process(data);
            return algorithm.ResolvedLevels.ToArray();
        }
    }
}
