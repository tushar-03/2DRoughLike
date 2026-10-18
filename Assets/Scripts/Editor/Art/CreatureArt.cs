using UnityEngine;

namespace Roughlike.Editor.Art
{
    /// <summary>Non-humanoid bodies: the floating bell-wisp, the charging boar and the swollen bloat, each also in a second-biome costume.</summary>
    public static class CreatureArt
    {
        const int F = HumanoidArt.FrameSize;
        const float FloorY = 34f;
        const float CentreX = 140f;
        static readonly Color Ink = new Color(0.07f, 0.05f, 0.1f);

        /// <summary>The colours of one creature costume, so the same body can be repainted for another biome.</summary>
        struct Theme
        {
            public Color Body, Accent, Flame, Dark, Eye;
        }

        static readonly Theme WispDrowned = new Theme { Body = new Color(0.55f, 0.78f, 0.92f), Accent = new Color(0.95f, 0.75f, 0.32f), Flame = new Color(0.45f, 0.9f, 1f), Eye = new Color(0.15f, 0.2f, 0.4f) };
        static readonly Theme WispEmber = new Theme { Body = new Color(0.62f, 0.3f, 0.22f), Accent = new Color(0.35f, 0.3f, 0.3f), Flame = new Color(1f, 0.5f, 0.12f), Eye = new Color(0.4f, 0.1f, 0.05f) };
        static readonly Theme BoarDrowned = new Theme { Body = new Color(0.56f, 0.38f, 0.28f), Dark = new Color(0.28f, 0.18f, 0.2f), Eye = new Color(1f, 0.45f, 0.18f) };
        static readonly Theme BoarCinder = new Theme { Body = new Color(0.3f, 0.26f, 0.28f), Dark = new Color(0.55f, 0.18f, 0.1f), Eye = new Color(1f, 0.8f, 0.2f) };
        static readonly Theme BloatDrowned = new Theme { Body = new Color(0.72f, 0.78f, 0.36f), Eye = new Color(0.9f, 0.3f, 0.15f) };
        static readonly Theme BloatAsh = new Theme { Body = new Color(0.42f, 0.38f, 0.4f), Eye = new Color(1f, 0.55f, 0.12f) };
        static Theme T;

        public static Color32[] Wisp(string clip, float t) { T = WispDrowned; return WispPaint(clip, t); }
        public static Color32[] EmberWisp(string clip, float t) { T = WispEmber; return WispPaint(clip, t); }
        public static Color32[] Boar(string clip, float t) { T = BoarDrowned; return BoarPaint(clip, t); }
        public static Color32[] CinderBoar(string clip, float t) { T = BoarCinder; return BoarPaint(clip, t); }
        public static Color32[] Bloat(string clip, float t) { T = BloatDrowned; return BloatPaint(clip, t); }
        public static Color32[] AshBomber(string clip, float t) { T = BloatAsh; return BloatPaint(clip, t); }

        static float Smooth(float t) => t * t * (3f - 2f * t);
        static Vector2 Dir(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }

        // ====================================================================== bell-wisp

        static Color32[] WispPaint(string clip, float t)
        {
            var pt = new Painter(F, F);
            float ph = t * Mathf.PI * 2f;
            float bob = 0f, squash = 1f, tilt = 0f, flame = 1f, shake = 0f, glowBoost = 0f;
            bool closed = false;
            switch (clip)
            {
                case "idle": bob = Mathf.Sin(ph) * 7f; flame = 1f + Mathf.Sin(ph * 2f) * 0.15f; tilt = Mathf.Sin(ph) * 4f; break;
                case "telegraph": squash = 1f - t * 0.16f; tilt = -16f * Smooth(Mathf.Min(1f, t * 2f)); shake = Mathf.Sin(t * 120f) * 3f * t; glowBoost = t; flame = 1.2f + t * 0.8f; bob = 10f * t; break;
                case "attack": squash = 1.28f; tilt = 40f; flame = 2f; glowBoost = 1f; bob = -4f; break;
                case "hurt": squash = 0.82f; tilt = -22f; closed = true; flame = 0.6f; break;
            }

            Vector2 c = new Vector2(CentreX + shake, FloorY + 80f + bob);
            float w = 64f / Mathf.Sqrt(squash), h = 72f * squash;
            Color aqua = Color.Lerp(T.Body, Color.white, glowBoost * 0.35f);
            Mat body = Mat.Of(aqua, new Color(0.9f, 1f, 1f));
            Mat gold = Mat.Of(T.Accent, Color.white);
            Mat flameMat = Mat.Glow(Color.Lerp(T.Flame, Color.white, 0.25f + glowBoost * 0.5f));
            Mat flameCore = Mat.Glow(Color.white);

            // flames licking up from the crown
            for (int i = 0; i < 3; i++)
            {
                float off = (i - 1) * 16f;
                float len = (40f + (i == 1 ? 26f : 0f)) * flame;
                float wob = Mathf.Sin(ph * 2f + i * 2f) * 6f;
                Vector2 b = c + new Vector2(off, h * 0.8f);
                pt.Poly(new[] { b + new Vector2(-9f, 0f), b + new Vector2(wob, len), b + new Vector2(9f, 0f) }, flameMat);
            }
            pt.Poly(new[] { c + new Vector2(-5f, h * 0.8f), c + new Vector2(Mathf.Sin(ph * 2f) * 3f, h * 0.8f + 34f * flame), c + new Vector2(5f, h * 0.8f) }, flameCore);

            // bell: dome plus flared skirt, rotated by tilt
            Vector2 R(Vector2 v) { float a = tilt * Mathf.Deg2Rad; return c + new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a)); }
            pt.Ellipse(R(new Vector2(0f, h * 0.18f)), w * 0.82f, h * 0.7f, tilt, body);
            pt.Poly(new[] { R(new Vector2(-w * 0.8f, h * 0.1f)), R(new Vector2(w * 0.8f, h * 0.1f)), R(new Vector2(w * 1.12f, -h * 0.7f)), R(new Vector2(-w * 1.12f, -h * 0.7f)) }, body);
            pt.Capsule(R(new Vector2(-w * 1.1f, -h * 0.72f)), R(new Vector2(w * 1.1f, -h * 0.72f)), 8f, gold);   // gold lip
            pt.Circle(R(new Vector2(0f, -h * 0.72f - 18f)), 9f, gold);                                           // clapper
            pt.Capsule(R(new Vector2(0f, -h * 0.4f)), R(new Vector2(0f, -h * 0.72f)), 2.5f, gold);

            // face
            Vector2 e1 = R(new Vector2(-w * 0.28f, h * 0.15f)), e2 = R(new Vector2(w * 0.38f, h * 0.15f));
            if (closed)
            {
                pt.Capsule(e1 + new Vector2(-7f, 0f), e1 + new Vector2(7f, -2f), 2.5f, Mat.Glow(Ink));
                pt.Capsule(e2 + new Vector2(-7f, 0f), e2 + new Vector2(7f, -2f), 2.5f, Mat.Glow(Ink));
            }
            else
            {
                Mat eye = Mat.Glow(Color.Lerp(T.Eye, new Color(1f, 0.6f, 0.2f), glowBoost));
                pt.Ellipse(e1, 8f, 11f, 0f, eye); pt.Ellipse(e2, 8f, 11f, 0f, eye);
                pt.Circle(e1 + new Vector2(2f, 3f), 2.6f, Mat.Glow(Color.white)); pt.Circle(e2 + new Vector2(2f, 3f), 2.6f, Mat.Glow(Color.white));
            }
            return pt.Render(3.2f, Ink);
        }

        // ====================================================================== tideboar

        static Color32[] BoarPaint(string clip, float t)
        {
            var pt = new Painter(F, F);
            float ph = t * Mathf.PI * 2f;
            Color fur = T.Body;
            Mat body = Mat.Of(fur, new Color(1f, 0.8f, 0.55f));
            Mat dark = Mat.Of(Color.Lerp(T.Dark, T.Body, 0.25f));
            Mat bristle = Mat.Of(T.Dark, new Color(0.9f, 0.6f, 0.4f));
            Mat tusk = Mat.Of(new Color(0.95f, 0.9f, 0.76f), Color.white);
            Mat hoof = Mat.Of(new Color(0.2f, 0.15f, 0.15f));
            Mat glowEye = Mat.Glow(T.Eye);

            float bodyY = FloorY + 66f, stride = 0f, lift = 0f, headDrop = 0f, bounce = 0f, rear = 0f, pawX = 0f, pawLift = 0f;
            bool closed = false;
            switch (clip)
            {
                case "idle": bounce = Mathf.Sin(ph) * 2f; headDrop = Mathf.Sin(ph) * 2f; break;
                case "walk": stride = 22f; lift = 12f; bounce = Mathf.Abs(Mathf.Cos(ph)) * 3f; break;
                case "telegraph": headDrop = 26f * Smooth(Mathf.Min(1f, t * 2.5f)); bounce = Mathf.Sin(t * 70f) * 2f; pawLift = 28f; pawX = Mathf.Sin(t * 42f) * 22f; break;
                case "attack": stride = 44f; lift = 30f; headDrop = 20f; bounce = Mathf.Sin(ph * 2f) * 5f; break;
                case "hurt": rear = 14f; closed = true; headDrop = -10f; break;
            }
            bodyY += bounce - rear * 0.3f;
            Vector2 chest = new Vector2(CentreX + 46f, bodyY + 6f + rear * 0.8f);
            Vector2 rump = new Vector2(CentreX - 52f, bodyY + 2f);

            // far legs, then body, then near legs
            Leg(pt, new Vector2(CentreX + 44f, bodyY - 12f), clip == "telegraph" ? new Vector2(CentreX + 54f, FloorY + 6f) : FootAt(CentreX + 48f, ph + 0.5f * 2f * Mathf.PI * 0.5f, stride, lift), dark, hoof, 1f);
            Leg(pt, new Vector2(CentreX - 48f, bodyY - 12f), FootAt(CentreX - 54f, ph, stride, lift), dark, hoof, -1f);

            // bristled mane and tail
            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(CentreX - 56f, CentreX + 52f, i / 5f);
                float y = bodyY + 38f + Mathf.Sin(i * 1.7f) * 3f + (i > 3 ? -6f : 0f);
                pt.Poly(new[] { new Vector2(x - 11f, y - 6f), new Vector2(x - 2f + (i < 3 ? -4f : 2f), y + 26f), new Vector2(x + 11f, y - 6f) }, bristle);
            }
            pt.Capsule(rump + new Vector2(-8f, 14f), rump + new Vector2(-34f, 34f + Mathf.Sin(ph * (clip == "attack" ? 2f : 1f)) * 6f), 5f, 3f, dark);

            pt.Ellipse(new Vector2(CentreX, bodyY), 84f, 48f + (clip == "idle" ? Mathf.Sin(ph) * 1.5f : 0f), 0f, body);
            pt.Ellipse(new Vector2(CentreX - 24f, bodyY - 20f), 52f, 22f, 0f, Mat.Of(Color.Lerp(fur, new Color(0.85f, 0.7f, 0.5f), 0.4f)));

            Leg(pt, new Vector2(CentreX + 52f, bodyY - 14f), clip == "telegraph" ? new Vector2(CentreX + 62f + pawX, FloorY + 6f + Mathf.Max(0f, Mathf.Sin(t * 42f)) * pawLift) : FootAt(CentreX + 56f, ph, stride, lift), body, hoof, 1f);
            Leg(pt, new Vector2(CentreX - 44f, bodyY - 14f), FootAt(CentreX - 50f, ph + 0.5f * 2f * Mathf.PI * 0.5f, stride, lift), body, hoof, -1f);

            // head
            Vector2 head = chest + new Vector2(46f, -headDrop + rear * 0.5f);
            pt.Ellipse(head, 40f, 33f, -8f - headDrop * 0.4f, body);
            Vector2 snout = head + new Vector2(34f, -10f - headDrop * 0.1f);
            pt.Ellipse(snout, 22f, 18f, -10f, Mat.Of(Color.Lerp(fur, new Color(0.9f, 0.6f, 0.55f), 0.35f)));
            pt.Circle(snout + new Vector2(14f, 4f), 4f, Mat.Glow(Ink));
            pt.Poly(new[] { head + new Vector2(14f, -22f), head + new Vector2(50f, 6f), head + new Vector2(30f, -22f) }, tusk);          // tusk
            pt.Poly(new[] { head + new Vector2(-24f, 20f), head + new Vector2(-34f, 52f), head + new Vector2(-4f, 30f) }, dark);          // ear
            Vector2 eye = head + new Vector2(14f, 12f);
            if (closed) pt.Capsule(eye + new Vector2(-6f, 0f), eye + new Vector2(8f, -2f), 2.4f, Mat.Glow(Ink));
            else { pt.Ellipse(eye, 7f, 5f, 15f, glowEye); pt.Circle(eye + new Vector2(2f, 1f), 2f, Mat.Glow(Color.white)); }
            return pt.Render(3.4f, Ink);
        }

        static Vector2 FootAt(float x, float phase, float stride, float lift)
        {
            float p = Mathf.Repeat(phase / (Mathf.PI * 2f), 1f);
            if (stride <= 0f) return new Vector2(x + 4f, FloorY + 6f);
            if (p < 0.5f) return new Vector2(x + Mathf.Lerp(stride, -stride, p * 2f), FloorY + 6f);
            float u = (p - 0.5f) * 2f;
            return new Vector2(x + Mathf.Lerp(-stride, stride, Smooth(u)), FloorY + 6f + lift * Mathf.Sin(Mathf.PI * u));
        }

        static void Leg(Painter pt, Vector2 root, Vector2 foot, Mat m, Mat hoof, float kneeSide)
        {
            Vector2 d = foot - root;
            float l = Mathf.Min(d.magnitude, 90f);
            Vector2 mid = root + d * 0.5f + new Vector2(kneeSide * 10f, 0f) * (1f - Mathf.Clamp01(l / 92f) * 0.6f);
            pt.Capsule(root, mid, 15f, 11f, m);
            pt.Capsule(mid, foot, 11f, 9f, m);
            pt.Capsule(foot + new Vector2(-4f, 2f), foot + new Vector2(10f, 0f), 9f, hoof);
        }

        // ====================================================================== bloat

        static Color32[] BloatPaint(string clip, float t)
        {
            var pt = new Painter(F, F);
            float ph = t * Mathf.PI * 2f;
            float sx = 1f, sy = 1f, shake = 0f, crack = 0f, flash = 0f, stepL = 0f, stepR = 0f;
            bool closed = false, mouthOpen = false;
            switch (clip)
            {
                case "idle": sy = 1f + Mathf.Sin(ph) * 0.035f; sx = 1f - Mathf.Sin(ph) * 0.02f; break;
                case "walk": sx = 1f + Mathf.Sin(ph * 2f) * 0.05f; sy = 1f - Mathf.Sin(ph * 2f) * 0.05f; stepL = Mathf.Max(0f, Mathf.Sin(ph)) * 14f; stepR = Mathf.Max(0f, -Mathf.Sin(ph)) * 14f; break;
                case "telegraph": sx = sy = 1f + t * 0.12f; shake = Mathf.Sin(t * 110f) * 3.5f * t; crack = t; mouthOpen = t > 0.4f; break;
                case "attack": sx = sy = 1.3f; crack = 1f; flash = 1f; mouthOpen = true; break;
                case "hurt": sx = 1.12f; sy = 0.86f; closed = true; break;
            }

            Color sick = Color.Lerp(T.Body, Color.white, flash * 0.45f);
            Mat body = Mat.Of(sick, new Color(1f, 0.95f, 0.6f));
            Mat belly = Mat.Of(Color.Lerp(sick, new Color(0.95f, 0.9f, 0.6f), 0.5f));
            Mat pus = Mat.Of(new Color(0.95f, 0.88f, 0.45f), Color.white);
            Mat foot = Mat.Of(new Color(0.4f, 0.4f, 0.28f));
            Mat glowRed = Mat.Glow(Color.Lerp(T.Eye, new Color(1f, 0.9f, 0.4f), crack));

            Vector2 c = new Vector2(CentreX + shake, FloorY + 66f * sy + 8f);
            pt.Capsule(new Vector2(CentreX - 24f, FloorY + 14f + stepL), new Vector2(CentreX - 28f, FloorY + 40f), 11f, foot);
            pt.Capsule(new Vector2(CentreX + 24f, FloorY + 14f + stepR), new Vector2(CentreX + 28f, FloorY + 40f), 11f, foot);
            pt.Ellipse(c, 66f * sx, 62f * sy, 0f, body);
            pt.Ellipse(c + new Vector2(-6f, -22f * sy), 44f * sx, 28f * sy, 0f, belly);

            // pustules and cracks
            pt.Circle(c + new Vector2(-36f * sx, 30f * sy), 13f, pus);
            pt.Circle(c + new Vector2(30f * sx, 40f * sy), 10f, pus);
            pt.Circle(c + new Vector2(-14f * sx, 52f * sy), 8f, pus);
            if (crack > 0.05f)
            {
                float k = crack;
                pt.Capsule(c + new Vector2(-40f, 8f) * k, c + new Vector2(-10f, 34f) * k, 2.6f * k + 0.5f, glowRed);
                pt.Capsule(c + new Vector2(8f, -30f) * k, c + new Vector2(38f, 4f) * k, 2.6f * k + 0.5f, glowRed);
                pt.Capsule(c + new Vector2(-24f, -34f) * k, c + new Vector2(6f, -4f) * k, 2.2f * k + 0.5f, glowRed);
            }

            // face: one big eye, wide grin
            Vector2 eye = c + new Vector2(22f * sx, 18f * sy);
            if (closed) pt.Capsule(eye + new Vector2(-12f, 0f), eye + new Vector2(12f, -3f), 3f, Mat.Glow(Ink));
            else
            {
                pt.Circle(eye, 17f, Mat.Glow(new Color(0.96f, 0.95f, 0.85f)));
                pt.Circle(eye + new Vector2(4f, 0f), 8f, Mat.Glow(new Color(0.9f, 0.3f, 0.12f)));
                pt.Circle(eye + new Vector2(6f, 1f), 3.5f, Mat.Glow(Ink));
            }
            Vector2 mouth = c + new Vector2(14f * sx, -14f * sy);
            if (mouthOpen)
            {
                pt.Ellipse(mouth, 26f, 15f, 0f, Mat.Glow(new Color(0.45f, 0.08f, 0.1f)));
                for (int i = -2; i <= 2; i++) pt.Poly(new[] { mouth + new Vector2(i * 10f - 4f, 12f), mouth + new Vector2(i * 10f, 2f), mouth + new Vector2(i * 10f + 4f, 12f) }, Mat.Glow(new Color(0.95f, 0.92f, 0.8f)));
            }
            else
            {
                pt.Capsule(mouth + new Vector2(-24f, 2f), mouth + new Vector2(26f, -4f), 3.2f, Mat.Glow(Ink));
                for (int i = -2; i <= 2; i++) pt.Poly(new[] { mouth + new Vector2(i * 10f - 3f, -1f), mouth + new Vector2(i * 10f, -10f), mouth + new Vector2(i * 10f + 3f, -1f) }, Mat.Glow(new Color(0.95f, 0.92f, 0.8f)));
            }
            return pt.Render(3.4f, Ink);
        }

        // ====================================================================== shield prop (tower shield for the Shell Guard)

        public static Color32[] ShieldProp(bool broken)
        {
            var pt = new Painter(F, F);
            Vector2 O = new Vector2(CentreX + 72f, FloorY);
            Color steel = broken ? new Color(0.36f, 0.38f, 0.42f) : new Color(0.55f, 0.66f, 0.8f);
            Mat m = Mat.Of(steel, broken ? (Color?)null : Color.white);
            Mat trim = Mat.Of(broken ? new Color(0.45f, 0.4f, 0.3f) : new Color(0.92f, 0.78f, 0.42f), broken ? (Color?)null : Color.white);
            pt.Poly(new[] { O + new Vector2(0f, 226f), O + new Vector2(26f, 196f), O + new Vector2(28f, 40f), O + new Vector2(0f, 6f), O + new Vector2(-28f, 40f), O + new Vector2(-26f, 196f) }, m);
            pt.Capsule(O + new Vector2(0f, 214f), O + new Vector2(0f, 20f), 5f, trim);
            pt.Capsule(O + new Vector2(-22f, 150f), O + new Vector2(22f, 150f), 4f, trim);
            pt.Circle(O + new Vector2(0f, 120f), 12f, trim);
            if (broken)
            {
                Mat crack = Mat.Glow(new Color(0.07f, 0.05f, 0.1f));
                pt.Capsule(O + new Vector2(-10f, 200f), O + new Vector2(4f, 150f), 2.6f, crack);
                pt.Capsule(O + new Vector2(4f, 150f), O + new Vector2(-8f, 100f), 2.6f, crack);
                pt.Capsule(O + new Vector2(-8f, 100f), O + new Vector2(10f, 60f), 2.6f, crack);
            }
            return pt.Render(3.4f, Ink);
        }
    }
}
