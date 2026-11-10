# Final checked-in sources are canonical; do not regenerate them from the historical launcher.
param([string]$Output=(Join-Path $PSScriptRoot 'VoidburnSetup.exe'))
& (Join-Path $PSScriptRoot 'build-release.ps1') -Output $Output