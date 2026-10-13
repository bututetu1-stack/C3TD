# m4a などの音を mp3 に変換する（Windows の Media Foundation を使う。追加のソフトは要らない）
# 使い方: powershell -NoProfile -ExecutionPolicy Bypass -File docs/snippets/to_mp3.ps1 -src <元のファイル> -dstDir <出力フォルダ> -dstName <名前.mp3>
param([string]$src, [string]$dstDir, [string]$dstName)
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$methods = [System.WindowsRuntimeSystemExtensions].GetMethods()
$asTaskOp = ($methods | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
$asTaskProg = ($methods | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncActionWithProgress`1' })[0]
function Await($op, $type) { $t = $asTaskOp.MakeGenericMethod($type).Invoke($null, @($op)); $t.Wait(-1) | Out-Null; $t.Result }
[void][Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
[void][Windows.Storage.StorageFolder, Windows.Storage, ContentType = WindowsRuntime]
[void][Windows.Media.Transcoding.MediaTranscoder, Windows.Media.Transcoding, ContentType = WindowsRuntime]
[void][Windows.Media.MediaProperties.MediaEncodingProfile, Windows.Media.MediaProperties, ContentType = WindowsRuntime]
$in = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($src)) ([Windows.Storage.StorageFile])
$props = Await ($in.Properties.GetMusicPropertiesAsync()) ([Windows.Storage.FileProperties.MusicProperties])
"source duration: " + $props.Duration + "  bitrate: " + $props.Bitrate
$folder = Await ([Windows.Storage.StorageFolder]::GetFolderFromPathAsync($dstDir)) ([Windows.Storage.StorageFolder])
$out = Await ($folder.CreateFileAsync($dstName, [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])
$profile = [Windows.Media.MediaProperties.MediaEncodingProfile]::CreateMp3([Windows.Media.MediaProperties.AudioEncodingQuality]::High)
$tc = New-Object Windows.Media.Transcoding.MediaTranscoder
$prep = Await ($tc.PrepareFileTranscodeAsync($in, $out, $profile)) ([Windows.Media.Transcoding.PrepareTranscodeResult])
if (-not $prep.CanTranscode) { "cannot transcode: " + $prep.FailureReason; exit 1 }
$t = $asTaskProg.MakeGenericMethod([double]).Invoke($null, @($prep.TranscodeAsync())); $t.Wait(-1) | Out-Null
"done: " + (Get-Item (Join-Path $dstDir $dstName)).Length + " bytes"
