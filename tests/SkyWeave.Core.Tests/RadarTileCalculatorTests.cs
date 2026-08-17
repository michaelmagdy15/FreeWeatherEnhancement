using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class RadarTileCalculatorTests
{
    [Fact]
    public void PositionToTile_Kjfk_ReturnsValidTile()
    {
        var (tileX, tileY, pixelX, pixelY) = RadarTileCalculator.PositionToTile(40.6399, -73.7787, 9);

        Assert.InRange(tileX, 0, 511);
        Assert.InRange(tileY, 0, 511);
        Assert.InRange(pixelX, 0, 256);
        Assert.InRange(pixelY, 0, 256);
    }

    [Fact]
    public void PositionToTile_Antimeridian_ClampsWithinWorld()
    {
        var (tileX, tileY, _, _) = RadarTileCalculator.PositionToTile(-16.5, 179.9, 9);

        Assert.InRange(tileX, 0, 511);
        Assert.InRange(tileY, 0, 511);
    }

    [Fact]
    public void PositionToTile_Poles_ClampsWithinWorld()
    {
        var (tileX, tileY, _, _) = RadarTileCalculator.PositionToTile(85.0, 0.0, 9);

        Assert.InRange(tileX, 0, 511);
        Assert.InRange(tileY, 0, 511);
    }

    [Fact]
    public void MosaicTiles_ReturnsNineTilesAroundCenter()
    {
        var tiles = RadarTileCalculator.MosaicTiles(100, 50, 9);

        Assert.Equal(9, tiles.Count);
        Assert.Contains((100, 50), tiles);
        Assert.Contains((99, 49), tiles);
        Assert.Contains((101, 51), tiles);
    }

    [Fact]
    public void MosaicTiles_AtWorldEdge_WrapsLongitudeAndClampsLatitude()
    {
        var tiles = RadarTileCalculator.MosaicTiles(0, 0, 9);

        Assert.Equal(9, tiles.Count);
        Assert.All(tiles, t => Assert.InRange(t.TileX, 0, 511));
        Assert.All(tiles, t => Assert.InRange(t.TileY, 0, 511));
        Assert.Equal(6, tiles.Distinct().Count());
    }

    [Fact]
    public void PixelsPerNm_IncreasesWithLatitude()
    {
        var equatorial = RadarTileCalculator.PixelsPerNm(0, 9);
        var midLat = RadarTileCalculator.PixelsPerNm(40, 9);

        Assert.True(equatorial > 0);
        Assert.True(midLat > 0);
        Assert.True(midLat > equatorial);
    }
}