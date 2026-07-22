// Elements.cs — Element-interactie-matrix (vertaling van src/core/elements.ts).
// Puur, zero-dependency, geen MonoBehaviour.

using System.Collections.Generic;

namespace IdleCloud.Core
{
    public static class Elements
    {
        // ── Element-chart ──────────────────────────────────────────────────────
        // Rij = aanvaller, kolom = verdediger. Waarden: 0.5 / 1.0 / 1.5.
        // Spiegelt de ELEMENT_CHART-constante in elements.ts exact.
        public static readonly Dictionary<Element, Dictionary<Element, double>> Chart =
            new Dictionary<Element, Dictionary<Element, double>>
            {
                {
                    Element.Physical, new Dictionary<Element, double>
                    {
                        { Element.Physical, 1.0 },
                        { Element.Fire,     1.0 },
                        { Element.Ice,      1.0 },
                        { Element.Nature,   1.0 },
                        { Element.Arcane,   1.0 },
                    }
                },
                {
                    Element.Fire, new Dictionary<Element, double>
                    {
                        { Element.Physical, 1.0 },
                        { Element.Fire,     0.5 },
                        { Element.Ice,      1.5 },
                        { Element.Nature,   1.5 },
                        { Element.Arcane,   1.0 },
                    }
                },
                {
                    Element.Ice, new Dictionary<Element, double>
                    {
                        { Element.Physical, 1.0 },
                        { Element.Fire,     1.5 },
                        { Element.Ice,      0.5 },
                        { Element.Nature,   0.5 },
                        { Element.Arcane,   1.0 },
                    }
                },
                {
                    Element.Nature, new Dictionary<Element, double>
                    {
                        { Element.Physical, 1.0 },
                        { Element.Fire,     0.5 },
                        { Element.Ice,      1.5 },
                        { Element.Nature,   0.5 },
                        { Element.Arcane,   1.0 },
                    }
                },
                {
                    Element.Arcane, new Dictionary<Element, double>
                    {
                        { Element.Physical, 1.5 },
                        { Element.Fire,     1.0 },
                        { Element.Ice,      1.0 },
                        { Element.Nature,   1.0 },
                        { Element.Arcane,   0.5 },
                    }
                },
            };

        /// <summary>
        /// Geeft de schade-multiplier voor aanvaller-element vs verdediger-element.
        /// Geeft 1.0 terug als de combinatie niet in de chart staat (veilige fallback).
        /// </summary>
        public static double ElementMultiplier(Element attacker, Element defender)
        {
            if (Chart.TryGetValue(attacker, out var row) &&
                row.TryGetValue(defender, out double mult))
                return mult;
            return 1.0;
        }
    }
}
