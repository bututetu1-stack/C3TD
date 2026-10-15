// ガチャの音（GachaSound がコードで作る音と、GachaMusic の BGM）の波の形を Temp/gacha_waves.png に描き、
// 長さ・いちばん大きいところ・隣どうしの差（大きいとプチッと鳴る）・終わりの大きさ（0 でないとプツッと切れる）を返す。
// BGM はループの継ぎ目の差（終わりと頭の差）と、作るのにかかった時間も返す
var names = new[] { "land", "charge0", "charge1", "charge2", "upgrade", "burst", "flip", "rare2", "rare3", "star", "stamp", "tap", "fanfare", "omen", "rainbow", "deal" };
var bf = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
var make = typeof(GachaSound).GetMethod("Make", bf);
var smooth = typeof(GachaSound).GetMethod("Smooth", bf);
int w = 600, h = 100, cols = 2, rows = 9;
var tex = new Texture2D(w * cols, h * rows, TextureFormat.RGBA32, false);
var px = new Color32[tex.width * tex.height];
for (int i = 0; i < px.Length; i++) px[i] = new Color32(16, 18, 28, 255);
var info = new System.Collections.Generic.List<string>();
var waves = new System.Collections.Generic.List<float[]>();
foreach (string name in names)
{
    float[] d = (float[])make.Invoke(null, new object[] { name });
    smooth.Invoke(null, new object[] { d });
    waves.Add(d);
}
var sw = System.Diagnostics.Stopwatch.StartNew();
var clip = GachaMusic.Loop();
sw.Stop();
var music = new float[clip.samples];
clip.GetData(music, 0);
waves.Add(music);
for (int n = 0; n < waves.Count; n++)
{
    float[] d = waves[n];
    string name = n < names.Length ? names[n] : "music";
    int ox = (n % cols) * w, oy = (rows - 1 - n / cols) * h;
    float peak = 0f, maxJump = 0f;
    for (int i = 0; i < d.Length; i++) { peak = Mathf.Max(peak, Mathf.Abs(d[i])); if (i > 0) maxJump = Mathf.Max(maxJump, Mathf.Abs(d[i] - d[i - 1])); }
    int rate = name == "music" ? clip.frequency : 44100;
    string extra = name == "music" ? " seam=" + Mathf.Abs(d[d.Length - 1] - d[0]).ToString("0.000") + " build=" + sw.ElapsedMilliseconds + "ms" : " end=" + Mathf.Abs(d[d.Length - 1]).ToString("0.000");
    info.Add(name + " len=" + (d.Length / (float)rate).ToString("0.00") + "s peak=" + peak.ToString("0.00") + " jump=" + maxJump.ToString("0.00") + extra);
    int per = Mathf.Max(1, d.Length / w);
    for (int x = 0; x < w; x++)
    {
        float lo = 0f, hi = 0f;
        for (int k = x * per; k < Mathf.Min(d.Length, (x + 1) * per); k++) { lo = Mathf.Min(lo, d[k]); hi = Mathf.Max(hi, d[k]); }
        int y0 = Mathf.Clamp((int)(h / 2 + lo * h / 2), 0, h - 1), y1 = Mathf.Clamp((int)(h / 2 + hi * h / 2), 0, h - 1);
        for (int y = y0; y <= y1; y++) px[(oy + y) * tex.width + ox + x] = name == "music" ? new Color32(122, 216, 240, 255) : new Color32(255, 210, 122, 255);
        px[(oy + h / 2) * tex.width + ox + x] = new Color32(80, 90, 110, 255);
    }
    for (int x = 0; x < w; x++) px[oy * tex.width + ox + x] = new Color32(60, 60, 80, 255);
}
tex.SetPixels32(px); tex.Apply();
System.IO.File.WriteAllBytes("Temp/gacha_waves.png", tex.EncodeToPNG());
return string.Join("\n", info);
