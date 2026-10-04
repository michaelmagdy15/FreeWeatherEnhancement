namespace SkyWeave.Core.Services;

public static class RadarTileCalculator
{
    public const int TileSizePx = 256;

    public static (int TileX, int TileY, double PixelX, double PixelY) PositionToTile(
        double latitude, double longitude, int zoom)
    {
        var n = 1 << zoom;
        var x = (longitude + 180.0) / 360.0 * n;
        var latRad = latitude * Math.PI / 180.0;
        var y = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;

        var tileX = (int)Math.Floor(x);
        var tileY = (int)Math.Floor(y);
        tileX = Math.Clamp(tileX, 0, n - 1);
        tileY = Math.Clamp(tileY, 0, n - 1);

        var pixelX = Math.Clamp((x - tileX) * TileSizePx, 0, TileSizePx);
        var pixelY = Math.Clamp((y - tileY) * TileSizePx, 0, TileSizePx);
        return (tileX, tileY, pixelX, pixelY);
    }

    public static List<(int TileX, int TileY)> MosaicTiles(int centerTileX, int centerTileY, int zoom, int radiusTiles = 1)
    {
        var n = 1 << zoom;
        var tiles = new List<(int, int)>();
        for (var dx = -radiusTiles; dx <= radiusTiles; dx++)
        {
            for (var dy = -radiusTiles; dy <= radiusTiles; dy++)
            {
                var x = ((centerTileX + dx) % n + n) % n;
                var y = Math.Clamp(centerTileY + dy, 0, n - 1);
                tiles.Add((x, y));
            }
        }
        return tiles;
    }

    public static double PixelsPerNm(double latitude, int zoom)
    {
        var latRad = latitude * Math.PI / 180.0;
        var nmPerTile = 360.0 / (1 << zoom) * 60.0 * Math.Cos(latRad);
        return TileSizePx / Math.Max(nmPerTile, 1.0);
    }

    public static (double X, double Y) PositionToCanvas(
        double latitude, double longitude,
        double centerLatitude, double centerLongitude,
        int zoom = 6, double canvasSizePx = 768.0)
    {
        var n = (1 << zoom) * TileSizePx;
        var centerLonPx = (centerLongitude + 180.0) / 360.0 * n;
        var centerLatRad = centerLatitude * Math.PI / 180.0;
        var centerLatPx = (1.0 - Math.Log(Math.Tan(centerLatRad) + 1.0 / Math.Cos(centerLatRad)) / Math.PI) / 2.0 * n;

        var targetLonPx = (longitude + 180.0) / 360.0 * n;
        var targetLatRad = latitude * Math.PI / 180.0;
        var targetLatPx = (1.0 - Math.Log(Math.Tan(targetLatRad) + 1.0 / Math.Cos(targetLatRad)) / Math.PI) / 2.0 * n;

        var dx = targetLonPx - centerLonPx;
        // Wrap around antimeridian
        if (dx > n / 2.0) dx -= n;
        if (dx < -n / 2.0) dx += n;

        var dy = targetLatPx - centerLatPx;

        var half = canvasSizePx / 2.0;
        return (half + dx, half + dy);
    }
}