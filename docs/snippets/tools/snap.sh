#!/bin/bash
# usage: snap.sh OUT.png [phone]  — 再生中（止めたままでよい）の画面を PNG に保存する。phone なら 2340x1080
# 例：isuzu-unity-cli call play_mode_play --paused; …; bash docs/snippets/tools/snap.sh C:/tmp/a.png
D=$(cd "$(dirname "$0")/.." && pwd)
OUT=$1
T=${TMP:-/tmp}/c3td_snap_$$.cs
if [ "$2" = "phone" ]; then isuzu-unity-cli call execute_code --file "$D/tools/cap_phone.cs" > /dev/null; else isuzu-unity-cli call execute_code --file "$D/cap_setup.cs" > /dev/null; fi
isuzu-unity-cli call play_mode_step --count 2 > /dev/null
sed "s#OUTPATH#$OUT#" "$D/cap_save_template.cs" > "$T"
isuzu-unity-cli call execute_code --file "$T"
rm -f "$T"
