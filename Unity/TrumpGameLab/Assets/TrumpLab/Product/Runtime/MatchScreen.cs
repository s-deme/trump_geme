#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField] private Button? helpButton;
        [SerializeField] private GameObject? contextHelpPanel;
        [SerializeField] private Text? contextHelpLabel;
        [SerializeField] private Button? closeHelpButton;

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
        public Button HelpButton => helpButton ?? throw Missing(nameof(helpButton));
        public GameObject ContextHelpPanel => contextHelpPanel ??
            throw Missing(nameof(contextHelpPanel));
        public Text ContextHelpLabel => contextHelpLabel ?? throw Missing(nameof(contextHelpLabel));
        public Button CloseHelpButton => closeHelpButton ?? throw Missing(nameof(closeHelpButton));
        public bool IsContextHelpVisible => contextHelpPanel != null && contextHelpPanel.activeSelf;
        public IReadOnlyList<string> RenderedActionIds { get; private set; } = Array.Empty<string>();
        public event System.Action<string>? ActionRequested;
        public event System.Action? ContextHelpOpened;
        public event System.Action? ContextHelpClosed;

        public void Configure(Text status, Text opponentHand, Text stock, Text discard,
            Text humanHand, Text actionSummary, RectTransform actions, Button actionTemplate,
            Button help, GameObject helpPanel, Text helpText, Button closeHelp)
        {
            statusLabel = status;
            opponentHandLabel = opponentHand;
            stockLabel = stock;
            discardLabel = discard;
            humanHandLabel = humanHand;
            actionSummaryLabel = actionSummary;
            actionRoot = actions;
            actionButtonTemplate = actionTemplate;
            helpButton = help;
            contextHelpPanel = helpPanel;
            contextHelpLabel = helpText;
            closeHelpButton = closeHelp;
        }

        private void Awake()
        {
            HelpButton.onClick.AddListener(ShowContextHelp);
            CloseHelpButton.onClick.AddListener(HideContextHelp);
            ContextHelpPanel.SetActive(false);
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
            ContextHelpLabel.text = model.ContextHelp;
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
                label.text = "✓ " + action.Label + "\n" + action.Reason;
                label.fontSize = 17;
                Image? image = button.GetComponent<Image>();
                if (image != null)
                    image.color = model.InputEnabled
                        ? new Color(0.12f, 0.52f, 0.31f, 1f)
                        : new Color(0.25f, 0.31f, 0.27f, 1f);
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

        public void ShowContextHelp()
        {
            if (IsContextHelpVisible) return;
            ContextHelpPanel.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(CloseHelpButton.gameObject);
            ContextHelpOpened?.Invoke();
        }

        public void HideContextHelp() => HideContextHelp(notify: true);

        public void HideContextHelp(bool notify)
        {
            if (!IsContextHelpVisible) return;
            ContextHelpPanel.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(
                ActionRoot.GetComponentsInChildren<Button>(false)
                    .FirstOrDefault()?.gameObject ?? HelpButton.gameObject);
            if (notify) ContextHelpClosed?.Invoke();
        }

        private void OnDestroy()
        {
            if (helpButton != null) helpButton.onClick.RemoveListener(ShowContextHelp);
            if (closeHelpButton != null) closeHelpButton.onClick.RemoveListener(HideContextHelp);
            ClearActionButtons();
        }

        private static Text Required(Text? value, string name) => value ??
            throw new InvalidOperationException("Match control is not configured: " + name);
        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException("Match control is not configured: " + name);
    }
}
