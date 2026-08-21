# SkyWeave In-Sim Weather Bridge

This is the MSFS 2024 HTML/JS side of the SkyWeave weather bridge.

The desktop app sends JSON over the documented SimConnect CommBus event
`SkyWeave.Weather.Apply`. This panel registers the MSFS `JS_LISTENER_WEATHER`
listener and calls its work-in-progress `UpdateTempWeatherPreset` method from
inside the simulator.

## Status

The event is not given a stable payload schema by the SDK. The bridge sends
the SkyWeave `WeatherState` object and reports only whether the Coherent call
was invoked. SkyWeave still requires ambient SimVar readback before it reports
weather as injected.

The panel displays its own live status (ready / events received / apply
result / ack sent) so the bridge state is visible in the simulator.

The installed SDK must expose `CallCommBusEvent`. If it does not, SkyWeave
falls back to the WPR preset path and logs that the bridge is unavailable.

## Package

Run `bridge/build-layout.ps1` from the repository first, then copy this folder
into the MSFS 2024 Community folder as a package and fully restart MSFS. The
panel must be loaded by the simulator's in-game panel system; merely copying
an unloaded HTML file cannot make a JS gauge run.

This package is an experimental bridge and must not be used alongside another
weather injector.
