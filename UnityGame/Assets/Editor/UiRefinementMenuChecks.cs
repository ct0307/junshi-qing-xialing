using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YanyiSandpan.Domain;
using YanyiSandpan.Presentation;

// 通过真实菜单按钮验收查看、入队和热区；记录仅写入隔离内存。
public static class UiRefinementMenuChecks
{
    public static void Verify(GameDatabase database)
    {
        using (var scene = new MenuScene(database))
        {
            LevelDefinition level = database.FindLevel("huangjin") ?? database.Levels[0];
            HeroDefinition[] available = database.Heroes.Where(hero => !level.BossHeroIds.Contains(hero.Id)).Take(2).ToArray();
            Require(available.Length == 2, "选将验收至少需要两名可用武将");
            VerifyFocusAndMembership(scene, level, available[0], available[1]);
            VerifyTierActivation(scene, level, available[0], available[1]);
            VerifyTouchAreas(scene, level, database);
        }
        Debug.Log("菜单精修检查通过：查看描边与入队独立、三阶激活、25卡88×88热区、文字边界及相邻射线");
    }

    private static void VerifyFocusAndMembership(MenuScene scene, LevelDefinition level,
        HeroDefinition first, HeroDefinition second)
    {
        scene.Flow.ShowRoster(level);
        Button firstCard = NamedButton(scene.Root, "选将卡_" + first.Id);
        Button secondCard = NamedButton(scene.Root, "选将卡_" + second.Id);
        Button firstChoice = NamedButton(scene.Root, "选择_" + first.Id);
        Button secondChoice = NamedButton(scene.Root, "选择_" + second.Id);
        Color emptyColor = firstCard.GetComponent<Image>().color;
        firstCard.onClick.Invoke();
        Require(HasCount(scene.Root, 0), "查看武将改变了入队人数");
        Require(Focused(firstCard), "查看武将缺少独立且启用的描边");
        Require(!Focused(secondCard), "查看描边错误应用到其他武将");
        Require(firstCard.GetComponent<Image>().color == emptyColor && ChoiceText(firstChoice) == "+",
            "查看状态占用了入队底色或勾选状态");

        firstChoice.onClick.Invoke();
        Color selectedColor = firstCard.GetComponent<Image>().color;
        Require(HasCount(scene.Root, 1) && ChoiceText(firstChoice) == "✓" && selectedColor != emptyColor,
            "加入武将未独立更新人数、勾选和底色");
        Require(Focused(firstCard), "加入武将丢失了当前查看描边");
        secondCard.onClick.Invoke();
        Require(!Focused(firstCard) && Focused(secondCard), "查看另一名武将未独立移动描边");
        Require(HasCount(scene.Root, 1) && firstCard.GetComponent<Image>().color == selectedColor &&
            ChoiceText(firstChoice) == "✓" && ChoiceText(secondChoice) == "+",
            "切换查看武将改变了既有阵容状态");

        firstCard.onClick.Invoke();
        secondChoice.onClick.Invoke();
        Require(HasCount(scene.Root, 2) && Focused(firstCard) && !Focused(secondCard),
            "独立加入按钮擅自切换了查看焦点");
        firstChoice.onClick.Invoke();
        Require(HasCount(scene.Root, 1) && Focused(firstCard) && ChoiceText(firstChoice) == "+" &&
            firstCard.GetComponent<Image>().color == emptyColor, "移出武将破坏了独立的查看状态");
        NamedButton(scene.Root, "清空阵容").onClick.Invoke();
        Require(HasCount(scene.Root, 0) && Focused(firstCard), "清空阵容错误清除了查看焦点");
    }

    private static void VerifyTierActivation(MenuScene scene, LevelDefinition level,
        HeroDefinition first, HeroDefinition second)
    {
        scene.Flow.ShowRoster(level);
        Button card = NamedButton(scene.Root, "选将卡_" + first.Id);
        card.onClick.Invoke();
        CheckActiveTier(scene.Root, 1);
        for (int tier = 1; tier <= 3; tier++)
        {
            NamedButton(scene.Root, "选将成长" + tier).onClick.Invoke();
            CheckActiveTier(scene.Root, tier);
            Text detail = scene.Root.GetComponentsInChildren<Text>().First(text => text.name == "选将详情");
            Require(detail.text.StartsWith(first.Name + " · ", StringComparison.Ordinal) && detail.text.Contains(first.Role + " · " + tier + "阶") &&
                detail.text.Contains(BattleSession.GetGrowthDescription(first, tier)), "激活阶数与实际成长详情不一致");
            Require(HasCount(scene.Root, 0) && Focused(card), "查看成长错误改变了阵容或武将焦点");
        }
        NamedButton(scene.Root, "选将卡_" + second.Id).onClick.Invoke();
        CheckActiveTier(scene.Root, 1);
    }

    private static void CheckActiveTier(RectTransform root, int activeTier)
    {
        for (int tier = 1; tier <= 3; tier++)
        {
            Button button = NamedButton(root, "选将成长" + tier);
            bool active = tier == activeTier;
            Require(button.GetComponent<Image>().color == (active ? BattlePalette.Jade : BattlePalette.Mist),
                "成长" + activeTier + "阶查看时，" + tier + "阶按钮没有明确的激活底色");
            Require(button.GetComponentInChildren<Text>().color == (active ? BattlePalette.Silk : BattlePalette.Ink),
                "成长阶数文字未随激活底色保持对比");
        }
    }

    private static void VerifyTouchAreas(MenuScene scene, LevelDefinition level, GameDatabase database)
    {
        scene.Flow.ShowRoster(level);
        Require(scene.Root.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("选将卡_")) == 25,
            "选将应保留25张卡片");
        scene.PrepareRaycasts();
        foreach (HeroDefinition hero in database.Heroes)
        {
            Button card = NamedButton(scene.Root, "选将卡_" + hero.Id);
            Button choice = NamedButton(scene.Root, "选择_" + hero.Id);
            RectTransform cardRect = (RectTransform)card.transform;
            RectTransform choiceRect = (RectTransform)choice.transform;
            Rect area = BoundsIn(choiceRect, cardRect);
            Require(area.width >= 87.9f && area.height >= 87.9f, hero.Name + "加入按钮热区不足88×88");
            Require(Contains(cardRect.rect, area), hero.Name + "加入按钮越出所属卡片");
            CheckCardText(card, choice, cardRect, area, hero.Name);
            // 覆盖矩形四角及中心，避免只有图标中央能点击。
            foreach (Vector2 fraction in new[] { new Vector2(.1f, .1f), new Vector2(.9f, .1f),
                new Vector2(.1f, .9f), new Vector2(.9f, .9f), new Vector2(.5f, .5f) })
            {
                Vector3 point = choiceRect.TransformPoint(new Vector3(
                    Mathf.Lerp(choiceRect.rect.xMin, choiceRect.rect.xMax, fraction.x),
                    Mathf.Lerp(choiceRect.rect.yMin, choiceRect.rect.yMax, fraction.y), 0));
                Require(scene.Hit(point) == choice, hero.Name + "加入按钮矩形热区被其他控件遮挡");
            }
            // 按钮左侧仍属于卡片查看区，不能扩大到武将说明上。
            Vector3 outside = cardRect.TransformPoint(new Vector3(area.xMin - 2, area.center.y, 0));
            Require(scene.Hit(outside) == card, hero.Name + "加入热区侵入卡片查看区域");
        }
        NamedButton(scene.Root, "推荐阵容").onClick.Invoke();
        foreach (HeroDefinition hero in database.Heroes)
        {
            Button card = NamedButton(scene.Root, "选将卡_" + hero.Id);
            Button choice = NamedButton(scene.Root, "选择_" + hero.Id);
            CheckCardText(card, choice, (RectTransform)card.transform,
                BoundsIn((RectTransform)choice.transform, (RectTransform)card.transform), hero.Name + "入队后");
        }
        RectTransform first = (RectTransform)NamedButton(scene.Root, "选将卡_" + database.Heroes[0].Id).transform;
        RectTransform next = (RectTransform)NamedButton(scene.Root, "选将卡_" + database.Heroes[1].Id).transform;
        Vector3 gap = scene.Root.TransformPoint(new Vector3(
            (BoundsIn(first, scene.Root).xMax + BoundsIn(next, scene.Root).xMin) * .5f,
            BoundsIn(first, scene.Root).center.y, 0));
        Require(scene.Hit(gap) == null, "相邻选将卡间隙被加入热区覆盖");
    }

    private static void CheckCardText(Button card, Button choice, RectTransform cardRect, Rect area, string description)
    {
        UiTypographyChecks.Check(choice.GetComponentInChildren<Text>(), description + "加入按钮文字", true);
        var textAreas = new List<Rect>();
        foreach (Text label in card.GetComponentsInChildren<Text>())
        {
            if (string.IsNullOrEmpty(label.text) || label.transform.IsChildOf(choice.transform)) continue;
            Rect textArea = BoundsIn(label.rectTransform, cardRect);
            Require(Contains(cardRect.rect, textArea), description + "文字越出卡片");
            Require(!textArea.Overlaps(area), description + "文字与加入热区重叠：" + label.text);
            Require(!textAreas.Any(previous => previous.Overlaps(textArea)), description + "卡片正文之间重叠：" + label.text);
            UiTypographyChecks.Check(label, description + label.text, true);
            textAreas.Add(textArea);
        }
    }

    private static Rect BoundsIn(RectTransform subject, RectTransform relativeTo)
    {
        var corners = new Vector3[4];
        subject.GetWorldCorners(corners);
        Vector3 a = relativeTo.InverseTransformPoint(corners[0]);
        Vector3 b = relativeTo.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }

    private static bool Contains(Rect outer, Rect inner)
    {
        return inner.xMin >= outer.xMin - .1f && inner.xMax <= outer.xMax + .1f &&
            inner.yMin >= outer.yMin - .1f && inner.yMax <= outer.yMax + .1f;
    }

    private static bool Focused(Button button)
    { Outline outline = button.GetComponent<Outline>(); return outline != null && outline.enabled && outline.effectColor.a > .5f; }

    private static bool HasCount(RectTransform root, int count)
    { return root.GetComponentsInChildren<Text>().Any(text => text.text == "已选 " + count + " / 10"); }

    private static string ChoiceText(Button button) { return button.GetComponentInChildren<Text>().text; }
    private static Button NamedButton(RectTransform root, string name)
    { return root.GetComponentsInChildren<Button>().First(button => button.name == name); }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private sealed class MenuScene : IDisposable
    {
        public readonly RectTransform Root;
        public readonly GameFlow Flow;
        private readonly GameObject canvasObject;
        private readonly Camera camera;
        private readonly GraphicRaycaster raycaster;
        private readonly RenderTexture renderTarget;

        public MenuScene(GameDatabase database)
        {
            canvasObject = new GameObject("菜单精修隔离画布", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            camera = new GameObject("菜单精修射线相机", typeof(Camera)).GetComponent<Camera>();
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rect = (RectTransform)canvas.transform;
            rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(1920, 1080);
            camera.orthographic = true; camera.orthographicSize = 540; camera.aspect = 1920f / 1080;
            // 固定离屏尺寸，射线坐标不依赖批处理隐藏GameView的宽高。
            renderTarget = RenderTexture.GetTemporary(1920, 1080, 24);
            camera.targetTexture = renderTarget;
            camera.transform.position = new Vector3(960, -540, -100);
            camera.nearClipPlane = .1f; camera.farClipPlane = 200;
            canvas.worldCamera = camera;
            raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            var ui = new UiFactory(); Root = ui.Rect(rect, "菜单精修界面", 0, 0, 1920, 1080);
            var values = new Dictionary<string, int>();
            var progress = new GameProgress((key, fallback) => { int value; return values.TryGetValue(key, out value) ? value : fallback; },
                (key, value) => values[key] = value, () => { });
            Flow = new GameFlow(ui, Root, database, progress);
        }

        public void PrepareRaycasts()
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
        }

        public Button Hit(Vector3 worldPoint)
        {
            var pointer = new PointerEventData(EventSystem.current);
            pointer.position = camera.WorldToScreenPoint(worldPoint);
            var hits = new List<RaycastResult>();
            raycaster.Raycast(pointer, hits);
            return hits.Count == 0 ? null : hits[0].gameObject.GetComponentInParent<Button>();
        }

        public void Dispose()
        {
            Flow.Dispose(); UnityEngine.Object.DestroyImmediate(canvasObject);
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(renderTarget);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }
}
