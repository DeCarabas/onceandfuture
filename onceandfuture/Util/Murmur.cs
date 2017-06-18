namespace OnceAndFuture
{
    static class Murmur3
    {
        public static uint Hash32(byte[] input, uint seed = 0)
        {
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;
            const byte m = 5;
            const uint n = 0xe6546b64;

            uint hash = seed;

            int index;
            for (index = 0; index + 3 < input.Length; index += 4)
            {
                uint k = (uint)(
                    input[index + 0] << 00 |
                    input[index + 1] << 08 |
                    input[index + 2] << 16 |
                    input[index + 3] << 24
                );

                k = k * c1;
                k = (k << 15) | (k >> 17); // ROL 15
                k = k * c2;

                hash = hash ^ k;
                hash = (hash << 13) | (hash >> 19); // ROL 13
                hash = hash * m + n;
            }

            if (index < input.Length)
            {
                uint k = 0;
                int d = input.Length - index;
                if (d >= 3) { k |= (uint)(input[index + 2] << 16); }
                if (d >= 2) { k |= (uint)(input[index + 1] << 08); }
                if (d >= 1) { k |= (uint)(input[index + 0] << 00); }

                k = k * c1;
                k = (k << 15) | (k >> 17); // ROL 15
                k = k * c2;

                hash = hash ^ k;
            }

            hash = hash ^ (uint)input.Length;

            hash = hash ^ (hash >> 16);
            hash = hash * 0x85ebca6b;
            hash = hash ^ (hash >> 13);
            hash = hash * 0xc2b2ae35;
            hash = hash ^ (hash >> 16);

            return hash;
        }
    }
}
