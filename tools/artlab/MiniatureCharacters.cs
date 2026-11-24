using System;
using System.Collections.Generic;
using UnityEngine;

// 只用于离线美术样例；人物朝负Z，根节点原点为台座底面中心。
public static class MiniatureCharacters
{
    public static GameObject Build(string heroId)
    {
        if (heroId != "guan_yu" && heroId != "zhao_yun")
            throw new ArgumentException("微缩武将样例仅支持关羽与赵云：" + heroId, "heroId");

        GameObject root = new GameObject(heroId == "guan_yu" ? "关羽_青绿微缩" : "赵云_青绿微缩");
        BuildPedestal(root.transform);
        Transform figure = MiniatureArt.Group(root.transform, "武将雕像", Vector3.zero);
        figure.localRotation = Quaternion.Euler(0, heroId == "guan_yu" ? -7 : 5, 0);
        if (heroId == "guan_yu") BuildGuanYu(figure);
        else BuildZhaoYun(figure);
        return root;
    }

    private static void BuildPedestal(Transform root)
    {
        Material ivory = MiniatureArt.Mat("DED7BF", 0, .26f);
        Material jade = MiniatureArt.Mat("477A70", 0, .32f);
        Material edge = MiniatureArt.Mat("A49465", .42f, .35f);
        MiniatureArt.Lathe(root, "象牙倒角台座", new[] {
            new Vector2(0, 0), new Vector2(.63f, 0), new Vector2(.70f, .034f),
            new Vector2(.715f, .075f), new Vector2(.69f, .115f), new Vector2(.64f, .13f),
            new Vector2(0, .13f) }, Vector3.zero, ivory, 64);
        MiniatureArt.Lathe(root, "青玉台面", new[] {
            new Vector2(0, 0), new Vector2(.64f, 0), new Vector2(.67f, .02f),
            new Vector2(.65f, .055f), new Vector2(.61f, .068f), new Vector2(0, .068f) },
            new Vector3(0, .12f, 0), jade, 64);
        MiniatureArt.Lathe(root, "台座嵌金细线", new[] {
            new Vector2(.699f, .058f), new Vector2(.716f, .058f),
            new Vector2(.716f, .069f), new Vector2(.699f, .069f) },
            Vector3.zero, edge, 64);
        // 前缘只保留三处短线装饰，缩小后仍以完整台座轮廓为主。
        for (int i = -1; i <= 1; i++)
        {
            float angle = Mathf.PI + i * .33f;
            Vector3 middle = new Vector3(Mathf.Sin(angle) * .706f, .095f, Mathf.Cos(angle) * .706f);
            MiniatureArt.Cylinder(root, "台缘玉纹" + i, middle - new Vector3(.024f, .009f, 0),
                middle + new Vector3(.024f, .009f, 0), .006f, edge, 8);
        }
    }

    private static void BuildGuanYu(Transform root)
    {
        Material robe = MiniatureArt.Mat("285E4C", 0, .10f);
        Material fold = MiniatureArt.Mat("4E8461", 0, .10f);
        Material darkGreen = MiniatureArt.Mat("183E35", 0, .10f);
        Material lining = MiniatureArt.Mat("793E36", 0, .10f);
        Material bronze = MiniatureArt.Mat("8E7952", .46f, .34f);
        Material gold = MiniatureArt.Mat("C5AA70", .52f, .39f);
        Material steel = MiniatureArt.Mat("AFC7C2", .58f, .43f);
        Material leather = MiniatureArt.Mat("343D33", 0, .20f);
        Material skin = MiniatureArt.Mat("A65B48", 0, .20f);
        Material skinShade = MiniatureArt.Mat("793D32", 0, .20f);
        Material hair = MiniatureArt.Mat("222C28", 0, .18f);
        Material hairLight = MiniatureArt.Mat("4A4836", 0, .18f);

        BuildBoot(root, "左靴", new Vector3(-.25f, .36f, -.12f), -10, leather, bronze);
        BuildBoot(root, "右靴", new Vector3(.25f, .36f, .07f), 8, leather, bronze);
        PleatedRobe(root, "绿袍垂地衣摆", new[] { .39f, .55f, .83f, 1.08f, 1.33f, 1.48f },
            new[] { .57f, .60f, .55f, .47f, .38f, .37f },
            new[] { .34f, .39f, .35f, .30f, .27f, .27f }, robe, .035f, -.025f);
        PleatedRobe(root, "上身交领绿袍", new[] { 1.34f, 1.57f, 1.79f, 1.96f, 2.06f },
            new[] { .36f, .39f, .45f, .43f, .23f },
            new[] { .27f, .31f, .30f, .25f, .19f }, robe, .013f, 0);
        ClothPanel(root, "暗红内衬", new Vector3(-.03f, 1.25f, -.395f),
            new[] { .22f, .28f, .30f, .30f }, new[] { 0f, -.23f, -.52f, -.79f }, lining, .018f, .05f, .09f);
        ClothPanel(root, "左襟厚褶", new Vector3(-.27f, 1.43f, -.435f),
            new[] { .20f, .24f, .28f, .25f }, new[] { 0f, -.29f, -.61f, -1.04f }, robe, .022f, -.08f, .09f, -.12f);
        ClothPanel(root, "右襟厚褶", new Vector3(.27f, 1.43f, -.435f),
            new[] { .20f, .23f, .26f, .24f }, new[] { 0f, -.28f, -.60f, -1.04f }, robe, .022f, .08f, .09f, .12f);
        Seam(root, "左衣缘", new[] { V(-.070f, 1.43f, -.421f), V(-.039f, 1.14f, -.451f),
            V(-.026f, .82f, -.481f), V(-.100f, .372f, -.511f) }, .013f, gold);
        Seam(root, "右衣缘", new[] { V(.070f, 1.43f, -.421f), V(.049f, 1.15f, -.451f),
            V(.046f, .83f, -.481f), V(.110f, .372f, -.511f) }, .013f, gold);

        FrontPlate(root, "护胸皮甲", V(0, 1.68f, -.29f), new[] {
            P(-.30f, -.24f), P(.29f, -.24f), P(.36f, .13f), P(.20f, .28f),
            P(-.19f, .28f), P(-.35f, .13f) }, .055f, darkGreen, gold, .014f);
        FrontPlate(root, "胸前铜护心", V(0, 1.76f, -.37f), new[] {
            P(-.15f, -.16f), P(.15f, -.16f), P(.21f, .04f), P(.10f, .19f),
            P(-.10f, .19f), P(-.21f, .04f) }, .038f, bronze, gold, .01f);
        Seam(root, "交领斜襟", new[] { V(-.20f, 2.05f, -.23f), V(-.08f, 1.91f, -.39f),
            V(.15f, 1.67f, -.39f), V(.28f, 1.46f, -.31f) }, .024f, fold);
        BuildBelt(root, 1.35f, darkGreen, bronze, gold);

        Sleeve(root, "持刀上臂", V(.41f, 1.94f, .0f), V(.70f, 1.58f, -.03f), .22f, .17f, robe);
        Sleeve(root, "持刀屈肘", V(.70f, 1.58f, -.03f), V(.77f, 1.76f, -.14f), .17f, .13f, robe);
        Sleeve(root, "左臂垂袖", V(-.40f, 1.94f, .02f), V(-.65f, 1.55f, -.08f), .22f, .19f, robe);
        Sleeve(root, "左手收于腹前", V(-.65f, 1.55f, -.08f), V(-.35f, 1.47f, -.39f), .18f, .12f, robe);
        MiniatureArt.Ellipsoid(root, "右肘连续布面", V(.70f, 1.58f, -.03f), V(.345f, .345f, .33f), robe);
        MiniatureArt.Ellipsoid(root, "左肘连续布面", V(-.65f, 1.55f, -.08f), V(.38f, .36f, .355f), robe);
        ShoulderArmor(root, "右肩吞口甲", V(.43f, 1.99f, -.015f), 1, bronze, gold, darkGreen);
        ShoulderArmor(root, "左肩吞口甲", V(-.43f, 1.99f, .005f), -1, bronze, gold, darkGreen);
        Bracer(root, "持刀腕甲", V(.76f, 1.70f, -.13f), .93f, bronze, gold);
        Bracer(root, "左腕甲", V(-.40f, 1.48f, -.34f), -.25f, bronze, gold);
        Hand(root, "握刀手", V(.77f, 1.78f, -.14f), skin, skinShade, true);
        Hand(root, "腹前左手", V(-.31f, 1.47f, -.44f), skin, skinShade, false);
        MiniatureArt.Ellipsoid(root, "颈部", V(0, 2.13f, 0), V(.29f, .35f, .28f), skin);

        Transform head = MiniatureArt.Group(root, "关羽头部", V(0, 2.40f, -.015f));
        head.localRotation = Quaternion.Euler(0, -5, -2);
        BuildFace(head, skin, skinShade, hair, true);
        MiniatureArt.Lathe(head, "青绿包巾", new[] { P(0, .26f), P(.41f, .26f), P(.49f, .35f),
            P(.43f, .50f), P(.29f, .58f), P(0, .61f) }, V(0, 0, .06f), robe, 40);
        Seam(head, "包巾前额金缘", new[] { V(-.38f, .29f, -.20f), V(-.21f, .32f, -.38f),
            V(0, .34f, -.43f), V(.21f, .32f, -.38f), V(.38f, .29f, -.20f) }, .017f, gold);
        FrontPlate(head, "额心玉扣", V(0, .35f, -.435f), new[] {
            P(-.055f, -.07f), P(.055f, -.07f), P(.072f, .012f), P(0, .084f), P(-.072f, .012f) },
            .02f, bronze, gold, .008f);
        ClothPanel(head, "包巾后垂", V(.30f, .26f, .24f), new[] { .12f, .11f, .075f },
            new[] { 0f, -.30f, -.64f }, darkGreen, .015f, .09f);

        // 长髯以一块连续体积托住轮廓，再以少数曲线分出髯束。
        FrontPlate(head, "美髯主体", V(0, -.25f, -.40f), new[] {
            P(-.27f, .07f), P(-.23f, -.24f), P(-.10f, -.48f), P(.025f, -.66f),
            P(.12f, -.48f), P(.24f, -.23f), P(.28f, .07f), P(.11f, .11f), P(-.11f, .11f) },
            .12f, hair, null, 0);
        for (int i = -2; i <= 2; i++)
        {
            float x = i * .073f;
            Seam(head, "长髯雕纹" + i, new[] { V(x, -.20f, -.52f), V(x * 1.05f, -.41f, -.57f),
                V(x * .80f + .012f, -.63f, -.53f), V(.025f + x * .23f, -.83f + Mathf.Abs(i) * .075f, -.46f) },
                .007f, hairLight);
        }
        Seam(head, "左八字髭", new[] { V(0, -.13f, -.50f), V(-.13f, -.15f, -.53f),
            V(-.23f, -.24f, -.50f) }, .033f, hair);
        Seam(head, "右八字髭", new[] { V(0, -.13f, -.50f), V(.13f, -.15f, -.53f),
            V(.23f, -.24f, -.50f) }, .033f, hair);
        BuildGlaive(root, gold, bronze, steel, darkGreen, lining);
    }

    private static void BuildZhaoYun(Transform root)
    {
        Material blue = MiniatureArt.Mat("3F6580", 0, .10f);
        Material blueShadow = MiniatureArt.Mat("253F55", 0, .10f);
        Material blueLight = MiniatureArt.Mat("7899AB", 0, .10f);
        Material white = MiniatureArt.Mat("E6E5D8", 0, .10f);
        Material whiteShadow = MiniatureArt.Mat("ACBFC0", 0, .10f);
        Material silver = MiniatureArt.Mat("B9CBD0", .52f, .38f);
        Material steelShade = MiniatureArt.Mat("697F8A", .43f, .33f);
        Material edge = MiniatureArt.Mat("DFE5D9", .58f, .43f);
        Material gold = MiniatureArt.Mat("BBA16B", .45f, .36f);
        Material leather = MiniatureArt.Mat("34464D", 0, .20f);
        Material skin = MiniatureArt.Mat("D3AE8C", 0, .20f);
        Material skinShade = MiniatureArt.Mat("936B55", 0, .20f);
        Material hair = MiniatureArt.Mat("303935", 0, .18f);
        Material red = MiniatureArt.Mat("B9483B", 0, .10f);
        Material redLight = MiniatureArt.Mat("DC7050", 0, .10f);

        BuildCape(root, white, whiteShadow, gold);
        BuildBoot(root, "前踏左靴", V(-.26f, .36f, -.19f), -12, leather, silver);
        BuildBoot(root, "后撑右靴", V(.25f, .36f, .09f), 10, leather, silver);
        PleatedRobe(root, "蓝衣下摆", new[] { .40f, .62f, .91f, 1.20f, 1.44f },
            new[] { .48f, .52f, .46f, .36f, .35f },
            new[] { .34f, .34f, .29f, .26f, .25f }, blue, .033f, -.035f);
        PleatedRobe(root, "蓝色战袍上身", new[] { 1.31f, 1.56f, 1.80f, 1.99f, 2.08f },
            new[] { .34f, .38f, .43f, .40f, .22f },
            new[] { .26f, .28f, .29f, .24f, .18f }, blue, .012f, 0);
        ClothPanel(root, "白色中垂战裙", V(-.055f, 1.32f, -.35f),
            new[] { .20f, .23f, .27f, .25f }, new[] { 0f, -.25f, -.55f, -.87f }, white, .022f, -.04f, .14f);
        Seam(root, "白袍左缘", new[] { V(-.255f, 1.32f, -.336f), V(-.289f, 1.07f, -.383f),
            V(-.343f, .77f, -.429f), V(-.345f, .432f, -.476f) }, .014f, gold);
        Seam(root, "白袍右缘", new[] { V(.145f, 1.32f, -.336f), V(.171f, 1.07f, -.383f),
            V(.197f, .77f, -.429f), V(.155f, .432f, -.476f) }, .014f, gold);
        FrontPlate(root, "银色护胸", V(0, 1.73f, -.28f), new[] {
            P(-.30f, -.27f), P(.29f, -.27f), P(.37f, .12f), P(.24f, .29f),
            P(.11f, .26f), P(0, .19f), P(-.11f, .26f), P(-.24f, .29f), P(-.37f, .12f) },
            .085f, silver, edge, .018f);
        FrontPlate(root, "护胸内嵌青玉", V(0, 1.77f, -.38f), new[] {
            P(-.12f, -.14f), P(.12f, -.14f), P(.18f, .07f), P(0, .18f), P(-.18f, .07f) },
            .032f, blueShadow, gold, .013f);
        Seam(root, "护胸压脊", new[] { V(0, 1.52f, -.40f), V(0, 1.70f, -.43f), V(0, 1.94f, -.39f) }, .013f, edge);
        BuildBelt(root, 1.35f, blueShadow, silver, gold);
        for (int side = -1; side <= 1; side += 2)
        {
            Transform hip = MiniatureArt.Group(root, side < 0 ? "左侧裙甲" : "右侧裙甲", V(side * .32f, 1.27f, -.19f));
            hip.localRotation = Quaternion.Euler(-3, side * -18, side * 10);
            for (int row = 0; row < 3; row++)
                FrontPlate(hip, "错层裙甲" + row, V(0, -.15f * row, -.015f * row), new[] {
                    P(-.145f, -.12f), P(.14f, -.12f), P(.18f, .07f), P(-.16f, .07f) },
                    .032f, row % 2 == 0 ? silver : steelShade, edge, .008f);
        }

        Sleeve(root, "执枪右上臂", V(.41f, 1.95f, 0), V(.66f, 1.58f, -.07f), .19f, .15f, blue);
        Sleeve(root, "执枪右小臂", V(.66f, 1.58f, -.07f), V(.71f, 1.53f, -.17f), .15f, .12f, blue);
        Sleeve(root, "前收左上臂", V(-.40f, 1.94f, 0), V(-.61f, 1.67f, -.13f), .19f, .16f, blue);
        Sleeve(root, "前收左小臂", V(-.61f, 1.67f, -.13f), V(-.33f, 1.54f, -.40f), .16f, .12f, blue);
        MiniatureArt.Ellipsoid(root, "左肘连续布面", V(-.61f, 1.67f, -.13f), V(.32f, .31f, .31f), blue);
        MiniatureArt.Ellipsoid(root, "右肘连续布面", V(.66f, 1.58f, -.07f), V(.30f, .30f, .29f), blue);
        ShoulderArmor(root, "银甲右肩", V(.42f, 1.99f, -.01f), 1, silver, edge, steelShade);
        ShoulderArmor(root, "银甲左肩", V(-.42f, 1.99f, .01f), -1, silver, edge, steelShade);
        Bracer(root, "执枪护腕", V(.70f, 1.57f, -.14f), .85f, silver, edge);
        Bracer(root, "左臂护腕", V(-.40f, 1.56f, -.34f), -.35f, silver, edge);
        Hand(root, "执枪右手", V(.70f, 1.51f, -.17f), skin, skinShade, true);
        Hand(root, "前收左手", V(-.29f, 1.53f, -.44f), skin, skinShade, false);
        MiniatureArt.Ellipsoid(root, "颈部", V(0, 2.12f, -.015f), V(.28f, .33f, .26f), skin);

        Transform head = MiniatureArt.Group(root, "赵云头部", V(0, 2.40f, -.045f));
        head.localRotation = Quaternion.Euler(0, -8, 1);
        BuildFace(head, skin, skinShade, hair, false);
        MiniatureArt.Lathe(head, "银盔穹顶", new[] {
            P(0, .24f), P(.43f, .24f), P(.49f, .33f), P(.46f, .45f),
            P(.35f, .59f), P(.19f, .67f), P(0, .71f) }, V(0, 0, .045f), silver, 48);
        Seam(head, "银盔眉檐", new[] { V(-.43f, .28f, -.18f), V(-.30f, .27f, -.36f),
            V(-.13f, .29f, -.44f), V(0, .36f, -.46f), V(.13f, .29f, -.44f),
            V(.30f, .27f, -.36f), V(.43f, .28f, -.18f) }, .023f, edge);
        FrontPlate(head, "盔额云纹脊", V(0, .47f, -.35f), new[] {
            P(-.065f, -.15f), P(.065f, -.15f), P(.095f, .045f), P(0, .22f), P(-.095f, .045f) },
            .055f, silver, edge, .012f);
        for (int side = -1; side <= 1; side += 2)
        {
            Transform guard = MiniatureArt.Group(head, side < 0 ? "左护颊" : "右护颊", V(side * .38f, -.07f, -.15f));
            guard.localRotation = Quaternion.Euler(0, side * -30, side * -7);
            FrontPlate(guard, "护颊叶片", V(0, 0, 0), new[] { P(-.075f, -.20f), P(.075f, -.14f),
                P(.09f, .20f), P(-.085f, .23f) }, .035f, steelShade, edge, .01f);
        }
        Seam(head, "盔顶蓝穗", new[] { V(0, .65f, .08f), V(.005f, .78f, .17f),
            V(.02f, .74f, .32f), V(.035f, .56f, .44f) }, .052f, blue);
        Seam(root, "领口布边", new[] { V(-.17f, 2.08f, -.20f), V(0, 2.03f, -.29f), V(.17f, 2.08f, -.20f) }, .022f, whiteShadow);
        Seam(root, "袖侧衣褶", new[] { V(-.58f, 1.80f, -.19f), V(-.57f, 1.66f, -.29f), V(-.43f, 1.57f, -.39f) }, .012f, blueLight);
        BuildSpear(root, leather, silver, edge, gold, red, redLight);
    }

    private static void BuildFace(Transform head, Material skin, Material shade, Material hair, bool mature)
    {
        MiniatureArt.Ellipsoid(head, "头面体积", V(0, 0, 0), V(.91f, .99f, .82f), skin, 40, 28);
        Material eyeWhite = MiniatureArt.Mat("E6DDC5", 0, .20f);
        Material iris = MiniatureArt.Mat("575442", 0, .20f);
        Material pupil = MiniatureArt.Mat("17231F", 0, .20f);
        for (int side = -1; side <= 1; side += 2)
        {
            MiniatureArt.Ellipsoid(head, "耳" + side, V(side * .445f, -.015f, .005f), V(.15f, .255f, .14f), skin);
            MiniatureArt.Ellipsoid(head, "耳内" + side, V(side * .471f, -.016f, -.055f), V(.07f, .15f, .035f), shade);
            // 眼白、虹膜和瞳孔独立前后分层，上睑与宽眉留出明确间隔。
            float eyeX = side * .17f;
            MiniatureArt.Ellipsoid(head, "眼窝" + side, V(eyeX, .043f, -.390f), V(.205f, .118f, .055f), shade);
            FrontPlate(head, "杏形眼白" + side, V(eyeX, .041f, -.425f), new[] {
                P(-.087f, -.002f), P(-.047f, -.037f), P(.043f, -.034f), P(.086f, .005f),
                P(.045f, .047f), P(-.043f, .044f) }, .012f, eyeWhite, null, 0);
            MiniatureArt.Ellipsoid(head, "虹膜" + side, V(eyeX + .012f, .043f, -.448f), V(.057f, .070f, .018f), iris, 24, 16);
            MiniatureArt.Ellipsoid(head, "瞳孔" + side, V(eyeX + .012f, .043f, -.460f), V(.029f, .048f, .013f), pupil, 20, 12);
            MiniatureArt.Ellipsoid(head, "目光微亮" + side, V(eyeX + .002f, .060f, -.470f), V(.010f, .012f, .008f), eyeWhite, 12, 8);
            Seam(head, "上眼睑" + side, new[] { V(eyeX - .085f, .045f, -.438f), V(eyeX - .042f, .085f, -.441f),
                V(eyeX + .043f, .087f, -.441f), V(eyeX + .085f, .046f, -.438f) }, .009f, hair);
            Seam(head, "下眼睑" + side, new[] { V(eyeX - .067f, .013f, -.432f), V(eyeX, .003f, -.436f),
                V(eyeX + .070f, .016f, -.432f) }, .005f, shade);
            Seam(head, "剑眉" + side, new[] { V(side * .073f, .143f, -.414f), V(side * .18f, .176f, -.414f),
                V(side * .29f, .156f, -.355f) }, mature ? .024f : .021f, hair);
        }
        BuildNose(head, skin, shade);
        if (!mature)
        {
            Seam(head, "唇线", new[] { V(-.084f, -.207f, -.375f), V(0, -.215f, -.384f), V(.084f, -.207f, -.375f) }, .007f, shade);
        }
    }

    private static void BuildNose(Transform head, Material skin, Material shade)
    {
        float[] heights = { .115f, .035f, -.075f, -.122f };
        float[] widths = { .031f, .042f, .068f, .046f };
        float[] sides = { -.373f, -.392f, -.390f, -.378f };
        float[] ridges = { -.403f, -.433f, -.466f, -.423f };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int row = 0; row < heights.Length; row++)
        {
            vertices.Add(V(-widths[row], heights[row], sides[row]));
            vertices.Add(V(0, heights[row], ridges[row]));
            vertices.Add(V(widths[row], heights[row], sides[row]));
        }
        for (int row = 0; row < heights.Length - 1; row++)
            for (int column = 0; column < 2; column++)
            {
                int a = row * 3 + column;
                AddTriangle(triangles, a, a + 1, a + 3);
                AddTriangle(triangles, a + 1, a + 4, a + 3);
            }
        MiniatureArt.Patch(head, "收敛鼻梁体面", vertices.ToArray(), triangles.ToArray(), skin);
        for (int side = -1; side <= 1; side += 2)
            Seam(head, "鼻翼暗线" + side, new[] { V(side * .037f, -.114f, -.425f),
                V(side * .055f, -.108f, -.412f) }, .005f, shade);
    }

    private static void BuildBoot(Transform root, string name, Vector3 position, float turn, Material leather, Material metal)
    {
        Transform boot = MiniatureArt.Group(root, name, position);
        boot.localRotation = Quaternion.Euler(0, turn, 0);
        MiniatureArt.Lathe(boot, "束口靴筒", new[] { P(.105f, -.13f), P(.13f, -.02f),
            P(.12f, .14f), P(.10f, .22f) }, V(0, 0, .025f), leather, 28);
        float[] depths = { .13f, .025f, -.13f, -.29f, -.365f };
        float[] widths = { .106f, .14f, .145f, .125f, .080f };
        float[] tops = { .015f, .035f, -.005f, -.065f, -.092f };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const int sides = 8;
        for (int row = 0; row < depths.Length; row++)
        {
            float width = widths[row], top = tops[row], sole = -.17f;
            foreach (Vector2 point in new[] { P(-width * .7f, sole), P(width * .7f, sole),
                P(width, sole + .022f), P(width, top - .03f), P(width * .68f, top),
                P(-width * .68f, top), P(-width, top - .03f), P(-width, sole + .022f) })
                vertices.Add(V(point.x, point.y, depths[row]));
        }
        for (int row = 0; row < depths.Length - 1; row++)
            for (int side = 0; side < sides; side++)
            {
                int a = row * sides + side, b = row * sides + (side + 1) % sides;
                AddTriangle(triangles, a, a + sides, b);
                AddTriangle(triangles, b, a + sides, b + sides);
            }
        for (int end = 0; end <= 1; end++)
        {
            int row = end == 0 ? 0 : depths.Length - 1;
            int center = vertices.Count;
            vertices.Add(V(0, (tops[row] - .17f) * .5f, depths[row]));
            for (int side = 0; side < sides; side++)
            {
                int a = row * sides + side, b = row * sides + (side + 1) % sides;
                if (end == 0) AddTriangle(triangles, center, a, b);
                else AddTriangle(triangles, center, b, a);
            }
        }
        MiniatureArt.Patch(boot, "平底收尖皮靴", vertices.ToArray(), triangles.ToArray(), leather);
        FrontPlate(boot, "胫前窄护片", V(0, .065f, -.115f), new[] {
            P(-.083f, -.09f), P(.083f, -.09f), P(.082f, .08f), P(0, .13f), P(-.082f, .08f) },
            .018f, metal, null, 0);
    }

    private static void BuildBelt(Transform root, float height, Material leather, Material metal, Material trim)
    {
        GameObject belt = MiniatureArt.Lathe(root, "束腰宽带", new[] {
            P(.34f, 0), P(.39f, .015f), P(.395f, .13f), P(.35f, .145f) }, V(0, height - .07f, 0), leather, 40);
        belt.transform.localScale = new Vector3(1, 1, .78f);
        FrontPlate(root, "腰带方胜扣", V(0, height, -.325f), new[] {
            P(-.105f, -.065f), P(.105f, -.065f), P(.13f, 0), P(.105f, .065f),
            P(-.105f, .065f), P(-.13f, 0) }, .035f, metal, trim, .012f);
    }

    private static void ShoulderArmor(Transform root, string name, Vector3 position, int side,
        Material metal, Material trim, Material dark)
    {
        Transform shoulder = MiniatureArt.Group(root, name, position + V(0, .10f, 0));
        shoulder.localRotation = Quaternion.Euler(-4, side * -9, side * -11);
        const int across = 12;
        // 弧面沿肩头向外展开，四列截面控制顶脊和垂边，避免肩甲成为方盒。
        float[] x = { -.14f, .02f, .20f, .33f };
        float[] y = { .02f, .12f, .07f, -.10f };
        float[] width = { .19f, .29f, .27f, .17f };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int row = 0; row < x.Length; row++)
            for (int column = 0; column <= across; column++)
            {
                float t = column / (float)across * 2 - 1;
                vertices.Add(V(side * x[row], y[row] - .14f * t * t, width[row] * t));
            }
        for (int row = 0; row < x.Length - 1; row++)
            for (int column = 0; column < across; column++)
            {
                int a = row * (across + 1) + column, b = a + 1, c = a + across + 1, d = c + 1;
                if (side > 0) { AddTriangle(triangles, a, b, c); AddTriangle(triangles, b, d, c); }
                else { AddTriangle(triangles, a, c, b); AddTriangle(triangles, b, c, d); }
            }
        MiniatureArt.Patch(shoulder, "弧面肩甲", vertices.ToArray(), triangles.ToArray(), metal);
        var rim = new List<Vector3>();
        for (int row = 0; row < x.Length; row++) rim.Add(V(side * x[row], y[row] - .14f, -width[row]));
        for (int column = 1; column <= across; column++)
        {
            float t = column / (float)across * 2 - 1;
            rim.Add(V(side * x[3], y[3] - .14f * t * t, width[3] * t));
        }
        for (int row = x.Length - 2; row >= 0; row--) rim.Add(V(side * x[row], y[row] - .14f, width[row]));
        Seam(shoulder, "肩甲卷边", rim.ToArray(), .016f, trim);
        for (int layer = 0; layer < 2; layer++)
        {
            Vector3 centre = V(side * (.21f + layer * .025f), -.18f - layer * .105f, -.235f);
            FrontPlate(shoulder, "下垂叠甲" + layer, centre, new[] {
                P(-.13f, -.08f), P(.13f, -.08f), P(.17f, .055f), P(0, .115f), P(-.17f, .055f) },
                .035f, layer == 0 ? metal : dark, trim, .01f);
        }
        for (int stud = -1; stud <= 1; stud++)
            MiniatureArt.Ellipsoid(shoulder, "肩甲铆钉" + stud, V(side * .14f, .065f - .10f * stud * stud, stud * .19f),
                V(.038f, .027f, .038f), trim, 12, 8);
    }

    private static void Bracer(Transform root, string name, Vector3 position, float lean, Material metal, Material trim)
    {
        Transform arm = MiniatureArt.Group(root, name, position);
        arm.localRotation = Quaternion.Euler(0, 0, lean * 18);
        FrontPlate(arm, "束腕护片", V(0, 0, -.14f), new[] {
            P(-.105f, -.12f), P(.105f, -.12f), P(.125f, .09f), P(0, .15f), P(-.125f, .09f) },
            .04f, metal, trim, .012f);
    }

    private static void Hand(Transform root, string name, Vector3 position, Material skin, Material shade, bool grip)
    {
        Transform hand = MiniatureArt.Group(root, name, position);
        MiniatureArt.Ellipsoid(hand, "手掌", Vector3.zero, V(.18f, .22f, .18f), skin, 24, 16);
        MiniatureArt.Ellipsoid(hand, "拇指", V(-.075f, .022f, -.045f), V(.085f, .14f, .09f), skin, 20, 12);
        for (int finger = 0; finger < 3; finger++)
        {
            float y = -.062f + finger * .043f;
            Seam(hand, "握指刻线" + finger, new[] { V(-.025f, y, -.087f), V(.04f, y - .006f, -.083f),
                V(.075f, y + .004f, grip ? -.042f : -.055f) }, .007f, shade);
        }
    }

    private static void BuildGlaive(Transform root, Material gold, Material bronze, Material steel, Material wood, Material red)
    {
        Vector3 bottom = V(.78f, .235f, -.09f), top = V(.78f, 2.72f, -.09f);
        MiniatureArt.Cylinder(root, "青龙刀长柄", bottom, top, .034f, wood, 20);
        MiniatureArt.Cylinder(root, "刀柄金属尾鐏", bottom, bottom + Vector3.up * .18f, .045f, bronze, 20);
        for (int ring = 0; ring < 5; ring++)
        {
            float y = .83f + ring * .20f;
            MiniatureArt.Cylinder(root, "刀柄束环" + ring, V(.78f, y, -.09f), V(.78f, y + .021f, -.09f), .041f, gold, 20);
        }
        MiniatureArt.Cylinder(root, "刀首吞口", V(.78f, 2.48f, -.09f), V(.78f, 2.74f, -.09f), .065f, bronze, 24);
        FrontPlate(root, "偃月刀刃", V(.78f, 2.68f, -.09f), new[] {
            P(-.025f, -.08f), P(.12f, -.01f), P(.31f, .17f), P(.39f, .37f),
            P(.31f, .54f), P(.23f, .38f), P(.08f, .22f), P(-.025f, .16f) }, .035f, steel, null, 0);
        Seam(root, "偃月刀开刃亮边", new[] { V(.90f, 2.67f, -.105f), V(1.09f, 2.85f, -.105f),
            V(1.17f, 3.05f, -.105f), V(1.09f, 3.22f, -.105f) }, .014f, steel);
        Seam(root, "刀背金饰", new[] { V(.76f, 2.60f, -.115f), V(.76f, 2.84f, -.115f),
            V(.86f, 2.90f, -.115f), V(1.00f, 3.06f, -.115f) }, .018f, gold);
        Seam(root, "青龙简化浮雕", new[] { V(.89f, 2.82f, -.14f), V(.98f, 2.86f, -.15f),
            V(1.03f, 2.96f, -.15f), V(.97f, 3.03f, -.15f), V(1.03f, 3.10f, -.14f) }, .011f, bronze);
        Ribbon(root, "刀首朱绦", new[] { V(.79f, 2.54f, -.15f), V(.91f, 2.44f, -.18f),
            V(.98f, 2.27f, -.15f), V(.94f, 2.10f, -.11f) }, .043f, red);
    }

    private static void BuildSpear(Transform root, Material wood, Material steel, Material edge,
        Material gold, Material red, Material redLight)
    {
        Vector3 bottom = V(.83f, .22f, .015f), collar = V(.57f, 2.82f, -.13f);
        MiniatureArt.Cylinder(root, "长枪硬木杆", bottom, collar, .027f, wood, 20);
        MiniatureArt.Cylinder(root, "枪杆尾鐏", bottom, Vector3.Lerp(bottom, collar, .065f), .037f, steel, 20);
        for (int ring = 0; ring < 4; ring++)
        {
            Vector3 point = Vector3.Lerp(bottom, collar, .40f + ring * .055f);
            MiniatureArt.Cylinder(root, "枪柄缠金" + ring, point, point + (collar - bottom).normalized * .018f, .033f, gold, 16);
        }
        MiniatureArt.Cylinder(root, "枪头金属套筒", collar - Vector3.up * .10f, collar + Vector3.up * .07f, .047f, steel, 24);
        FrontPlate(root, "银枪柳叶刃", V(.565f, 2.84f, -.13f), new[] {
            P(-.025f, -.02f), P(.095f, .12f), P(.025f, .40f), P(-.105f, .14f) }, .035f, steel, edge, .009f);
        Seam(root, "枪刃压脊", new[] { V(.54f, 2.84f, -.174f), V(.545f, 3.02f, -.188f), V(.59f, 3.24f, -.15f) }, .012f, edge);
        // 红缨是有宽度的绸带束，方向统一向外飘，避免一团红色球状体。
        for (int strand = 0; strand < 7; strand++)
        {
            float offset = (strand - 3) * .022f;
            Ribbon(root, "枪首红缨" + strand, new[] { V(.57f + offset, 2.79f, -.15f),
                V(.72f + offset, 2.68f, -.18f - strand * .008f),
                V(.89f + offset, 2.48f, -.16f - strand * .011f),
                V(.91f + offset * 2, 2.31f + Mathf.Abs(strand - 3) * .035f, -.12f) },
                .028f, strand % 3 == 0 ? redLight : red);
        }
    }

    private static void BuildCape(Transform root, Material cloth, Material shadow, Material trim)
    {
        const int rows = 8, columns = 24;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int row = 0; row <= rows; row++)
        {
            float t = row / (float)rows;
            float half = Mathf.Lerp(.40f, .73f, t);
            for (int column = 0; column <= columns; column++)
            {
                float u = column / (float)columns * 2 - 1;
                float x = u * half - .17f * t * t;
                float y = Mathf.Lerp(1.99f, .35f, t) + .045f * Mathf.Cos(u * Mathf.PI * 3) * t * t;
                float z = .16f + .25f * t + Mathf.Cos(u * Mathf.PI * 5) * (.025f + t * .045f);
                vertices.Add(V(x, y, z));
            }
        }
        for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int a = row * (columns + 1) + column, b = a + 1, c = a + columns + 1, d = c + 1;
                AddTriangle(triangles, a, b, c); AddTriangle(triangles, b, d, c);
            }
        MiniatureArt.Patch(root, "白袍大披风", vertices.ToArray(), triangles.ToArray(), cloth);
        var hem = new List<Vector3>();
        for (int column = 0; column <= columns; column++) hem.Add(vertices[rows * (columns + 1) + column] - V(0, 0, .008f));
        Seam(root, "披风下摆细边", hem.ToArray(), .014f, trim);
        for (int side = 0; side <= 1; side++)
        {
            var line = new List<Vector3>();
            for (int row = 0; row <= rows; row++) line.Add(vertices[row * (columns + 1) + side * columns] - V(0, 0, .006f));
            Seam(root, "披风侧缘" + side, line.ToArray(), .016f, trim);
        }
        for (int fold = -2; fold <= 2; fold++)
        {
            float u = fold * .32f;
            var line = new List<Vector3>();
            for (int row = 1; row <= rows; row++)
            {
                float t = row / (float)rows;
                line.Add(V(u * Mathf.Lerp(.40f, .73f, t) - .17f * t * t,
                    Mathf.Lerp(1.99f, .35f, t), .15f + .25f * t + Mathf.Cos(u * Mathf.PI * 5) * (.025f + t * .045f)));
            }
            Seam(root, "披风压褶" + fold, line.ToArray(), .008f, shadow);
        }
    }

    private static void PleatedRobe(Transform root, string name, float[] heights, float[] radiiX,
        float[] radiiZ, Material material, float foldDepth, float lean)
    {
        const int sides = 48;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int row = 0; row < heights.Length; row++)
        {
            float t = row / (float)(heights.Length - 1);
            for (int column = 0; column < sides; column++)
            {
                float angle = column * Mathf.PI * 2 / sides;
                float pleat = Mathf.Cos(angle * 12 + t * .65f) * foldDepth * (1 - t * .55f);
                float hem = row == 0 ? .025f * Mathf.Sin(angle * 5 + .4f) : 0;
                vertices.Add(V(Mathf.Sin(angle) * (radiiX[row] + pleat) + lean * (1 - t),
                    heights[row] + hem, Mathf.Cos(angle) * (radiiZ[row] + pleat) + .015f * t));
            }
        }
        for (int row = 0; row < heights.Length - 1; row++)
            for (int column = 0; column < sides; column++)
            {
                int a = row * sides + column, b = row * sides + (column + 1) % sides;
                int c = a + sides, d = b + sides;
                AddTriangle(triangles, a, b, c); AddTriangle(triangles, b, d, c);
            }
        MiniatureArt.Patch(root, name, vertices.ToArray(), triangles.ToArray(), material);
    }

    private static void ClothPanel(Transform root, string name, Vector3 position, float[] widths,
        float[] heights, Material material, float folds, float sway, float drapeForward = .045f, float outsideCurl = 0)
    {
        const int columns = 16;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int row = 0; row < heights.Length; row++)
        {
            float t = row / (float)(heights.Length - 1);
            for (int column = 0; column <= columns; column++)
            {
                float u = column / (float)columns * 2 - 1;
                float curl = Mathf.Abs(outsideCurl) * Mathf.Pow(Mathf.Max(0, u * Mathf.Sign(outsideCurl)), 2);
                vertices.Add(position + V(u * widths[row] + sway * t * t,
                    heights[row] + (row == heights.Length - 1 ? .018f * Mathf.Cos(u * Mathf.PI * 3) : 0),
                    -Mathf.Cos(u * Mathf.PI * 3) * folds - drapeForward * t + curl));
            }
        }
        for (int row = 0; row < heights.Length - 1; row++)
            for (int column = 0; column < columns; column++)
            {
                int a = row * (columns + 1) + column, b = a + 1, c = a + columns + 1, d = c + 1;
                AddTriangle(triangles, a, b, c); AddTriangle(triangles, b, d, c);
            }
        MiniatureArt.Patch(root, name, vertices.ToArray(), triangles.ToArray(), material);
    }

    private static void Sleeve(Transform root, string name, Vector3 a, Vector3 b, float firstRadius,
        float lastRadius, Material material)
    {
        const int rings = 7, sides = 24;
        Vector3 direction = (b - a).normalized;
        Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
        if (right.sqrMagnitude < .01f) right = Vector3.right;
        Vector3 forward = Vector3.Cross(right, direction).normalized;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int ring = 0; ring <= rings; ring++)
        {
            float t = ring / (float)rings;
            float radius = Mathf.Lerp(firstRadius, lastRadius, t) * (1 + .03f * Mathf.Sin(t * Mathf.PI * 2));
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2 / sides;
                float pleat = 1 + .025f * Mathf.Cos(angle * 6 + t);
                vertices.Add(Vector3.Lerp(a, b, t) + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius * pleat);
            }
        }
        for (int ring = 0; ring < rings; ring++)
            for (int side = 0; side < sides; side++)
            {
                int first = ring * sides + side, next = ring * sides + (side + 1) % sides;
                AddTriangle(triangles, first, first + sides, next);
                AddTriangle(triangles, next, first + sides, next + sides);
            }
        // 袖口封面在弯肘及护腕接缝处兜底，避免两段袖筒交接露出背景。
        for (int end = 0; end <= 1; end++)
        {
            int center = vertices.Count, ring = end == 0 ? 0 : rings * sides;
            vertices.Add(end == 0 ? a : b);
            for (int side = 0; side < sides; side++) vertices.Add(vertices[ring + side]);
            for (int side = 0; side < sides; side++)
            {
                int first = center + 1 + side, next = center + 1 + (side + 1) % sides;
                if (end == 0) AddTriangle(triangles, center, first, next);
                else AddTriangle(triangles, center, next, first);
            }
        }
        MiniatureArt.Patch(root, name, vertices.ToArray(), triangles.ToArray(), material);
    }

    private static void FrontPlate(Transform root, string name, Vector3 position, Vector2[] outline,
        float depth, Material material, Material trim, float trimRadius)
    {
        int count = outline.Length;
        var vertices = new Vector3[count * 2 + 2];
        Vector2 center = Vector2.zero;
        float area = 0;
        for (int i = 0; i < count; i++)
        {
            center += outline[i];
            Vector2 next = outline[(i + 1) % count];
            area += outline[i].x * next.y - next.x * outline[i].y;
            vertices[i] = position + V(outline[i].x, outline[i].y, 0);
            vertices[count + i] = vertices[i] + V(0, 0, depth * .48f);
        }
        center /= count;
        vertices[count * 2] = position + V(center.x, center.y, -depth);
        vertices[count * 2 + 1] = position + V(center.x, center.y, depth * .48f);
        var triangles = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            if (area > 0)
            {
                AddTriangle(triangles, count * 2, next, i);
                AddTriangle(triangles, count * 2 + 1, count + i, count + next);
                AddTriangle(triangles, i, next, count + i);
                AddTriangle(triangles, next, count + next, count + i);
            }
            else
            {
                AddTriangle(triangles, count * 2, i, next);
                AddTriangle(triangles, count * 2 + 1, count + next, count + i);
                AddTriangle(triangles, i, count + i, next);
                AddTriangle(triangles, next, count + i, count + next);
            }
        }
        MiniatureArt.Patch(root, name, vertices, triangles.ToArray(), material);
        if (trim != null && trimRadius > 0)
        {
            var points = new Vector3[count + 1];
            for (int i = 0; i <= count; i++) points[i] = vertices[i % count] - V(0, 0, .004f);
            Seam(root, name + "卷边", points, trimRadius, trim);
        }
    }

    private static void Ribbon(Transform root, string name, Vector3[] points, float halfWidth, Material material)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const int steps = 20;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps, inverse = 1 - t;
            Vector3 point = inverse * inverse * inverse * points[0] + 3 * inverse * inverse * t * points[1] +
                3 * inverse * t * t * points[2] + t * t * t * points[3];
            float width = halfWidth * Mathf.Lerp(1, .18f, t);
            vertices.Add(point - V(width, 0, 0));
            vertices.Add(point + V(width, 0, 0));
        }
        for (int i = 0; i < steps; i++)
        {
            int a = i * 2;
            AddTriangle(triangles, a, a + 1, a + 2);
            AddTriangle(triangles, a + 1, a + 3, a + 2);
        }
        MiniatureArt.Patch(root, name, vertices.ToArray(), triangles.ToArray(), material);
    }

    private static void Seam(Transform parent, string name, Vector3[] points, float radius, Material material)
    {
        MiniatureArt.Tube(parent, name, points, radius, material, 10);
    }

    private static void AddTriangle(List<int> triangles, int a, int b, int c)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
    }

    private static Vector2 P(float x, float y) { return new Vector2(x, y); }
    private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
}
