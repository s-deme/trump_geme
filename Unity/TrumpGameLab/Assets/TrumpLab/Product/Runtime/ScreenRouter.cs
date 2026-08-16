#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public interface IPreferredFocusProvider
    {
        Selectable? PreferredFocus { get; }
    }

    public sealed class ScreenRouter : MonoBehaviour
    {
        [SerializeField] private ProductScreen[] screens = Array.Empty<ProductScreen>();

        public ScreenId? Current { get; private set; }
        public IReadOnlyList<ProductScreen> Screens => screens;

        public void Configure(ProductScreen[] configuredScreens)
        {
            screens = configuredScreens?.ToArray() ??
                throw new ArgumentNullException(nameof(configuredScreens));
            ValidateScreens();
        }

        public ProductScreen Get(ScreenId id) => screens.Single(screen => screen.Id == id);

        public void RestoreFocus()
        {
            if (Current.HasValue) FocusFirstControl(Get(Current.Value));
        }

        public static Selectable? FindFocusTarget(ProductScreen screen)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            Selectable? preferred = (screen as IPreferredFocusProvider)?.PreferredFocus;
            return preferred != null && preferred.IsActive() && preferred.IsInteractable()
                ? preferred
                : screen.GetComponentsInChildren<Selectable>(includeInactive: false)
                    .FirstOrDefault(control => control.IsActive() && control.IsInteractable());
        }

        public void Show(ScreenId id)
        {
            ValidateScreens();
            ProductScreen requested = Get(id);
            foreach (ProductScreen screen in screens) screen.SetVisible(screen == requested);
            Current = id;
            FocusFirstControl(requested);
        }

        private void ValidateScreens()
        {
            if (screens.Length != Enum.GetValues(typeof(ScreenId)).Length ||
                screens.Any(screen => screen == null) ||
                screens.Select(screen => screen.Id).Distinct().Count() != screens.Length)
                throw new InvalidOperationException("Screen router requires one screen for every ScreenId.");
        }

        private static void FocusFirstControl(ProductScreen screen)
        {
            EventSystem? eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            eventSystem.SetSelectedGameObject(null);
            Selectable? first = FindFocusTarget(screen);
            if (first != null) eventSystem.SetSelectedGameObject(first.gameObject);
        }
    }
}
