using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SkyWeave.Core.Builders;
using SkyWeave.Core.Decoders;
using SkyWeave.Core.Fetchers;
using SkyWeave.Core.Models;
using SkyWeave.Core.Services;
using Xunit;

namespace SkyWeave.Core.Tests;

public class VatsimAtisTests
{
    private readonly VatsimAtisFetcher _fetcher = new();

    #region ATIS Text Parsing Tests

    [Fact]
    public void ParseAtis_QnhAltimeter_Q1013_ExtractsHpaAndInHg()
    {
        var raw = "EGLL ATIS INFO A 1200Z 27010KT 9999 SCT025 15/10 Q1013";

        var info = _fetcher.ParseAtis(raw, "EGLL");

        Assert.NotNull(info);
        Assert.Equal("EGLL", info.IcaoId);
        Assert.Equal("A", info.AtisLetter);
        Assert.Equal(1013.0, info.AltimeterHpa);
        Assert.Equal(29.91, info.AltimeterInHg);
        Assert.Equal(270, info.WindDirection);
        Assert.Equal(10, info.WindSpeedKt);
    }

    [Fact]
    public void ParseAtis_InHgAltimeter_A2992_ExtractsInHgAndHpa()
    {
        var raw = "KORD ATIS INFO B 1200Z 27010KT 10SM CLR 20/12 A2992";

        var info = _fetcher.ParseAtis(raw, "KORD");

        Assert.NotNull(info);
        Assert.Equal("KORD", info.IcaoId);
        Assert.Equal("B", info.AtisLetter);
        Assert.Equal(29.92, info.AltimeterInHg);
        Assert.Equal(1013.2, info.AltimeterHpa);
    }

    [Fact]
    public void ParseAtis_SpokenAltimeter_ExtractsAltimeter2992()
    {
        var raw = "KDEN INFO C 1200Z WIND 270 AT 12 KNOTS ALTIMETER 29.92";

        var info = _fetcher.ParseAtis(raw, "KDEN");

        Assert.NotNull(info);
        Assert.Equal(29.92, info.AltimeterInHg);
        Assert.Equal(1013.2, info.AltimeterHpa);
        Assert.Equal(270, info.WindDirection);
        Assert.Equal(12, info.WindSpeedKt);
    }

    [Fact]
    public void ParseAtis_SpokenAltimeterFourDigits_ExtractsInHg()
    {
        var raw = "KATL INFO D 1200Z ALTIMETER 2992";

        var info = _fetcher.ParseAtis(raw, "KATL");

        Assert.NotNull(info);
        Assert.Equal(29.92, info.AltimeterInHg);
        Assert.Equal(1013.2, info.AltimeterHpa);
    }

    [Fact]
    public void ParseAtis_SpokenQnh_ExtractsHpaAndInHg()
    {
        var raw = "LFPG INFO E QNH 1026";

        var info = _fetcher.ParseAtis(raw, "LFPG");

        Assert.NotNull(info);
        Assert.Equal(1026.0, info.AltimeterHpa);
        Assert.Equal(30.30, info.AltimeterInHg);
        Assert.Equal("E", info.AtisLetter);
    }

    [Fact]
    public void ParseAtis_InfoBravo_ExtractsAtisLetterB()
    {
        var raw = "KLAX ATIS INFO BRAVO 1353Z. 08003KT 10SM A2994";

        var info = _fetcher.ParseAtis(raw, "KLAX");

        Assert.NotNull(info);
        Assert.Equal("B", info.AtisLetter);
        Assert.Equal("BRAVO", info.AtisPhonetic);
        Assert.Equal(29.94, info.AltimeterInHg);
    }

    [Fact]
    public void ParseAtis_InformationJuliet_ExtractsAtisLetterJ()
    {
        var raw = "THIS IS FLESLAND INFORMATION JULIET .. TIME 1420 .. RUNWAY IN USE 35 .. WIND 330 DEGREES 5 KNOTS .. QNH 1026";

        var info = _fetcher.ParseAtis(raw, "ENBR");

        Assert.NotNull(info);
        Assert.Equal("ENBR", info.IcaoId);
        Assert.Equal("J", info.AtisLetter);
        Assert.Equal("JULIET", info.AtisPhonetic);
        Assert.Equal(330, info.WindDirection);
        Assert.Equal(5, info.WindSpeedKt);
        Assert.Equal(1026.0, info.AltimeterHpa);
        Assert.Equal("35", info.RunwayInUse);
    }

    [Fact]
    public void ParseAtis_AtisMetarFormat_ExtractsAtisLetterG()
    {
        var raw = "ATIS EDDC G METAR 211420 RWY 22 28013G25KT QNH1024";

        var info = _fetcher.ParseAtis(raw, "EDDC");

        Assert.NotNull(info);
        Assert.Equal("EDDC", info.IcaoId);
        Assert.Equal("G", info.AtisLetter);
        Assert.Equal("GOLF", info.AtisPhonetic);
        Assert.Equal("22", info.RunwayInUse);
        Assert.Equal(280, info.WindDirection);
        Assert.Equal(13, info.WindSpeedKt);
        Assert.Equal(25, info.WindGustKt);
        Assert.Equal(1024.0, info.AltimeterHpa);
    }

    [Fact]
    public void ParseAtis_SpokenWind_ExtractsWind270At12Knots()
    {
        var raw = "KDFW INFO C WIND 270 AT 12 KNOTS ALTIMETER 30.01";

        var info = _fetcher.ParseAtis(raw, "KDFW");

        Assert.NotNull(info);
        Assert.Equal(270, info.WindDirection);
        Assert.Equal(12, info.WindSpeedKt);
        Assert.Null(info.WindGustKt);
        Assert.Equal(30.01, info.AltimeterInHg);
    }

    [Fact]
    public void ParseAtis_SpokenWindWithGusts_ExtractsGustSpeed()
    {
        var raw = "KSFO INFO H WIND 270 AT 12 GUSTS 20 KNOTS A2990";

        var info = _fetcher.ParseAtis(raw, "KSFO");

        Assert.NotNull(info);
        Assert.Equal(270, info.WindDirection);
        Assert.Equal(12, info.WindSpeedKt);
        Assert.Equal(20, info.WindGustKt);
    }

    [Fact]
    public void ParseAtis_WindCalm_ExtractsZeroWind()
    {
        var raw = "KPHX ATIS INFO W 1200Z WIND CALM 10SM CLR 35/10 A2980";

        var info = _fetcher.ParseAtis(raw, "KPHX");

        Assert.NotNull(info);
        Assert.Equal(0, info.WindDirection);
        Assert.Equal(0, info.WindSpeedKt);
    }

    [Fact]
    public void ParseAtis_WindVariable_ExtractsZeroDirAndSpeed()
    {
        var raw = "CYUL INFO J WIND VARIABLE AT 4 KNOTS A3012";

        var info = _fetcher.ParseAtis(raw, "CYUL");

        Assert.NotNull(info);
        Assert.Equal(0, info.WindDirection);
        Assert.Equal(4, info.WindSpeedKt);
    }

    [Fact]
    public void ParseAtis_RunwayInUse_ExtractsSingleAndMultipleRunways()
    {
        var raw1 = "KJFK INFO A RUNWAY IN USE 31L Q1015";
        var info1 = _fetcher.ParseAtis(raw1, "KJFK");
        Assert.NotNull(info1);
        Assert.Equal("31L", info1.RunwayInUse);

        var raw2 = "KLAX INFO B RY 24R AND 25L A2992";
        var info2 = _fetcher.ParseAtis(raw2, "KLAX");
        Assert.NotNull(info2);
        Assert.Equal("24R, 25L", info2.RunwayInUse);
    }

    [Fact]
    public void ParseAtis_RealWorldKLAXSample_ParsesAllFields()
    {
        var raw = """
            KLAX ATIS INFO W 1353Z. 08003KT 10SM FEW016 SCT030 OVC041 21/16
            A2994 (TWO NINER NINER FOUR). INST APCHS AND RNAV RNP APCHS RY
            24R AND 25L, OR VCTR FOR VISUAL APCH WILL BE PROVIDED, SIMUL
            VISUAL APCHS TO ALL RWYS ARE IN PROG. SIMUL INSTR DEPARTURES IN
            PROG RWYS 24 AND 25. CALL 4 PUSH ONTO ALL TWYS AND CITY RAMP.
            ...ADVS YOU HAVE INFO W
            """;

        var info = _fetcher.ParseAtis(raw, "KLAX");

        Assert.NotNull(info);
        Assert.Equal("KLAX", info.IcaoId);
        Assert.Equal("W", info.AtisLetter);
        Assert.Equal("WHISKEY", info.AtisPhonetic);
        Assert.Equal(80, info.WindDirection);
        Assert.Equal(3, info.WindSpeedKt);
        Assert.Equal(29.94, info.AltimeterInHg);
        Assert.Equal("24R, 25L", info.RunwayInUse);
    }

    #endregion

    #region Resilience Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t \r\n ")]
    public void ParseAtis_NullOrWhitespace_ReturnsNull(string? input)
    {
        var info = _fetcher.ParseAtis(input!, "KLAX");
        Assert.Null(info);
    }

    [Fact]
    public void ParseAtis_MalformedUnrelatedText_ReturnsNull()
    {
        var raw = "Welcome to our virtual airline discord channel, check rules in announcements.";
        var info = _fetcher.ParseAtis(raw, "EGLL");
        Assert.Null(info);
    }

    [Fact]
    public void ParseAtis_PartialText_ExtractsWhatIsAvailableWithoutCrashing()
    {
        var raw = "INFO BRAVO";
        var info = _fetcher.ParseAtis(raw, "KORD");

        Assert.NotNull(info);
        Assert.Equal("B", info.AtisLetter);
        Assert.Null(info.AltimeterInHg);
        Assert.Null(info.WindSpeedKt);
    }

    #endregion

    #region NetworkClientDetector Tests

    [Fact]
    public void NetworkClientDetector_WhenVPilotRunning_DetectsTrueAndName()
    {
        var runningProcesses = new[] { "svchost", "vPilot", "msedge" };
        using var detector = new NetworkClientDetector(() => runningProcesses);

        Assert.True(detector.IsOnlineClientRunning());
        Assert.Equal("vPilot", detector.GetDetectedClientName());
    }

    [Fact]
    public void NetworkClientDetector_WhenAltitudeRunning_DetectsAltitude()
    {
        var runningProcesses = new[] { "Altitude.exe", "discord" };
        using var detector = new NetworkClientDetector(() => runningProcesses);

        Assert.True(detector.IsOnlineClientRunning());
        Assert.Equal("Altitude", detector.GetDetectedClientName());
    }

    [Fact]
    public void NetworkClientDetector_WhenXPilotRunning_DetectsXPilot()
    {
        var runningProcesses = new[] { "xpilot" };
        using var detector = new NetworkClientDetector(() => runningProcesses);

        Assert.True(detector.IsOnlineClientRunning());
        Assert.Equal("xPilot", detector.GetDetectedClientName());
    }

    [Fact]
    public void NetworkClientDetector_WhenSwiftGuiRunning_DetectsSwift()
    {
        var runningProcesses = new[] { "swift-gui", "steam" };
        using var detector = new NetworkClientDetector(() => runningProcesses);

        Assert.True(detector.IsOnlineClientRunning());
        Assert.Equal("Swift", detector.GetDetectedClientName());
    }

    [Fact]
    public void NetworkClientDetector_WhenVatsimProcessRunning_DetectsVatsim()
    {
        var runningProcesses = new[] { "vatsim_client" };
        using var detector = new NetworkClientDetector(() => runningProcesses);

        Assert.True(detector.IsOnlineClientRunning());
        Assert.Equal("vatsim", detector.GetDetectedClientName());
    }

    [Fact]
    public void NetworkClientDetector_WhenIvaoProcessRunning_DetectsIvao()
    {
        var runningProcesses = new[] { "ivao_pilot" };
        using var detector = new NetworkClientDetector(() => runningProcesses);

        Assert.True(detector.IsOnlineClientRunning());
        Assert.Equal("ivao", detector.GetDetectedClientName());
    }

    [Fact]
    public void NetworkClientDetector_WhenNoKnownClientRunning_ReturnsFalseAndNull()
    {
        var runningProcesses = new[] { "explorer", "FlightSimulator", "chrome" };
        using var detector = new NetworkClientDetector(() => runningProcesses);

        Assert.False(detector.IsOnlineClientRunning());
        Assert.Null(detector.GetDetectedClientName());
    }

    [Fact]
    public void NetworkClientDetector_OnlineClientStatusChanged_FiresOnTransitions()
    {
        var list = new List<string>();
        using var detector = new NetworkClientDetector(() => list);

        bool? lastStatus = null;
        string? lastClient = null;
        int eventCount = 0;

        detector.OnlineClientStatusChanged += (isRunning, client) =>
        {
            lastStatus = isRunning;
            lastClient = client;
            eventCount++;
        };

        // Initially no client
        detector.CheckStatus();
        Assert.Equal(0, eventCount);

        // vPilot starts
        list.Add("vPilot");
        detector.CheckStatus();
        Assert.Equal(1, eventCount);
        Assert.True(lastStatus);
        Assert.Equal("vPilot", lastClient);

        // vPilot continues running (no change -> no event)
        detector.CheckStatus();
        Assert.Equal(1, eventCount);

        // vPilot stops
        list.Clear();
        detector.CheckStatus();
        Assert.Equal(2, eventCount);
        Assert.False(lastStatus);
        Assert.Null(lastClient);
    }

    [Fact]
    public void NetworkClientDetector_ProcessProviderException_IsIsolated()
    {
        using var detector = new NetworkClientDetector(() => throw new InvalidOperationException("Process provider failure"));

        var (isRunning, clientName) = detector.CheckStatus();

        Assert.False(isRunning);
        Assert.Null(clientName);
    }

    #endregion

    #region Offline Fetcher End-to-End Tests

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, HttpResponseMessage> _urlResponses = new(StringComparer.OrdinalIgnoreCase);

        public void RegisterResponse(string url, HttpResponseMessage response)
        {
            _urlResponses[url] = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            if (_urlResponses.TryGetValue(url, out var response))
            {
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task GetAtisAsync_WhenVatsimDataContainsAtis_ExtractsAndReturns()
    {
        var stub = new StubHandler();
        var vatsimDataJson = """
            {
              "atis": [
                {
                  "cid": 814615,
                  "callsign": "KLAX_ATIS",
                  "atis_code": "W",
                  "text_atis": [
                    "KLAX ATIS INFO W 1353Z. 08003KT 10SM FEW016 A2994",
                    "RY 24R AND 25L"
                  ],
                  "last_updated": "2026-09-21T14:50:49Z"
                }
              ]
            }
            """;

        stub.RegisterResponse("https://data.vatsim.net/v3/vatsim-data.json",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(vatsimDataJson, Encoding.UTF8, "application/json")
            });

        using var httpClient = new HttpClient(stub);
        var fetcher = new VatsimAtisFetcher(httpClient);

        var atis = await fetcher.GetAtisAsync("KLAX");

        Assert.NotNull(atis);
        Assert.Equal("KLAX", atis.IcaoId);
        Assert.Equal("W", atis.AtisLetter);
        Assert.Equal(29.94, atis.AltimeterInHg);
        Assert.Equal(80, atis.WindDirection);
        Assert.Equal(3, atis.WindSpeedKt);
        Assert.Equal("24R, 25L", atis.RunwayInUse);
    }

    [Fact]
    public async Task GetAtisAsync_WhenNotInVatsimData_FallsBackToMetarVatsim()
    {
        var stub = new StubHandler();
        var vatsimDataJson = """{ "atis": [] }""";

        stub.RegisterResponse("https://data.vatsim.net/v3/vatsim-data.json",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(vatsimDataJson, Encoding.UTF8, "application/json")
            });

        var metarText = "KJFK 211451Z 04009KT 10SM FEW020 27/21 A2981 RMK AO2";
        stub.RegisterResponse("https://metar.vatsim.net/metar.php?id=KJFK",
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(metarText, Encoding.UTF8, "text/plain")
            });

        using var httpClient = new HttpClient(stub);
        var fetcher = new VatsimAtisFetcher(httpClient);

        var atis = await fetcher.GetAtisAsync("KJFK");

        Assert.NotNull(atis);
        Assert.Equal("KJFK", atis.IcaoId);
        Assert.Equal(29.81, atis.AltimeterInHg);
        Assert.Equal(40, atis.WindDirection);
        Assert.Equal(9, atis.WindSpeedKt);
    }

    [Fact]
    public async Task GetAtisAsync_WhenBothSourcesFail_ReturnsNull()
    {
        var stub = new StubHandler();
        using var httpClient = new HttpClient(stub);
        var fetcher = new VatsimAtisFetcher(httpClient);

        var atis = await fetcher.GetAtisAsync("KZZZ");

        Assert.Null(atis);
    }

    #endregion

    #region Online ATC Integration & Weather Matching Tests

    [Fact]
    public async Task MetarFetcher_WhenPreferVatsimTrue_PrioritizesVatsimMetar()
    {
        var stub = new StubHandler();
        var awcJson = """[{"icaoId":"KJFK","rawOb":"KJFK 211200Z 18010KT 10SM CLR 25/15 A2992","fltCat":"VFR","temp":25,"dewp":15,"wdir":180,"wspd":10,"visib":10,"altim":1013.2}]""";
        var vatsimText = "KJFK 211200Z 27020G30KT 10SM BKN025 22/14 A3000";

        stub.RegisterResponse("https://aviationweather.gov/api/data/metar?ids=KJFK&format=json",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(awcJson, Encoding.UTF8, "application/json") });
        stub.RegisterResponse("https://metar.vatsim.net/metar.php?id=KJFK",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(vatsimText, Encoding.UTF8, "text/plain") });

        using var client = new HttpClient(stub);
        var fetcher = new MetarFetcher(client) { PreferVatsim = true };

        var metar = await fetcher.FetchMetarAsync("KJFK");

        Assert.NotNull(metar);
        Assert.Equal(270, metar.WindDirectionDegrees);
        Assert.Equal(20, metar.WindSpeedKnots);
        Assert.Equal(30, metar.WindGustKnots);
    }

    [Fact]
    public async Task MetarFetcher_WhenPreferVatsimFalse_PrioritizesAwcMetar()
    {
        var stub = new StubHandler();
        var awcJson = """[{"icaoId":"KJFK","rawOb":"KJFK 211200Z 18010KT 10SM CLR 25/15 A2992","fltCat":"VFR","temp":25,"dewp":15,"wdir":180,"wspd":10,"visib":10,"altim":1013.2}]""";
        var vatsimText = "KJFK 211200Z 27020KT 10SM CLR 22/14 A3000";

        stub.RegisterResponse("https://aviationweather.gov/api/data/metar?ids=KJFK&format=json",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(awcJson, Encoding.UTF8, "application/json") });
        stub.RegisterResponse("https://metar.vatsim.net/metar.php?id=KJFK",
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(vatsimText, Encoding.UTF8, "text/plain") });

        using var client = new HttpClient(stub);
        var fetcher = new MetarFetcher(client) { PreferVatsim = false };

        var metar = await fetcher.FetchMetarAsync("KJFK");

        Assert.NotNull(metar);
        Assert.Equal(180, metar.WindDirectionDegrees);
        Assert.Equal(10, metar.WindSpeedKnots);
    }

    [Fact]
    public void WeatherPipeline_WhenAutoMatchOnlineAtcActive_OverridesSurfaceWindAndQnhWithAtis()
    {
        var metar = new MetarData
        {
            StationId = "KJFK",
            TemperatureCelsius = 20,
            DewpointCelsius = 15,
            AltimeterHpa = 1013.2,
            WindDirectionDegrees = 180,
            WindSpeedKnots = 10,
            WindGustKnots = null,
            VisibilityMeters = 10000,
            FlightCategory = "VFR"
        };
        var atis = new VatsimAtisInfo
        {
            IcaoId = "KJFK",
            AtisLetter = "C",
            AltimeterHpa = 1025.0,
            AltimeterInHg = 30.27,
            WindDirection = 270,
            WindSpeedKt = 22,
            WindGustKt = 35,
            RunwayInUse = "31L",
            RawText = "KJFK ATIS INFO CHARLIE 1200Z 27022G35KT QNH 1025 RWY 31L"
        };
        var winds = new WindLayerBuilder().BuildWindLayers(metar, null, 13);
        var data = new PipelineData(metar, null, new(), winds, new(), new(), new(), new(), new(), 0, null, null, atis);

        var pipeline = new WeatherPipeline(null!, null!, null!, null!, null!, null!, null!,
            new CloudLayerBuilder(), new WindLayerBuilder(), new IcingCalculator(),
            new TurbulenceCalculator(), new StormModeler(), new WakeTurbulenceEngine(),
            null!, new HazardAggregator())
        {
            AutoMatchOnlineAtcWeather = true,
            PreferOnlineAtisQnh = true
        };

        var state = pipeline.BuildWeatherState(data, 40.64, -73.78, 13);

        Assert.Equal(1025.0, state.AltimeterHpa);
        Assert.Equal(1025.0, state.PressureHpa);
        Assert.Equal(270, state.WindDirectionDegrees);
        Assert.Equal(22, state.WindSpeedKnots);
        Assert.Equal(35, state.WindGustKnots);
        Assert.NotNull(state.Atis);
        Assert.Equal("C", state.Atis.AtisLetter);

        var surfaceWind = Assert.Single(state.WindsAloft, w => w.IsSurfaceLayer);
        Assert.Equal(270, surfaceWind.DirectionDegrees);
        Assert.Equal(22, surfaceWind.SpeedKnots);
        Assert.Equal(35, surfaceWind.GustSpeedKnots);
    }

    [Fact]
    public void WeatherPipeline_WhenAutoMatchOnlineAtcDisabled_PreservesObservedMetar()
    {
        var metar = new MetarData
        {
            StationId = "KJFK",
            TemperatureCelsius = 20,
            DewpointCelsius = 15,
            AltimeterHpa = 1013.2,
            WindDirectionDegrees = 180,
            WindSpeedKnots = 10,
            WindGustKnots = null,
            VisibilityMeters = 10000,
            FlightCategory = "VFR"
        };
        var atis = new VatsimAtisInfo
        {
            IcaoId = "KJFK",
            AtisLetter = "D",
            AltimeterHpa = 1030.0,
            WindDirection = 360,
            WindSpeedKt = 25
        };
        var winds = new WindLayerBuilder().BuildWindLayers(metar, null, 13);
        var data = new PipelineData(metar, null, new(), winds, new(), new(), new(), new(), new(), 0, null, null, atis);

        var pipeline = new WeatherPipeline(null!, null!, null!, null!, null!, null!, null!,
            new CloudLayerBuilder(), new WindLayerBuilder(), new IcingCalculator(),
            new TurbulenceCalculator(), new StormModeler(), new WakeTurbulenceEngine(),
            null!, new HazardAggregator())
        {
            AutoMatchOnlineAtcWeather = false
        };

        var state = pipeline.BuildWeatherState(data, 40.64, -73.78, 13);

        Assert.Equal(1013.2, state.AltimeterHpa);
        Assert.Equal(180, state.WindDirectionDegrees);
        Assert.Equal(10, state.WindSpeedKnots);
        Assert.Null(state.WindGustKnots);
    }

    [Fact]
    public void SmoothingPipeline_PreservesAtisAcrossInterpolationAndCloning()
    {
        var pipeline = new SmoothingPipeline();
        var atis = new VatsimAtisInfo
        {
            IcaoId = "EGLL",
            AtisLetter = "A",
            AltimeterHpa = 1015.0,
            WindDirection = 270,
            WindSpeedKt = 15
        };
        var state = new WeatherState
        {
            StationId = "EGLL",
            Atis = atis,
            WindDirectionDegrees = 270,
            WindSpeedKnots = 15,
            AltimeterHpa = 1015.0
        };

        pipeline.SetTarget(state);
        var current = pipeline.GetCurrentState();

        Assert.NotNull(current.Atis);
        Assert.Equal("A", current.Atis.AtisLetter);
        Assert.Equal("EGLL", current.Atis.IcaoId);
    }

    [Fact]
    public void WeatherEngine_OnlineAtcProperties_WireToPipeline()
    {
        using var engine = new WeatherEngine();

        Assert.True(engine.AutoMatchOnlineAtcWeather);
        Assert.True(engine.PreferOnlineAtisQnh);
        Assert.False(engine.PreferVatsimMetar);

        engine.PreferVatsimMetar = true;
        Assert.True(engine.PreferVatsimMetar);

        engine.AutoMatchOnlineAtcWeather = false;
        Assert.False(engine.AutoMatchOnlineAtcWeather);

        engine.PreferOnlineAtisQnh = false;
        Assert.False(engine.PreferOnlineAtisQnh);
    }

    #endregion
}
