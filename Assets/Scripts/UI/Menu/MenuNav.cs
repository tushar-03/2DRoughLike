using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Roughlike.UI
{
    /// <summary>
    /// Builds explicit controller / keyboard navigation for whatever is on screen. Controls are grouped into rows by their screen
    /// position; left and right step along a row, up and down move to the nearest control in the row above or below. A row that
    /// holds a single slider or cycle control has nothing on its left or right, so those keys adjust the control itself.
    /// Automatic navigation guesses from geometry and, with sliders spanning the whole window, guesses wrong.
    /// </summary>
    public static class MenuNav
    {
        sealed class Item
        {
            public Selectable S;
            public float X, Y, HalfHeight;
        }

        /// <summary>Links every active, interactable selectable under <paramref name="root"/>. Safe to call again after the contents change.</summary>
        public static void AutoLink(Transform root)
        {
            if (root == null) return;
            var items = new List<Item>();
            foreach (var s in root.GetComponentsInChildren<Selectable>(false))
            {
                if (s == null || !s.gameObject.activeInHierarchy || !s.IsActive() || s is Scrollbar) continue;
                var rt = s.transform as RectTransform;
                if (rt == null) continue;
                Vector3 c = rt.TransformPoint(rt.rect.center);
                items.Add(new Item { S = s, X = c.x, Y = c.y, HalfHeight = Mathf.Max(4f, rt.rect.height * Mathf.Abs(rt.lossyScale.y) * 0.5f) });
            }
            if (items.Count == 0) return;

            // top to bottom, then rows of items whose centres sit at about the same height
            items.Sort((a, b) => b.Y.CompareTo(a.Y));
            var rows = new List<List<Item>>();
            foreach (var it in items)
            {
                List<Item> row = rows.Count > 0 ? rows[rows.Count - 1] : null;
                if (row != null && Mathf.Abs(row[0].Y - it.Y) < Mathf.Min(row[0].HalfHeight, it.HalfHeight) * 0.9f) row.Add(it);
                else rows.Add(new List<Item> { it });
            }
            foreach (var r in rows) r.Sort((a, b) => a.X.CompareTo(b.X));

            for (int ri = 0; ri < rows.Count; ri++)
            {
                for (int ci = 0; ci < rows[ri].Count; ci++)
                {
                    var it = rows[ri][ci];
                    var nav = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnLeft = ci > 0 ? rows[ri][ci - 1].S : null,
                        selectOnRight = ci < rows[ri].Count - 1 ? rows[ri][ci + 1].S : null,
                        selectOnUp = ri > 0 ? Nearest(rows[ri - 1], it.X) : null,
                        selectOnDown = ri < rows.Count - 1 ? Nearest(rows[ri + 1], it.X) : null
                    };
                    it.S.navigation = nav;
                }
            }
        }

        static Selectable Nearest(List<Item> row, float x)
        {
            Item best = row[0];
            foreach (var it in row) if (Mathf.Abs(it.X - x) < Mathf.Abs(best.X - x)) best = it;
            return best.S;
        }
    }
}
