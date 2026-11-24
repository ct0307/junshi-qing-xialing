using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YanyiSandpan.Presentation
{
    // 所有位置均为1920×1080画布坐标，左上角为原点。
    public sealed class UiFactory
    {
        public static readonly Color Ink = Hex("183F38");
        public static readonly Color Deep = Hex("123B33");
        public static readonly Color Panel = Hex("2D5544");
        public static readonly Color Paper = Hex("EFE8CD");
        public static readonly Color Gold = Hex("D8BC7A");
        public static readonly Color Muted = Hex("B9C9A5");
        public static readonly Color Red = Hex("B65F48");
        public static readonly Color Jade = Hex("79A17B");
        public readonly Font Font;
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static Sprite rounded;
        private static Sprite disc;
        private static Sprite ring;

        public UiFactory()
        {
            Font = Resources.Load<Font>("Game/Fonts/Chinese");
            if (Font == null) Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (rounded == null) rounded = MakeShape(false, false);
            if (disc == null) disc = MakeShape(true, false);
            if (ring == null) ring = MakeShape(true, true);
        }

        public static Color Hex(string value)
        {
            Color color;
            return ColorUtility.TryParseHtmlString("#" + value, out color) ? color : Color.white;
        }

        public RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        public Image Box(Transform parent, string name, float x, float y, float width, float height,
            Color color, bool round = true)
        {
            Image image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (round) { image.sprite = rounded; image.type = Image.Type.Sliced; }
            return image;
        }

        public Image Circle(Transform parent, string name, float x, float y, float width, float height,
            Color color, bool outline = false)
        {
            Image image = Box(parent, name, x, y, width, height, color, false);
            image.sprite = outline ? ring : disc;
            return image;
        }

        public Text Label(Transform parent, string value, float x, float y, float width, float height,
            int size = 26, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            Text text = Rect(parent, "文字", x, y, width, height).gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.color = color ?? Paper;
            text.alignment = align;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            // 中文字体的实际行高可能大于字号；为动态更新的空标签也预留完整一行。
            var settings = text.GetGenerationSettings(new Vector2(width, 0));
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            float lineHeight = text.cachedTextGeneratorForLayout.GetPreferredHeight("守关", settings) / text.pixelsPerUnit;
            text.rectTransform.sizeDelta = new Vector2(width, Mathf.Max(height, Mathf.Ceil(lineHeight) + 2));
            return text;
        }

        public UiButton Button(Transform parent, string value, float x, float y, float width, float height,
            Action action, Color? color = null, int fontSize = 28, bool playSound = true)
        {
            Image panel = Box(parent, value, x, y, width, height, color ?? Gold);
            panel.raycastTarget = true;
            Button button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1);
            colors.pressedColor = new Color(.78f, .81f, .75f, 1);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.63f, .66f, .61f, .85f);
            button.colors = colors;
            if (action != null) button.onClick.AddListener(() => { if (playSound) GameAudio.Button(); action(); });
            Text label = Label(panel.transform, value, 8, 0, width - 16, height, fontSize,
                color.HasValue && color.Value == Panel ? Paper : Ink, TextAnchor.MiddleCenter);
            return new UiButton(button, panel, label);
        }

        public Image Picture(Transform parent, string path, float x, float y, float width, float height)
        {
            Image image = Box(parent, "素材", x, y, width, height, Color.white, false);
            image.sprite = LoadSprite(path);
            image.preserveAspect = true;
            if (image.sprite == null) image.color = Color.clear;
            return image;
        }

        public Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Sprite sprite;
            if (sprites.TryGetValue(path, out sprite)) return sprite;
            sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(path);
                if (texture != null) sprite = Sprite.Create(texture,
                    new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            }
            sprites[path] = sprite;
            return sprite;
        }

        public void Border(Image image, Color color, float thickness = 2)
        {
            Outline border = image.GetComponent<Outline>();
            if (border == null) border = image.gameObject.AddComponent<Outline>();
            border.effectColor = color;
            border.effectDistance = new Vector2(thickness, -thickness);
            border.useGraphicAlpha = true;
        }

        public static void Move(RectTransform rt, float x, float y)
        {
            rt.anchoredPosition = new Vector2(x, -y);
        }

        public static void FillWidth(RectTransform rt, float width)
        {
            Vector2 size = rt.sizeDelta;
            size.x = Mathf.Max(0, width);
            rt.sizeDelta = size;
        }

        public void Line(Transform parent, Vector2 a, Vector2 b, Color color, float width = 3)
        {
            Vector2 delta = b - a;
            Image line = Box(parent, "连线", a.x, a.y, delta.magnitude, width, color, false);
            line.rectTransform.pivot = new Vector2(0, .5f);
            line.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        public static string PortraitPath(string id) { return "Game/Portraits/" + id; }

        private static Sprite MakeShape(bool circle, bool hollow)
        {
            int size = hollow ? 512 : 128;
            float center = size * .5f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = circle ? "程序圆形" : "程序圆角";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance;
                    if (circle) distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(center, center));
                    else
                    {
                        float dx = Mathf.Max(0, Mathf.Abs(x + .5f - 64) - 48);
                        float dy = Mathf.Max(0, Mathf.Abs(y + .5f - 64) - 48);
                        distance = Mathf.Sqrt(dx * dx + dy * dy) + 48;
                    }
                    float alpha = Mathf.Clamp01(center - distance);
                    if (hollow) alpha *= Mathf.Clamp01(distance - (center - 3));
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100,
                0, SpriteMeshType.FullRect, circle ? Vector4.zero : new Vector4(18, 18, 18, 18));
        }
    }

    public sealed class UiButton
    {
        public readonly Button Button;
        public readonly Image Background;
        public readonly Text Label;
        public RectTransform Rect { get { return Background.rectTransform; } }
        public UiButton(Button button, Image background, Text label)
        { Button = button; Background = background; Label = label; }
    }
}
