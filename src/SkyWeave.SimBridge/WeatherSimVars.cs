namespace SkyWeave.SimBridge;

public static class WeatherSimVars
{
    public const string AMBIENT_TEMPERATURE = "AMBIENT TEMPERATURE";
    public const string AMBIENT_WIND_DIRECTION = "AMBIENT WIND DIRECTION";
    public const string AMBIENT_WIND_VELOCITY = "AMBIENT WIND VELOCITY";
    public const string AMBIENT_WIND_X = "AMBIENT WIND X";
    public const string AMBIENT_WIND_Y = "AMBIENT WIND Y";
    public const string AMBIENT_WIND_Z = "AMBIENT WIND Z";
    public const string AMBIENT_DENSITY = "AMBIENT DENSITY";
    public const string AMBIENT_IN_CLOUD = "AMBIENT IN CLOUD";
    public const string SEA_LEVEL_PRESSURE = "SEA LEVEL PRESSURE";
    public const string SEA_LEVEL_AMBIENT_TEMPERATURE = "SEA LEVEL AMBIENT TEMPERATURE";
    public const string PLANE_LATITUDE = "PLANE LATITUDE";
    public const string PLANE_LONGITUDE = "PLANE LONGITUDE";
    public const string PLANE_ALTITUDE = "PLANE ALTITUDE";
    public const string INDICATED_ALTITUDE = "INDICATED ALTITUDE";
    public const string KOHLSMAN_SETTING_HG = "KOHLSMAN SETTING HG";
    public const string STRUCTURAL_ICE_PCT = "STRUCTURAL ICE PCT";
    public const string ENV_CLOUD_DENSITY = "ENV CLOUD DENSITY";
    public const string GROUND_ALTITUDE = "GROUND ALTITUDE";
    public const string SIM_ON_GROUND = "SIM ON GROUND";

    public static readonly string[] WeatherVars = new[]
    {
        AMBIENT_TEMPERATURE,
        AMBIENT_WIND_DIRECTION,
        AMBIENT_WIND_VELOCITY,
        SEA_LEVEL_PRESSURE,
        AMBIENT_IN_CLOUD,
        ENV_CLOUD_DENSITY
    };

    public static readonly string[] PositionVars = new[]
    {
        PLANE_LATITUDE,
        PLANE_LONGITUDE,
        PLANE_ALTITUDE,
        INDICATED_ALTITUDE
    };

    public static readonly string[] AllVars = WeatherVars.Concat(PositionVars).ToArray();
}
