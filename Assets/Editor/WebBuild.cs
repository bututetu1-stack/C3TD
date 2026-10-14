using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// ブラウザ版を Builds/WebGL に作る（C3_shogi の WebBuild と同じ形）。
/// unityroom などに置くときは Builds/WebGL/Build の4つ（.loader.js・.data.gz・.framework.js.gz・.wasm.gz）を上げる。
/// 圧縮は Gzip（Brotli の .br を読めないサーバーがある）、解凍のフォールバックはなし。
/// ページは Assets/WebGLTemplates/C3TD（窓いっぱいに出す。スマホは横向きの案内と、描く解像度を2倍までに）。
/// 手元では node server/web-preview.js で開く。作ると、エディタの向き先は Web のままになる。
/// </summary>
public static class WebBuild
{
    public const string OutputPath = "Builds/WebGL";

    [MenuItem("C3TD/ブラウザ版を作る")]
    public static void BuildFromMenu()
    {
        string result = Build();
        EditorUtility.DisplayDialog("ブラウザ版", result, "OK");
    }

    /// <summary>ビルドして結果を返す（isuzu-unity-cli の execute_code からも呼ぶ）</summary>
    public static string Build()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.WebGL.template = "PROJECT:C3TD";
        ShrinkTexturesForWeb();
        CompressMusicForWeb();

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        string text = summary.result + " " + OutputPath + "（" + summary.totalTime.TotalSeconds.ToString("0") + "秒、エラー " + summary.totalErrors + "）";
        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("ブラウザ版のビルドに失敗: " + text);
            return text;
        }
        string dir = Path.Combine(OutputPath, "Build");
        if (Directory.Exists(dir))
            foreach (string f in Directory.GetFiles(dir))
                text += "\n" + Path.GetFileName(f) + " " + (new FileInfo(f).Length / 1048576f).ToString("0.0") + "MB";
        return text;
    }

    /// <summary>
    /// ブラウザ版だけ、Resources/Art の画像を、読み込むときに縮める大きさ（Art.cs）にそろえて圧縮する（Windows 版の設定は変えない）。
    /// 背景とカットインの立ち絵は大きく出すので 2048、ほかは 512。新しく置いた画像も、ビルドのたびにここでそろえる
    /// </summary>
    private static void ShrinkTexturesForWeb()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Art" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            int size = path.Contains("/Art/Background/") || path.Contains("/Art/CutIn/") ? 2048 : 512;
            TextureImporterPlatformSettings s = importer.GetPlatformTextureSettings("WebGL");
            if (s.overridden && s.maxTextureSize == size && s.textureCompression == TextureImporterCompression.Compressed) continue;
            s.overridden = true;
            s.maxTextureSize = size;
            s.format = TextureImporterFormat.Automatic;
            s.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(s);
            importer.SaveAndReimport();
        }
    }

    /// <summary>ブラウザ版だけ、BGM を圧縮したままメモリに置く（解凍すると1曲 40MB ほどになるため）。SE は解凍のまま</summary>
    private static void CompressMusicForWeb()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio/BGM" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;
            AudioImporterSampleSettings s = importer.GetOverrideSampleSettings(BuildTargetGroup.WebGL);
            if (importer.ContainsSampleSettingsOverride(BuildTargetGroup.WebGL) && s.loadType == AudioClipLoadType.CompressedInMemory) continue;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            importer.SetOverrideSampleSettings(BuildTargetGroup.WebGL, s);
            importer.SaveAndReimport();
        }
    }
}
