#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class ResultScreen : ProductScreen
    {
        [SerializeField] private Text? summaryLabel;
        [SerializeField] private Button? detailsButton;
        [SerializeField] private Button? rematchButton;
        [SerializeField] private Button? titleButton;
        private IProductText text = ProductTextCatalog.English;

        public override ScreenId Id => ScreenId.Result;
        public Text SummaryLabel => summaryLabel ?? throw new InvalidOperationException(
            "Result summary is not configured.");
        public ProductResultOutcome? LastOutcome { get; private set; }
        public event System.Action? RematchRequested;
        public event System.Action? DetailsRequested;
        public event System.Action? TitleRequested;

        public void Configure(Text summary, Button details, Button rematch, Button title)
        {
            summaryLabel = summary;
            detailsButton = details;
            rematchButton = rematch;
            titleButton = title;
            RefreshButtonText();
        }

        public void SetText(IProductText configuredText)
        {
            text = configuredText ?? throw new ArgumentNullException(nameof(configuredText));
            RefreshButtonText();
        }

        public void Render(ResultViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            LastOutcome = model.Outcome;
            SummaryLabel.text = text.Get("result.with_marker",
                OutcomeMarker(model.Outcome), model.Summary);
        }

        private void Awake()
        {
            if (summaryLabel == null || detailsButton == null || rematchButton == null ||
                titleButton == null)
                throw new InvalidOperationException("Result screen controls are not configured.");
            detailsButton.onClick.AddListener(HandleDetails);
            rematchButton.onClick.AddListener(HandleRematch);
            titleButton.onClick.AddListener(HandleTitle);
        }

        private void OnDestroy()
        {
            if (rematchButton != null) rematchButton.onClick.RemoveListener(HandleRematch);
            if (detailsButton != null) detailsButton.onClick.RemoveListener(HandleDetails);
            if (titleButton != null) titleButton.onClick.RemoveListener(HandleTitle);
        }

        private void HandleDetails() => DetailsRequested?.Invoke();
        private void HandleRematch() => RematchRequested?.Invoke();
        private void HandleTitle() => TitleRequested?.Invoke();

        private string OutcomeMarker(ProductResultOutcome outcome)
        {
            switch (outcome)
            {
                case ProductResultOutcome.Win: return text.Get("result.marker_win");
                case ProductResultOutcome.Loss: return text.Get("result.marker_loss");
                case ProductResultOutcome.Draw: return text.Get("result.marker_draw");
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome,
                        "Unknown product result outcome.");
            }
        }

        private void RefreshButtonText()
        {
            SetButtonText(detailsButton, "result.details");
            SetButtonText(rematchButton, "result.rematch");
            SetButtonText(titleButton, "common.title");
        }

        private void SetButtonText(Button? button, string key)
        {
            Text? label = button?.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text.Get(key);
        }

    }
}
