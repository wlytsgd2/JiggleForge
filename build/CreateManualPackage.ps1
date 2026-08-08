param(
    [string]$Configuration = 'Release'
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
$packageDirectory = Join-Path $artifactRoot "JiggleForge-manual-v$version"
$zipPath = Join-Path $artifactRoot "JiggleForge-manual-v$version.zip"
$checksumPath = "$zipPath.sha256"
$runtimeSource = Join-Path $repositoryRoot 'StandaloneShaderFixes'
$templateRoot = Join-Path $repositoryRoot 'packaging\manual'

if (Test-Path -LiteralPath $packageDirectory) {
    $resolvedArtifactRoot = [System.IO.Path]::GetFullPath($artifactRoot).TrimEnd('\') + '\'
    $resolvedPackageDirectory = [System.IO.Path]::GetFullPath($packageDirectory)
    if (-not $resolvedPackageDirectory.StartsWith(
            $resolvedArtifactRoot,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected package directory: $resolvedPackageDirectory"
    }

    Remove-Item -LiteralPath $packageDirectory -Recurse -Force
}

$modTarget = Join-Path $packageDirectory 'Mods\JiggleForgeShaderFix'
$shaderFixesTarget = Join-Path $packageDirectory 'ShaderFixes'
New-Item -ItemType Directory -Path $modTarget -Force | Out-Null
New-Item -ItemType Directory -Path $shaderFixesTarget -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $runtimeSource 'JiggleForge.ini') `
    -Destination $modTarget
$manualRuntimeIni = Join-Path $modTarget 'JiggleForge.ini'
$manualRuntimeContents = Get-Content -LiteralPath $manualRuntimeIni -Raw
$manualDragKeyBlock = @"
; JIGGLEFORGE_DRAG_KEY_BEGIN
[KeyJiggleForgeDrag1]
key = VK_LBUTTON
type = hold
`$mouseDown = 1
post `$mouseDown = 0

[KeyJiggleForgeDrag2]
key = X
type = hold
`$mouseDown = 1
post `$mouseDown = 0
; JIGGLEFORGE_DRAG_KEY_END
"@.Replace("`n", "`r`n")
$dragKeyPattern = '(?ms)^; JIGGLEFORGE_DRAG_KEY_BEGIN\r?\n.*?^; JIGGLEFORGE_DRAG_KEY_END'
$dragKeyMatches = [regex]::Matches($manualRuntimeContents, $dragKeyPattern)
if ($dragKeyMatches.Count -ne 1) {
    throw "Expected one controlled drag-key block, found $($dragKeyMatches.Count)."
}
$manualRuntimeContents = [regex]::Replace(
    $manualRuntimeContents,
    $dragKeyPattern,
    [System.Text.RegularExpressions.MatchEvaluator]{ param($match) $manualDragKeyBlock })
[System.IO.File]::WriteAllText(
    $manualRuntimeIni,
    $manualRuntimeContents,
    [System.Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $runtimeSource 'JiggleForge') `
    -Destination $modTarget `
    -Recurse
Remove-Item -LiteralPath (Join-Path $modTarget 'JiggleForge\WheelBridge.exe') `
    -Force `
    -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $modTarget 'JiggleForge\WheelBridge.txt') `
    -Force `
    -ErrorAction SilentlyContinue

Get-ChildItem -LiteralPath (Join-Path $runtimeSource 'ShaderFixes') `
        -Filter '*-vs_replace.txt' `
        -File |
    Copy-Item -Destination $shaderFixesTarget
Copy-Item -LiteralPath (Join-Path $runtimeSource 'ShaderFixes\JiggleForgeRuntime') `
    -Destination $shaderFixesTarget `
    -Recurse

Copy-Item -LiteralPath (Join-Path $templateRoot 'INSTALL-zh-CN.txt') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $templateRoot 'INSTALL-en.txt') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $templateRoot 'UNINSTALL.txt') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md') -Destination $packageDirectory

$forbiddenExtensions = @(
    '.exe', '.dll', '.com', '.scr', '.msi', '.msp', '.bat', '.cmd', '.ps1',
    '.vbs', '.js', '.jse', '.wsf', '.jar'
)
$forbiddenFiles = Get-ChildItem -LiteralPath $packageDirectory -Recurse -Force -File |
    Where-Object { $forbiddenExtensions -contains $_.Extension.ToLowerInvariant() }
if ($forbiddenFiles) {
    throw "Manual package contains executable or script files: $($forbiddenFiles.FullName -join ', ')"
}

$requiredPaths = @(
    'Mods\JiggleForgeShaderFix\JiggleForge.ini',
    'Mods\JiggleForgeShaderFix\JiggleForge\runtime\motion_model.hlsl',
    'ShaderFixes\c280f6945b23a42a-vs_replace.txt',
    'ShaderFixes\JiggleForgeRuntime\deformation_field.hlsl',
    'INSTALL-zh-CN.txt',
    'INSTALL-en.txt',
    'UNINSTALL.txt',
    'LICENSE'
)
foreach ($relativePath in $requiredPaths) {
    $requiredPath = Join-Path $packageDirectory $relativePath
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Manual package is missing: $relativePath"
    }
}

$manualRuntimeContents = Get-Content -LiteralPath $manualRuntimeIni -Raw
foreach ($expectedKey in @('key = VK_LBUTTON', 'key = X')) {
    if (-not $manualRuntimeContents.Contains($expectedKey)) {
        throw "Manual runtime is missing default drag key: $expectedKey"
    }
}

$replacementShaders = @(
    Get-ChildItem -LiteralPath $shaderFixesTarget -Filter '*-vs_replace.txt' -File
)
if ($replacementShaders.Count -eq 0) {
    throw 'Manual package contains no replacement shaders.'
}

$uninstallContents = Get-Content -LiteralPath (Join-Path $packageDirectory 'UNINSTALL.txt') -Raw
foreach ($shader in $replacementShaders) {
    $hash = $shader.BaseName -replace '-vs_replace$', ''
    if (-not $uninstallContents.Contains("'$hash'")) {
        throw "UNINSTALL.txt does not remove replacement shader hash $hash."
    }
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $checksumPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $packageDirectory '*') `
    -DestinationPath $zipPath `
    -CompressionLevel Optimal
$sha256 = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
    $checksumPath,
    "$sha256  $([System.IO.Path]::GetFileName($zipPath))`r`n",
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Created $zipPath"
Write-Host "SHA-256 $sha256"
