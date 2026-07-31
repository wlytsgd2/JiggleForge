param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Publish output does not exist: $root"
}

$manifestPath = Join-Path $root 'JiggleForge.manifest.sha256'
$lines = Get-ChildItem -LiteralPath $root -Recurse -Force -File |
    Where-Object {
        $_.FullName -ne $manifestPath -and
        $_.Extension -ne '.pdb'
    } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($root.TrimEnd('\').Length + 1).Replace('\', '/')
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash *$relative"
    }

if ($lines.Count -eq 0) {
    throw 'No publish files were found for the integrity manifest.'
}

[System.IO.File]::WriteAllLines(
    $manifestPath,
    [string[]]$lines,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Generated $manifestPath with $($lines.Count) files."
