#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] private Button? actionButtonTemplate;

        public override ScreenId Id => ScreenId.Match;
        public Text StatusLabel => Required(statusLabel, nameof(statusLabel));
        public Text OpponentHandLabel => Required(opponentHandLabel, nameof(opponentHandLabel));
        public Text StockLabel => Required(stockLabel, nameof(stockLabel));
        public Text DiscardLabel => Required(discardLabel, nameof(discardLabel));
        public Text HumanHandLabel => Required(humanHandLabel, nameof(humanHandLabel));
        public Text ActionSummaryLabel => Required(actionSummaryLabel, nameof(actionSummaryLabel));
        public RectTransform ActionRoot => actionRoot ?? throw new InvalidOperationException(
            "Match action root is not configured.");
        public Button ActionButtonTemplate => actionButtonTemplate ?? throw new InvalidOperationException(
            "Match action button template is not configured.");
        public IReadOnlyList<string> RenderedActionIds { get; private set; } = Array.Empty<string>();
        public event System.Action<string>? ActionRequested;

        public void Configure(Text status, Text opponentHand, Text stock, Text discard,
            Text humanHand, Text actionSummary, RectTransform actions, Button actionTemplate)
        {
            statusLabel = status;
            opponentHandLabel = opponentHand;
            stockLabel = stock;
            discardLabel = discard;
            humanHandLabel = humanHand;
            actionSummaryLabel = actionSummary;
            actionRoot = actions;
            actionButtonTemplate = actionTemplate;
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
            RenderActions(model);
        }

        private void RenderActions(MatchViewModel model)
        {
            ClearActionButtons();
            RenderedActionIds = Array.AsReadOnly(model.Actions.Select(action => action.Id).ToArray());
            foreach (MatchActionViewModel action in model.Actions)
            {
                Button button = Instantiate(ActionButtonTemplate, ActionRoot);
                button.name = "Action_" + action.Id;
                button.gameObject.SetActive(true);
                button.interactable = model.InputEnabled;
                Text? label = button.GetComponentInChildren<Text>(true);
                if (label == null)
                    throw new InvalidOperationException("Action button template requires a Text label.");
                label.text = action.Label;
                string actionId = action.Id;
                button.onClick.AddListener(() => ActionRequested?.Invoke(actionId));
            }
        }

        private void ClearActionButtons()
        {
            if (actionRoot == null) return;
            Button[] existingButtons = actionRoot.GetComponentsInChildren<Button>(true);
            foreach (Button button in existingButtons)
            {
                if (button == null || button == actionButtonTemplate) continue;
                button.onClick.RemoveAllListeners();
                button.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(button.gameObject);
                else DestroyImmediate(button.gameObject);
            }
        }

        private void OnDestroy() => ClearActionButtons();

        private static Text Required(Text? value, string name) => value ??
            throw new InvalidOperationException("Match control is not configured: " + name);
    }
}
