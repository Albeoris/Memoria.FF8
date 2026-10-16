using System;
using System.Collections.Generic;

namespace Albeoris.Games.FF8.Jsm
{
    public static partial class Jsm
    {
        public interface IJsmControl
        {
            IEnumerable<Segment> EnumerateSegments();
        }
    }
}