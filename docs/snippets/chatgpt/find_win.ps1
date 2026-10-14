if (-not ('Win' -as [type])) {
Add-Type @'
using System; using System.Text; using System.Runtime.InteropServices;
public class Win { public delegate bool P(IntPtr h, IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(P p, IntPtr l);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
public static IntPtr FindSave() { IntPtr found = IntPtr.Zero; EnumWindows((h,l)=>{ if(IsWindowVisible(h)){ var s=new StringBuilder(256); GetWindowText(h,s,256); var c=new StringBuilder(64); GetClassName(h,c,64); if(c.ToString()=="#32770" && s.ToString().Contains(new string(new char[]{(char)0x4fdd,(char)0x5b58}))){ found=h; return false; } } return true; }, IntPtr.Zero); return found; } }
'@
}
