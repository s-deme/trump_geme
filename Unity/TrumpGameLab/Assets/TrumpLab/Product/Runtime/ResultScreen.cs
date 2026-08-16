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
        }

        public void Render(ResultViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            LastOutcome = model.Outcome;
            SummaryLabel.text = OutcomeSymbol(model.Outcome) + "  " + model.Summary;
            SummaryLabel.color = OutcomeColor(model.Outcome);
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

        private static string OutcomeSymbol(ProductResultOutcome outcome)
        {
            switch (outcome)
            {
                case ProductResultOutcome.Win: return "★ WIN";
                case ProductResultOutcome.Loss: return "◆ LOSS";
                case ProductResultOutcome.Draw: return "= DRAW";
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome,
                        "Unknown product result outcome.");
            }
        }

        private static Color OutcomeColor(ProductResultOutcome outcome)
        {
            switch (outcome)
            {
                case ProductResultOutcome.Win:
                    return new Color(0.98f, 0.84f, 0.28f, 1f);
                case ProductResultOutcome.Loss:
                    return new Color(0.98f, 0.52f, 0.48f, 1f);
                case ProductResultOutcome.Draw:
                    return new Color(0.62f, 0.84f, 0.98f, 1f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome,
                        "Unknown product result outcome.");
            }
        }
    }
}
