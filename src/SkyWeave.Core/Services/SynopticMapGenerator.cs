using System.Globalization;
using System.Text;
using SkyWeave.Core.Models;

namespace SkyWeave.Core.Services;

public class SynopticMapGenerator
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public SynopticMapData Generate(
        WeatherState state,
        double rangeNm = 100.0,
        double canvasWidth = 768.0,
        double canvasHeight = 768.0,
        IReadOnlyList<AirportData>? nearbyStations = null)
    {
        var altimeter = state.AltimeterHpa > 800 ? state.AltimeterHpa : 1013.25;
        var windDir = state.WindDirectionDegrees;
        var windSpd = state.WindSpeedKnots;
        return Generate(state.Latitude, state.Longitude, altimeter, windDir, windSpd, nearbyStations, rangeNm, canvasWidth, canvasHeight);
    }

    public SynopticMapData Generate(
        double centerLat,
        double centerLon,
        double centerAltimeterHpa,
        double centerWindDirDeg,
        double centerWindSpdKt,
        IReadOnlyList<AirportData>? nearbyStations,
        double rangeNm = 100.0,
        double canvasWidth = 768.0,
        double canvasHeight = 768.0)
    {
        var data = new SynopticMapData
        {
            CenterLatitude = centerLat,
            CenterLongitude = centerLon,
            RangeNm = rangeNm
        };

        var cx = canvasWidth / 2.0;
        var cy = canvasHeight / 2.0;
        var pixelsPerNm = (canvasWidth / 2.0) / Math.Max(10.0, rangeNm);

        // 1. Generate Wind Barbs
        // Always generate aircraft / center barb
        data.Barbs.Add(new WindBarb
        {
            StationId = "ACFT",
            Latitude = centerLat,
            Longitude = centerLon,
            X = cx,
            Y = cy,
            DirectionDegrees = centerWindDirDeg,
            SpeedKnots = centerWindSpdKt,
            SvgPath = GenerateWindBarbSvg(cx, cy, centerWindDirDeg, centerWindSpdKt)
        });

        // Add nearby stations with estimated or observed wind
        if (nearbyStations != null)
        {
            foreach (var st in nearbyStations.Take(16))
            {
                var dxNm = (st.Longitude - centerLon) * 60.0 * Math.Cos(centerLat * Math.PI / 180.0);
                var dyNm = (centerLat - st.Latitude) * 60.0;
                var distNm = Math.Sqrt(dxNm * dxNm + dyNm * dyNm);
                if (distNm <= 0.5 || distNm > rangeNm * 1.3) continue;

                var sx = cx + dxNm * pixelsPerNm;
                var sy = cy + dyNm * pixelsPerNm;

                // Derive plausible station wind using regional cyclonic curvature
                var stationBearing = (Math.Atan2(dxNm, -dyNm) * 180.0 / Math.PI + 360.0) % 360.0;
                var angularOffset = Math.Sin((stationBearing - centerWindDirDeg) * Math.PI / 180.0) * 25.0;
                var stWindDir = (centerWindDirDeg + angularOffset + 360.0) % 360.0;
                var stWindSpd = Math.Max(2.0, centerWindSpdKt + Math.Cos(stationBearing * Math.PI / 180.0) * 4.0);

                data.Barbs.Add(new WindBarb
                {
                    StationId = st.IcaoId,
                    Latitude = st.Latitude,
                    Longitude = st.Longitude,
                    X = sx,
                    Y = sy,
                    DirectionDegrees = stWindDir,
                    SpeedKnots = stWindSpd,
                    SvgPath = GenerateWindBarbSvg(sx, sy, stWindDir, stWindSpd)
                });
            }
        }

        // 2. Geostrophic Pressure Centers & Isobars
        // In Northern hemisphere (lat >= 0), Low pressure is 90° to the left of the wind vector.
        // In Southern hemisphere (lat < 0), Low pressure is 90° to the right of the wind vector.
        var isNorth = centerLat >= 0;
        var windToRad = ((centerWindDirDeg + 180.0) % 360.0) * Math.PI / 180.0;
        var lowAngleRad = isNorth
            ? windToRad - Math.PI / 2.0 // 90° left
            : windToRad + Math.PI / 2.0; // 90° right

        var highAngleRad = lowAngleRad + Math.PI;

        // Pressure gradient: stronger wind = steeper gradient
        var gradHpaPer100Nm = Math.Clamp(1.2 + (centerWindSpdKt / 12.0), 1.0, 5.0);

        var lowDistNm = Math.Max(120.0, rangeNm * 1.4);
        var highDistNm = Math.Max(160.0, rangeNm * 1.8);

        // Canvas position of Low and High centers
        var lowX = cx + Math.Sin(lowAngleRad) * lowDistNm * pixelsPerNm;
        var lowY = cy - Math.Cos(lowAngleRad) * lowDistNm * pixelsPerNm;
        var lowPressure = Math.Round(centerAltimeterHpa - (lowDistNm / 100.0) * gradHpaPer100Nm);

        var highX = cx + Math.Sin(highAngleRad) * highDistNm * pixelsPerNm;
        var highY = cy - Math.Cos(highAngleRad) * highDistNm * pixelsPerNm;
        var highPressure = Math.Round(centerAltimeterHpa + (highDistNm / 100.0) * gradHpaPer100Nm);

        data.Centers.Add(new PressureCenter
        {
            Type = "L",
            PressureHpa = lowPressure,
            X = lowX,
            Y = lowY,
            ColorHex = "#FF4757"
        });

        data.Centers.Add(new PressureCenter
        {
            Type = "H",
            PressureHpa = highPressure,
            X = highX,
            Y = highY,
            ColorHex = "#00D2D3"
        });

        // 3. Generate Isobar Contours at 4-hPa intervals
        var minP = Math.Floor((centerAltimeterHpa - 14.0) / 4.0) * 4.0;
        var maxP = Math.Ceiling((centerAltimeterHpa + 14.0) / 4.0) * 4.0;

        for (var p = minP; p <= maxP; p += 4.0)
        {
            var deltaP = p - centerAltimeterHpa;
            // Distance of this isobar from center along the gradient axis
            var offsetNm = (deltaP / gradHpaPer100Nm) * 100.0;
            var offsetPx = offsetNm * pixelsPerNm;

            // Isobar orientation runs parallel to wind (perpendicular to gradient)
            var gradUnitX = Math.Sin(highAngleRad);
            var gradUnitY = -Math.Cos(highAngleRad);
            var isobarUnitX = -gradUnitY;
            var isobarUnitY = gradUnitX;

            var anchorX = cx + gradUnitX * offsetPx;
            var anchorY = cy + gradUnitY * offsetPx;

            // Generate curved contour across canvas
            var spanPx = canvasWidth * 1.5;
            var x1 = anchorX - isobarUnitX * (spanPx / 2.0);
            var y1 = anchorY - isobarUnitY * (spanPx / 2.0);
            var x2 = anchorX + isobarUnitX * (spanPx / 2.0);
            var y2 = anchorY + isobarUnitY * (spanPx / 2.0);

            // Add subtle curvature towards Low
            var curveDepth = spanPx * 0.08 * (isNorth ? 1.0 : -1.0);
            var ctrlX = anchorX - gradUnitX * curveDepth;
            var ctrlY = anchorY - gradUnitY * curveDepth;

            var path = string.Format(Inv, "M {0:F1},{1:F1} Q {2:F1},{3:F1} {4:F1},{5:F1}",
                x1, y1, ctrlX, ctrlY, x2, y2);

            var labelX = Math.Clamp(anchorX + isobarUnitX * 30.0, 50.0, canvasWidth - 50.0);
            var labelY = Math.Clamp(anchorY + isobarUnitY * 30.0, 30.0, canvasHeight - 30.0);

            data.Isobars.Add(new IsobarLine
            {
                PressureHpa = p,
                SvgPath = path,
                Label = $"{p:F0}",
                LabelX = labelX,
                LabelY = labelY,
                StrokeColor = (p == 1012.0 || p == 1016.0) ? "#6638BDF8" : "#3338BDF8"
            });
        }

        return data;
    }

    public static string GenerateWindBarbSvg(double x, double y, double directionDeg, double speedKt)
    {
        var sb = new StringBuilder();

        if (speedKt < 3.0)
        {
            // Calm: small ring
            sb.AppendFormat(Inv, "M {0:F1},{1:F1} m -3,0 a 3,3 0 1,0 6,0 a 3,3 0 1,0 -6,0", x, y);
            return sb.ToString();
        }

        // Screen angle: 0° is up (from North), 90° is right (from East)
        var rad = (directionDeg - 90.0) * Math.PI / 180.0;
        var dirX = Math.Cos(rad);
        var dirY = Math.Sin(rad);

        // Barb feather angle: perpendicular to staff (left side in NH)
        var barbRad = rad + (Math.PI * 0.65);
        var barbX = Math.Cos(barbRad);
        var barbY = Math.Sin(barbRad);

        const double staffLen = 24.0;
        var tipX = x + dirX * staffLen;
        var tipY = y + dirY * staffLen;

        // Staff line: from base (x, y) to tip (tipX, tipY)
        sb.AppendFormat(Inv, "M {0:F1},{1:F1} L {2:F1},{3:F1} ", x, y, tipX, tipY);

        var remainingSpd = speedKt;
        var posAlongStaff = staffLen;

        // 50 kt pennants (triangular flags)
        while (remainingSpd >= 48.0 && posAlongStaff >= 6.0)
        {
            var p1X = x + dirX * posAlongStaff;
            var p1Y = y + dirY * posAlongStaff;
            var p2X = p1X + barbX * 12.0;
            var p2Y = p1Y + barbY * 12.0;
            var p3X = x + dirX * (posAlongStaff - 5.0);
            var p3Y = y + dirY * (posAlongStaff - 5.0);

            sb.AppendFormat(Inv, "M {0:F1},{1:F1} L {2:F1},{3:F1} L {4:F1},{5:F1} Z ",
                p1X, p1Y, p2X, p2Y, p3X, p3Y);

            posAlongStaff -= 6.0;
            remainingSpd -= 50.0;
        }

        // 10 kt full barbs
        while (remainingSpd >= 8.0 && posAlongStaff >= 4.0)
        {
            var p1X = x + dirX * posAlongStaff;
            var p1Y = y + dirY * posAlongStaff;
            var p2X = p1X + barbX * 10.0;
            var p2Y = p1Y + barbY * 10.0;

            sb.AppendFormat(Inv, "M {0:F1},{1:F1} L {2:F1},{3:F1} ", p1X, p1Y, p2X, p2Y);

            posAlongStaff -= 4.0;
            remainingSpd -= 10.0;
        }

        // 5 kt half barb
        if (remainingSpd >= 3.0 && posAlongStaff >= 2.0)
        {
            var p1X = x + dirX * posAlongStaff;
            var p1Y = y + dirY * posAlongStaff;
            var p2X = p1X + barbX * 5.0;
            var p2Y = p1Y + barbY * 5.0;

            sb.AppendFormat(Inv, "M {0:F1},{1:F1} L {2:F1},{3:F1} ", p1X, p1Y, p2X, p2Y);
        }

        return sb.ToString();
    }
}
