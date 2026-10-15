#!/bin/bash
# usage: fetch_one.sh ID  — press Save on the Brave dialog, then move Audio/SE|BGM/c3td_ID.png to gen/dl/ID.png
S=${C3TD_SCRATCH:-/c/Users/Kokik/AppData/Local/Temp/c3td-gen}  # 作業用フォルダ（gen/click_save.ps1 と gen/dl を置く）
A=/c/Users/Kokik/unity/C3TD/Assets/Resources/Audio
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w $S/gen/click_save.ps1)" -TimeoutSec ${2:-15}
F=
for i in $(seq 1 20); do for d in SE BGM; do [ -f "$A/$d/c3td_$1.png" ] && F="$A/$d/c3td_$1.png"; done; [ -n "$F" ] && break; sleep 0.5; done
if [ -n "$F" ]; then sleep 1; mv "$F" "$S/gen/dl/$1.png"; rm -f "$F.meta"; echo "got $1 $(stat -c %s $S/gen/dl/$1.png) bytes"; else echo "missing $1"; fi
