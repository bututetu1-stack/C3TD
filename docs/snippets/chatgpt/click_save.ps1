# Press Save on the Brave save dialog (keeps the suggested name and the last folder)
param([int]$TimeoutSec = 30)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
. "$PSScriptRoot\find_win.ps1"
if (-not ('Msg' -as [type])) {
Add-Type @'
using System; using System.Runtime.InteropServices;
public class Msg { [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l); }
'@
}
$deadline = (Get-Date).AddSeconds($TimeoutSec)
$h = [IntPtr]::Zero
while ($h -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline) { $h = [Win]::FindSave(); if ($h -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 400 } }
if ($h -eq [IntPtr]::Zero) { 'no dialog'; exit 1 }
$dlg = [System.Windows.Automation.AutomationElement]::FromHandle($h)
$btn = $null
foreach ($e in $dlg.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
  $c = $e.Current
  if ($c.AutomationId -eq '1' -and $c.ClassName -eq 'Button') { $btn = $c.NativeWindowHandle }
}
if (-not $btn) { 'no button'; exit 1 }
[void][Msg]::SendMessage([IntPtr]$btn, 0x00F5, [IntPtr]::Zero, [IntPtr]::Zero)
'clicked'
