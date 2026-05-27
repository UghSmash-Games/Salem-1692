/*
* AUTHOR: Ron Bresett
* REFERENCES:
* NOTES:
* TODO: [Planned improvements]
* FIXME: [Known bugs or issues]
*/

using System;

namespace Salem.Data
{
    public sealed class XorShiftRng : IRng
    {
        private ulong s0, s1;
        public XorShiftRng(ulong seed)
        {
            s0 = seed | 1UL;
            s1 = (seed ^ 0x9E3779B97F4A7C15UL) | 1UL;
        }
        public int NextInt(int minInclusive, int maxExclusive)
        {
            ulong x = s0, y = s1;
            s0 = y;
            x ^= x << 23;
            x ^= x >> 17;
            x ^= y ^ (y >> 26);
            s1 = x;
            uint u = (uint)(x + y);
            int range = maxExclusive - minInclusive;
            return minInclusive + (int)(u % (uint)range);
        }
    }
}

