using System;
using System.Text;

namespace CMOMapTilerPlugin
{
    public static class TileConverter
    {
        /// <summary>
        /// Converts a Bing QuadKey string into standard Web Mercator Slippy Map (z, x, y) coordinates.
        /// </summary>
        public static void QuadKeyToTileXY(string quadKey, out int x, out int y, out int z)
        {
            if (string.IsNullOrEmpty(quadKey))
            {
                throw new ArgumentException("QuadKey cannot be null or empty.", nameof(quadKey));
            }

            x = 0;
            y = 0;
            z = quadKey.Length;

            for (int i = z; i > 0; i--)
            {
                int mask = 1 << (i - 1);
                char c = quadKey[z - i];
                switch (c)
                {
                    case '0':
                        break;
                    case '1':
                        x |= mask;
                        break;
                    case '2':
                        y |= mask;
                        break;
                    case '3':
                        x |= mask;
                        y |= mask;
                        break;
                    default:
                        throw new ArgumentException($"Invalid QuadKey character '{c}' at position {z - i}.", nameof(quadKey));
                }
            }
        }

        /// <summary>
        /// Converts Web Mercator (z, x, y) coordinates to a Bing QuadKey string.
        /// </summary>
        public static string TileXYToQuadKey(int x, int y, int z)
        {
            var quadKey = new StringBuilder(z);
            for (int i = z; i > 0; i--)
            {
                char digit = '0';
                int mask = 1 << (i - 1);
                if ((x & mask) != 0)
                {
                    digit++;
                }
                if ((y & mask) != 0)
                {
                    digit += (char)2;
                }
                quadKey.Append(digit);
            }
            return quadKey.ToString();
        }
    }
}
