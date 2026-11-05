param()
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$payload = Join-Path $PSScriptRoot 'payload'
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest.runtimeFiles) {
    $file = Join-Path $payload $entry.path
    if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
        throw "Frozen runtime differs from verified r1: $($entry.path)"
    }
}
if (@(Get-ChildItem -LiteralPath $payload -File -Recurse).Count -ne $manifest.runtimeFiles.Count) {
    throw 'Unexpected files in frozen runtime payload.'
}
& (Join-Path $repo 'build\CreateReleasePackage.ps1') `
    -VersionOverride '0.1.26-beta.3' `
    -RuntimePayloadSource $payload `
    -TestBuildNotesDirectory $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'Test application packaging failed.' }
