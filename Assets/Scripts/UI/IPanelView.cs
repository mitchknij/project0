// IPanelView.cs — Gedeelde interface voor alle UI-panels.
// Implementeer dit op elke panel-presenter; UIRefreshDriver en PanelManager
// werken uitsluitend via deze interface.

namespace IdleCloud.UI
{
    public interface IPanelView
    {
        /// <summary>Updatet visuele waarden vanuit GameManager-state (4 Hz of na mutatie).</summary>
        void Refresh();

        /// <summary>Maakt het panel zichtbaar.</summary>
        void Show();

        /// <summary>Verbergt het panel.</summary>
        void Hide();

        /// <summary>Geeft aan of het panel momenteel zichtbaar is.</summary>
        bool IsVisible { get; }
    }
}
