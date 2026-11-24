using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YanyiSandpan.Domain;

namespace YanyiSandpan.Presentation
{
    public sealed partial class BattleEffects : IDisposable
    {
        private readonly UiFactory ui;
        private readonly RectTransform parent;
        private readonly RectTransform fieldParent;
        private readonly Dictionary<string, Sprite[]> atlases = new Dictionary<string, Sprite[]>();
        private readonly List<Effect> active = new List<Effect>();
        private readonly Stack<Effect> pool = new Stack<Effect>();
        private readonly List<CueStamp> recentCues = new List<CueStamp>();
        private readonly Dictionary<string, float> nextAttackCues = new Dictionary<string, float>();
        private readonly BattleFeedbackState fallbackFeedback = new BattleFeedbackState(.7f);
        private const int MaxEffects = 90;
        private const int MaxNumbers = 14;
        private float gameTime;
        private float eventAge;
        private Effect killCombo;

        public BattleEffects(UiFactory ui, RectTransform parent, RectTransform fieldParent = null)
        { this.ui = ui; this.parent = parent; this.fieldParent = fieldParent ?? parent; }

        public void Emit(BattleEvent item) { Emit(item, null); }

        public void Emit(BattleEvent item, HeroDefinition hero)
        {
            EmitCore(item, hero, fallbackFeedback.Observe(item), 0);
        }

        public void Emit(BattleEvent item, HeroDefinition hero, BattleFeedbackResult feedback)
        {
            EmitCore(item, hero, feedback, Mathf.Max(0, gameTime - item.OccurredAt));
        }

        private void EmitCore(BattleEvent item, HeroDefinition hero, BattleFeedbackResult feedback, float age)
        {
            if (item == null) return;
            eventAge = age;
            switch (item.Kind)
            {
                case "basic_attack":
                    // 普攻单独表现，不复用吕布扇面、张飞怒喝等特色技能几何。
                    Projectile(item.Position, item.Target, UiFactory.Paper, .18f, 19);
                    break;
                case "attack":
                    if (IsRepresentative(hero) && item.AttackSequenceId > 0) break;
                    AttackCue(item, hero);
                    break;
                case "skill_cast":
                    RepresentativeCast(item, hero);
                    break;
                case "hit":
                    if (item.Amount > 0 && AllowCue("damage", item.Position, .14f))
                        Floating(item.Position, Mathf.CeilToInt(item.Amount).ToString(), UiFactory.Paper, .6f, false, true);
                    break;
                case "recruit":
                    Sequence("recruit", item.Position, .48f, 160, true);
                    Floating(item.Position, "入阵", UiFactory.Gold, .85f, true);
                    break;
                case "upgrade":
                    Ring(item.Position, 108, .7f, UiFactory.Gold, true);
                    break;
                case "skill_charge":
                    ChargeCue(item);
                    break;
                case "faction_hit":
                    if (AllowCue("faction", item.Target, .8f)) Ring(item.Target, 46, .22f, BattlePalette.Jade, false);
                    break;
                case "fire":
                    if (hero != null && hero.Skill == "fire") break;
                    bool explosion = !string.IsNullOrEmpty(item.Text) && item.Text.IndexOf("引爆", StringComparison.Ordinal) >= 0;
                    if (AllowCue("fire", item.Target, .25f))
                        Sequence("fire", item.Target, explosion ? .64f : Mathf.Min(3, Mathf.Max(.64f, item.Amount)), explosion ? 145 : 112);
                    break;
                case "stun":
                    if (AllowCue("stun", item.Position, .8f))
                    {
                        Ring(item.Position, 94, .42f, UiFactory.Gold, true);
                        Floating(item.Position, "震慑", UiFactory.Gold, .75f, true);
                    }
                    break;
                case "slow":
                    if (AllowCue("slow", item.Target, .9f))
                    {
                        Ring(item.Target, 96, .55f, UiFactory.Hex("9FD8CB"), true);
                        Floating(item.Target, "迟滞", UiFactory.Hex("C5E6DC"), .7f, true);
                    }
                    break;
                case "boss_warning": Sequence("boss_warning", item.Position, .8f, 240, true, 4); break;
                case "boss_phase":
                    Sequence("boss_warning", item.Position, .8f, 260, true, 4);
                    Floating(item.Position, "战意激昂", UiFactory.Red, 1.2f, true);
                    break;
                case "kill":
                    if (!feedback.Accepted) break;
                    KillCue(item, feedback);
                    if (AllowCue("reward", item.Position, .2f))
                        Floating(item.Position, item.Amount > 0 ? "+" + Mathf.RoundToInt(item.Amount) : "击破", UiFactory.Gold, .75f, false, true);
                    break;
                case "leak":
                    if (AllowCue("leak", item.Position, .65f)) Floating(item.Position, "城防 -" + Mathf.RoundToInt(item.Amount), UiFactory.Red, 1.2f, true);
                    break;
                case "notice":
                    if (!string.IsNullOrEmpty(item.Text) && item.Text.EndsWith("登场", StringComparison.Ordinal))
                        Sequence("boss_warning", item.Position, .8f, 240, true);
                    break;
                case "link_created": case "link_replaced": case "link_hit":
                    LinkCue(item);
                    break;
            }
            eventAge = 0;
        }

        private void AttackCue(BattleEvent item, HeroDefinition hero)
        {
            string skill = hero == null ? "" : hero.Skill;
            string heroId = hero == null ? item.HeroId : hero.Id;
            float next;
            float cueAt = gameTime - eventAge;
            float interval = skill == "slow" ? .18f : skill == "stun" ? .2f : .08f;
            if (!string.IsNullOrEmpty(heroId))
            {
                bool sharedTrajectory = skill == "cleave" || skill == "pierce" || skill == "slow" || skill == "stun";
                string cueKey = heroId;
                if (!sharedTrajectory)
                    cueKey += item.TargetEnemyId > 0 ? ":" + item.TargetEnemyId : ":" + item.Target.X + ":" + item.Target.Y;
                if (nextAttackCues.TryGetValue(cueKey, out next) && cueAt < next) return;
                nextAttackCues[cueKey] = cueAt + interval;
            }
            switch (skill)
            {
                case "focus":
                    Sequence("slash", new Point2(item.Target.X, item.Target.Y - 34), .28f, 104);
                    break;
                case "cleave":
                    AttackArea(item, hero, true, new Color(1, .71f, .29f, .09f));
                    Sequence("slash", new Point2(item.Target.X, item.Target.Y - 34), .28f, 140);
                    break;
                case "pierce":
                    AttackArea(item, hero, false, new Color(.64f, .93f, .91f, .19f));
                    Sequence("slash", new Point2(item.Target.X, item.Target.Y - 32), .22f, 68);
                    break;
                case "knockback":
                    Vector2 direction = new Vector2(item.Target.X - item.Position.X, item.Target.Y - item.Position.Y).normalized;
                    Point2 end = new Point2(item.Target.X + direction.x * 40, item.Target.Y + direction.y * 40);
                    Streak(item.Position, end, new Color(.8f, .94f, .93f, .88f), .2f, 5);
                    Sequence("slash", new Point2(item.Target.X, item.Target.Y - 32), .22f, 68);
                    Ring(item.Target, 48, .22f, UiFactory.Gold, false);
                    break;
                case "fire": case "spread_fire":
                    Projectile(item.Position, item.Target, new Color(1, .62f, .28f, 1), .22f, 17);
                    break;
                case "slow":
                    Ring(item.Position, 82, .48f, UiFactory.Hex("9FD8CB"), false);
                    break;
                case "stun":
                    Ring(item.Position, (item.SkillRange > 0 ? item.SkillRange : hero.Range) * 2, .5f, UiFactory.Gold, true);
                    break;
                case "assist":
                    if (!string.IsNullOrEmpty(item.TriggerHeroId)) AssistCue(item);
                    else Projectile(item.Position, item.Target, UiFactory.Paper, .19f, 30);
                    break;
                case "rain":
                    Projectile(new Point2(item.Target.X - 18, item.Target.Y - 155), item.Target, UiFactory.Gold, .22f, 38);
                    Ring(item.Target, 55, .28f, UiFactory.Gold, false);
                    break;
                case "sniper": case "twin": case "execute":
                    Projectile(item.Position, item.Target, UiFactory.Paper, .19f, 30);
                    break;
                default:
                    Projectile(item.Position, item.Target, UiFactory.Gold, .2f, 18);
                    break;
            }
        }

        private void AttackArea(BattleEvent item, HeroDefinition hero, bool cone, Color color, bool outlineOnly = false)
        {
            Effect effect = Take(.30f, cone);
            if (effect == null) return;
            if (effect.Area == null)
            {
                RectTransform shape = ui.Rect(effect.Root, "瞬时攻击范围", 0, 0, 1, 1);
                effect.Area = shape.gameObject.AddComponent<BattleAttackGraphic>();
            }
            effect.Color = color;
            effect.Area.color = color;
            effect.Area.gameObject.SetActive(true);
            int tier = Mathf.Max(1, item.HeroLevel);
            HeroGrowthProfile growth = HeroGrowth.Resolve(hero, tier);
            effect.Area.Configure(cone, item.SkillRange > 0 ? item.SkillRange : BattleSession.GetMaximumRange(hero, tier),
                new Vector2(item.Target.X - item.Position.X, item.Position.Y - item.Target.Y),
                cone ? (growth.FullCircle ? Mathf.PI : tier >= 2 ? Mathf.PI * .5f : Mathf.Acos(.3f)) : 0,
                growth.LineWidth, outlineOnly);
            UiFactory.Move(effect.Root, item.Position.X, item.Position.Y);
        }

        private void KillCue(BattleEvent item, BattleFeedbackResult feedback)
        {
            Ring(item.Position, 48, .24f, UiFactory.Gold, false);
            if (feedback.Tier == 0 || eventAge >= .8f) return;

            Point2 center = new Point2(Mathf.Clamp(feedback.Center.X, 160, 1760), Mathf.Clamp(feedback.Center.Y, 220, 900));
            if (feedback.CrossedTier && feedback.Tier >= 2)
                Ring(center, feedback.Tier == 3 ? 96 : 72, .3f, UiFactory.Gold, false);
            if (killCombo == null) killCombo = Floating(center, "连破 ×" + feedback.Count, UiFactory.Gold, .8f);
            if (killCombo == null) return;
            killCombo.Age = eventAge;
            killCombo.Label.text = "连破 ×" + feedback.Count;
            killCombo.Label.fontSize = feedback.Tier == 3 ? 37 : feedback.Tier == 2 ? 33 : 29;
            // 最高档中文字体的实际行高为54，保留上下余量避免裁切。
            killCombo.Label.rectTransform.sizeDelta = new Vector2(210, 64);
            killCombo.Pop = feedback.CrossedTier ? (feedback.Tier == 3 ? .14f : feedback.Tier == 2 ? .10f : .06f) : 0;
            killCombo.Root.localScale = Vector3.one;
            killCombo.Label.color = UiFactory.Gold;
            if (killCombo.LabelOutline == null) killCombo.LabelOutline = killCombo.Label.gameObject.AddComponent<Outline>();
            killCombo.LabelOutline.effectColor = new Color(.06f, .12f, .1f, 1);
            killCombo.LabelOutline.effectDistance = new Vector2(1.25f, -1.25f);
            killCombo.LabelOutline.useGraphicAlpha = true;
            killCombo.LabelOutline.enabled = true;
            UiFactory.Move(killCombo.Label.rectTransform, -105, killCombo.FloatY);
            UiFactory.Move(killCombo.Root, center.X, center.Y);
        }

        // 同一小片区域限频，时钟为有效实秒，暂停冻结且不随进军倍率改变。
        private bool AllowCue(string kind, Point2 position, float interval)
        {
            float cueAt = gameTime - eventAge;
            foreach (CueStamp cue in recentCues)
                if (cue.Kind == kind && Point2.Distance(cue.Position, position) < 78 && cueAt < cue.Until) return false;
            if (recentCues.Count >= 64) recentCues.RemoveAt(0);
            recentCues.Add(new CueStamp { Kind = kind, Position = position, Until = cueAt + interval });
            return true;
        }

        private Effect Take(float duration, bool important = false, int priority = 0)
        {
            if (eventAge >= duration) return null;
            int effectivePriority = priority > 0 ? priority : important ? 2 : 0;
            if (active.Count >= MaxEffects)
            {
                if (!important) return null;
                int expendable = active.FindIndex(item => item.Number);
                if (expendable < 0) expendable = active.FindIndex(item => !item.Important);
                if (expendable < 0) expendable = active.FindIndex(item => item.Priority < effectivePriority);
                if (expendable < 0) return null;
                Release(expendable);
            }
            Effect effect;
            if (pool.Count > 0) effect = pool.Pop();
            else
            {
                RectTransform root = ui.Rect(parent, "短暂反馈", 0, 0, 0, 0);
                Image image = ui.Circle(root, "图形", 0, 0, 150, 150, Color.white, true);
                Text label = ui.Label(root, "", -105, -80, 210, 48, 25, UiFactory.Paper, TextAnchor.MiddleCenter);
                Image head = ui.Box(root, "箭镞", -3, -3, 6, 6, Color.white, false);
                head.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                effect = new Effect { Root = root, Image = image, Label = label, Head = head, RingSprite = image.sprite };
            }
            effect.Root.SetParent(parent, false);
            effect.Root.gameObject.SetActive(true);
            effect.Root.SetAsLastSibling();
            effect.Root.localScale = Vector3.one;
            effect.Root.localRotation = Quaternion.identity;
            effect.Image.gameObject.SetActive(false);
            effect.Label.gameObject.SetActive(false);
            effect.Head.gameObject.SetActive(false);
            if (effect.Area != null)
            {
                effect.Area.gameObject.SetActive(false);
                effect.Area.Configure(false, 0, Vector2.right);
                effect.Area.color = Color.clear;
            }
            if (effect.Field != null) effect.Field.gameObject.SetActive(false);
            effect.Label.fontSize = 25;
            if (effect.LabelOutline != null) effect.LabelOutline.enabled = false;
            effect.Label.rectTransform.sizeDelta = new Vector2(210, 48);
            effect.Label.text = "";
            effect.Image.rectTransform.localRotation = Quaternion.identity;
            effect.Image.rectTransform.pivot = new Vector2(0, 1);
            effect.Image.sprite = null;
            effect.Frames = null;
            effect.Age = eventAge;
            effect.Duration = duration;
            effect.FrameDuration = duration;
            effect.Floating = false;
            effect.Important = important;
            effect.Priority = effectivePriority;
            effect.Charging = false;
            effect.Number = false;
            effect.Projectile = false;
            effect.Expanding = false;
            effect.Persistent = false;
            effect.FieldId = 0;
            effect.Pop = 0;
            active.Add(effect);
            return effect;
        }

        private void Sequence(string name, Point2 point, float duration, float size, bool important = false, int priority = 0)
        {
            Sprite[] frames = Frames(name);
            if (frames == null || frames.Length == 0) return;
            Effect effect = Take(duration, important, priority);
            if (effect == null) return;
            effect.Frames = frames;
            effect.FrameDuration = name == "fire" ? .64f : duration;
            effect.Image.gameObject.SetActive(true);
            effect.Image.sprite = frames[0];
            effect.Image.color = Color.white;
            effect.Image.rectTransform.sizeDelta = new Vector2(size, size);
            UiFactory.Move(effect.Image.rectTransform, -size * .5f, -size * (name == "slash" ? .5f : .78125f));
            UiFactory.Move(effect.Root, point.X, point.Y);
        }

        private void Streak(Point2 from, Point2 to, Color color, float duration, float width)
        {
            Effect effect = Take(duration);
            if (effect == null) return;
            UiFactory.Move(effect.Root, from.X, from.Y - 64);
            Vector2 delta = new Vector2(to.X - from.X, to.Y - from.Y + 23);
            effect.Image.gameObject.SetActive(true);
            effect.Image.color = color;
            effect.Color = color;
            effect.Image.rectTransform.sizeDelta = new Vector2(delta.magnitude, width);
            effect.Image.rectTransform.pivot = new Vector2(0, .5f);
            UiFactory.Move(effect.Image.rectTransform, 0, 0);
            effect.Image.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void Projectile(Point2 from, Point2 to, Color color, float duration, float length, bool important = false)
        {
            Effect effect = Take(duration, important);
            if (effect == null) return;
            effect.Projectile = true;
            effect.Color = color;
            effect.Start = new Vector2(from.X, from.Y - 64);
            effect.End = new Vector2(to.X, to.Y - 38);
            Vector2 delta = effect.End - effect.Start;
            effect.Root.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            effect.Image.gameObject.SetActive(true);
            effect.Image.color = color;
            effect.Image.rectTransform.sizeDelta = new Vector2(length, 3);
            UiFactory.Move(effect.Image.rectTransform, -length, -1.5f);
            effect.Head.gameObject.SetActive(true);
            effect.Head.color = color;
            UiFactory.Move(effect.Root, effect.Start.x, effect.Start.y);
        }

        private void Ring(Point2 position, float size, float duration, Color color, bool important)
        {
            Effect effect = Take(duration, important);
            if (effect == null) return;
            effect.Expanding = true;
            effect.Size = size;
            effect.Color = new Color(color.r, color.g, color.b, .75f);
            effect.Image.gameObject.SetActive(true);
            effect.Image.sprite = effect.RingSprite;
            effect.Image.color = effect.Color;
            effect.Image.rectTransform.sizeDelta = new Vector2(size * .35f, size * .35f);
            UiFactory.Move(effect.Image.rectTransform, -size * .175f, -size * .175f);
            UiFactory.Move(effect.Root, position.X, position.Y);
        }

        private Effect Floating(Point2 position, string value, Color color, float duration, bool important = false, bool number = false)
        {
            int numberCount = 0;
            foreach (Effect item in active) if (item.Number) numberCount++;
            if (number && numberCount >= MaxNumbers) return null;
            Effect effect = Take(duration, important);
            if (effect == null) return null;
            effect.Floating = true;
            effect.Number = number;
            effect.Color = color;
            effect.Label.gameObject.SetActive(true);
            effect.Label.text = value;
            effect.Label.color = color;
            effect.Label.fontSize = number ? 21 : 25;
            effect.FloatX = number ? (numberCount % 3 - 1) * 18 : 0;
            UiFactory.Move(effect.Label.rectTransform, -105 + effect.FloatX, number ? -84 : -106);
            effect.FloatY = number ? -84 : -106;
            UiFactory.Move(effect.Root, position.X, position.Y);
            return effect;
        }

        private void Release(int index)
        {
            Effect effect = active[index];
            if (effect.Persistent) liveFields.Remove(effect.FieldId);
            if (effect == killCombo) killCombo = null;
            effect.Root.gameObject.SetActive(false);
            active.RemoveAt(index);
            pool.Push(effect);
        }

        public void Advance(float gameDelta)
        {
            if (gameDelta <= 0) return;
            gameTime += gameDelta;
            for (int i = recentCues.Count - 1; i >= 0; i--)
                if (gameTime >= recentCues[i].Until) recentCues.RemoveAt(i);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Effect effect = active[i];
                if (effect.Persistent)
                {
                    if (gameTime >= effect.Duration) Release(i);
                    continue;
                }
                effect.Age += gameDelta;
                if (effect.Age >= effect.Duration)
                {
                    Release(i);
                    continue;
                }
                float progress = effect.Age / effect.Duration;
                if (effect.Frames != null)
                {
                    effect.Image.sprite = effect.Frames[Mathf.FloorToInt(effect.Age / effect.FrameDuration * effect.Frames.Length) % effect.Frames.Length];
                    effect.Image.color = new Color(1, 1, 1, 1 - Mathf.InverseLerp(.78f, 1, progress));
                }
                else
                {
                    Color color = effect.Color;
                    color.a *= 1 - progress;
                    if (effect.Floating)
                    {
                        effect.Label.color = color;
                        effect.Root.localScale = Vector3.one * (1 + effect.Pop * Mathf.Sin(Mathf.Clamp01(effect.Age / .18f) * Mathf.PI));
                        UiFactory.Move(effect.Label.rectTransform, -105 + effect.FloatX, effect.FloatY - progress * 32);
                    }
                    else
                    {
                        if (effect.Area != null && effect.Area.gameObject.activeSelf) effect.Area.color = color;
                        effect.Image.color = color;
                        if (effect.Projectile)
                        {
                            Vector2 position = Vector2.Lerp(effect.Start, effect.End, progress);
                            UiFactory.Move(effect.Root, position.x, position.y);
                            effect.Head.color = color;
                        }
                        if (effect.Expanding)
                        {
                            float size = effect.Size * (.35f + .65f * progress);
                            effect.Image.rectTransform.sizeDelta = new Vector2(size, size);
                            UiFactory.Move(effect.Image.rectTransform, -size * .5f, -size * .5f);
                        }
                        if (effect.Charging)
                        {
                            float size = Mathf.Lerp(152, 74, progress);
                            effect.Image.rectTransform.sizeDelta = new Vector2(size, size);
                            UiFactory.Move(effect.Image.rectTransform, -size * .5f, -size * .5f);
                            effect.Image.color = new Color(1, .73f, .28f, Mathf.Lerp(.35f, .95f, progress));
                        }
                    }
                }
            }
        }

        private Sprite[] Frames(string name)
        {
            Sprite[] result;
            if (atlases.TryGetValue(name, out result)) return result;
            Texture2D texture = Resources.Load<Texture2D>("Game/Effects/" + name + "_atlas");
            if (texture == null)
            {
                Sprite atlasSprite = Resources.Load<Sprite>("Game/Effects/" + name + "_atlas");
                if (atlasSprite != null) texture = atlasSprite.texture;
            }
            if (texture == null) { atlases[name] = null; return null; }
            int size = texture.height;
            int count = texture.width / size;
            result = new Sprite[count];
            for (int i = 0; i < count; i++)
                result[i] = Sprite.Create(texture, new Rect(i * size, 0, size, size), new Vector2(.5f, .5f), 100);
            atlases[name] = result;
            return result;
        }

        public void Clear()
        {
            for (int i = active.Count - 1; i >= 0; i--) Release(i);
            recentCues.Clear();
            nextAttackCues.Clear();
            fallbackFeedback.Clear();
            liveFields.Clear();
            currentFields.Clear();
            staleFields.Clear();
            feedbackHeroes.Clear();
            killCombo = null;
            gameTime = 0;
        }

        public void Dispose()
        {
            Clear();
            foreach (Sprite[] frames in atlases.Values)
                if (frames != null) foreach (Sprite sprite in frames)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                    else UnityEngine.Object.DestroyImmediate(sprite);
                }
            atlases.Clear();
            active.Clear();
            pool.Clear();
            recentCues.Clear();
        }

        private sealed class Effect
        {
            public RectTransform Root;
            public Image Image;
            public Image Head;
            public Text Label;
            public Outline LabelOutline;
            public BattleAttackGraphic Area;
            public BattleFieldGraphic Field;
            public Sprite RingSprite;
            public Sprite[] Frames;
            public Color Color;
            public float Age;
            public float Duration;
            public float FrameDuration;
            public bool Floating;
            public bool Important;
            public bool Number;
            public bool Projectile;
            public bool Expanding, Persistent;
            public bool Charging;
            public int Priority;
            public float Pop;
            public int FieldId;
            public float Size, FloatX, FloatY;
            public Vector2 Start, End;
        }

        private sealed class CueStamp
        {
            public string Kind;
            public Point2 Position;
            public float Until;
        }
    }
}
