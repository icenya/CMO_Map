using System;
using Xunit;
using CMOMapTilerPlugin;

namespace CMOMapTilerPlugin.Tests
{
    public class TileConverterTests
    {
        [Theory]
        [InlineData("0", 0, 0, 1)]
        [InlineData("1", 1, 0, 1)]
        [InlineData("2", 0, 1, 1)]
        [InlineData("3", 1, 1, 1)]
        [InlineData("120", 4, 2, 3)]
        [InlineData("132120", 52, 26, 6)]
        public void QuadKeyToTileXY_KnownCoordinates_Match(string quadKey, int expectedX, int expectedY, int expectedZ)
        {
            TileConverter.QuadKeyToTileXY(quadKey, out int x, out int y, out int z);

            Assert.Equal(expectedX, x);
            Assert.Equal(expectedY, y);
            Assert.Equal(expectedZ, z);
        }

        [Theory]
        [InlineData(0, 0, 1, "0")]
        [InlineData(1, 0, 1, "1")]
        [InlineData(0, 1, 1, "2")]
        [InlineData(1, 1, 1, "3")]
        [InlineData(4, 2, 3, "120")]
        [InlineData(52, 26, 6, "132120")]
        public void TileXYToQuadKey_KnownCoordinates_Match(int x, int y, int z, string expectedQuadKey)
        {
            string quadKey = TileConverter.TileXYToQuadKey(x, y, z);
            Assert.Equal(expectedQuadKey, quadKey);
        }

        [Fact]
        public void RoundTrip_AllZoomLevels_Consistent()
        {
            // Test 1000 pseudo-random tile coordinates across Zoom 1..18
            var rand = new Random(42);
            for (int zoom = 1; zoom <= 18; zoom++)
            {
                int maxCoord = (1 << zoom) - 1;
                for (int iter = 0; iter < 50; iter++)
                {
                    int x = rand.Next(0, maxCoord + 1);
                    int y = rand.Next(0, maxCoord + 1);

                    string qk = TileConverter.TileXYToQuadKey(x, y, zoom);
                    Assert.Equal(zoom, qk.Length);

                    TileConverter.QuadKeyToTileXY(qk, out int rx, out int ry, out int rz);
                    Assert.Equal(x, rx);
                    Assert.Equal(y, ry);
                    Assert.Equal(zoom, rz);
                }
            }
        }

        [Fact]
        public void InvalidQuadKey_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => TileConverter.QuadKeyToTileXY("", out _, out _, out _));
            Assert.Throws<ArgumentException>(() => TileConverter.QuadKeyToTileXY(null!, out _, out _, out _));
            Assert.Throws<ArgumentException>(() => TileConverter.QuadKeyToTileXY("1243", out _, out _, out _)); // '4' is invalid
            Assert.Throws<ArgumentException>(() => TileConverter.QuadKeyToTileXY("abc", out _, out _, out _));
        }

        [Fact]
        public void PluginConfig_FormatUrl_ReplacesVariablesCorrectly()
        {
            var config = new PluginConfig
            {
                ApiKey = "TEST_API_KEY_12345",
                UrlTemplate = "https://api.maptiler.com/maps/hybrid/{z}/{x}/{y}.jpg?key={key}&qk={quadkey}"
            };

            string formatted = config.FormatUrl(10, 512, 384, "1321203001");
            Assert.Equal("https://api.maptiler.com/maps/hybrid/10/512/384.jpg?key=TEST_API_KEY_12345&qk=1321203001", formatted);
        }
    }
}
