// ガチャの音（GachaSound）と BGM（GachaMusic）を Temp/gacha_sounds/*.wav に書き出す（ゲームを動かさずに聞くため）
var names = new[] { "land", "charge0", "charge1", "charge2", "upgrade", "burst", "flip", "deal", "rare2", "rare3", "star", "stamp", "tap", "fanfare", "omen", "rainbow" };
var bf = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
var make = typeof(GachaSound).GetMethod("Make", bf);
var smooth = typeof(GachaSound).GetMethod("Smooth", bf);
string dir = "Temp/gacha_sounds";
System.IO.Directory.CreateDirectory(dir);
System.Action<string, float[], int> write = (path, data, rate) =>
{
    float peak = 0f;
    foreach (float x in data) peak = Mathf.Max(peak, Mathf.Abs(x));
    float scale = peak > 0.9f ? 0.9f / peak : 1f;
    using (var w = new System.IO.BinaryWriter(System.IO.File.Create(path)))
    {
        int bytes = data.Length * 2;
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
        foreach (float x in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(x * scale * 32767f), -32768, 32767));
    }
};
int n = 0;
foreach (string name in names)
{
    float[] d = (float[])make.Invoke(null, new object[] { name });
    smooth.Invoke(null, new object[] { d });
    write(dir + "/" + (++n).ToString("00") + "_" + name + ".wav", d, 44100);
}
var clip = GachaMusic.Loop();
var music = new float[clip.samples];
clip.GetData(music, 0);
// ループが分かるよう 2 周ぶん
var twice = new float[music.Length * 2];
music.CopyTo(twice, 0); music.CopyTo(twice, music.Length);
write(dir + "/00_music_loop_x2.wav", twice, clip.frequency);
return "wrote " + (n + 1) + " files to " + dir;
