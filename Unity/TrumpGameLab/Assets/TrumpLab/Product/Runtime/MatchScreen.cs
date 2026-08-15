#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class MatchScreen : ProductScreen
    {
        [SerializeField] private Text? statusLabel;
        [SerializeField] private Text? opponentHandLabel;
        [SerializeField] private Text? stockLabel;
        [SerializeField] private Text? discardLabel;
        [SerializeField] private Text? humanHandLabel;
        [SerializeField] private Text? actionSummaryLabel;
        [SerializeField] private RectTransform? actionRoot;

        public override ScreenId Id => ScreenId.Match;
        public Text StatusLabel => Required(statusLabel, nameof(statusLabel));
        public Text OpponentHandLabel => Required(opponentHandLabel, nameof(opponentHandLabel));
        public Text StockLabel => Required(stockLabel, nameof(stockLabel));
        public Text DiscardLabel => Required(discardLabel, nameof(discardLabel));
        public Text HumanHandLabel => Required(humanHandLabel, nameof(humanHandLabel));
        public Text ActionSummaryLabel => Required(actionSummaryLabel, nameof(actionSummaryLabel));
        public RectTransform ActionRoot => actionRoot ?? throw new InvalidOperationException(
            "Match action root is not configured.");

        public void Configure(Text status, Text opponentHand, Text stock, Text discard,
            Text humanHand, Text actionSummary, RectTransform actions)
        {
            statusLabel = status;
            opponentHandLabel = opponentHand;
            stockLabel = stock;
            discardLabel = discard;
            humanHandLabel = humanHand;
            actionSummaryLabel = actionSummary;
            actionRoot = actions;
        }

        public void Render(MatchViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            StatusLabel.text = model.Status;
            OpponentHandLabel.text = model.OpponentHand;
            StockLabel.text = model.Stock;
            DiscardLabel.text = model.Discard;
            HumanHandLabel.text = model.HumanHand;
            ActionSummaryLabel.text = model.ActionSummary;
        }

        private static Text Required(Text? value, string name) => value ??
            throw new InvalidOperationException("Match control is not configured: " + name);
    }
}
