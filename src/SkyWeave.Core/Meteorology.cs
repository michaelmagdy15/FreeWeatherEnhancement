namespace SkyWeave.Core;

public static class Meteorology
{
    private const double MagnusA = 17.27;
    private const double MagnusB = 237.7;

    public static double CalculateRelativeHumidity(double temperatureCelsius, double dewpointCelsius)
    {
        if (double.IsNaN(temperatureCelsius) || double.IsInfinity(temperatureCelsius) ||
            double.IsNaN(dewpointCelsius) || double.IsInfinity(dewpointCelsius))
            return 0;

        if (temperatureCelsius <= -100 || dewpointCelsius <= -100)
            return 0;

        var exponent = (MagnusA * dewpointCelsius) / (MagnusB + dewpointCelsius) -
                       (MagnusA * temperatureCelsius) / (MagnusB + temperatureCelsius);

        if (double.IsNaN(exponent) || double.IsInfinity(exponent))
            return 0;

        var humidity = 100 * Math.Exp(exponent);
        if (double.IsNaN(humidity) || double.IsInfinity(humidity))
            return 0;

        return Math.Clamp(humidity, 0, 100);
    }
}