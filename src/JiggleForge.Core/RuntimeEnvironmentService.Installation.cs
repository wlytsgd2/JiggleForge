using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class RuntimeEnvironmentService
{
    public void Install(
        string zzmiRoot,
        string dragKey = DefaultDragKey,
        PhysicsSettings? defaultPhysics = null,
        string runtimeToggleKey = DefaultRuntimeToggleKey)
    {
        Install(zzmiRoot, [dragKey], defaultPhysics, runtimeToggleKey);
    }

    public void Install(
        string zzmiRoot,
        IReadOnlyCollection<string> dragKeys,
        PhysicsSettings? defaultPhysics = null,
        string runtimeToggleKey = DefaultRuntimeToggleKey)
    {
        IReadOnlyList<string> validatedDragKeys = ValidateDragKeys(dragKeys);
        string validatedRuntimeToggleKey = ValidateRuntimeToggleKey(runtimeToggleKey);
        string root = NormalizeRoot(zzmiRoot);
        RuntimeEnvironmentStatus status = Inspect(root);
        if (!status.PayloadAvailable)
        {
            throw new DirectoryNotFoundException($"Runtime payload was not found: {payloadRoot}");
        }
        if (!status.ModsDirectoryExists || !status.ShaderFixesDirectoryExists)
        {
            throw new DirectoryNotFoundException("The selected folder must contain both Mods and ShaderFixes directories.");
        }

        string shaderFixesRoot = Path.Combine(root, "ShaderFixes");
        RemoveCompatibilityLayer(root);
        foreach (string hash in RequiredShaderHashes)
        {
            string source = PayloadShaderPath(hash);
            string target = InstalledShaderPath(shaderFixesRoot, hash);
            string backup = target + BackupSuffix;
            if (File.Exists(target) && !FilesEqual(source, target) && !IsJiggleForgeReplacement(target))
            {
                if (File.Exists(backup))
                {
                    throw new InvalidOperationException(
                        $"{Path.GetFileName(target)} conflicts with an existing ShaderFix and already has a JiggleForge backup. " +
                        "Resolve the file manually before updating.");
                }

                File.Move(target, backup);
            }
        }
        RestoreObsoleteManagedShaders(shaderFixesRoot);

        string targetRoot = Path.Combine(root, "Mods", RuntimeFolderName);
        if (Directory.Exists(targetRoot))
        {
            Directory.Delete(targetRoot, recursive: true);
        }
        Directory.CreateDirectory(targetRoot);
        string runtimeIniTarget = Path.Combine(targetRoot, "JiggleForge.ini");
        File.Copy(Path.Combine(payloadRoot, "JiggleForge.ini"), runtimeIniTarget, overwrite: true);
        CopyTree(Path.Combine(payloadRoot, "JiggleForge"), Path.Combine(targetRoot, "JiggleForge"));
        WriteDragKeys(runtimeIniTarget, validatedDragKeys);
        WriteRuntimeToggleKey(runtimeIniTarget, validatedRuntimeToggleKey);
        if (defaultPhysics is not null)
        {
            WriteDefaultPhysics(runtimeIniTarget, defaultPhysics);
        }
        WriteWheelBridgeDragKeys(
            Path.Combine(targetRoot, "JiggleForge", WheelBridgeConfigName),
            validatedDragKeys);

        foreach (string hash in RequiredShaderHashes)
        {
            File.Copy(PayloadShaderPath(hash), InstalledShaderPath(shaderFixesRoot, hash), overwrite: true);
        }

        string shaderIncludeTarget = Path.Combine(shaderFixesRoot, ShaderIncludeFolderName);
        if (Directory.Exists(shaderIncludeTarget))
        {
            Directory.Delete(shaderIncludeTarget, recursive: true);
        }
        string legacyShaderIncludeTarget = Path.Combine(
            shaderFixesRoot,
            RetiredShaderIncludeFolderName);
        if (Directory.Exists(legacyShaderIncludeTarget))
        {
            Directory.Delete(legacyShaderIncludeTarget, recursive: true);
        }
        CopyTree(PayloadShaderIncludeRoot(), shaderIncludeTarget);
    }

    public void Uninstall(string zzmiRoot, bool stopWheelBridge = true)
    {
        string root = NormalizeRoot(zzmiRoot);
        if (stopWheelBridge)
        {
            StopWheelBridge(requestElevation: true);
        }

        string targetRoot = Path.Combine(root, "Mods", RuntimeFolderName);
        if (Directory.Exists(targetRoot))
        {
            Directory.Delete(targetRoot, recursive: true);
        }

        string shaderFixesRoot = Path.Combine(root, "ShaderFixes");
        foreach (string hash in RequiredShaderHashes.Concat(ObsoleteManagedShaderHashes))
        {
            string target = InstalledShaderPath(shaderFixesRoot, hash);
            string backup = target + BackupSuffix;
            if (File.Exists(target) && IsJiggleForgeReplacement(target))
            {
                File.Delete(target);
            }

            if (File.Exists(backup) && !File.Exists(target))
            {
                File.Move(backup, target);
            }
        }

        string shaderIncludeTarget = Path.Combine(shaderFixesRoot, ShaderIncludeFolderName);
        if (Directory.Exists(shaderIncludeTarget))
        {
            Directory.Delete(shaderIncludeTarget, recursive: true);
        }
        string legacyShaderIncludeTarget = Path.Combine(
            shaderFixesRoot,
            RetiredShaderIncludeFolderName);
        if (Directory.Exists(legacyShaderIncludeTarget))
        {
            Directory.Delete(legacyShaderIncludeTarget, recursive: true);
        }

        string legacyIni = Path.Combine(shaderFixesRoot, "JiggleForge.ini");
        string legacyFolder = Path.Combine(shaderFixesRoot, "JiggleForge");
        if (File.Exists(legacyIni))
        {
            File.Delete(legacyIni);
        }
        if (Directory.Exists(legacyFolder))
        {
            Directory.Delete(legacyFolder, recursive: true);
        }

        RemoveCompatibilityLayer(root);
    }

    public void UninstallKeepingCompatibility(string zzmiRoot, bool stopWheelBridge = true)
    {
        string root = NormalizeRoot(zzmiRoot);
        Uninstall(root, stopWheelBridge);

        string compatibilityRoot = Path.Combine(root, "Mods", CompatibilityFolderName);
        Directory.CreateDirectory(compatibilityRoot);
        File.WriteAllText(
            Path.Combine(compatibilityRoot, CompatibilityIniName),
            CompatibilityLayerContents.ReplaceLineEndings("\r\n") + "\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void RemoveCompatibilityLayer(string root)
    {
        string compatibilityRoot = Path.Combine(root, "Mods", CompatibilityFolderName);
        string compatibilityIni = Path.Combine(compatibilityRoot, CompatibilityIniName);
        if (!File.Exists(compatibilityIni))
        {
            return;
        }

        string contents = File.ReadAllText(compatibilityIni);
        if (!contents.Contains(CompatibilityMarker, StringComparison.Ordinal))
        {
            return;
        }

        Directory.Delete(compatibilityRoot, recursive: true);
    }

    private static void RestoreObsoleteManagedShaders(string shaderFixesRoot)
    {
        foreach (string hash in ObsoleteManagedShaderHashes)
        {
            string target = InstalledShaderPath(shaderFixesRoot, hash);
            string backup = target + BackupSuffix;
            if (File.Exists(target) && IsJiggleForgeReplacement(target))
            {
                File.Delete(target);
            }

            if (File.Exists(backup) && !File.Exists(target))
            {
                File.Move(backup, target);
            }
        }
    }

    private string PayloadShaderPath(string hash) =>
        Path.Combine(payloadRoot, "ShaderFixes", hash + ReplacementSuffix);

    private string PayloadShaderIncludeRoot() =>
        Path.Combine(payloadRoot, "ShaderFixes", ShaderIncludeFolderName);

    private static string InstalledShaderPath(string shaderFixesRoot, string hash) =>
        Path.Combine(shaderFixesRoot, hash + ReplacementSuffix);

    private static bool IsJiggleForgeReplacement(string path)
    {
        try
        {
            string contents = File.ReadAllText(path);
            return contents.Contains(
                       "JiggleForgeState",
                       StringComparison.Ordinal)
                   || ((contents.Contains(
                            "JF_MotionState",
                            StringComparison.Ordinal)
                        || contents.Contains(
                            "JF2_MotionState",
                            StringComparison.Ordinal))
                       && contents.Contains(
                           "JiggleForgeDirectStateIndex",
                           StringComparison.Ordinal));
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void CopyTree(string sourceRoot, string targetRoot)
    {
        foreach (string directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetRoot, Path.GetRelativePath(sourceRoot, directory)));
        }
        Directory.CreateDirectory(targetRoot);
        foreach (string source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(targetRoot, Path.GetRelativePath(sourceRoot, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }

}
