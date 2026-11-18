using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

// Solution-wide semantic indexer. Built and run by Update-Index.ps1.
// Usage: Indexer.exe <workspaceRoot> <indexDir> <evalDir> <outDir> <update|verify> [projectIdsCsv]
namespace AgentIndex
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                return new IndexRun(args).Execute();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }

    internal sealed class ProjectSpec
    {
        public string Id;
        public string RelativePath;
        public string FullPath;
        public string Tf;
        public string[] ShadowTfs = new string[0];
        public string[] StateTypes = new string[] { "Globals" };
        public bool AllowBindingErrors;
        public string AllowReason;
        public bool IsSdk;
        public bool FilesOnly;
        public readonly List<ProjectBuild> Builds = new List<ProjectBuild>();
        public ProjectBuild Primary { get { return Builds[0]; } }
    }

    internal sealed class ProjectBuild
    {
        public ProjectSpec Spec;
        public bool Shadow;
        public string TargetFramework;
        public string IntermediateOutputPath;
        public string AssemblyName;
        public string OutputType;
        public string[] Defines;
        public bool AllowUnsafe;
        public Dictionary<string, Dictionary<string, object>> CompileItems;
        public List<string> Analyzers = new List<string>();
        public List<string> InternalsVisibleTo = new List<string>();
        public List<string> ExternalReferences = new List<string>();
        public List<string> MissingReferences = new List<string>();
        public List<ProjectSpec> ProjectReferences = new List<ProjectSpec>();
        public List<string> SkippedProjectReferences = new List<string>();
        public List<Dictionary<string, object>> Resources = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> Pages = new List<Dictionary<string, object>>();
        public SyntaxTree[] Trees;
        public SyntaxTree[] GeneratedTrees = new SyntaxTree[0];
        public CSharpCompilation Compilation;
        public Diagnostic[] Errors = new Diagnostic[0];
        public bool IsFramework { get { return TargetFramework != null && (TargetFramework.StartsWith("net4", StringComparison.OrdinalIgnoreCase) || TargetFramework.StartsWith("v4", StringComparison.OrdinalIgnoreCase)); } }
    }

    internal sealed class IndexRun
    {
        private readonly string _workspace;
        private readonly string _indexDir;
        private readonly string _evalDir;
        private readonly string _outDir;
        private readonly bool _verify;
        private readonly HashSet<string> _only;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
        private readonly List<ProjectSpec> _specs = new List<ProjectSpec>();
        private readonly Dictionary<string, PortableExecutableReference> _referenceCache = new Dictionary<string, PortableExecutableReference>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ISourceGenerator[]> _generatorCache = new Dictionary<string, ISourceGenerator[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _assemblyToProject = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _symbols = new List<string>();
        private readonly List<string> _files = new List<string>();
        private readonly List<string> _edges = new List<string>();
        private readonly List<string> _states = new List<string>();
        private readonly List<object> _callerLocationUses = new List<object>();
        private readonly Dictionary<string, HashSet<string>> _declared = new Dictionary<string, HashSet<string>>();
        private readonly SortedDictionary<string, List<object>> _formatHits = new SortedDictionary<string, List<object>>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, string> _inputHashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, int>> _stats = new Dictionary<string, Dictionary<string, int>>();
        private Dictionary<string, object> _annotations;

        public IndexRun(string[] args)
        {
            if (args.Length < 5) throw new ArgumentException("Usage: Indexer <workspaceRoot> <indexDir> <evalDir> <outDir> <update|verify> [projectIds]");
            _workspace = Path.GetFullPath(args[0]);
            if (!_workspace.EndsWith("\\")) _workspace += "\\";
            _indexDir = Path.GetFullPath(args[1]);
            _evalDir = Path.GetFullPath(args[2]);
            _outDir = Path.GetFullPath(args[3]);
            _verify = args[4] == "verify";
            _only = args.Length > 5 && !string.IsNullOrWhiteSpace(args[5]) ? new HashSet<string>(args[5].Split(','), StringComparer.OrdinalIgnoreCase) : null;
        }

        public int Execute()
        {
            LoadSpecs();
            foreach (ProjectSpec spec in _specs) LoadEvaluation(spec);
            ParseTrees();
            BuildCompilations();
            CheckDiagnostics();
            _annotations = Obj(_json.DeserializeObject(File.ReadAllText(Path.Combine(_indexDir, "annotations.json"))));
            foreach (ProjectSpec spec in _specs)
            {
                Console.WriteLine("Indexing " + spec.Id + " ...");
                EmitProject(spec.Primary);
            }
            HashSharedInputs();
            WriteOutputs();
            return 0;
        }

        // ---------- loading ----------

        private static Dictionary<string, object> Obj(object value) { return (Dictionary<string, object>)value; }

        private static string Str(Dictionary<string, object> map, string key)
        {
            object v;
            return map != null && map.TryGetValue(key, out v) && v != null ? v.ToString() : string.Empty;
        }

        private string Relative(string path)
        {
            string full = Path.GetFullPath(path);
            return full.Substring(_workspace.Length);
        }

        private void LoadSpecs()
        {
            var root = Obj(_json.DeserializeObject(File.ReadAllText(Path.Combine(_indexDir, "projects.json"))));
            foreach (object entry in (object[])root["projects"])
            {
                var map = Obj(entry);
                var spec = new ProjectSpec { Id = Str(map, "id"), RelativePath = Str(map, "path"), Tf = Str(map, "tf") };
                spec.FullPath = Path.Combine(_workspace, spec.RelativePath);
                if (!File.Exists(spec.FullPath)) throw new Exception("Project file not found for '" + spec.Id + "': " + spec.FullPath + ". Is the sibling checkout present under " + _workspace + "?");
                spec.IsSdk = !spec.RelativePath.EndsWith(".wixproj", StringComparison.OrdinalIgnoreCase) && File.ReadAllText(spec.FullPath).Contains("<Project Sdk=");
                if (map.ContainsKey("filesOnly")) spec.FilesOnly = Convert.ToBoolean(map["filesOnly"]);
                if (map.ContainsKey("shadowTfs")) spec.ShadowTfs = ((object[])map["shadowTfs"]).Cast<string>().ToArray();
                if (map.ContainsKey("stateTypes")) spec.StateTypes = ((object[])map["stateTypes"]).Cast<string>().ToArray();
                if (map.ContainsKey("allowBindingErrors"))
                {
                    spec.AllowBindingErrors = true;
                    spec.AllowReason = Str(map, "allowBindingErrors");
                    if (string.IsNullOrWhiteSpace(spec.AllowReason)) throw new Exception("allowBindingErrors for '" + spec.Id + "' requires a reason string.");
                }
                _specs.Add(spec);
            }
            if (_specs.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _specs.Count) throw new Exception("Duplicate project ids in projects.json.");
        }

        private ProjectBuild ReadBuild(ProjectSpec spec, string evalFile, bool shadow)
        {
            if (!File.Exists(evalFile)) throw new Exception("Missing evaluation for " + spec.Id + ": " + evalFile + ". Run Update-Index.ps1.");
            var evaluated = Obj(_json.DeserializeObject(File.ReadAllText(evalFile)));
            var items = evaluated.ContainsKey("Items") ? Obj(evaluated["Items"]) : new Dictionary<string, object>();
            var props = Obj(evaluated["Properties"]);
            var build = new ProjectBuild { Spec = spec, Shadow = shadow };
            build.TargetFramework = Str(props, "TargetFramework");
            if (build.TargetFramework.Length == 0) build.TargetFramework = Str(props, "TargetFrameworkVersion");
            build.IntermediateOutputPath = Str(props, "IntermediateOutputPath");
            build.AssemblyName = Str(props, "AssemblyName");
            build.OutputType = Str(props, "OutputType");
            build.AllowUnsafe = string.Equals(Str(props, "AllowUnsafeBlocks"), "true", StringComparison.OrdinalIgnoreCase);
            build.Defines = Str(props, "DefineConstants").Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            build.CompileItems = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in ListItems(items, "Compile")) build.CompileItems[Str(item, "FullPath")] = item;
            if (spec.IsSdk && !string.IsNullOrEmpty(build.TargetFramework))
            {
                string projDir = Path.GetDirectoryName(spec.FullPath);
                string objDir = !string.IsNullOrEmpty(build.IntermediateOutputPath)
                    ? (Path.IsPathRooted(build.IntermediateOutputPath) ? build.IntermediateOutputPath : Path.Combine(projDir, build.IntermediateOutputPath))
                    : Path.Combine(projDir, "obj", "Debug", build.TargetFramework);
                if (Directory.Exists(objDir))
                {
                    string genAssembly = Path.Combine(objDir, spec.Id + ".AssemblyInfo.cs");
                    if (File.Exists(genAssembly) && !build.CompileItems.ContainsKey(genAssembly))
                    {
                        build.CompileItems[genAssembly] = new Dictionary<string, object> { { "FullPath", genAssembly } };
                    }
                    foreach (string giFile in Directory.GetFiles(objDir, "*.g.i.cs", SearchOption.AllDirectories))
                    {
                        string fullGi = Path.GetFullPath(giFile);
                        if (!build.CompileItems.ContainsKey(fullGi))
                        {
                            build.CompileItems[fullGi] = new Dictionary<string, object> { { "FullPath", fullGi } };
                        }
                    }
                }
            }
            foreach (var item in ListItems(items, "Analyzer"))
            {
                string path = Str(item, "FullPath");
                if (File.Exists(path) && path.IndexOf("SourceGenerators", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    build.Analyzers.Add(path);
                }
            }
            foreach (var item in ListItems(items, "InternalsVisibleTo"))
            {
                string name = Str(item, "Identity");
                if (!string.IsNullOrWhiteSpace(name)) build.InternalsVisibleTo.Add(name);
            }
            var solutionAssemblies = new HashSet<string>(_specs.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var item in ListItems(items, "ReferencePath"))
            {
                string path = Str(item, "FullPath");
                string name = Path.GetFileNameWithoutExtension(path);
                if (Str(item, "ReferenceSourceTarget") == "ResolveProjectReferences" || item.ContainsKey("ProjectReferenceOriginalItemSpec")) continue;
                if (_specs.Any(s => string.Equals(s.Id, name, StringComparison.OrdinalIgnoreCase))) continue;
                if (File.Exists(path)) build.ExternalReferences.Add(path); else build.MissingReferences.Add(path);
            }
            foreach (var item in ListItems(items, "ProjectReference"))
            {
                string full = Path.GetFullPath(Str(item, "FullPath"));
                ProjectSpec dep = _specs.FirstOrDefault(s => string.Equals(s.FullPath, full, StringComparison.OrdinalIgnoreCase));
                if (dep != null) build.ProjectReferences.Add(dep);
                else build.SkippedProjectReferences.Add(full);
            }
            if (!shadow)
            {
                foreach (var item in ListItems(items, "EmbeddedResource")) build.Resources.Add(item);
                foreach (var item in ListItems(items, "Page")) build.Pages.Add(item);
            }
            return build;
        }

        private static IEnumerable<Dictionary<string, object>> ListItems(Dictionary<string, object> items, string name)
        {
            if (!items.ContainsKey(name) || items[name] == null) return new Dictionary<string, object>[0];
            return ((object[])items[name]).Select(Obj);
        }

        private void LoadEvaluation(ProjectSpec spec)
        {
            ProjectBuild primary = ReadBuild(spec, Path.Combine(_evalDir, spec.Id + ".json"), false);
            spec.Builds.Add(primary);
            foreach (string tf in spec.ShadowTfs) spec.Builds.Add(ReadBuild(spec, Path.Combine(_evalDir, spec.Id + "@" + tf + ".json"), true));
            _assemblyToProject[primary.AssemblyName] = spec.Id;
            if (!spec.FilesOnly)
            {
                foreach (ProjectBuild b in spec.Builds)
                {
                    string[] missing = b.CompileItems.Keys.Where(p => !File.Exists(p)).ToArray();
                    if (missing.Length != 0) throw new Exception(spec.Id + " lists compile items that do not exist (generated files need a restore or build once, for example Build-Isolated.ps1 -Project " + spec.Id + "): " + string.Join(", ", missing.Take(5)));
                }
                if (!spec.IsSdk) CheckUnregistered(spec, primary);
            }
        }

        private void CheckUnregistered(ProjectSpec spec, ProjectBuild build)
        {
            string dir = Path.GetDirectoryName(spec.FullPath) + "\\";
            string[] skip = new[] { "\\bin\\", "\\obj\\", "\\.agent-index\\", "\\packages\\", "\\.vs\\" };
            string[] stray = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                .Where(p => !skip.Any(s => p.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) && !p.EndsWith("\\Properties\\Application.Designer.cs", StringComparison.OrdinalIgnoreCase))
                .Except(build.CompileItems.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
            if (stray.Length != 0) throw new Exception("Unregistered " + spec.Id + " source files (add them to the project): " + string.Join(", ", stray.Select(Relative)));
        }

        // ---------- compilation ----------

        private void ParseTrees()
        {
            foreach (ProjectSpec spec in _specs)
            {
                if (spec.FilesOnly)
                {
                    foreach (ProjectBuild b in spec.Builds) b.Trees = new SyntaxTree[0];
                    continue;
                }
                foreach (ProjectBuild b in spec.Builds)
                {
                    var options = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: b.Defines);
                    b.Trees = b.CompileItems.Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), options, p)).ToArray();
                    var parseErrors = b.Trees.SelectMany(t => t.GetDiagnostics()).Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                    if (parseErrors.Length != 0) throw new Exception("Parse errors in " + spec.Id + ":" + Environment.NewLine + string.Join(Environment.NewLine, parseErrors.Take(12).Select(d => d.ToString())));
                }
            }
        }

        private PortableExecutableReference Reference(string path)
        {
            PortableExecutableReference reference;
            if (!_referenceCache.TryGetValue(path, out reference)) _referenceCache[path] = reference = MetadataReference.CreateFromFile(path);
            return reference;
        }

        private ProjectBuild PickBuild(ProjectSpec dep, ProjectBuild consumer)
        {
            return dep.Builds.FirstOrDefault(b => b.IsFramework == consumer.IsFramework) ?? dep.Primary;
        }

        private void BuildCompilations()
        {
            var done = new HashSet<ProjectBuild>();
            foreach (ProjectSpec spec in _specs)
            {
                if (spec.FilesOnly) continue;
                foreach (ProjectBuild b in spec.Builds) Compile(b, done, new HashSet<ProjectBuild>());
            }
        }

        private ISourceGenerator[] LoadGenerators(IEnumerable<string> analyzerPaths)
        {
            var result = new List<ISourceGenerator>();
            foreach (string path in analyzerPaths)
            {
                ISourceGenerator[] cached;
                if (!_generatorCache.TryGetValue(path, out cached))
                {
                    Assembly asm = Assembly.LoadFrom(path);
                    cached = asm.GetTypes()
                        .Where(t => !t.IsAbstract && t.GetInterfaces().Any(i => i.Name == "IIncrementalGenerator"))
                        .Select(t => GeneratorExtensions.AsSourceGenerator((IIncrementalGenerator)Activator.CreateInstance(t)))
                        .ToArray();
                    _generatorCache[path] = cached;
                }
                result.AddRange(cached);
            }
            return result.ToArray();
        }

        private void Compile(ProjectBuild build, HashSet<ProjectBuild> done, HashSet<ProjectBuild> stack)
        {
            if (build.Spec.FilesOnly || done.Contains(build)) return;
            if (!stack.Add(build)) throw new Exception("Project reference cycle at " + build.Spec.Id);
            var references = new List<MetadataReference>(build.ExternalReferences.Select(Reference));
            foreach (ProjectSpec dep in build.ProjectReferences)
            {
                if (dep.FilesOnly) continue;
                ProjectBuild chosen = PickBuild(dep, build);
                Compile(chosen, done, stack);
                references.Add(chosen.Compilation.ToMetadataReference());
            }
            OutputKind kind = build.OutputType == "WinExe" ? OutputKind.WindowsApplication : build.OutputType == "Exe" ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary;
            var options = new CSharpCompilationOptions(kind).WithAllowUnsafe(build.AllowUnsafe).WithNullableContextOptions(NullableContextOptions.Disable);
            var compilationTrees = new List<SyntaxTree>(build.Trees);
            if (build.InternalsVisibleTo.Count != 0 && !build.CompileItems.Keys.Any(k => k.EndsWith(".AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)))
            {
                string ivtSource = string.Join(Environment.NewLine, build.InternalsVisibleTo.Select(n => "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"" + n + "\")]"));
                var parseOpts = build.Trees.Length != 0 ? (CSharpParseOptions)build.Trees[0].Options : new CSharpParseOptions(LanguageVersion.Preview);
                compilationTrees.Add(CSharpSyntaxTree.ParseText(ivtSource, parseOpts));
            }
            build.Compilation = CSharpCompilation.Create(build.AssemblyName, compilationTrees, references, options);
            if (build.Analyzers.Count != 0 && build.Trees.Length != 0)
            {
                ISourceGenerator[] generators = LoadGenerators(build.Analyzers);
                if (generators.Length != 0)
                {
                    GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: (CSharpParseOptions)build.Trees[0].Options);
                    Compilation updated;
                    System.Collections.Immutable.ImmutableArray<Diagnostic> genDiagnostics;
                    driver.RunGeneratorsAndUpdateCompilation(build.Compilation, out updated, out genDiagnostics);
                    var existingTrees = new HashSet<SyntaxTree>(compilationTrees);
                    build.GeneratedTrees = updated.SyntaxTrees.Where(t => !existingTrees.Contains(t)).ToArray();
                    build.Compilation = (CSharpCompilation)updated;
                }
            }
            stack.Remove(build);
            done.Add(build);
        }

        private void CheckDiagnostics()
        {
            foreach (ProjectSpec spec in _specs)
            {
                if (spec.FilesOnly) continue;
                foreach (ProjectBuild b in spec.Builds)
                {
                    b.Errors = b.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                    if (b.Errors.Length != 0 && !spec.AllowBindingErrors)
                        throw new Exception("Semantic index cannot be complete for " + spec.Id + (b.Shadow ? " (" + b.TargetFramework + " shadow)" : "") + ":" + Environment.NewLine + string.Join(Environment.NewLine, b.Errors.Take(12).Select(d => d.ToString())));
                }
            }
        }

        // ---------- emission ----------

        private static string Id(ISymbol s)
        {
            return s == null ? null : s.OriginalDefinition.GetDocumentationCommentId() ?? s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static int Start(SyntaxNode n) { return n.GetLocation().GetLineSpan().StartLinePosition.Line + 1; }

        private static int End(SyntaxNode n) { return n.GetLocation().GetLineSpan().EndLinePosition.Line + 1; }

        private static bool IsDeclaration(SyntaxNode n)
        {
            return n is BaseTypeDeclarationSyntax || n is DelegateDeclarationSyntax || n is BaseMethodDeclarationSyntax || n is BasePropertyDeclarationSyntax || n is EnumMemberDeclarationSyntax
                || n is VariableDeclaratorSyntax && n.Parent.Parent is BaseFieldDeclarationSyntax;
        }

        private static SyntaxNode OwnerNode(SyntaxNode n) { return n.Ancestors().FirstOrDefault(IsDeclaration); }

        private static bool IsGenerated(string path)
        {
            return path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
                || path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string ProjectOf(ISymbol symbol)
        {
            string project;
            return symbol != null && symbol.ContainingAssembly != null && _assemblyToProject.TryGetValue(symbol.ContainingAssembly.Name, out project) ? project : null;
        }

        private string Line(object value) { return _json.Serialize(value); }

        private void Count(string project, string key)
        {
            Dictionary<string, int> map;
            if (!_stats.TryGetValue(project, out map)) _stats[project] = map = new Dictionary<string, int>();
            int current;
            map.TryGetValue(key, out current);
            map[key] = current + 1;
        }

        private void AddFormat(string key, SyntaxNode node, string owner, string project)
        {
            if (!_formatHits.ContainsKey(key)) _formatHits[key] = new List<object>();
            _formatHits[key].Add(new { project, path = Relative(node.SyntaxTree.FilePath), line = Start(node), owner });
        }

        private void EmitProject(ProjectBuild build)
        {
            ProjectSpec spec = build.Spec;
            string project = spec.Id;
            var declaredIds = new HashSet<string>(StringComparer.Ordinal);
            _declared[project] = declaredIds;
            if (spec.FilesOnly)
            {
                foreach (var pair in build.CompileItems)
                {
                    string path = pair.Key;
                    if (!File.Exists(path)) continue;
                    string rel = Relative(path);
                    string sha = Hash(path);
                    _inputHashes[rel] = sha;
                    int lines = File.ReadAllLines(path).Length;
                    _files.Add(Line(new { project, path = rel, sha256 = sha, lines, generated = false, types = new string[0], dependentUpon = (string)null, oversizedReason = (string)null, largeMembers = new object[0] }));
                    Count(project, "files");
                }
                _inputHashes[Relative(spec.FullPath)] = Hash(spec.FullPath);
                return;
            }
            var constantRoutes = _annotations.ContainsKey("constantRoutes") ? ((object[])_annotations["constantRoutes"]).Select(Obj).Where(r => ProjectListed(r, project)).ToArray() : new Dictionary<string, object>[0];

            foreach (SyntaxTree tree in build.Trees)
            {
                string path = tree.FilePath;
                string rel = Relative(path);
                bool generated = IsGenerated(path);
                SyntaxNode root = tree.GetRoot();
                SemanticModel model = build.Compilation.GetSemanticModel(tree);
                var declarations = root.DescendantNodes().Where(IsDeclaration).ToArray();
                var declaredTypes = new SortedSet<string>(StringComparer.Ordinal);
                foreach (SyntaxNode n in declarations)
                {
                    ISymbol s = model.GetDeclaredSymbol(n);
                    if (s == null) throw new Exception("Unresolved declaration: " + rel + ":" + Start(n));
                    string key = Id(s);
                    declaredIds.Add(key);
                    if (s is INamedTypeSymbol) declaredTypes.Add(key);
                    string initializer = null;
                    var variable = n as VariableDeclaratorSyntax;
                    var property = n as PropertyDeclarationSyntax;
                    if (variable != null && variable.Initializer != null) initializer = variable.Initializer.Value.ToString();
                    if (property != null && property.Initializer != null) initializer = property.Initializer.Value.ToString();
                    _symbols.Add(Line(new { project, id = key, name = s.Name, kind = s.Kind.ToString(), type = Id(s.ContainingType), accessibility = s.DeclaredAccessibility.ToString(), isStatic = s.IsStatic, path = rel, start = Start(n), end = End(n), generated, initializer }));
                    Count(project, "declarations");
                    var primaryType = n as TypeDeclarationSyntax;
                    if (primaryType != null && primaryType.ParameterList != null)
                    {
                        foreach (var constructor in ((INamedTypeSymbol)s).InstanceConstructors.Where(c => c.DeclaringSyntaxReferences.Any(r => r.SyntaxTree == tree && r.Span.Contains(primaryType.ParameterList.Span))))
                        {
                            string constructorId = Id(constructor);
                            declaredIds.Add(constructorId);
                            _symbols.Add(Line(new { project, id = constructorId, name = constructor.Name, kind = "Method", type = key, accessibility = constructor.DeclaredAccessibility.ToString(), isStatic = false, path = rel, start = Start(primaryType.ParameterList), end = End(primaryType.ParameterList), generated, initializer = (string)null }));
                            Count(project, "declarations");
                        }
                    }
                    bool stateType = s.ContainingType != null && spec.StateTypes.Contains(s.ContainingType.Name);
                    if (!generated && (s.IsStatic || stateType) && (s is IFieldSymbol || s is IPropertySymbol || s is IEventSymbol))
                    {
                        _states.Add(Line(new { project, id = key, path = rel, line = Start(n), initializer, lifecycle = "See annotations.json and source; no inferred ownership guarantee", useEvidence = "relationships.jsonl; direct field/property/event references only; mutation through aliases is not exhaustively tracked" }));
                        Count(project, "states");
                    }
                }

                int lines = tree.GetText().Lines.Count;
                var bigMembers = declarations.Where(n => n is BaseMethodDeclarationSyntax && End(n) - Start(n) >= 700).Select(n => new { id = Id(model.GetDeclaredSymbol(n)), start = Start(n), end = End(n), blocks = n.DescendantNodes().OfType<SwitchSectionSyntax>().Select(b => new { line = Start(b), label = string.Join(" ", b.Labels.Select(l => l.ToString())) }).ToArray() }).ToArray();
                Dictionary<string, object> compileItem = build.CompileItems[path];
                bool inObj = path.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0;
                string sha = inObj ? null : Hash(path);
                if (sha != null) _inputHashes[rel] = sha;
                _files.Add(Line(new { project, path = rel, sha256 = sha, lines, generated, types = declaredTypes.ToArray(), dependentUpon = compileItem.ContainsKey("DependentUpon") ? compileItem["DependentUpon"] : null, oversizedReason = !generated && lines > 1000 ? "Whole existing method retained to preserve behavior; see largeMembers for block anchors." : null, largeMembers = bigMembers }));
                Count(project, "files");
                if (generated) continue;
                EmitEdges(project, rel, root, model, constantRoutes);
            }
            foreach (SyntaxTree tree in build.GeneratedTrees)
            {
                SyntaxNode root = tree.GetRoot();
                SemanticModel model = build.Compilation.GetSemanticModel(tree);
                foreach (SyntaxNode n in root.DescendantNodes().Where(IsDeclaration))
                {
                    ISymbol s = model.GetDeclaredSymbol(n);
                    if (s == null) continue;
                    string key = Id(s);
                    if (!declaredIds.Add(key)) continue;
                    SyntaxReference primaryRef = s.ContainingType != null
                        ? s.ContainingType.DeclaringSyntaxReferences
                            .Where(r => r.SyntaxTree.FilePath.StartsWith(_workspace, StringComparison.OrdinalIgnoreCase) && !IsGenerated(r.SyntaxTree.FilePath))
                            .OrderBy(r => r.SyntaxTree.FilePath, StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault()
                        : null;
                    if (primaryRef == null) continue;
                    string ownerPath = Relative(primaryRef.SyntaxTree.FilePath);
                    _symbols.Add(Line(new { project, id = key, name = s.Name, kind = s.Kind.ToString(), type = Id(s.ContainingType), accessibility = s.DeclaredAccessibility.ToString(), isStatic = s.IsStatic, path = ownerPath, start = Start(primaryRef.GetSyntax()), end = End(primaryRef.GetSyntax()), generated = true, initializer = (string)null }));
                    Count(project, "declarations");
                }
            }
            foreach (var item in build.Resources)
            {
                string full = Str(item, "FullPath");
                if (full.StartsWith(_workspace, StringComparison.OrdinalIgnoreCase) && File.Exists(full)) _inputHashes[Relative(full)] = Hash(full);
            }
            foreach (var item in build.Pages)
            {
                string full = Str(item, "FullPath");
                if (!File.Exists(full) || full.IndexOf("\\obj\\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string rel = Relative(full);
                string sha = Hash(full);
                _inputHashes[rel] = sha;
                int lines = File.ReadAllLines(full).Length;
                _files.Add(Line(new { project, path = rel, sha256 = sha, lines, generated = false, types = new string[0], dependentUpon = (string)null, oversizedReason = (string)null, largeMembers = new object[0] }));
                Count(project, "files");
            }
            _inputHashes[Relative(spec.FullPath)] = Hash(spec.FullPath);
            foreach (string sibling in new[] { "app.config", "packages.config", "Directory.Build.props" })
            {
                string candidate = Path.Combine(Path.GetDirectoryName(spec.FullPath), sibling);
                if (File.Exists(candidate)) _inputHashes[Relative(candidate)] = Hash(candidate);
            }
        }

        private static bool ProjectListed(Dictionary<string, object> entry, string project)
        {
            if (!entry.ContainsKey("projects")) return true;
            return ((object[])entry["projects"]).Cast<string>().Contains(project, StringComparer.OrdinalIgnoreCase);
        }

        private void EmitEdges(string project, string rel, SyntaxNode root, SemanticModel model, Dictionary<string, object>[] constantRoutes)
        {
            foreach (SyntaxNode n in root.DescendantNodes())
            {
                if (n is InvocationExpressionSyntax || n is ObjectCreationExpressionSyntax || n is AssignmentExpressionSyntax)
                {
                    SyntaxNode owner = OwnerNode(n);
                    string from = owner == null ? null : Id(model.GetDeclaredSymbol(owner));
                    var assignment = n as AssignmentExpressionSyntax;
                    if (assignment != null)
                    {
                        if (assignment.IsKind(SyntaxKind.AddAssignmentExpression) || assignment.IsKind(SyntaxKind.SubtractAssignmentExpression))
                        {
                            ISymbol target = model.GetSymbolInfo(assignment.Left).Symbol;
                            if (target is IEventSymbol) AddEdge(project, new { project, kind = "event-wiring", from, to = Id(target), toProject = ProjectOf(target), path = rel, line = Start(n), operation = assignment.OperatorToken.Text, handler = assignment.Right.ToString(), evidence = "semantic" }, false);
                        }
                        continue;
                    }
                    IOperation operation = model.GetOperation(n);
                    if (operation is INameOfOperation) continue;
                    ISymbol callee = model.GetSymbolInfo(n).Symbol;
                    bool delegateCreation = operation is IDelegateCreationOperation;
                    bool dynamicCall = operation is IDynamicInvocationOperation;
                    if (delegateCreation) callee = operation.Type;
                    string evidence = dynamicCall ? "runtime-overload-binding" : callee == null ? "unresolved" : "semantic";
                    AddEdge(project, new { project, kind = delegateCreation ? "delegate-creation" : dynamicCall ? "dynamic-call" : "call", from, to = Id(callee), toProject = ProjectOf(callee), path = rel, line = Start(n), expression = n is InvocationExpressionSyntax ? ((InvocationExpressionSyntax)n).Expression.ToString() : ((ObjectCreationExpressionSyntax)n).Type.ToString(), evidence }, evidence == "unresolved");
                    var method = callee as IMethodSymbol;
                    if (method != null && method.Parameters.SelectMany(p => p.GetAttributes()).Any(a => a.AttributeClass.Name == "CallerFilePathAttribute" || a.AttributeClass.Name == "CallerLineNumberAttribute")) _callerLocationUses.Add(new { project, from, to = Id(callee), path = rel, line = Start(n) });
                }
                if (n is IdentifierNameSyntax)
                {
                    ISymbol s = model.GetSymbolInfo(n).Symbol;
                    if ((s is IFieldSymbol || s is IPropertySymbol || s is IEventSymbol) && ProjectOf(s) != null)
                    {
                        SyntaxNode owner = OwnerNode(n);
                        string from = owner == null ? null : Id(model.GetDeclaredSymbol(owner));
                        var assignment = n.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault();
                        bool write = assignment != null && assignment.Left.Span.Contains(n.Span);
                        var argument = n.Ancestors().OfType<ArgumentSyntax>().FirstOrDefault();
                        string use = argument != null && !argument.RefKindKeyword.IsKind(SyntaxKind.None) ? argument.RefKindKeyword.Text : write ? "write-or-target-mutation" : "read-or-object-mutation";
                        AddEdge(project, new { project, kind = "state-use", from, to = Id(s), toProject = ProjectOf(s), path = rel, line = Start(n), use, evidence = "semantic; direct reference, not transitive alias analysis" }, false);
                    }
                }
                var member = n as MemberAccessExpressionSyntax;
                if (member != null)
                {
                    foreach (var route in constantRoutes)
                    {
                        if (member.Expression.ToString() == Str(route, "type"))
                        {
                            SyntaxNode owner = OwnerNode(n);
                            AddFormat(member.Name.Identifier.ValueText, n, owner == null ? null : Id(model.GetDeclaredSymbol(owner)), project);
                        }
                    }
                }
                var label = n as CaseSwitchLabelSyntax;
                if (label != null && label.Value is LiteralExpressionSyntax && constantRoutes.Length != 0)
                {
                    var literal = (LiteralExpressionSyntax)label.Value;
                    if (literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        string alias = literal.Token.ValueText;
                        foreach (var route in constantRoutes)
                        {
                            bool exact = route.ContainsKey("aliasExact") && ((object[])route["aliasExact"]).Cast<string>().Contains(alias);
                            bool contains = route.ContainsKey("aliasContains") && ((object[])route["aliasContains"]).Cast<string>().Any(m => alias.Contains(m));
                            if (exact || contains)
                            {
                                SyntaxNode owner = OwnerNode(n);
                                AddFormat(alias, n, owner == null ? null : Id(model.GetDeclaredSymbol(owner)), project);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void AddEdge(string project, object edge, bool unresolved)
        {
            _edges.Add(Line(edge));
            Count(project, "relationships");
            if (unresolved) Count(project, "unresolvedCalls");
        }

        private string Hash(string path)
        {
            using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        private void HashSharedInputs()
        {
            foreach (string path in Directory.GetFiles(_indexDir, "*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(path);
                if (name == "annotations.json" || name == "projects.json" || name == "INDEX.md") _inputHashes[Relative(path)] = Hash(path);
            }
            foreach (string path in Directory.GetFiles(Path.Combine(_indexDir, "tools"), "*", SearchOption.AllDirectories)) _inputHashes[Relative(path)] = Hash(path);
        }

        // ---------- routes and output ----------

        private List<string> BuildRoutes()
        {
            var routes = new List<string>();
            foreach (object entry in (object[])_annotations["routes"])
            {
                var route = Obj(entry);
                string[] selectors = ((object[])route["selectors"]).Cast<string>().ToArray();
                string[] projects = route.ContainsKey("projects") ? ((object[])route["projects"]).Cast<string>().ToArray() : _specs.Select(s => s.Id).ToArray();
                foreach (string p in projects) if (!_declared.ContainsKey(p)) throw new Exception("Route '" + Str(route, "task") + "' names unknown project: " + p);
                var matches = new SortedSet<string>(StringComparer.Ordinal);
                foreach (string selector in selectors)
                {
                    bool found = false;
                    foreach (string p in projects)
                    {
                        foreach (string id in _declared[p])
                        {
                            if (id.IndexOf(selector, StringComparison.OrdinalIgnoreCase) >= 0) { found = true; matches.Add(p + "|" + id); }
                        }
                    }
                    if (!found) throw new Exception("Route selector has no source symbol: '" + selector + "' (task '" + Str(route, "task") + "', projects " + string.Join(",", projects) + ")");
                }
                routes.Add(Line(new { task = route["task"], projects, symbols = matches.Select(m => new { project = m.Substring(0, m.IndexOf('|')), id = m.Substring(m.IndexOf('|') + 1) }).ToArray(), notes = route.ContainsKey("notes") ? route["notes"] : null }));
            }
            foreach (var pair in _formatHits) routes.Add(Line(new { task = "format:" + pair.Key, hits = pair.Value.Select(h => Line(h)).Distinct().OrderBy(h => h, StringComparer.Ordinal).Select(h => _json.DeserializeObject(h)).ToArray() }));
            return routes;
        }

        private static string Join(IEnumerable<string> lines)
        {
            return string.Join("\n", lines.OrderBy(l => l, StringComparer.Ordinal)) + "\n";
        }

        private void WriteOutputs()
        {
            var output = new SortedDictionary<string, string>(StringComparer.Ordinal);
            List<string> routes = BuildRoutes();
            output["files.jsonl"] = Join(_files);
            output["symbols.jsonl"] = Join(_symbols);
            output["relationships.jsonl"] = Join(_edges);
            output["state.jsonl"] = Join(_states);
            output["routes.jsonl"] = Join(routes);
            var perProject = _specs.Select(s =>
            {
                Dictionary<string, int> st;
                if (!_stats.TryGetValue(s.Id, out st)) st = new Dictionary<string, int>();
                Func<string, int> get = k => st.ContainsKey(k) ? st[k] : 0;
                ProjectBuild b = s.Primary;
                return new
                {
                    id = s.Id, path = s.RelativePath, targetFramework = b.TargetFramework, assembly = b.AssemblyName, sdkStyle = s.IsSdk, filesOnly = s.FilesOnly, defines = b.Defines,
                    compileItems = b.CompileItems.Count, declarations = get("declarations"), stateRecords = get("states"), relationships = get("relationships"), unresolvedCalls = get("unresolvedCalls"),
                    projectReferences = b.ProjectReferences.Select(p => p.Id).ToArray(), skippedProjectReferences = b.SkippedProjectReferences.Select(p => Relative(p)).ToArray(),
                    missingExternalReferences = b.MissingReferences.ToArray(), shadowFrameworks = s.ShadowTfs,
                    semanticErrors = s.Builds.Sum(x => x.Errors.Length), allowedBindingErrors = s.AllowBindingErrors ? s.AllowReason : null,
                    resources = b.Resources.Select(i => new { path = Str(i, "FullPath").StartsWith(_workspace, StringComparison.OrdinalIgnoreCase) ? Relative(Str(i, "FullPath")) : Str(i, "FullPath"), dependentUpon = i.ContainsKey("DependentUpon") ? i["DependentUpon"] : null, logicalName = i.ContainsKey("LogicalName") ? i["LogicalName"] : null }).ToArray()
                };
            }).ToArray();
            output["manifest.json"] = _json.Serialize(new
            {
                schema = 2, generator = 2, scope = "Solution-wide: AMRIE C# projects listed in projects.json",
                configurations = "Evaluated Debug. Project references bind as source; multi-target libraries use per-framework shadow compilations. No external package source is indexed.",
                projects = perProject, callerLocationUses = _callerLocationUses, formatRoutes = _formatHits.Count, inputHashes = _inputHashes,
                limitations = new[] { "Direct semantic references are not complete alias analysis.", "XAML, WiX and linked asset contents are not parsed; XAML-generated code is indexed only when present in obj.", "Use annotations.json for verified lifecycle guidance; source remains authoritative." }
            }) + "\n";
            Directory.CreateDirectory(_outDir);
            foreach (var pair in output)
            {
                string destination = Path.Combine(_outDir, pair.Key);
                if (_verify)
                {
                    if (!File.Exists(destination) || File.ReadAllText(destination) != pair.Value) throw new Exception("Stale or missing index: " + pair.Key + ". Run Update-Index.ps1 after source changes.");
                }
                else File.WriteAllText(destination, pair.Value, new UTF8Encoding(false));
            }
            int compileItems = _specs.Sum(s => s.Primary.CompileItems.Count);
            int semanticErrors = _specs.SelectMany(s => s.Builds).Sum(b => b.Errors.Length);
            Console.WriteLine((_verify ? "Verified" : "Generated") + ": " + _specs.Count + " projects, " + compileItems + " compile items, " + _symbols.Count + " declarations, " + _edges.Count + " relationships, " + _states.Count + " state records, " + _formatHits.Count + " format routes; semantic errors=" + semanticErrors);
        }
    }
}

