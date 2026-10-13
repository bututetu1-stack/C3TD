var b = Battle.Current;
var sb = new System.Text.StringBuilder();
sb.Append("t=" + b.State.ElapsedTime.ToString("F1") + " dp=" + b.State.DpInt + " life=" + b.State.Life + " killed=" + b.State.EnemiesDefeated + " leaked=" + b.State.EnemiesLeaked + " phase=" + b.State.Phase + " ts=" + Time.timeScale);
foreach (var c in b.Characters) sb.Append(" | " + c.Data.id + " hp=" + Mathf.RoundToInt(c.Hp) + " blocked=" + c.Blocked.Count);
foreach (var e in b.Enemies) sb.Append(" | " + e.Data.id + " hp=" + Mathf.RoundToInt(e.Hp) + (e.BlockedBy != null ? "(B)" : ""));
foreach (var m in b.Squad) sb.Append(" | sq:" + m.Data.id + " cd=" + m.Cooldown.ToString("F0") + (m.Deployed != null ? " D" : ""));
return sb.ToString();
