param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'app\JiggleForge\JiggleForge.csproj'
[xml]$project = Get-Content -LiteralPath $projectPath -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'JiggleForge.csproj does not define Version.'
}

$artifactRoot = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = Join-Path $artifactRoot "JiggleForge-v$version"
$zipPath = Join-Path $artifactRoot "JiggleForge-win-x64-v$version.zip"
$checksumPath = "$zipPath.sha256"

if (Test-Path -LiteralPath $publishDirectory) {
    $resolvedArtifactRoot = [System.IO.Path]::GetFullPath($artifactRoot).TrimEnd('\') + '\'
    $resolvedPublishDirectory = [System.IO.Path]::GetFullPath($publishDirectory)
    if (-not $resolvedPublishDirectory.StartsWith($resolvedArtifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected publish directory: $resolvedPublishDirectory"
    }

    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --output $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.en.md') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'CHANGELOG.md') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'BRANDING.md') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md') -Destination $publishDirectory
New-Item -ItemType Directory -Path (Join-Path $publishDirectory 'docs') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\user-guide') `
    -Destination (Join-Path $publishDirectory 'docs') `
    -Recurse

& powershell -NoProfile -ExecutionPolicy Bypass `
    -File (Join-Path $PSScriptRoot 'GenerateIntegrityManifest.ps1') `
    -OutputDirectory $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Integrity manifest generation failed with exit code $LASTEXITCODE"
}

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $checksumPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal
$sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
    $checksumPath,
    "$sha256  $([System.IO.Path]::GetFileName($zipPath))`r`n",
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Created $zipPath"
Write-Host "SHA-256 $sha256"
