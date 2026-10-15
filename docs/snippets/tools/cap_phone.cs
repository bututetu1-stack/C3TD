// スマホの形（2340x1080）で撮る準備。先に UIFactory.SetZoom(1.35f) を流すとスマホの UI の大きさになる
var rt = new RenderTexture(2340, 1080, 24, RenderTextureFormat.ARGB32);
rt.name = "CaptureRT";
var doc = UnityEngine.Object.FindFirstObjectByType<UnityEngine.UIElements.UIDocument>();
var ps = doc.panelSettings;
ps.targetTexture = rt;
ps.clearColor = false;
Camera.main.targetTexture = rt;
return "ok";
