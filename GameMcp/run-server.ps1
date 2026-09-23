# Launches the Game MCP server for Claude Code.
#
# Claude Code keeps the server process running for the whole session, which locks the executable it
# started. Building straight into that path then fails, and the build is exactly what a new tool needs.
# So the session runs a copy: this script refreshes the copy at start-up, while nothing holds it, and
# runs that. Build normally into bin/Release; restart Claude Code to pick the new build up.

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$built = Join-Path $here 'Server\bin\Release\net10.0'
$live = Join-Path $here 'Server\bin\live'

# Every message on stdout belongs to the MCP protocol, so progress and problems go to stderr.
# Only when the normal build is the newer one: a build sent straight into the live folder (the way to
# refresh it while a session still holds bin/Release) must not be overwritten by a stale bin/Release.
$builtDll = Join-Path $built 'GameMcp.dll'
$liveDll = Join-Path $live 'GameMcp.dll'
$builtIsNewer = (Test-Path $builtDll) -and
    (-not (Test-Path $liveDll) -or
     (Get-Item $builtDll).LastWriteTimeUtc -gt (Get-Item $liveDll).LastWriteTimeUtc)

if ($builtIsNewer) {
    try {
        New-Item -ItemType Directory -Force -Path $live | Out-Null
        Copy-Item -Path (Join-Path $built '*') -Destination $live -Recurse -Force
    }
    catch {
        [Console]::Error.WriteLine("[run-server] Could not refresh the live copy, running the old one: $_")
    }
}

$exe = Join-Path $live 'GameMcp.exe'
if (-not (Test-Path $exe)) {
    [Console]::Error.WriteLine("[run-server] No server build found. Run: dotnet build GameMcp/Server -c Release")
    exit 1
}

& $exe @args
exit $LASTEXITCODE
