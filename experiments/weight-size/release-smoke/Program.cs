using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using JiggleForge.Core;

if (args.Length != 4) throw new ArgumentException("Expected: package-root zip-path output-root expected-version");
string package = Path.GetFullPath(args[0]), zipPath = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
string version = args[3];
Directory.CreateDirectory(output);
int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; }
string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
string Unix(string p) => p.Replace('\\', '/');
string Read(string path) => File.ReadAllText(path).Replace("\r", "");
string SizeBlock(string text) => Regex.Match(text, @"; JIGGLEFORGE_WEIGHT_SIZE_BEGIN[\s\S]*?; JIGGLEFORGE_WEIGHT_SIZE_END").Value;
Check(Hash(typeof(RuntimeEnvironmentService).Assembly.Location) == Hash(Path.Combine(package, "App", "JiggleForge.Core.dll")), "Smoke host did not load the exact packaged Core assembly");

// Check the app's own integrity implementation against all published files.
ApplicationUpdateService integrity = new(version, package);
var integrityResult = integrity.VerifyInstallation();
Check(integrityResult.IsValid, "Packaged Core integrity verification failed: " + JsonSerializer.Serialize(integrityResult));

// Compare every zip entry with the staged package, without extracting or running it.
HashSet<string> found = new(StringComparer.OrdinalIgnoreCase);
using (ZipArchive archive = ZipFile.OpenRead(zipPath))
{
    foreach (ZipArchiveEntry entry in archive.Entries)
    {
        string name = Unix(entry.FullName);
        if (name.EndsWith('/')) continue;
        string candidate = Path.GetFullPath(Path.Combine(package, name));
        Check(candidate.StartsWith(package.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Archive path escapes package");
        Check(found.Add(name), "Duplicate archive entry: " + name);
        Check(File.Exists(candidate), "Archive entry absent from staging: " + name);
        using Stream stream = entry.Open();
        string zipHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        Check(Hash(candidate) == zipHash, "Archive content differs: " + name);
    }
}
string[] staged = Directory.GetFiles(package, "*", SearchOption.AllDirectories).Select(f => Unix(Path.GetRelativePath(package, f))).ToArray();
Check(staged.Length == found.Count && staged.All(found.Contains), "Archive does not exactly represent staging");

// All installer mutations below are confined to a new GUID-named test directory.
// The real ZZMI root, app preferences and character files are never consulted.
string fixture = Path.Combine(output, "fixture-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixture);
string zzmi = Path.Combine(fixture, "ZZMI");
Directory.CreateDirectory(Path.Combine(zzmi, "Mods"));
Directory.CreateDirectory(Path.Combine(zzmi, "ShaderFixes"));
string marker = Path.Combine(zzmi, "Mods", "Unrelated.ini");
File.WriteAllText(marker, "; fixture Mod; must remain unchanged\n");
string markerHash = Hash(marker);
string payload = Path.Combine(package, "Runtime");
RuntimeEnvironmentService installer = new(payload);
Check(installer.SupportsBoneWeights, "The packaged app must recognize the bone-weight option");
string sourceIni = Read(Path.Combine(payload, "JiggleForge.ini"));
Check(SizeBlock(sourceIni).Length > 0, "WeightSize marker missing from packaged payload");
installer.Install(zzmi, RuntimeEnvironmentService.DefaultDragKeys, defaultPhysics: null, boneWeightEnabled: true);
RuntimeEnvironmentStatus status = installer.Inspect(zzmi);
Check(status.RuntimeCurrent && status.CurrentShaderCount == RuntimeEnvironmentService.RequiredShaderHashes.Count, "Clean install is not current");
Check(status.BoneWeightEnabled == true, "Bone weights not enabled after clean install");
string installedIni = Path.Combine(zzmi, "Mods", "JiggleForgeShaderFix", "JiggleForge.ini");
Check(SizeBlock(Read(installedIni)) == SizeBlock(sourceIni), "Installer changed frozen sizing options");
Check(Read(installedIni).Contains("global $defaultMaxOffset = 0.05"), "Default .05 did not survive installation");
int installedExactFiles = 0;
foreach (string source in Directory.GetFiles(payload, "*", SearchOption.AllDirectories))
{
    string relative = Unix(Path.GetRelativePath(payload, source));
    if (relative is "JiggleForge.ini" or "JiggleForge/WheelBridge.txt") continue;
    string target = relative.StartsWith("ShaderFixes/", StringComparison.Ordinal)
        ? Path.Combine(zzmi, relative)
        : Path.Combine(zzmi, "Mods", "JiggleForgeShaderFix", relative);
    Check(File.Exists(target) && Hash(target) == Hash(source), "Installer changed/missed runtime file: " + relative);
    installedExactFiles++;
}

// Exercise normal UI option writes and reinstall semantics using the shipped DLL.
installer.SetBoneWeightEnabled(zzmi, false);
Check(installer.Inspect(zzmi).BoneWeightEnabled == false && installer.Inspect(zzmi).RuntimeCurrent, "Bone-off option made runtime stale");
installer.Install(zzmi, new[] { "C" }, new PhysicsSettings { MaxOffset = .02 }, "VK_F9");
status = installer.Inspect(zzmi);
Check(status.RuntimeCurrent && status.BoneWeightEnabled == false, "Reinstall failed to preserve bone selection");
Check(Read(installedIni).Contains("global $defaultMaxOffset = 0.02"), "Saved lower physics cap was overwritten");
Check(SizeBlock(Read(installedIni)) == SizeBlock(sourceIni), "Custom physics/key writes changed sizing options");
Check(status.DragKeys?.SequenceEqual(new[] { "C" }) == true && status.RuntimeToggleKey == "VK_F9", "Custom inputs not applied");
installer.SetBoneWeightEnabled(zzmi, true);
Check(installer.Inspect(zzmi).RuntimeCurrent && installer.Inspect(zzmi).BoneWeightEnabled == true, "Bone-on write failed");
Check(Hash(marker) == markerHash, "Unrelated fixture Mod was modified");
var result = new
{
    pass = true, checks, archiveFiles = found.Count, installedExactFiles,
    packagedCore = typeof(RuntimeEnvironmentService).Assembly.GetName().Version?.ToString(),
    fixture = Path.GetRelativePath(output, fixture),
    installedIntoRealGame = false, gameTested = false,
    integrityVerifiedByPackagedCore = true,
    boneToggleAndCustomSettingsPreserved = true
};
File.WriteAllText(Path.Combine(output, "installer-smoke.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(result));
