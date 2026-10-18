using UnityEngine;

namespace Roughlike.Editor.Art
{
    /// <summary>
    /// Painted icons for everything you can carry or pick up: skills, amulets, outfits, mutations, coins, Cells, scrolls, flasks and
    /// the props skills leave behind. 128 px frames, drawn with the same cel painter as the characters so they share their look
    /// (dark outline, rim light, warm highlights). Each is a clip of the "ItemIcons" and "Pickups" sheets.
    /// </summary>
    public static class IconArt
    {
        public const int S = 128;
        static readonly Color Ink = new Color(0.07f, 0.05f, 0.1f);

        static Painter P() => new Painter(S, S);
        static Color32[] Done(Painter p) => p.Render(2.6f, Ink);
        static Vector2 V(float x, float y) => new Vector2(x, y);

        static Mat Gold => Mat.Of(new Color(0.95f, 0.76f, 0.3f), Color.white, 3f);
        static Mat Steel => Mat.Of(new Color(0.7f, 0.74f, 0.82f), Color.white, 3f);
        static Mat Iron => Mat.Of(new Color(0.24f, 0.25f, 0.32f), new Color(0.7f, 0.74f, 0.9f), 2.5f);
        static Mat Wood => Mat.Of(new Color(0.5f, 0.32f, 0.2f), new Color(1f, 0.7f, 0.4f), 3f);
        static Mat Leather => Mat.Of(new Color(0.42f, 0.26f, 0.18f), new Color(0.95f, 0.65f, 0.4f), 2.5f);
        static Mat Chain => Mat.Of(new Color(0.55f, 0.57f, 0.65f), Color.white, 2f);

        // ------------------------------------------------------------------ skills

        public static Color32[] Skill(string id)
        {
            var p = P();
            switch (id)
            {
                case "PowderKeg":
                {
                    Mat keg = Mat.Of(new Color(0.55f, 0.34f, 0.2f), new Color(1f, 0.7f, 0.4f), 3.5f);
                    p.Ellipse(V(64f, 62f), 38f, 44f, 0f, keg);
                    p.Capsule(V(28f, 80f), V(100f, 80f), 4.5f, Iron);
                    p.Capsule(V(26f, 46f), V(102f, 46f), 4.5f, Iron);
                    p.Capsule(V(64f, 104f), V(78f, 122f), 2.5f, Mat.Of(new Color(0.8f, 0.7f, 0.5f)));       // fuse
                    p.Circle(V(80f, 124f), 8f, Mat.Glow(new Color(1f, 0.6f, 0.15f)));
                    p.Circle(V(80f, 124f), 4f, Mat.Glow(new Color(1f, 0.95f, 0.6f)));
                    p.Poly(new[] { V(52f, 56f), V(76f, 56f), V(64f, 72f) }, Mat.Of(new Color(0.12f, 0.1f, 0.1f)));   // a painted skull mark
                    break;
                }
                case "BellTurret":
                {
                    p.Capsule(V(34f, 10f), V(60f, 52f), 4.5f, Wood);                                     // tripod
                    p.Capsule(V(94f, 10f), V(68f, 52f), 4.5f, Wood);
                    p.Capsule(V(64f, 8f), V(64f, 56f), 4f, Wood);
                    Mat brass = Mat.Of(new Color(0.9f, 0.7f, 0.3f), new Color(1f, 0.95f, 0.7f), 3.5f);
                    p.Ellipse(V(64f, 80f), 30f, 28f, 0f, brass);
                    p.Poly(new[] { V(30f, 74f), V(98f, 74f), V(106f, 54f), V(22f, 54f) }, brass);
                    p.Capsule(V(18f, 56f), V(110f, 56f), 5f, Gold);
                    p.Circle(V(64f, 46f), 7f, Gold);                                                        // clapper
                    p.Capsule(V(72f, 84f), V(120f, 94f), 6f, Iron);                                         // the barrel
                    p.Circle(V(54f, 82f), 5f, Mat.Glow(new Color(0.4f, 0.95f, 1f)));
                    break;
                }
                case "SnareTrap":
                {
                    Mat jaw = Mat.Of(new Color(0.46f, 0.5f, 0.56f), Color.white, 3f);
                    p.Ellipse(V(64f, 46f), 52f, 14f, 0f, Iron);                                           // base ring
                    for (int i = 0; i < 8; i++)                                                              // teeth
                    {
                        float x = 22f + i * 12f;
                        p.Poly(new[] { V(x - 5f, 54f), V(x + 5f, 54f), V(x, 78f + (i % 2) * 8f) }, jaw);
                    }
                    p.Capsule(V(14f, 52f), V(114f, 52f), 6f, jaw);
                    p.Capsule(V(36f, 38f), V(92f, 38f), 4f, Mat.Of(new Color(0.4f, 0.8f, 0.35f), Color.white, 3f));   // green spring
                    p.Circle(V(64f, 42f), 8f, Gold);
                    break;
                }
                default: // VenomFlask
                {
                    Mat glass = Mat.Of(new Color(0.7f, 0.9f, 0.85f), Color.white, 3f);
                    Mat venom = Mat.Glow(new Color(0.5f, 0.95f, 0.25f));
                    p.Circle(V(64f, 46f), 38f, glass);
                    p.Capsule(V(64f, 76f), V(64f, 106f), 12f, 9f, glass);
                    p.Ellipse(V(64f, 40f), 31f, 27f, 0f, venom);
                    p.Ellipse(V(52f, 50f), 8f, 6f, 20f, Mat.Glow(new Color(0.85f, 1f, 0.6f)));
                    p.Circle(V(78f, 36f), 5f, Mat.Glow(new Color(0.85f, 1f, 0.6f)));
                    p.Capsule(V(64f, 106f), V(64f, 116f), 10f, Wood);                                     // cork
                    p.Circle(V(40f, 108f), 4f, venom);
                    break;
                }
            }
            return Done(p);
        }

        // ------------------------------------------------------------------ amulets and outfits

        static void ChainLoop(Painter p, float cx, float top)
        {
            p.Capsule(V(cx - 26f, 70f), V(cx, top), 2.6f, Chain);
            p.Capsule(V(cx + 26f, 70f), V(cx, top), 2.6f, Chain);
            p.Circle(V(cx, top), 4f, Gold);
        }

        public static Color32[] Gear(string id)
        {
            var p = P();
            switch (id)
            {
                case "FerrymansCoin":
                    ChainLoop(p, 64f, 124f);
                    p.Circle(V(64f, 48f), 40f, Gold);
                    p.Circle(V(64f, 48f), 31f, Mat.Of(new Color(0.8f, 0.6f, 0.2f), Color.white, 2.5f));
                    p.Poly(new[] { V(54f, 38f), V(74f, 38f), V(74f, 58f), V(54f, 58f) }, Mat.Of(new Color(0.12f, 0.1f, 0.12f)));   // the ferryman's square hole
                    break;
                case "CrackedReliquary":
                {
                    ChainLoop(p, 64f, 124f);
                    p.Poly(new[] { V(30f, 30f), V(98f, 30f), V(104f, 74f), V(64f, 96f), V(24f, 74f) }, Mat.Of(new Color(0.4f, 0.3f, 0.45f), Color.white, 3f));
                    p.Poly(new[] { V(40f, 38f), V(88f, 38f), V(92f, 70f), V(64f, 86f), V(36f, 70f) }, Mat.Glow(new Color(0.65f, 0.35f, 0.9f)));
                    p.Capsule(V(70f, 84f), V(58f, 60f), 2.6f, Mat.Glow(Ink));                               // the crack
                    p.Capsule(V(58f, 60f), V(70f, 44f), 2.6f, Mat.Glow(Ink));
                    p.Circle(V(52f, 66f), 5f, Mat.Glow(new Color(0.9f, 0.7f, 1f)));
                    break;
                }
                case "BellFragment":
                {
                    ChainLoop(p, 64f, 124f);
                    Mat brass = Mat.Of(new Color(0.95f, 0.88f, 0.5f), Color.white, 3f);
                    p.Poly(new[] { V(34f, 24f), V(94f, 20f), V(100f, 50f), V(78f, 74f), V(70f, 96f), V(44f, 70f) }, brass);
                    p.Capsule(V(40f, 28f), V(92f, 26f), 4f, Gold);
                    p.Circle(V(64f, 48f), 6f, Mat.Glow(new Color(1f, 1f, 0.8f)));
                    break;
                }
                case "LanternCharm":
                {
                    ChainLoop(p, 64f, 124f);
                    p.Capsule(V(40f, 90f), V(88f, 90f), 5f, Iron);
                    p.Poly(new[] { V(42f, 86f), V(86f, 86f), V(92f, 38f), V(36f, 38f) }, Mat.Of(new Color(0.9f, 0.55f, 0.2f), Color.white, 3f));
                    p.Ellipse(V(64f, 60f), 17f, 22f, 0f, Mat.Glow(new Color(1f, 0.82f, 0.4f)));
                    p.Capsule(V(34f, 38f), V(94f, 38f), 5f, Iron);
                    p.Capsule(V(64f, 90f), V(64f, 104f), 3f, Iron);
                    break;
                }
                case "TideLocket":
                {
                    ChainLoop(p, 64f, 124f);
                    p.Ellipse(V(64f, 52f), 36f, 42f, 0f, Mat.Of(new Color(0.35f, 0.75f, 0.78f), Color.white, 3f));
                    p.Ellipse(V(64f, 52f), 28f, 34f, 0f, Mat.Of(new Color(0.18f, 0.4f, 0.5f), Color.white, 2f));
                    for (int i = 0; i < 3; i++)                                                              // waves
                    {
                        float y = 38f + i * 14f;
                        p.Capsule(V(44f, y), V(58f, y + 6f), 2.6f, Mat.Glow(new Color(0.7f, 1f, 1f)));
                        p.Capsule(V(58f, y + 6f), V(72f, y), 2.6f, Mat.Glow(new Color(0.7f, 1f, 1f)));
                        p.Capsule(V(72f, y), V(84f, y + 6f), 2.6f, Mat.Glow(new Color(0.7f, 1f, 1f)));
                    }
                    break;
                }
                case "EmberSigil":
                {
                    ChainLoop(p, 64f, 124f);
                    p.Circle(V(64f, 52f), 40f, Mat.Of(new Color(0.55f, 0.15f, 0.12f), new Color(1f, 0.6f, 0.4f), 3f));
                    p.Circle(V(64f, 52f), 30f, Mat.Of(new Color(0.3f, 0.07f, 0.08f)));
                    p.Poly(new[] { V(64f, 82f), V(48f, 54f), V(56f, 58f), V(54f, 34f), V(66f, 50f), V(72f, 28f), V(80f, 58f), V(84f, 54f) }, Mat.Glow(new Color(1f, 0.55f, 0.15f)));
                    p.Poly(new[] { V(64f, 72f), V(58f, 56f), V(64f, 44f), V(70f, 56f) }, Mat.Glow(new Color(1f, 0.9f, 0.5f)));
                    break;
                }

                // ---- outfits
                case "ApprenticeRobes":
                    Garment(p, new Color(0.92f, 0.86f, 0.74f), new Color(0.6f, 0.3f, 0.28f), true);
                    break;
                case "DrownedVestments":
                    Garment(p, new Color(0.3f, 0.58f, 0.55f), new Color(0.18f, 0.35f, 0.34f), false);
                    p.Capsule(V(34f, 30f), V(30f, 8f), 3f, Mat.Of(new Color(0.25f, 0.55f, 0.3f)));            // weed
                    p.Capsule(V(90f, 30f), V(98f, 10f), 3f, Mat.Of(new Color(0.25f, 0.55f, 0.3f)));
                    break;
                case "BoathookHarness":
                    Garment(p, new Color(0.6f, 0.42f, 0.28f), new Color(0.35f, 0.22f, 0.15f), false);
                    p.Capsule(V(40f, 100f), V(88f, 40f), 5f, Leather);
                    p.Capsule(V(88f, 100f), V(40f, 40f), 5f, Leather);
                    p.Circle(V(64f, 70f), 8f, Steel);
                    break;
                case "AshCowl":
                    Garment(p, new Color(0.42f, 0.42f, 0.48f), new Color(0.2f, 0.2f, 0.26f), true);
                    p.Circle(V(100f, 12f), 3f, Mat.Glow(new Color(1f, 0.5f, 0.2f)));
                    break;
                default: // VerdigrisMantle
                    Garment(p, new Color(0.38f, 0.72f, 0.52f), new Color(0.2f, 0.4f, 0.32f), false);
                    p.Circle(V(64f, 98f), 7f, Gold);
                    break;
            }
            return Done(p);
        }

        /// <summary>A tunic or cloak seen from the front: shoulders, sleeves, a skirt, an optional hood.</summary>
        static void Garment(Painter p, Color cloth, Color shade, bool hood)
        {
            Mat body = Mat.Of(cloth, Color.Lerp(cloth, Color.white, 0.6f), 3f);
            Mat dark = Mat.Of(shade, Color.Lerp(shade, Color.white, 0.4f), 2.5f);
            p.Poly(new[] { V(26f, 92f), V(14f, 44f), V(34f, 38f), V(44f, 66f) }, dark);          // sleeves
            p.Poly(new[] { V(102f, 92f), V(114f, 44f), V(94f, 38f), V(84f, 66f) }, dark);
            p.Poly(new[] { V(36f, 100f), V(30f, 20f), V(98f, 20f), V(92f, 100f), V(64f, 108f) }, body);   // the body
            p.Poly(new[] { V(30f, 20f), V(98f, 20f), V(104f, 8f), V(24f, 8f) }, dark);                   // hem
            p.Capsule(V(40f, 58f), V(88f, 58f), 4f, dark);                                                // belt
            if (hood) p.Poly(new[] { V(40f, 100f), V(64f, 124f), V(88f, 100f), V(76f, 90f), V(52f, 90f) }, dark);
            else p.Poly(new[] { V(44f, 100f), V(64f, 92f), V(84f, 100f), V(64f, 114f) }, dark);          // collar
        }

        // ------------------------------------------------------------------ mutations

        public static Color32[] Mutation(string id, Color tint)
        {
            var p = P();
            Mat disc = Mat.Of(Color.Lerp(tint, Color.black, 0.55f), Color.Lerp(tint, Color.white, 0.5f), 4f);
            Mat rim = Mat.Of(Color.Lerp(tint, new Color(1f, 0.85f, 0.4f), 0.5f), Color.white, 3f);
            Mat mark = Mat.Of(Color.Lerp(tint, Color.white, 0.55f), Color.white, 2.5f);
            Mat glow = Mat.Glow(Color.Lerp(tint, Color.white, 0.8f));
            p.Circle(V(64f, 64f), 58f, rim);
            p.Circle(V(64f, 64f), 50f, disc);
            switch (id)
            {
                case "TemperedFrame":      // a heart on a shield
                    p.Poly(new[] { V(34f, 96f), V(94f, 96f), V(94f, 62f), V(64f, 28f), V(34f, 62f) }, mark);
                    p.Circle(V(54f, 72f), 12f, Mat.Of(new Color(0.85f, 0.2f, 0.25f), Color.white, 2f));
                    p.Circle(V(74f, 72f), 12f, Mat.Of(new Color(0.85f, 0.2f, 0.25f), Color.white, 2f));
                    p.Poly(new[] { V(43f, 66f), V(85f, 66f), V(64f, 42f) }, Mat.Of(new Color(0.85f, 0.2f, 0.25f), Color.white, 2f));
                    break;
                case "FleetFoot":          // a winged boot
                    p.Poly(new[] { V(40f, 92f), V(62f, 92f), V(62f, 58f), V(96f, 50f), V(96f, 34f), V(40f, 34f) }, mark);
                    p.Poly(new[] { V(36f, 78f), V(8f, 92f), V(18f, 70f), V(6f, 66f), V(30f, 62f) }, glow);
                    break;
                case "Recklessness":       // a blade breaking a skull
                    p.Circle(V(64f, 74f), 22f, mark);
                    p.Poly(new[] { V(50f, 54f), V(78f, 54f), V(72f, 40f), V(56f, 40f) }, mark);
                    p.Circle(V(55f, 76f), 6f, Mat.Glow(Ink)); p.Circle(V(73f, 76f), 6f, Mat.Glow(Ink));
                    p.Capsule(V(26f, 100f), V(104f, 36f), 5f, glow);
                    break;
                case "Bloodletter":        // a drop and a dagger
                    p.Poly(new[] { V(64f, 104f), V(40f, 64f), V(48f, 46f), V(64f, 40f), V(80f, 46f), V(88f, 64f) }, Mat.Of(new Color(0.85f, 0.15f, 0.22f), Color.white, 3f));
                    p.Capsule(V(64f, 98f), V(64f, 30f), 3.2f, glow);
                    p.Capsule(V(50f, 84f), V(78f, 84f), 3f, glow);
                    break;
                case "Counterstrike":      // a shield with an arrow turning back
                    p.Poly(new[] { V(34f, 94f), V(94f, 94f), V(94f, 62f), V(64f, 28f), V(34f, 62f) }, mark);
                    p.Capsule(V(40f, 50f), V(88f, 82f), 4f, Mat.Glow(Ink));
                    p.Poly(new[] { V(88f, 82f), V(70f, 84f), V(84f, 66f) }, Mat.Glow(Ink));
                    break;
                case "Momentum":           // rising chevrons
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 38f + i * 22f;
                        p.Poly(new[] { V(28f, y), V(64f, y + 22f), V(100f, y), V(100f, y + 12f), V(64f, y + 34f), V(28f, y + 12f) }, i == 2 ? glow : mark);
                    }
                    break;
                case "SecondWind":         // a feather
                    p.Poly(new[] { V(34f, 30f), V(60f, 66f), V(102f, 98f), V(90f, 56f), V(64f, 40f) }, mark);
                    p.Capsule(V(30f, 26f), V(98f, 96f), 2.8f, Mat.Glow(Ink));
                    p.Poly(new[] { V(52f, 58f), V(62f, 76f), V(46f, 70f) }, glow);
                    break;
                default: // Gilded: a stack of coins
                    for (int i = 0; i < 4; i++) p.Ellipse(V(64f, 36f + i * 16f), 34f, 12f, 0f, i % 2 == 0 ? Gold : Mat.Of(new Color(0.85f, 0.65f, 0.25f), Color.white, 3f));
                    p.Circle(V(88f, 100f), 5f, glow);
                    break;
            }
            return Done(p);
        }

        // ------------------------------------------------------------------ pickups and props

        public static Color32[] Coin(int i, int n)
        {
            var p = P();
            float a = (float)i / n * Mathf.PI * 2f;
            float w = Mathf.Max(0.12f, Mathf.Abs(Mathf.Cos(a)));           // spinning: the face turns edge-on and back
            p.Ellipse(V(64f, 64f), 40f * w, 40f, 0f, Gold);
            if (w > 0.35f)
            {
                p.Ellipse(V(64f, 64f), 31f * w, 31f, 0f, Mat.Of(new Color(0.85f, 0.65f, 0.22f), Color.white, 2.5f));
                p.Capsule(V(64f - 8f * w, 50f), V(64f - 8f * w, 78f), 3.2f, Mat.Glow(new Color(1f, 0.92f, 0.6f)));
                p.Capsule(V(64f - 8f * w, 78f), V(64f + 8f * w, 78f), 3.2f, Mat.Glow(new Color(1f, 0.92f, 0.6f)));
            }
            return Done(p);
        }

        public static Color32[] Cell(int i, int n)
        {
            var p = P();
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)i / n * Mathf.PI * 2f);
            Mat crystal = Mat.Of(Color.Lerp(new Color(0.25f, 0.75f, 0.85f), new Color(0.5f, 0.95f, 1f), pulse), new Color(0.9f, 1f, 1f), 4f);
            p.Poly(new[] { V(64f, 122f), V(96f, 80f), V(88f, 36f), V(64f, 8f), V(40f, 36f), V(32f, 80f) }, crystal);
            p.Poly(new[] { V(64f, 112f), V(82f, 80f), V(76f, 40f), V(64f, 22f) }, Mat.Of(new Color(0.55f, 0.97f, 1f), Color.white, 2f));
            p.Capsule(V(48f, 74f), V(54f, 46f), 3f, Mat.Glow(new Color(0.95f, 1f, 1f)));
            p.Circle(V(100f, 100f), 3f + 3f * pulse, Mat.Glow(new Color(0.9f, 1f, 1f)));
            p.Circle(V(26f, 40f), 2f + 3f * (1f - pulse), Mat.Glow(new Color(0.9f, 1f, 1f)));
            return Done(p);
        }

        public static Color32[] Scroll(Color seal)
        {
            var p = P();
            Mat paper = Mat.Of(new Color(0.95f, 0.9f, 0.75f), Color.white, 3f);
            Mat roll = Mat.Of(new Color(0.85f, 0.78f, 0.6f), Color.white, 3f);
            p.Poly(new[] { V(30f, 24f), V(98f, 24f), V(98f, 104f), V(30f, 104f) }, paper);
            p.Capsule(V(24f, 22f), V(104f, 22f), 9f, roll);
            p.Capsule(V(24f, 106f), V(104f, 106f), 9f, roll);
            for (int i = 0; i < 4; i++) p.Capsule(V(42f, 90f - i * 12f), V(86f - (i % 2) * 14f, 90f - i * 12f), 1.8f, Mat.Glow(new Color(0.45f, 0.35f, 0.25f)));
            p.Circle(V(64f, 38f), 14f, Mat.Of(seal, Color.white, 3f));                               // wax seal
            p.Poly(new[] { V(64f, 48f), V(58f, 30f), V(70f, 30f) }, Mat.Of(Color.Lerp(seal, Color.white, 0.5f), Color.white, 2f));
            return Done(p);
        }

        public static Color32[] Flask(bool full)
        {
            var p = P();
            Mat glass = Mat.Of(new Color(0.75f, 0.9f, 0.88f), Color.white, 3f);
            p.Circle(V(64f, 44f), 36f, glass);
            p.Capsule(V(64f, 74f), V(64f, 104f), 12f, 9f, glass);
            if (full)
            {
                p.Ellipse(V(64f, 38f), 29f, 24f, 0f, Mat.Glow(new Color(0.35f, 0.9f, 0.45f)));
                p.Ellipse(V(52f, 48f), 7f, 5f, 20f, Mat.Glow(new Color(0.8f, 1f, 0.8f)));
            }
            p.Capsule(V(64f, 104f), V(64f, 116f), 10f, Wood);
            p.Capsule(V(48f, 88f), V(80f, 88f), 3f, Mat.Of(new Color(0.9f, 0.7f, 0.3f), Color.white, 2f));   // a brass band
            return Done(p);
        }

        /// <summary>The open jaws of a snare trap waiting on the floor.</summary>
        public static Color32[] TrapSet()
        {
            var p = P();
            Mat jaw = Mat.Of(new Color(0.5f, 0.54f, 0.6f), Color.white, 3f);
            p.Ellipse(V(64f, 30f), 54f, 12f, 0f, Iron);
            for (int i = 0; i < 6; i++)
            {
                float x = 28f + i * 14.5f;
                p.Poly(new[] { V(x - 5f, 30f), V(x + 5f, 30f), V(x, 56f + (i % 2) * 6f) }, jaw);
            }
            p.Capsule(V(36f, 22f), V(92f, 22f), 4f, Mat.Of(new Color(0.4f, 0.8f, 0.35f), Color.white, 3f));
            p.Circle(V(64f, 24f), 7f, Gold);
            return Done(p);
        }
    }
}
