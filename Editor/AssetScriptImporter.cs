using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Mono.CSharp;

#if UNITY_2020_3_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

public class AssetScriptHelper
{
    internal static AssetImportContext m_CurrentCtx;

    static readonly string m_CurrentDirectory = System.Environment.CurrentDirectory;

    public string assetPath => m_CurrentCtx.assetPath;
    public BuildTarget selectedBuildTarget => m_CurrentCtx.selectedBuildTarget;

    public void AddObjectToAsset(string identifier, Object obj)
    {
        m_CurrentCtx.AddObjectToAsset(identifier, obj);
    }

    public void AddObjectToAsset(string identifier, Object obj, Texture2D thumbnail)
    {
        m_CurrentCtx.AddObjectToAsset(identifier, obj, thumbnail);
    }

    public void DependsOnArtifact(GUID guid)
    {
        m_CurrentCtx.DependsOnArtifact(guid);
    }

    public void DependsOnArtifact(string path)
    {
        m_CurrentCtx.DependsOnArtifact(path);
    }

    public void DependsOnCustomDependency(string dependency)
    {
        m_CurrentCtx.DependsOnCustomDependency(dependency);
    }

    public void DependsOnSourceAsset(string path)
    {
        m_CurrentCtx.DependsOnSourceAsset(path);
    }

    public void DependsOnSourceAsset(GUID guid)
    {
        m_CurrentCtx.DependsOnSourceAsset(guid);
    }

    public string GetResultPath(string extension)
    {
        return m_CurrentCtx.GetResultPath(extension);
    }

    public void LogImportError(string msg, Object obj = null)
    {
        m_CurrentCtx.LogImportError(msg, obj);
    }

    public void LogImportWarning(string msg, Object obj = null)
    {
        m_CurrentCtx.LogImportWarning(msg, obj);
    }

    public void SetMainObject(Object obj)
    {
        m_CurrentCtx.SetMainObject(obj);
    }

    public T LoadAsset<T>(string path) where T: Object
    {
        var dir = GetAssetDirectoryPath();
        var relativePath = dir + "/" + path;

        if (File.Exists(relativePath))
        {
            path = Path.GetFullPath(relativePath);
        }

        if (path.StartsWith(m_CurrentDirectory))
        {
            path = path.Substring(m_CurrentDirectory.Length + 1);
        }

        if (path.Contains("\\"))
        {
            path = path.Replace('\\', '/');
        }

        m_CurrentCtx.DependsOnArtifact(path);

        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset;
    }

    public T CopyAsset<T>(string path) where T: Object
    {
        var asset = LoadAsset<T>(path);

        if (asset != null)
        {
            return Instantiate<T>(asset);
        }

        return null;
    }

    public string GetAssetDirectoryPath()
    {
        return Path.GetDirectoryName(m_CurrentCtx.assetPath).Replace('\\', '/');
    }

    public string GetAssetPathWithoutExtension()
    {
        return GetAssetDirectoryPath() + "/" + Path.GetFileNameWithoutExtension(assetPath);
    }

    public T CreateInstance<T>() where T : ScriptableObject
    {
        return ScriptableObject.CreateInstance<T>();
    }

    public T Instantiate<T>(T original) where T : Object
    {
        if (original != null)
        {
            var obj = Object.Instantiate(original);
            obj.name = original.name;
            return obj;
        }
        return null;
    }

    public void Destroy(Object obj)
    {
        Object.DestroyImmediate(obj);
    }
}

[ScriptedImporter(1, "assetscript")]
public class AssetScriptImporter : ScriptedImporter
{
    static CompilerSettings m_CompilerSettings;

    static readonly Regex RegUsing = new Regex("#using (.*)");
    static readonly Regex RegInclude = new Regex("#include \"(.*)\"");

    const string ScriptTemplate = @"
class ImporterImpl : AssetScriptHelper
{
    public void Run()
    {
        ${MAIN};
    }
}
";

    Evaluator m_Evaluator;
    HashSet<string> m_IncludedFiles = new HashSet<string>();

    [MenuItem("Assets/Create/Asset Script")]
    static void OnCreateAsset()
    {
        ProjectWindowUtil.CreateAssetWithContent("New Asset Script.assetscript", "#using UnityEngine\n");
    }

    void Include(List<string> source, string path)
    {
        if (!m_IncludedFiles.Add(path))
        {
            return;
        }

        if (path != assetPath)
        {
            AssetScriptHelper.m_CurrentCtx.DependsOnSourceAsset(path);
        }

        var lines = File.ReadAllLines(path);

        for (var i = 0; i < lines.Length; ++i)
        {
            Match match;

            match = RegInclude.Match(lines[i]);
            if (match.Success)
            {
                var includePath = match.Groups[1].Value;
                var baseDir = Path.GetDirectoryName(path).Replace('\\', '/');

                if (File.Exists(baseDir + "/" + includePath))
                {
                    includePath = baseDir + "/" + includePath;
                }

                Include(source, includePath);
                continue;
            }

            match = RegUsing.Match(lines[i]);
            if (match.Success)
            {
                var ns = match.Groups[1].Value;
                m_Evaluator.Run($"using {ns};");
                continue;
            }

            source.Add(lines[i]);
        }
    }

    public override void OnImportAsset(AssetImportContext ctx)
    {
        AssetScriptHelper.m_CurrentCtx = ctx;

        if (m_CompilerSettings == null)
        {
            m_CompilerSettings = new CompilerSettings();

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                m_CompilerSettings.AssemblyReferences.Add(assembly.FullName);
            }
        }

        var errorWriter = new StringWriter();
        var printer = new ConsoleReportPrinter(errorWriter);
        var compilerContext = new CompilerContext(m_CompilerSettings, printer);
        m_Evaluator = new Evaluator(compilerContext);

        var source = new List<string>();

        Include(source, ctx.assetPath);

        var script = ScriptTemplate.Replace("${MAIN}", string.Join("\n", source));

        m_Evaluator.Compile(script);

        if (0 < compilerContext.Report.Errors)
        {
            ctx.LogImportError(ctx.assetPath + errorWriter.ToString());
            return;
        }

        m_Evaluator.Run("new ImporterImpl().Run();");

        if (0 < compilerContext.Report.Errors)
        {
            ctx.LogImportError(ctx.assetPath + errorWriter.ToString());
            return;
        }
    }
}
