#!/bin/bash
# usage: balance.sh NOVICE(true|false) RUNS "stage_01 stage_02 …" "squad ids …"
# 初めて遊ぶ人のまね（NOVICE=true、NoviceCut 0.6）か上手な置き方で RUNS 回ずつ戦わせ、勝った割合を出す。
# 先に tools/backup.cs、終わったら再生を止めて tools/restore.cs（クリアの記録が残るため）。強化・育成は効かない（Upgrades.Enabled）
arr(){ local out=""; for s in $1; do out="$out\"$s\","; done; echo "new[]{${out%,}}"; }
T=${TMP:-/tmp}/c3td_br_$$.cs
cat > "$T" <<CS
foreach (var old in UnityEngine.Object.FindObjectsByType<BalanceRun>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(old);
var boot = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
boot.SetSquad(new System.Collections.Generic.List<string>($(arr "$4")));
var br = boot.gameObject.AddComponent<BalanceRun>();
br.Stages = new System.Collections.Generic.List<string>($(arr "$3"));
br.Runs = $2;
br.Novice = $1;
br.NoviceCut = 0.6f;
br.UseSkills = true;
br.MaxTime = 2400f;
return "ok";
CS
S=${TMP:-/tmp}/c3td_brs_$$.cs
cat > "$S" <<'CS'
var br = UnityEngine.Object.FindAnyObjectByType<BalanceRun>();
return br.Done ? "DONE\n" + br.Report : "running";
CS
isuzu-unity-cli call play_mode_status | grep -q '"isPlaying":true' || { isuzu-unity-cli call play_mode_play > /dev/null; isuzu-unity-cli call play_mode_step --seconds 1 > /dev/null; }
isuzu-unity-cli call execute_code --file "$T" > /dev/null || exit 1
for i in $(seq 1 400); do
  isuzu-unity-cli call play_mode_step --seconds 1000 > /dev/null
  r=$(isuzu-unity-cli call execute_code --file "$S")
  if echo "$r" | grep -q DONE; then echo "$r" | sed 's/\n/\n/g'; rm -f "$T" "$S"; exit 0; fi
done
echo "not done"
