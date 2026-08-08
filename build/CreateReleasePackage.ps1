param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repositoryRoot 'app\JiggleForge\JiggleForge.csproj'
$launcherProjectPath = Join-Path $repositoryRoot 'src\JiggleForge.Launcher\JiggleForge.Launcher.csproj'
[xml]$project = Get-Content -LiteralPath $projectPath -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'JiggleForge.csproj does not define Version.'
}

$artifactRoot = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = Join-Path $artifactRoot "JiggleForge-v$version"
$applicationDirectory = Join-Path $publishDirectory 'App'
$runtimeDirectory = Join-Path $publishDirectory 'Runtime'
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
    --output $applicationDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$sourceRuntimeIni = Join-Path $repositoryRoot 'StandaloneShaderFixes\JiggleForge.ini'
$publishedRuntimePayload = Join-Path $applicationDirectory 'RuntimePayload'
$publishedRuntimeIni = Join-Path $publishedRuntimePayload 'JiggleForge.ini'
if (-not (Test-Path -LiteralPath $publishedRuntimeIni -PathType Leaf)) {
    throw "Published runtime payload is missing: $publishedRuntimeIni"
}

$sourceRuntimeHash = (Get-FileHash -LiteralPath $sourceRuntimeIni -Algorithm SHA256).Hash
$publishedRuntimeHash = (Get-FileHash -LiteralPath $publishedRuntimeIni -Algorithm SHA256).Hash
if ($sourceRuntimeHash -ne $publishedRuntimeHash) {
    throw 'Published RuntimePayload\JiggleForge.ini does not match the current source runtime.'
}

$requiredSelfContainedFiles = @(
    'hostfxr.dll',
    'Microsoft.WindowsAppRuntime.dll',
    'Microsoft.UI.Xaml.dll',
    'DWriteCore.dll'
)
foreach ($requiredFile in $requiredSelfContainedFiles) {
    $requiredPath = Join-Path $applicationDirectory $requiredFile
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Self-contained publish is missing required runtime file: $requiredFile"
    }
}

Move-Item -LiteralPath $publishedRuntimePayload -Destination $runtimeDirectory
Remove-Item -LiteralPath (Join-Path $applicationDirectory 'JiggleForge.manifest.sha256') `
    -Force `
    -ErrorAction SilentlyContinue

dotnet build $launcherProjectPath `
    --configuration $Configuration `
    --property:Platform=x64
if ($LASTEXITCODE -ne 0) {
    throw "Launcher build failed with exit code $LASTEXITCODE"
}

$launcherOutput = Join-Path $repositoryRoot "src\JiggleForge.Launcher\bin\x64\$Configuration\net48\JiggleForge.exe"
if (-not (Test-Path -LiteralPath $launcherOutput -PathType Leaf)) {
    throw "Launcher output is missing: $launcherOutput"
}

Copy-Item -LiteralPath $launcherOutput -Destination (Join-Path $publishDirectory 'JiggleForge.exe')

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

$requiredLayoutPaths = @(
    'JiggleForge.exe',
    'App\JiggleForge.exe',
    'App\JiggleForge.Updater.exe',
    'Runtime\JiggleForge.ini',
    'README.md',
    'LICENSE'
)
foreach ($relativePath in $requiredLayoutPaths) {
    $requiredPath = Join-Path $publishDirectory $relativePath
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Organized release layout is missing: $relativePath"
    }
}

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
