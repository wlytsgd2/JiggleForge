[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$temporaryRoot = Join-Path $env:TEMP 'JiggleForge-GpuParity'
New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null

$windowsKitRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$fxc = Get-ChildItem -LiteralPath $windowsKitRoot -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object {
        $candidate = Join-Path $_.FullName 'x64\fxc.exe'
        if (Test-Path -LiteralPath $candidate) {
            $candidate
        }
    } |
    Select-Object -First 1
if (-not $fxc) {
    throw 'Windows SDK x64 fxc.exe was not found.'
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio Installer vswhere.exe was not found.'
}

$visualStudioRoot = & $vswhere `
    -latest `
    -products '*' `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -property installationPath
if (-not $visualStudioRoot) {
    throw 'A Visual Studio installation with the C++ x64 tools was not found.'
}

$developerCommand = Join-Path $visualStudioRoot 'Common7\Tools\VsDevCmd.bat'
$shaderInclude = Join-Path $projectRoot 'StandaloneShaderFixes\JiggleForge\runtime'
$shaderNames = @('MotionParity', 'InputControllerParity')
$compiledShaders = @{}
foreach ($shaderName in $shaderNames) {
    $shaderSource = Join-Path $PSScriptRoot "$shaderName.hlsl"
    $compiledShader = Join-Path $temporaryRoot "$shaderName.cso"
    & $fxc `
        /nologo `
        /T cs_5_0 `
        /E main `
        /I $shaderInclude `
        /Fo $compiledShader `
        $shaderSource
    if ($LASTEXITCODE -ne 0) {
        throw "FXC failed for $shaderName with exit code $LASTEXITCODE."
    }

    $compiledShaders[$shaderName] = $compiledShader
}

$runtimeShaders = @(
    'update_input_cs.hlsl',
    'build_diagnostic_text_cs.hlsl',
    'update_motion_cs.hlsl',
    'register_draw_parameters_cs.hlsl',
    'register_default_parameters_cs.hlsl'
)
foreach ($runtimeShader in $runtimeShaders) {
    $shaderSource = Join-Path $shaderInclude $runtimeShader
    $compiledShader = Join-Path $temporaryRoot "$runtimeShader.cso"
    & $fxc `
        /nologo `
        /WX `
        /T cs_5_0 `
        /E main `
        /I $shaderInclude `
        /Fo $compiledShader `
        $shaderSource
    if ($LASTEXITCODE -ne 0) {
        throw "FXC failed for $runtimeShader with exit code $LASTEXITCODE."
    }
}

$framePickResetSource = Join-Path $projectRoot `
    'StandaloneShaderFixes\JiggleForge\runtime\reset_frame_pick_cs.hlsl'
$framePickResetOutput = Join-Path $temporaryRoot 'reset_frame_pick_cs.cso'
& $fxc `
    /nologo `
    /WX `
    /T cs_5_0 `
    /E main `
    /Fo $framePickResetOutput `
    $framePickResetSource
if ($LASTEXITCODE -ne 0) {
    throw "FXC failed for reset_frame_pick_cs.hlsl with exit code $LASTEXITCODE."
}

$consumerShaders = @(
    'c280f6945b23a42a',
    '26214fb5eedfcbdd',
    '699981e2a62dd9b4',
    '402766e2987d7821',
    '6883e4375b728e90',
    '1f6ab42231416fdb',
    'aa59281029db3a5a',
    '160b58ea1824c794',
    'a0b37a7c7c2a1905',
    'ad24b1c214866fd7',
    'd0a1a756bd3bde31'
)
foreach ($consumerHash in $consumerShaders) {
    $consumerSource = Join-Path $projectRoot `
        "StandaloneShaderFixes\ShaderFixes\$consumerHash-vs_replace.txt"
    $consumerOutput = Join-Path $temporaryRoot "$consumerHash-runtime-consumer.cso"
    $consumerArguments = @(
        '/nologo',
        '/T', 'vs_5_0',
        '/E', 'main',
        '/Fo', $consumerOutput,
        $consumerSource
    )
    $partialOutputHashes = @(
        '402766e2987d7821',
        '6883e4375b728e90',
        'aa59281029db3a5a',
        '160b58ea1824c794',
        'a0b37a7c7c2a1905',
        'ad24b1c214866fd7',
        'd0a1a756bd3bde31'
    )
    if ($consumerHash -notin $partialOutputHashes) {
        $consumerArguments = @('/WX') + $consumerArguments
    }
    & $fxc $consumerArguments
    if ($LASTEXITCODE -ne 0) {
        throw "FXC failed for runtime consumer VS $consumerHash with exit code $LASTEXITCODE."
    }
}

$pickerShaders = @(
    'c280_jiggle.hlsl',
    '2621_jiggle.hlsl'
)
foreach ($pickerShader in $pickerShaders) {
    $pickerSource = Join-Path $projectRoot `
        "StandaloneShaderFixes\JiggleForge\shaders\$pickerShader"
    $pickerOutput = Join-Path $temporaryRoot "$pickerShader.cso"
    & $fxc `
        /nologo `
        /WX `
        /T vs_5_0 `
        /E main `
        /Fo $pickerOutput `
        $pickerSource
    if ($LASTEXITCODE -ne 0) {
        throw "FXC failed for runtime picker VS $pickerShader with exit code $LASTEXITCODE."
    }
}

$calibrationPixelSource = Join-Path $projectRoot `
    'StandaloneShaderFixes\JiggleForge\modules\capture_composite_uv_ps.hlsl'
$calibrationPixelOutput = Join-Path $temporaryRoot 'capture_composite_uv_ps.cso'
& $fxc `
    /nologo `
    /WX `
    /T ps_5_0 `
    /E main `
    /Fo $calibrationPixelOutput `
    $calibrationPixelSource
if ($LASTEXITCODE -ne 0) {
    throw "FXC failed for capture_composite_uv_ps.hlsl with exit code $LASTEXITCODE."
}

$roleCalibrationPixelSource = Join-Path $projectRoot `
    'StandaloneShaderFixes\JiggleForge\modules\capture_role_composite_uv_ps.hlsl'
$roleCalibrationPixelOutput = Join-Path $temporaryRoot `
    'capture_role_composite_uv_ps.cso'
& $fxc `
    /nologo `
    /WX `
    /T ps_5_0 `
    /E main `
    /Fo $roleCalibrationPixelOutput `
    $roleCalibrationPixelSource
if ($LASTEXITCODE -ne 0) {
    throw "FXC failed for capture_role_composite_uv_ps.hlsl with exit code $LASTEXITCODE."
}

$filteredRoleCalibrationPixelSource = Join-Path $projectRoot `
    'StandaloneShaderFixes\JiggleForge\modules\capture_filtered_role_composite_uv_ps.hlsl'
$filteredRoleCalibrationPixelOutput = Join-Path $temporaryRoot `
    'capture_filtered_role_composite_uv_ps.cso'
& $fxc `
    /nologo `
    /WX `
    /T ps_5_0 `
    /E main `
    /Fo $filteredRoleCalibrationPixelOutput `
    $filteredRoleCalibrationPixelSource
if ($LASTEXITCODE -ne 0) {
    throw "FXC failed for capture_filtered_role_composite_uv_ps.hlsl with exit code $LASTEXITCODE."
}

$roleComparisonSource = Join-Path $projectRoot `
    'StandaloneShaderFixes\JiggleForge\modules\compare_role_composite_cs.hlsl'
$roleComparisonOutput = Join-Path $temporaryRoot 'compare_role_composite_cs.cso'
& $fxc `
    /nologo `
    /WX `
    /T cs_5_0 `
    /E main `
    /Fo $roleComparisonOutput `
    $roleComparisonSource
if ($LASTEXITCODE -ne 0) {
    throw "FXC failed for compare_role_composite_cs.hlsl with exit code $LASTEXITCODE."
}

$roleCalibrationVertexSource = Join-Path $projectRoot `
    'StandaloneShaderFixes\JiggleForge\modules\capture_role_composite_raw_vs.hlsl'
$roleCalibrationVertexOutput = Join-Path $temporaryRoot `
    'capture_role_composite_raw_vs.cso'
& $fxc `
    /nologo `
    /WX `
    /T vs_5_0 `
    /E main `
    /Fo $roleCalibrationVertexOutput `
    $roleCalibrationVertexSource
if ($LASTEXITCODE -ne 0) {
    throw "FXC failed for capture_role_composite_raw_vs.hlsl with exit code $LASTEXITCODE."
}

$nativePickerSource = Join-Path $projectRoot `
    'StandaloneShaderFixes\JiggleForge\modules\native_world_pick_split.hlsl'
foreach ($stage in @(
    @{ Name = 'gs'; Target = 'gs_5_0'; Define = 'GEOMETRY_SHADER' },
    @{ Name = 'ps'; Target = 'ps_5_0'; Define = 'PIXEL_SHADER' }
)) {
    $nativePickerOutput = Join-Path $temporaryRoot `
        "native_world_pick_split-$($stage.Name).cso"
    & $fxc `
        /nologo `
        /WX `
        /D $stage.Define `
        /T $stage.Target `
        /E main `
        /Fo $nativePickerOutput `
        $nativePickerSource
    if ($LASTEXITCODE -ne 0) {
        throw "FXC failed for native_world_pick_split.hlsl $($stage.Name) with exit code $LASTEXITCODE."
    }
}

$nativeSource = Join-Path $projectRoot 'tests\native\GpuReadback.cpp'
$nativeRunner = Join-Path $temporaryRoot 'GpuReadback.exe'
$nativeObject = Join-Path $temporaryRoot 'GpuReadback.obj'
$compileTemplate = 'call "{0}" -arch=x64 -host_arch=x64 >nul && ' +
    'cl.exe /nologo /std:c++17 /EHsc /W4 /WX "{1}" ' +
    '/Fo:"{2}" /Fe:"{3}" /link d3d11.lib'
$compileCommand = $compileTemplate -f `
    $developerCommand, `
    $nativeSource, `
    $nativeObject, `
    $nativeRunner
& cmd.exe /d /c $compileCommand
if ($LASTEXITCODE -ne 0) {
    throw "The native GPU runner build failed with exit code $LASTEXITCODE."
}

$parityProject = Join-Path $projectRoot `
    'tests\JiggleForge.GpuParity\JiggleForge.GpuParity.csproj'
& dotnet run `
    --project $parityProject `
    --configuration Release `
    -- `
    $nativeRunner `
    $compiledShaders['MotionParity'] `
    $compiledShaders['InputControllerParity']
if ($LASTEXITCODE -ne 0) {
    throw "CPU/GPU parity validation failed with exit code $LASTEXITCODE."
}
