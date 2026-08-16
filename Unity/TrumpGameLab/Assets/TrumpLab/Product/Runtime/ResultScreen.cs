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

        public override ScreenId Id => ScreenId.Result;
        public Text SummaryLabel => summaryLabel ?? throw new InvalidOperationException(
            "Result summary is not configured.");
        public event System.Action? RematchRequested;
        public event System.Action? DetailsRequested;
        public event System.Action? TitleRequested;

        public void Configure(Text summary, Button details, Button rematch, Button title)
        {
            summaryLabel = summary;
            detailsButton = details;
            rematchButton = rematch;
            titleButton = title;
        }

        public void Render(ResultViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            SummaryLabel.text = model.Summary;
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
    }
}
