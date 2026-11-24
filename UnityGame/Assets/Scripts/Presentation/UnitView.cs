using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    // 规则状态由BattleSession提供，这里仅负责位置、血条与动作。
    public sealed class UnitView
    {
        public readonly RectTransform Root;
        public Point2 Position { get; private set; }
        public bool IsRetiring { get; private set; }
        public bool IsRetired { get; private set; }
        public bool IsBoss { get; private set; }
        private readonly UiFactory ui;
        private readonly Image body;
        private readonly Image contactShadow;
        private readonly Image enemyMarker;
        private readonly Image healthTrack;
        private readonly Image healthFill;
        private readonly Text name;
        private readonly Text levelText;
        private readonly Text status;
        private readonly bool enemy;
        private readonly CanvasGroup visibility;
        private readonly RectTransform labelGroup;
        private string displayName;
        private int displayedLevel = -1;
        private float size;
        private float attackAt = -10;
        private float skillAt = -10;
        private float hitAt = -10;
        private float retireAt;
        private float chargeAt = -10, chargeUntil = -10;
        private Vector2 attackDirection = Vector2.right;
        private string skillId;
        private Color bodyTint = Color.white;
        private Vector2 bodyOrigin;

        public UnitView(UiFactory ui, RectTransform parent, bool enemy, float verticalScale = 1, RectTransform labelParent = null)
        {
            this.ui = ui;
            this.enemy = enemy;
            Root = ui.Rect(parent, enemy ? "敌军" : "守将", 0, 0, 0, 0);
            Root.localScale = new Vector3(1, 1 / verticalScale, 1);
            visibility = Root.gameObject.AddComponent<CanvasGroup>();
            visibility.blocksRaycasts = false;
            contactShadow = ui.Circle(Root, "接地阴影", -48, -4, 96, 20, new Color(.045f, .12f, .075f, .22f));
            enemyMarker = ui.Box(Root, "敌军识别", -6, 4, 12, 12, BattlePalette.Danger, false);
            enemyMarker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            enemyMarker.gameObject.SetActive(enemy);
            body = ui.Box(Root, "人物", 0, 0, 190, 190, Color.white, false);
            body.preserveAspect = true;
            healthTrack = ui.Box(Root, "生命底", -29, -80, 58, 6, UiFactory.Ink);
            healthFill = ui.Box(Root, "生命", -29, -80, 58, 6, UiFactory.Red);
            healthTrack.gameObject.SetActive(enemy);
            healthFill.gameObject.SetActive(enemy);
            RectTransform nameParent = Root;
            if (labelParent != null)
            {
                // 敌军生命信息独立排序；守将姓名移至将册，避免跨人物遮挡。
                labelGroup = ui.Rect(labelParent, enemy ? "敌军名签组" : "守将名签组", 0, 0, 0, 0);
                labelGroup.localScale = Root.localScale;
                nameParent = labelGroup;
                healthTrack.rectTransform.SetParent(labelGroup, false);
                healthFill.rectTransform.SetParent(labelGroup, false);
            }
            Image namePlate = ui.Box(nameParent, "名签底", enemy ? -56 : -74, 18, enemy ? 112 : 154, 36,
                enemy ? BattlePalette.Danger : BattlePalette.Silk, false);
            ui.Border(namePlate, enemy ? BattlePalette.Danger : BattlePalette.Line, 1);
            name = ui.Label(namePlate.transform, "", 0, 0, 112, 36, 24,
                enemy ? BattlePalette.Silk : BattlePalette.Ink, TextAnchor.MiddleCenter);
            BattleTypography.Display(name, 22);
            Image levelBadge = ui.Box(nameParent, "等级徽标", -14, 5, 28, 30, BattlePalette.Jade, false);
            levelText = ui.Label(levelBadge.transform, "", 0, 0, 28, 30, 18, BattlePalette.Silk, TextAnchor.MiddleCenter);
            levelBadge.gameObject.SetActive(false);
            status = ui.Label(Root, "", -48, -130, 96, 30, 23, UiFactory.Gold, TextAnchor.MiddleCenter);
            enemyMarker.rectTransform.SetAsLastSibling();
        }

        public void SetDefinition(string spritePath, string displayName, float width, bool boss)
        {
            Root.gameObject.SetActive(true);
            if (labelGroup != null) labelGroup.gameObject.SetActive(true);
            visibility.alpha = 1;
            IsRetiring = IsRetired = false;
            status.text = "";
            status.color = UiFactory.Gold;
            IsBoss = boss;
            enemyMarker.color = boss ? BattlePalette.Copper : BattlePalette.Danger;
            enemyMarker.rectTransform.sizeDelta = boss ? new Vector2(18, 18) : new Vector2(12, 12);
            enemyMarker.gameObject.SetActive(enemy);
            hitAt = -10;
            bodyTint = Color.white;
            healthTrack.gameObject.SetActive(enemy);
            healthFill.gameObject.SetActive(enemy);
            body.rectTransform.localScale = Vector3.one;
            size = width;
            body.sprite = ui.LoadSprite(spritePath);
            body.color = body.sprite == null ? Color.clear : Color.white;
            contactShadow.gameObject.SetActive(body.sprite != null);
            contactShadow.rectTransform.sizeDelta = new Vector2(width * .58f, width * .12f);
            UiFactory.Move(contactShadow.rectTransform, -width * .29f, -width * .025f);
            body.rectTransform.sizeDelta = new Vector2(width, width);
            // 人物画布固定768方，台座中心(360,660)对齐阵位；uGUI不读取Sprite的pivot。
            bodyOrigin = new Vector2(-width * 360f / 768f, width * 660f / 768f);
            body.rectTransform.anchoredPosition = bodyOrigin;
            this.displayName = displayName;
            name.text = this.displayName;
            name.transform.parent.gameObject.SetActive(false);
            displayedLevel = -1;
            levelText.transform.parent.gameObject.SetActive(false);
            UiFactory.Move(healthTrack.rectTransform, -29, -width * .78f);
            UiFactory.Move(healthFill.rectTransform, -29, -width * .78f);
            UiFactory.Move(status.rectTransform, -48, -width * .91f);
            attackAt = -10;
            skillAt = -10;
            chargeAt = chargeUntil = -10;
            attackDirection = Vector2.right;
            skillId = "";
            if (enemy) healthFill.color = boss ? UiFactory.Red : UiFactory.Hex("B89853");
        }

        public void Place(Point2 position, int level, float health, bool stunned, bool slowed, float time)
        {
            if (IsRetiring) return;
            Position = position;
            UiFactory.Move(Root, position.X, position.Y);
            if (labelGroup != null) UiFactory.Move(labelGroup, position.X, position.Y);
            UiFactory.FillWidth(healthFill.rectTransform, 58 * Mathf.Clamp01(health));
            bool showHealth = enemy && (IsBoss || health < .999f);
            healthTrack.gameObject.SetActive(showHealth);
            healthFill.gameObject.SetActive(showHealth);
            status.text = stunned ? "眩晕" : slowed ? "减速" : "";
            if (!enemy && displayedLevel != level)
            {
                displayedLevel = level;
                levelText.text = level > 0 ? level.ToString() : "";
                levelText.transform.parent.gameObject.SetActive(false);
            }
            bodyTint = stunned ? new Color(.83f, .84f, .7f, 1) : slowed ? new Color(.73f, .91f, 1, 1) : Color.white;
            body.color = body.sprite == null ? Color.clear : bodyTint;
        }

        public void Attack(float time) { Attack(time, true); }
        public void Attack(float time, bool skill)
        {
            // 技能动作短暂优先；普攻仍有独立规则事件和弹道，不重启技能姿态。
            if (skill) skillAt = time;
            attackAt = time;
        }

        public RectTransform RankRect { get { return (RectTransform)levelText.transform.parent; } }
        public RectTransform BodyRect { get { return body.rectTransform; } }
        public void SetRankVisible(bool visible) { RankRect.gameObject.SetActive(!enemy && visible && displayedLevel > 0 && !IsRetiring); }
        public void Charge(float time, float until) { chargeAt = time; chargeUntil = until; }
        public void Cast(float time, string skill, Point2 target)
        {
            skillId = skill;
            Vector2 direction = new Vector2(target.X - Position.X, Position.Y - target.Y);
            attackDirection = direction.sqrMagnitude > .01f ? direction.normalized : Vector2.right;
            chargeUntil = time;
            Attack(time, true);
        }

        public void SetHealthLane(int lane)
        {
            if (!enemy) return;
            float lift = IsBoss ? 0 : (lane % 3) * 10;
            UiFactory.Move(healthTrack.rectTransform, -29, -size * .78f - lift);
            UiFactory.Move(healthFill.rectTransform, -29, -size * .78f - lift);
        }
        public void Hit(float time) { if (enemy && !IsRetired) hitAt = time; }
        // 规则层已移除敌人后，展示层继续用同一游戏时钟推进短暂退场。
        public void Retire(float time)
        {
            // 同批旧离场事件可以回溯起点；后到的较晚时刻不能延长退场。
            if (IsRetiring) { retireAt = Mathf.Min(retireAt, time); return; }
            IsRetiring = true;
            retireAt = time;
            status.text = "";
            healthTrack.gameObject.SetActive(false);
            healthFill.gameObject.SetActive(false);
            name.transform.parent.gameObject.SetActive(false);
            levelText.transform.parent.gameObject.SetActive(false);
            if (labelGroup != null) labelGroup.gameObject.SetActive(false);
        }
        public void ClearStatus() { status.text = ""; status.color = UiFactory.Gold; }
        public void SetWarning(bool warning) { if (warning) { status.text = "蓄势！"; status.color = UiFactory.Gold; } }
        public void SetFeedback(EnemyFeedbackSnapshot state)
        {
            if (!enemy || IsRetiring) return;
            string value = "";
            int count = 0;
            if (state.Stunned) { value = "晕"; count++; }
            if (state.Slowed) { value += count++ > 0 ? " 缓" : "缓"; }
            if (state.Linked && count < 2) { value += count++ > 0 ? " 链" : "链"; }
            if (state.Burning && count < 2) value += count > 0 ? " 燃" : "燃";
            status.text = value; status.color = state.Stunned ? UiFactory.Gold : UiFactory.Hex("E0DAB4");
        }
        public void Tint(Color tint) { bodyTint = tint; if (body.sprite != null) body.color = tint; }

        public void Animate(float time)
        {
            // 致命一击也先亮起，再随同一实秒时钟淡出；暂停时两者一起冻结。
            float hitElapsed = time - hitAt;
            float flash = hitElapsed >= 0 && hitElapsed < .12f ? 1 - hitElapsed / .12f : 0;
            body.color = body.sprite == null ? Color.clear : Color.Lerp(bodyTint, new Color(1, .91f, .69f, 1), flash * .82f);
            if (IsRetiring)
            {
                float progress = Mathf.Clamp01((time - retireAt) / .24f);
                visibility.alpha = 1 - progress;
                body.rectTransform.anchoredPosition = bodyOrigin + new Vector2(0, -progress * 8);
                float retiringScale = 1 - progress * .08f;
                body.rectTransform.localScale = new Vector3(retiringScale, retiringScale, 1);
                IsRetired = progress >= 1;
                return;
            }
            bool skillActive = time - skillAt >= 0 && time - skillAt < .3f;
            if (!enemy && time >= chargeAt && time < chargeUntil)
            {
                float charge = Mathf.Clamp01((time - chargeAt) / Mathf.Max(.01f, chargeUntil - chargeAt));
                body.rectTransform.anchoredPosition = bodyOrigin + new Vector2(-6 * charge, -3 * charge);
                body.rectTransform.localScale = Vector3.one * (1 - .035f * charge);
                body.color = Color.Lerp(bodyTint, new Color(1, .81f, .49f, 1), charge * .35f);
                return;
            }
            float elapsed = time - (skillActive ? skillAt : attackAt);
            float duration = skillActive ? .3f : .18f;
            float attack = elapsed >= 0 && elapsed < duration ? Mathf.Sin(elapsed / duration * Mathf.PI) : 0;
            float bob = Mathf.Sin(time * 2.3f + Position.X * .025f) * 1.4f;
            float reach = skillActive ? (skillId == "cleave" ? 22 : skillId == "rain" ? 5 : skillId == "fire" ? 8 : 17) : 7;
            Vector2 movement = skillActive ? attackDirection * (attack * reach) : Vector2.right * (attack * reach);
            body.rectTransform.anchoredPosition = bodyOrigin + movement + new Vector2(0, bob + attack * 4);
            float scale = 1 + attack * (skillActive ? .10f : .06f);
            body.rectTransform.localScale = new Vector3(scale, scale, 1);
        }

        public void Hide()
        {
            chargeAt = chargeUntil = -10;
            status.text = "";
            Root.gameObject.SetActive(false);
            if (labelGroup != null) labelGroup.gameObject.SetActive(false);
        }
    }
}
