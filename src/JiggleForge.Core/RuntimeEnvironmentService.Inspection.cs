using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JiggleForge.Core;

public sealed partial class RuntimeEnvironmentService
{
    public RuntimeEnvironmentStatus Inspect(string zzmiRoot)
    {
        string root = NormalizeRoot(zzmiRoot);
        string modsRoot = Path.Combine(root, "Mods");
        string shaderFixesRoot = Path.Combine(root, "ShaderFixes");
        string runtimeTarget = Path.Combine(modsRoot, RuntimeFolderName);
        string runtimeSource = Path.Combine(payloadRoot, "JiggleForge");
        string runtimeIniSource = Path.Combine(payloadRoot, "JiggleForge.ini");
        string runtimeIniTarget = Path.Combine(runtimeTarget, "JiggleForge.ini");
        string runtimeFolderTarget = Path.Combine(runtimeTarget, "JiggleForge");
        string shaderIncludeSource = PayloadShaderIncludeRoot();
        string shaderIncludeTarget = Path.Combine(shaderFixesRoot, ShaderIncludeFolderName);
        IReadOnlyList<string>? dragKeys = ReadDragKeys(runtimeIniTarget);
        string? runtimeToggleKey = ReadRuntimeToggleKey(runtimeIniTarget);

        bool payloadAvailable = File.Exists(runtimeIniSource) &&
                                Directory.Exists(runtimeSource) &&
                                Directory.Exists(shaderIncludeSource) &&
                                RequiredShaderHashes.All(hash => File.Exists(PayloadShaderPath(hash)));
        bool runtimePresent = File.Exists(runtimeIniTarget) || Directory.Exists(runtimeFolderTarget);
        bool runtimeInstalled = File.Exists(runtimeIniTarget) && Directory.Exists(runtimeFolderTarget);
        bool runtimeCurrent = runtimeInstalled && dragKeys is not null && runtimeToggleKey is not null && payloadAvailable &&
                              RuntimeIniMatches(runtimeIniSource, runtimeIniTarget) &&
                              SourceTreeMatches(runtimeSource, runtimeFolderTarget) &&
                              SourceTreeMatches(shaderIncludeSource, shaderIncludeTarget);

        int installedShaders = 0;
        int currentShaders = 0;
        int backups = 0;
        foreach (string hash in RequiredShaderHashes)
        {
            string installed = InstalledShaderPath(shaderFixesRoot, hash);
            if (File.Exists(installed))
            {
                installedShaders++;
                if (payloadAvailable && FilesEqual(PayloadShaderPath(hash), installed))
                {
                    currentShaders++;
                }
            }

            if (File.Exists(installed + BackupSuffix))
            {
                backups++;
            }
        }

        return new RuntimeEnvironmentStatus(
            root,
            payloadAvailable,
            Directory.Exists(root),
            Directory.Exists(modsRoot),
            Directory.Exists(shaderFixesRoot),
            runtimePresent,
            runtimeInstalled,
            runtimeCurrent,
            installedShaders,
            currentShaders,
            RequiredShaderHashes.Count,
            backups,
            IsWheelBridgeRunning(),
            dragKeys,
            runtimeToggleKey);
    }

    private static bool RuntimeIniMatches(string source, string target)
    {
        if (!File.Exists(source) || !File.Exists(target))
        {
            return false;
        }

        string sourceText = MarkedDragKeyRegex().Replace(
            File.ReadAllText(source),
            BuildDragKeyBlock(DefaultDragKeys),
            count: 1);
        string targetText = MarkedDragKeyRegex().Replace(
            File.ReadAllText(target),
            BuildDragKeyBlock(DefaultDragKeys),
            count: 1);
        sourceText = MarkedRuntimeToggleKeyRegex().Replace(
            sourceText,
            BuildRuntimeToggleKeyBlock(DefaultRuntimeToggleKey),
            count: 1);
        targetText = MarkedRuntimeToggleKeyRegex().Replace(
            targetText,
            BuildRuntimeToggleKeyBlock(DefaultRuntimeToggleKey),
            count: 1);
        sourceText = MarkedDefaultPhysicsRegex().Replace(
            sourceText,
            BuildDefaultPhysicsBlock(new PhysicsSettings()),
            count: 1);
        targetText = MarkedDefaultPhysicsRegex().Replace(
            targetText,
            BuildDefaultPhysicsBlock(new PhysicsSettings()),
            count: 1);
        sourceText = ReplaceDefaultPhysicsResource(
            sourceText,
            new PhysicsSettings(),
            requireResource: false);
        targetText = ReplaceDefaultPhysicsResource(
            targetText,
            new PhysicsSettings(),
            requireResource: false);
        return string.Equals(sourceText, targetText, StringComparison.Ordinal);
    }

    private static bool SourceTreeMatches(string sourceRoot, string targetRoot)
    {
        foreach (string source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceRoot, source);
            string target = Path.Combine(targetRoot, relative);
            bool matches = string.Equals(relative, WheelBridgeConfigName, StringComparison.OrdinalIgnoreCase)
                ? WheelBridgeConfigMatches(source, target)
                : FilesEqual(source, target);
            if (!matches)
            {
                return false;
            }
        }
        return true;
    }

    private static bool WheelBridgeConfigMatches(string source, string target)
    {
        if (!File.Exists(source) || !File.Exists(target))
        {
            return false;
        }

        string sourceText = WheelBridgeDragKeysRegex().Replace(File.ReadAllText(source), string.Empty).TrimEnd();
        string targetText = WheelBridgeDragKeysRegex().Replace(File.ReadAllText(target), string.Empty).TrimEnd();
        return string.Equals(sourceText, targetText, StringComparison.Ordinal);
    }

    private static bool FilesEqual(string first, string second)
    {
        if (!File.Exists(first) || !File.Exists(second))
        {
            return false;
        }

        FileInfo firstInfo = new(first);
        FileInfo secondInfo = new(second);
        if (firstInfo.Length != secondInfo.Length)
        {
            return false;
        }

        const int bufferSize = 81920;
        byte[] firstBuffer = new byte[bufferSize];
        byte[] secondBuffer = new byte[bufferSize];
        using FileStream firstStream = File.OpenRead(first);
        using FileStream secondStream = File.OpenRead(second);
        while (true)
        {
            int firstRead = firstStream.Read(firstBuffer, 0, firstBuffer.Length);
            int secondRead = secondStream.Read(secondBuffer, 0, secondBuffer.Length);
            if (firstRead != secondRead)
            {
                return false;
            }
            if (firstRead == 0)
            {
                return true;
            }
            if (!firstBuffer.AsSpan(0, firstRead).SequenceEqual(secondBuffer.AsSpan(0, secondRead)))
            {
                return false;
            }
        }
    }

}
