#!/bin/bash
# usage: stop.sh  — 再生を止めてから、play.sh で取っておいたセーブを書き戻す
D=$(cd "$(dirname "$0")" && pwd)
isuzu-unity-cli call play_mode_stop > /dev/null
isuzu-unity-cli call execute_code --file "$D/restore.cs"
