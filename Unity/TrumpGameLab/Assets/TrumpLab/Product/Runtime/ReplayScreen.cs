#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class ReplayScreen : ProductScreen
    {
        [SerializeField] private Text? statusLabel;
        [SerializeField] private Text? tableLabel;
        [SerializeField] private Button? backButton;

        public override ScreenId Id => ScreenId.Replay;
        public Text StatusLabel => statusLabel ?? throw Missing(nameof(statusLabel));
        public Text TableLabel => tableLabel ?? throw Missing(nameof(tableLabel));
        public event System.Action? BackRequested;

        public void Configure(Text status, Text table, Button back)
        {
            statusLabel = status;
            tableLabel = table;
            backButton = back;
        }

        public void Render(GamePresentation presentation, int appliedActions)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            MatchViewModel model = CrazyEightsMatchPresenter.Create(presentation, inputEnabled: false);
            StatusLabel.text = "Replayed " + appliedActions + " actions  ·  " + model.Status;
            string result = presentation.Result == null
                ? "Saved before the match ended."
                : CrazyEightsResultPresenter.Create(presentation.Result).Summary;
            TableLabel.text = model.OpponentHand + "\n\n" + model.Stock + "     " +
                model.Discard + "\n\n" + model.HumanHand + "\n\n" + result;
        }

        private void Awake()
        {
            if (statusLabel == null || tableLabel == null || backButton == null)
                throw new InvalidOperationException("Replay controls are not configured.");
            backButton.onClick.AddListener(HandleBack);
        }

        private void OnDestroy()
        {
            if (backButton != null) backButton.onClick.RemoveListener(HandleBack);
        }

        private void HandleBack() => BackRequested?.Invoke();
        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException("Replay control is not configured: " + name);
    }
}
