using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using SevenDaysBeyond;

try
{
    return Run(args);
}
catch (Exception error)
{
    Console.Error.WriteLine("Dependency check failed: " + error.Message);
    return 1;
}

static int Run(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    bool referenceOnly = false;
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--reference-only") { referenceOnly = true; continue; }
        if (!new[] { "--upstream", "--managed", "--harmony", "--output" }.Contains(args[i]) || i + 1 == args.Length)
            throw new ArgumentException("Use --upstream DLL [--managed DIRECTORY --harmony DIRECTORY] --output JSON [--reference-only].");
        options.Add(args[i], args[++i]);
    }
    if (!options.TryGetValue("--upstream", out string? upstreamPath) || !options.TryGetValue("--output", out string? output))
        throw new ArgumentException("--upstream and --output are required.");

    AssemblyInfo upstream = ReadAssembly(upstreamPath);
    bool pinned = upstream.Name == MultiWorldContract.AssemblyName && upstream.Sha256 == MultiWorldContract.Sha256;
    var hooks = CheckHooks(upstreamPath);
    var candidates = new Dictionary<string, List<AssemblyInfo>>(StringComparer.OrdinalIgnoreCase);
    var skipped = new List<string>();
    var inputErrors = new List<string>();
    foreach (string key in new[] { "--managed", "--harmony" })
    {
        if (!options.TryGetValue(key, out string? dir)) continue;
        if (!Directory.Exists(dir)) { inputErrors.Add("Directory does not exist: " + dir); continue; }
        foreach (string file in Directory.EnumerateFiles(dir, "*.dll", SearchOption.AllDirectories).Order())
        {
            AssemblyInfo info;
            try { info = ReadAssembly(file); }
            catch (BadImageFormatException) { skipped.Add(Path.GetFullPath(file)); continue; }
            if (!candidates.TryGetValue(info.Name, out var list)) candidates[info.Name] = list = new();
            if (!list.Any(a => a.Sha256 == info.Sha256)) list.Add(info);
        }
    }
    var dependencies = upstream.References.Select(reference =>
    {
        candidates.TryGetValue(reference.Name, out var available);
        string status = available == null ? "missing" : available.Count != 1 ? "ambiguous"
            : available[0].Version != reference.Version ? "version-mismatch" : "present";
        return new { reference.Name, ExpectedVersion = reference.Version, Status = status, Candidates = available ?? new() };
    }).ToArray();
    bool contractOk = pinned && hooks.All(h => h.Matches == 1);
    bool dependenciesOk = inputErrors.Count == 0 && dependencies.All(d => d.Status == "present");
    // Metadata versions such as 0.0.0.0 cannot establish the game release/build.
    var report = new
    {
        SchemaVersion = 1, Upstream = upstream, UpstreamPinned = pinned, Hooks = hooks,
        ContractOk = contractOk, DependenciesOk = dependenciesOk, GameVersionVerified = false,
        GameBuildTested = false, GameTested = false, Dependencies = dependencies,
        InputErrors = inputErrors, SkippedNonManagedFiles = skipped,
        CompilerReferences = dependencies.Where(d => d.Status == "present").Select(d => d.Candidates[0].Path).ToArray()
    };
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
    File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    Console.WriteLine($"Upstream contract: {(contractOk ? "PASS" : "FAIL")} ({hooks.Count(h => h.Matches == 1)}/{hooks.Length} hooks)");
    Console.WriteLine($"Dependencies: {dependencies.Count(d => d.Status == "present")}/{dependencies.Length}; report: {output}");
    foreach (var dependency in dependencies.Where(d => d.Status != "present"))
        Console.WriteLine($"  {dependency.Name}: {dependency.Status} (expected {dependency.ExpectedVersion})");
    return contractOk && (referenceOnly || dependenciesOk) ? 0 : 2;
}

static HookResult[] CheckHooks(string path)
{
    using var file = File.OpenRead(path);
    using var pe = new PEReader(file);
    var reader = pe.GetMetadataReader();
    var names = new SignatureNames();
    var methods = reader.TypeDefinitions
        .Where(h => names.GetTypeFromDefinition(reader, h, 0) == MultiWorldContract.TypeName)
        .SelectMany(h => reader.GetTypeDefinition(h).GetMethods())
        .Select(h => reader.GetMethodDefinition(h)).ToArray();
    return MultiWorldContract.Hooks.Select(hook => new HookResult(hook.Name, hook.ReturnType, hook.Parameters,
        methods.Count(method =>
        {
            if (reader.GetString(method.Name) != hook.Name || (method.Attributes & MethodAttributes.Static) == 0) return false;
            var signature = method.DecodeSignature(names, (object?)null);
            return signature.ReturnType == hook.ReturnType && signature.ParameterTypes.SequenceEqual(hook.Parameters);
        }))).ToArray();
}

static AssemblyInfo ReadAssembly(string path)
{
    path = Path.GetFullPath(path);
    using var file = File.OpenRead(path);
    string hash = Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
    file.Position = 0;
    using var pe = new PEReader(file);
    if (!pe.HasMetadata) throw new BadImageFormatException("No managed metadata: " + path);
    var reader = pe.GetMetadataReader();
    if (!reader.IsAssembly) throw new BadImageFormatException("Not an assembly: " + path);
    var definition = reader.GetAssemblyDefinition();
    return new AssemblyInfo(path, reader.GetString(definition.Name), definition.Version.ToString(),
        reader.GetGuid(reader.GetModuleDefinition().Mvid).ToString(), hash,
        reader.AssemblyReferences.Select(h => reader.GetAssemblyReference(h))
            .Select(r => new ReferenceInfo(reader.GetString(r.Name), r.Version.ToString())).ToArray());
}

internal record ReferenceInfo(string Name, string Version);
internal record AssemblyInfo(string Path, string Name, string Version, string Mvid, string Sha256, ReferenceInfo[] References);
internal record HookResult(string Name, string ReturnType, string[] Parameters, int Matches);
