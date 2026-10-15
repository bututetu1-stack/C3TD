#!/bin/bash
# usage: play.sh  — オーナーのセーブを取っておいてから、止めたまま再生を始める（テストはいつもこれで始める）
# 終わったら stop.sh（再生を止めてからセーブを戻す）。backup.cs を忘れて再生すると、
# タイトルを出しただけで今日のログインボーナスがオーナーのセーブに入ってしまう（2026-10-15 に実際にあった）
D=$(cd "$(dirname "$0")" && pwd)
isuzu-unity-cli call execute_code --file "$D/backup.cs" || exit 1
isuzu-unity-cli call play_mode_play --paused > /dev/null && isuzu-unity-cli call play_mode_step --seconds ${1:-2.5} > /dev/null
