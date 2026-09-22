param(
    [string]$PackagePath = "$PSScriptRoot\SkyWeaveWeatherBridge"
)

$root = (Resolve-Path -LiteralPath $PackagePath).Path
$files = Get-ChildItem -LiteralPath $root -Recurse -File |
    Where-Object { $_.Name -ne 'layout.json' -and $_.Name -ne 'README.md' -and $_.Name -ne 'manifest.json' } |
    ForEach-Object {
        $relative = $_.FullName.Substring($root.Length + 1).Replace('\', '/').ToLowerInvariant()
        [ordered]@{
            path = $relative
            size = $_.Length
            # MSFS package layouts use Windows FILETIME, not .NET ticks.
            date = $_.LastWriteTimeUtc.ToFileTimeUtc()
        }
    }

$layout = [ordered]@{ content = @($files) } |
    ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $root 'layout.json'), $layout)
Write-Output "Updated $root\layout.json"
