using UnityEngine;
using UnityEngine.UI;

namespace Shakki.Presentation.UI
{
    /// <summary>
    /// UGUI "flow/wrap" layout: asettelee lapset vasemmalta oikealle ja rivitt‰‰ kun leveys loppuu.
    /// Ei pakota yhten‰ist‰ cellsizea kuten GridLayoutGroup.
    ///
    /// Drop-in version:
    /// - k‰ytt‰‰ LayoutUtility Min/Preferred -arvoja oikein
    /// - clampaa lapsen leveyden aina rivin leveyteen (est‰‰ "valtavat" tagit)
    /// - tukee pakotettua korkeutta (pillereille)
    /// </summary>
    [AddComponentMenu("Layout/Flow Layout Group")]
    public sealed class FlowLayoutGroup : LayoutGroup
    {
        [Header("Flow")]
        [SerializeField] private float spacingX = 8f;
        [SerializeField] private float spacingY = 8f;

        [SerializeField] private bool expandWidth = false;     // jos true, venytt‰‰ lapsen rivin leveyteen (usein false pillereille)
        [SerializeField] private bool forceChildHeight = false;
        [SerializeField] private float forcedHeight = 28f;

        [Header("Safety")]
        [SerializeField] private bool ignoreInactive = true;
        [SerializeField] private bool clampChildWidth = true;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            // Emme tarjoa "preferred width" t‰lle groupille (se on yleens‰ parentin m‰‰r‰‰m‰).
            SetLayoutInputForAxis(0, 0, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float innerWidth = GetInnerWidth();
            float x = 0f;
            float y = 0f;
            float rowH = 0f;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                if (ignoreInactive && !child.gameObject.activeInHierarchy) continue;

                float w = GetChildWidth(child, innerWidth);
                float h = GetChildHeight(child);

                // wrap
                if (x > 0f && x + w > innerWidth)
                {
                    x = 0f;
                    y += rowH + spacingY;
                    rowH = 0f;
                }

                x += w + spacingX;
                rowH = Mathf.Max(rowH, h);
            }

            float totalH = y + rowH + padding.top + padding.bottom;
            SetLayoutInputForAxis(totalH, totalH, -1, 1);
        }

        public override void SetLayoutHorizontal() => DoLayout();
        public override void SetLayoutVertical() => DoLayout();

        private void DoLayout()
        {
            float innerWidth = GetInnerWidth();

            float x = padding.left;
            float y = padding.top;
            float rowH = 0f;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                var child = rectChildren[i];
                if (ignoreInactive && !child.gameObject.activeInHierarchy) continue;

                float w = GetChildWidth(child, innerWidth);
                float h = GetChildHeight(child);

                // wrap
                if (x > padding.left && (x - padding.left) + w > innerWidth)
                {
                    x = padding.left;
                    y += rowH + spacingY;
                    rowH = 0f;
                }

                // optional: expand to full available width (list items, not pills)
                float finalW = expandWidth ? innerWidth : w;

                if (clampChildWidth)
                    finalW = Mathf.Min(finalW, innerWidth);

                SetChildAlongAxis(child, 0, x, finalW);
                SetChildAlongAxis(child, 1, y, h);

                x += finalW + spacingX;
                rowH = Mathf.Max(rowH, h);
            }
        }

        private float GetInnerWidth()
        {
            float w = rectTransform.rect.width - padding.left - padding.right;
            return (w <= 0f) ? 1f : w;
        }

        private float GetChildWidth(RectTransform child, float maxWidth)
        {
            // Preferred voi palauttaa parentin leveyden jos child on stretch / ilman layout-infoa.
            float min = LayoutUtility.GetMinWidth(child);
            float pref = LayoutUtility.GetPreferredWidth(child);

            float w = Mathf.Max(min, pref);

            // Fallback: joskus LayoutUtility palauttaa 0 jos mit‰‰n layout-komponentteja ei ole.
            if (w <= 0.01f) w = child.rect.width;
            if (w <= 0.01f) w = 1f;

            if (clampChildWidth)
                w = Mathf.Min(w, maxWidth);

            return w;
        }

        private float GetChildHeight(RectTransform child)
        {
            if (forceChildHeight) return forcedHeight;

            float min = LayoutUtility.GetMinHeight(child);
            float pref = LayoutUtility.GetPreferredHeight(child);

            float h = Mathf.Max(min, pref);

            if (h <= 0.01f) h = child.rect.height;
            if (h <= 0.01f) h = 1f;

            return h;
        }
    }
}
