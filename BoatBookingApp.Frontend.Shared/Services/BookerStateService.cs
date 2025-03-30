using System;
using BoatBookingApp.Frontend.Shared.Models;

namespace BoatBookingApp.Frontend.Shared.Services
{
    public class BookerStateService
    {
        public string SelectedBookerShortName { get; set; } = "ID"; // Defaultna vrijednost "ID"

        public event Action OnChange;

        public void SetSelectedBookerShortName(string shortName)
        {
            SelectedBookerShortName = shortName;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}