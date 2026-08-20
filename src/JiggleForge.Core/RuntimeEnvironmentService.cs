using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class RuntimeEnvironmentService
{
    public const string DefaultDragKey = "VK_LBUTTON";
    public static readonly IReadOnlyList<string> DefaultDragKeys =
    [
        "VK_LBUTTON",
        "X",
    ];
    public const string DefaultRuntimeToggleKey = "VK_F7";
    public static readonly IReadOnlyList<string> SupportedDragKeys =
    [
        "VK_LBUTTON",
        "VK_RBUTTON",
        "VK_MBUTTON",
        "VK_XBUTTON1",
        "VK_XBUTTON2",
        "X",
        "C",
        "V",
    ];

    public static readonly IReadOnlyList<string> SupportedRuntimeToggleKeys =
    [
        "VK_F5",
        "VK_F7",
        "VK_F9",
        "VK_F11",
        "VK_F12",
        "VK_INSERT",
        "VK_HOME",
        "VK_END",
        "VK_PAUSE",
    ];

    public static readonly IReadOnlyList<string> RequiredShaderHashes =
    [
        "26214fb5eedfcbdd",
        "c280f6945b23a42a",
        "6883e4375b728e90",
        "aa59281029db3a5a",
        "1f6ab42231416fdb",
        "699981e2a62dd9b4",
        "402766e2987d7821",
        "a0b37a7c7c2a1905",
        "160b58ea1824c794",
        "ad24b1c214866fd7",
        "d0a1a756bd3bde31",
    ];

    private static readonly IReadOnlyList<string> ObsoleteManagedShaderHashes =
    [
        "1b6d08acd285344c",
        "a6030ebb8c49cf02",
    ];

    private const string RuntimeFolderName = "JiggleForgeShaderFix";
    private const string CompatibilityFolderName = "JiggleForgeCompatibility";
    private const string CompatibilityIniName = "JiggleForgeCompatibility.ini";
    private const string CompatibilityMarker = "; JIGGLEFORGE_COMPATIBILITY_LAYER";
    private const string ShaderIncludeFolderName = "JiggleForgeRuntime";
    private const string RetiredShaderIncludeFolderName = "JiggleForgeV2";
    private const string ReplacementSuffix = "-vs_replace.txt";
    private const string BackupSuffix = ".pre_jiggleForge_backup";
    private const string DragKeyBeginMarker = "; JIGGLEFORGE_DRAG_KEY_BEGIN";
    private const string DragKeyEndMarker = "; JIGGLEFORGE_DRAG_KEY_END";
    private const string RuntimeToggleKeyBeginMarker = "; JIGGLEFORGE_RUNTIME_TOGGLE_KEY_BEGIN";
    private const string RuntimeToggleKeyEndMarker = "; JIGGLEFORGE_RUNTIME_TOGGLE_KEY_END";
    private const string DefaultPhysicsBeginMarker = "; JIGGLEFORGE_DEFAULT_PHYSICS_BEGIN";
    private const string DefaultPhysicsEndMarker = "; JIGGLEFORGE_DEFAULT_PHYSICS_END";
    private const string WheelBridgeConfigName = "WheelBridge.txt";
    private readonly string payloadRoot;

    internal static string CompatibilityLayerContents => $$"""
        {{CompatibilityMarker}}
        ; Keeps Mods adapted by JiggleForge loadable after the deformation runtime is removed.
        ; This file performs no draw, dispatch, copy, clear, or per-frame command.
        namespace = jiggle_forge

        [Constants]
        global $activePickPipeline = 0
        global $pickObjectID = 1
        global $pickSourceDraw = 0
        global $pickRangeAuto = 0
        global $pickRangeCount = 0
        global $pickRangeFirst = 0
        global $pickRangeBase = 0
        global $mouseDown = 0

        [CommandListEnableAdaptedOnly]
        ; Intentionally empty.

        [CommandListPickVisibleRange]
        ; Intentionally empty.

        [CommandListRegisterGroupParameters]
        ; Intentionally empty.

        [ResourceCapturedPick]
        type = Buffer
        format = R32G32B32A32_FLOAT
        array = 7
        data = 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0

        [ResourceMotionStates]
        type = Buffer
        format = R32G32B32A32_FLOAT
        array = 1
        data = 0 0 0 0

        [ResourceGroupParameters]
        type = Buffer
        format = R32G32B32A32_FLOAT
        array = 5
        data = 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0
        """;

    public RuntimeEnvironmentService(string payloadRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadRoot);
        this.payloadRoot = Path.GetFullPath(payloadRoot);
    }

    public static string DefaultZzmiRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XXMI Launcher",
        "ZZMI");

    private static string NormalizeRoot(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return Path.GetFullPath(root.Trim().Trim('"'));
    }

}

public sealed record RuntimeEnvironmentStatus(
    string ZzmiRoot,
    bool PayloadAvailable,
    bool ZzmiRootExists,
    bool ModsDirectoryExists,
    bool ShaderFixesDirectoryExists,
    bool RuntimePresent,
    bool RuntimeInstalled,
    bool RuntimeCurrent,
    int InstalledShaderCount,
    int CurrentShaderCount,
    int RequiredShaderCount,
    int BackupCount,
    bool WheelBridgeRunning,
    IReadOnlyList<string>? DragKeys,
    string? RuntimeToggleKey)
{
    public bool Ready => PayloadAvailable && RuntimeCurrent && CurrentShaderCount == RequiredShaderCount;

    public string? DragKey => DragKeys?.FirstOrDefault();
}
