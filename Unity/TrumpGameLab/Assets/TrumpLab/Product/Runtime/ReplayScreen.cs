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
        private IProductText text = ProductTextCatalog.English;
        private GamePresentation? lastPresentation;
        private int lastAppliedActions;

        public override ScreenId Id => ScreenId.Replay;
        public Text StatusLabel => statusLabel ?? throw Missing(nameof(statusLabel));
        public Text TableLabel => tableLabel ?? throw Missing(nameof(tableLabel));
        public event System.Action? BackRequested;

        public void Configure(Text status, Text table, Button back)
        {
            statusLabel = status;
            tableLabel = table;
            backButton = back;
            RefreshButtonText();
        }

        public void SetText(IProductText configuredText)
        {
            text = configuredText ?? throw new ArgumentNullException(nameof(configuredText));
            RefreshButtonText();
            if (lastPresentation != null) Render(lastPresentation, lastAppliedActions);
        }

        public void Render(GamePresentation presentation, int appliedActions)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            lastPresentation = presentation;
            lastAppliedActions = appliedActions;
            MatchViewModel model = CrazyEightsMatchPresenter.Create(
                presentation, inputEnabled: false, text: text);
            StatusLabel.text = text.Get("replay.status", appliedActions, model.Status);
            string result = presentation.Result == null
                ? text.Get("replay.not_finished")
                : CrazyEightsResultPresenter.Create(presentation.Result, text: text).Summary;
            TableLabel.text = text.Get("replay.table", model.OpponentHand, model.Stock,
                model.Discard, model.HumanHand, result);
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
        private void RefreshButtonText()
        {
            Text? label = backButton?.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text.Get("replay.back");
        }
        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException("Replay control is not configured: " + name);
    }
}
