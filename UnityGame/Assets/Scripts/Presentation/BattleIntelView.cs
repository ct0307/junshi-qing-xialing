using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 顶栏战况只读取会话，不消费事件，不改变出兵与战斗时间。
    public sealed class BattleIntelView
    {
        private readonly GameDatabase database;
        private readonly LevelDefinition level;
        private readonly Text title;
        private readonly Text summary;
        private readonly Text count;
        private readonly Image bossTrack;
        private readonly Image bossFill;
        private readonly List<string> enemyNames = new List<string>();
        private readonly List<string> entrances = new List<string>();
        private readonly StringBuilder builder = new StringBuilder();
        private int mode = -1;
        private int cachedWaveIndex = -1;
        private string cachedWaveName = "";
        private string cachedBrief = "";
        private BattleState lastState = (BattleState)(-1);
        private bool lastActive;
        private int lastCount = -1;
        private int lastBossId = -1;
        private int lastHealth = -1;
        private int lastMaximumHealth = -1;
        private int lastWarning = -1;
        private bool lastEnraged;

        public BattleIntelView(UiFactory ui, RectTransform parent, GameDatabase db, LevelDefinition level)
        {
            database = db;
            this.level = level;
            RectTransform root = ui.Rect(parent, "战况情报", 198, 108, 1698, 58);
            title = ui.Label(root, "", 0, 0, 330, 46, 24, BattlePalette.Ink);
            title.gameObject.name = "战况标题";
            count = ui.Label(root, "", 1242, 0, 456, 46, 26, BattlePalette.Jade, TextAnchor.MiddleRight);
            count.gameObject.name = "战况计数";
            summary = ui.Label(root, "", 354, 0, 862, 46, 22, BattlePalette.Muted);
            summary.gameObject.name = "战况摘要";
            bossTrack = ui.Box(root, "敌将生命槽", 1242, 53, 456, 4, BattlePalette.Line, false);
            bossFill = ui.Box(root, "敌将生命", 1242, 53, 456, 4, BattlePalette.Danger, false);
            bossTrack.gameObject.SetActive(false);
            bossFill.gameObject.SetActive(false);
        }

        public void Refresh(BattleSession session)
        {
            if (session.State == BattleState.Victory || session.State == BattleState.Defeat)
            {
                int nextMode = session.State == BattleState.Victory ? 2 : 3;
                if (mode == nextMode) return;
                ChangeMode(nextMode);
                title.text = nextMode == 2 ? "守关成功" : "守关失利";
                title.color = nextMode == 2 ? BattlePalette.Jade : BattlePalette.Danger;
                count.text = "击退 " + session.DefeatedCount;
                count.color = BattlePalette.Ink;
                summary.text = nextMode == 2 ? "敌军已清空 · 城防 " + session.CastleHealth : session.DefeatReason;
                summary.color = BattlePalette.Muted;
                return;
            }

            EnemyUnit boss = null;
            foreach (EnemyUnit enemy in session.Enemies)
                if (enemy.Definition.IsBoss && enemy.Health > 0) { boss = enemy; break; }
            if (boss != null) RefreshBoss(session, boss);
            else RefreshWave(session);
        }

        private void RefreshWave(BattleSession session)
        {
            ChangeMode(0);
            bool active = session.IsWaveActive;
            int waveIndex = active ? session.CurrentWave - 1 : session.CurrentWave;
            bool waveChanged = cachedWaveIndex != waveIndex;
            if (waveChanged) CacheBrief(waveIndex);
            if (waveChanged || active != lastActive || session.State != lastState)
            {
                title.text = cachedWaveName.Length == 0 ? "暂无敌军" : (active ? "本波 · " : "下一波 · ") + cachedWaveName;
                title.color = BattlePalette.Ink;
                summary.text = cachedBrief;
                summary.color = BattlePalette.Muted;
                lastActive = active;
                lastState = session.State;
                lastCount = -1;
            }
            int value = active ? session.RemainingEnemies : Mathf.CeilToInt(session.NextWaveCountdown);
            if (value == lastCount) return;
            lastCount = value;
            count.text = active ? "余敌 " + value : session.State == BattleState.Preparation ? "开战后" + value + "秒" : value + "秒后";
            count.color = BattlePalette.Jade;
        }

        private void RefreshBoss(BattleSession session, EnemyUnit boss)
        {
            ChangeMode(1);
            bool bossChanged = lastBossId != boss.Id;
            if (bossChanged)
            {
                lastBossId = boss.Id;
                title.text = boss.Definition.Name + " · 敌将";
                lastHealth = lastMaximumHealth = lastWarning = -1;
            }
            int health = Mathf.CeilToInt(boss.Health), maximum = Mathf.CeilToInt(boss.MaxHealth);
            if (lastHealth != health || lastMaximumHealth != maximum)
            {
                lastHealth = health;
                lastMaximumHealth = maximum;
                count.text = health + "/" + maximum;
            }
            int warning = Mathf.CeilToInt(Mathf.Max(0, boss.WarningUntil - session.Time) * 10);
            if (bossChanged || warning != lastWarning || boss.Enraged != lastEnraged)
            {
                lastWarning = warning;
                lastEnraged = boss.Enraged;
                title.color = boss.Enraged ? BattlePalette.Danger : BattlePalette.Ink;
                summary.color = warning > 0 ? BattlePalette.Danger : BattlePalette.Muted;
                summary.text = warning > 0 ? (boss.Enraged ? "第二阶段 · " : "第一阶段 · ") + "蓄势 " + (warning / 10f).ToString("0.0") + "秒 · " + WarningText(boss.Definition.Skill) :
                    boss.Enraged ? "第二阶段 · 战意激昂，注意敌将加速" : "第一阶段 · 留意蓄势预告与技能破绽";
            }
            count.color = BattlePalette.Ink;
            UiFactory.FillWidth(bossFill.rectTransform, 456 * Mathf.Clamp01(boss.MaxHealth <= 0 ? 0 : boss.Health / boss.MaxHealth));
        }

        private void ChangeMode(int nextMode)
        {
            if (mode == nextMode) return;
            mode = nextMode;
            bool showBoss = mode == 1;
            bossTrack.gameObject.SetActive(showBoss);
            bossFill.gameObject.SetActive(showBoss);
            summary.rectTransform.sizeDelta = new Vector2(862, 46);
            cachedWaveIndex = -1;
            lastBossId = lastCount = -1;
            lastState = (BattleState)(-1);
        }

        private void CacheBrief(int waveIndex)
        {
            cachedWaveIndex = waveIndex;
            if (waveIndex < 0 || waveIndex >= level.Waves.Count)
            {
                cachedWaveName = "";
                cachedBrief = "等待战况更新";
                return;
            }
            WaveDefinition wave = level.Waves[waveIndex];
            cachedWaveName = wave.Name;
            enemyNames.Clear();
            entrances.Clear();
            foreach (SpawnGroup group in wave.Groups)
            {
                EnemyDefinition enemy = database.FindEnemy(group.EnemyId);
                if (enemy != null && !enemyNames.Contains(enemy.Name)) enemyNames.Add(enemy.Name);
                foreach (RouteDefinition route in level.Routes)
                    if (route.Id == group.RouteId && route.Points.Count > 0)
                    {
                        string entrance = Entrance(route.Points[0]);
                        if (!entrances.Contains(entrance)) entrances.Add(entrance);
                        break;
                    }
            }
            builder.Length = 0;
            builder.Append("敌型：");
            int visible = Mathf.Min(4, enemyNames.Count);
            for (int i = 0; i < visible; i++)
            {
                if (i > 0) builder.Append("、");
                builder.Append(enemyNames[i]);
            }
            if (enemyNames.Count > visible) builder.Append("等");
            builder.Append(" · 入口：");
            for (int i = 0; i < entrances.Count; i++)
            {
                if (i > 0) builder.Append("、");
                builder.Append(entrances[i]);
            }
            cachedBrief = builder.ToString();
        }

        private static string Entrance(Point2 point)
        {
            float left = point.X, right = 1920 - point.X, top = point.Y, bottom = 1080 - point.Y;
            float nearest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));
            if (nearest == left) return point.Y <= 360 ? "左上" : point.Y >= 650 ? "左下" : "左侧";
            if (nearest == right) return point.Y <= 360 ? "右上" : point.Y >= 650 ? "右下" : "右侧";
            return nearest == top ? "上方" : "下方";
        }

        private static string WarningText(string skill)
        {
            switch (skill)
            {
                case "charge": return "即将冲锋";
                case "rage": return "即将突进";
                case "summon": return "即将召唤援军";
                case "guard": return "即将恢复并护卫";
                case "disrupt": return "即将干扰守将";
                default: return "即将施展技能";
            }
        }
    }
}
