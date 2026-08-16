#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class MatchScreen : ProductScreen, IPreferredFocusProvider
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
        [SerializeField] private Button? rulesButton;
        [SerializeField] private GameObject? contextHelpPanel;
        [SerializeField] private Text? contextHelpLabel;
        [SerializeField] private Button? closeHelpButton;
        [SerializeField] private GameObject? tutorialPanel;
        [SerializeField] private Text? tutorialProgressLabel;
        [SerializeField] private Text? tutorialHeadingLabel;
        [SerializeField] private Text? tutorialInstructionLabel;
        [SerializeField] private Text? tutorialGuidanceLabel;
        [SerializeField] private Button? tutorialContinueButton;
        [SerializeField] private Button? tutorialExitButton;

        private bool lastInputEnabled;

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
        public Button RulesButton => rulesButton ?? throw Missing(nameof(rulesButton));
        public GameObject ContextHelpPanel => contextHelpPanel ??
            throw Missing(nameof(contextHelpPanel));
        public Text ContextHelpLabel => contextHelpLabel ?? throw Missing(nameof(contextHelpLabel));
        public Button CloseHelpButton => closeHelpButton ?? throw Missing(nameof(closeHelpButton));
        public GameObject TutorialPanel => tutorialPanel ?? throw Missing(nameof(tutorialPanel));
        public Text TutorialProgressLabel => tutorialProgressLabel ??
            throw Missing(nameof(tutorialProgressLabel));
        public Text TutorialHeadingLabel => tutorialHeadingLabel ??
            throw Missing(nameof(tutorialHeadingLabel));
        public Text TutorialInstructionLabel => tutorialInstructionLabel ??
            throw Missing(nameof(tutorialInstructionLabel));
        public Text TutorialGuidanceLabel => tutorialGuidanceLabel ??
            throw Missing(nameof(tutorialGuidanceLabel));
        public Button TutorialContinueButton => tutorialContinueButton ??
            throw Missing(nameof(tutorialContinueButton));
        public Button TutorialExitButton => tutorialExitButton ??
            throw Missing(nameof(tutorialExitButton));
        public bool IsContextHelpVisible => contextHelpPanel != null && contextHelpPanel.activeSelf;
        public bool IsTutorialVisible => tutorialPanel != null && tutorialPanel.activeSelf;
        public bool IsPresentationLocked { get; private set; }
        public string? HighlightedActionId { get; private set; }
        public Selectable? PreferredFocus
        {
            get
            {
                if (IsContextHelpVisible) return CloseHelpButton;
                if (IsPresentationLocked)
                    return IsTutorialVisible ? TutorialExitButton : HelpButton;
                if (IsTutorialVisible)
                {
                    if (TutorialContinueButton.gameObject.activeSelf)
                        return TutorialContinueButton;
                    if (HighlightedActionId != null)
                    {
                        Button? expected = ActionRoot.GetComponentsInChildren<Button>(false)
                            .SingleOrDefault(button =>
                                button.name == "Action_" + HighlightedActionId);
                        if (expected != null) return expected;
                    }
                    return TutorialExitButton;
                }
                return ActionRoot.GetComponentsInChildren<Button>(false).FirstOrDefault() ??
                    HelpButton;
            }
        }
        public IReadOnlyList<string> RenderedActionIds { get; private set; } = Array.Empty<string>();
        public event System.Action<string>? ActionRequested;
        public event System.Action? ContextHelpOpened;
        public event System.Action? ContextHelpClosed;
        public event System.Action? RulesRequested;
        public event System.Action? TutorialContinueRequested;
        public event System.Action? TutorialExitRequested;

        public void Configure(Text status, Text opponentHand, Text stock, Text discard,
            Text humanHand, Text actionSummary, RectTransform actions, Button actionTemplate,
            Button help, Button rules, GameObject helpPanel, Text helpText, Button closeHelp,
            GameObject guidedPanel, Text guidedProgress, Text guidedHeading,
            Text guidedInstruction, Text guidedGuidance, Button guidedContinue,
            Button guidedExit)
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
            rulesButton = rules;
            contextHelpPanel = helpPanel;
            contextHelpLabel = helpText;
            closeHelpButton = closeHelp;
            tutorialPanel = guidedPanel;
            tutorialProgressLabel = guidedProgress;
            tutorialHeadingLabel = guidedHeading;
            tutorialInstructionLabel = guidedInstruction;
            tutorialGuidanceLabel = guidedGuidance;
            tutorialContinueButton = guidedContinue;
            tutorialExitButton = guidedExit;
        }

        private void Awake()
        {
            HelpButton.onClick.AddListener(ShowContextHelp);
            RulesButton.onClick.AddListener(HandleRules);
            CloseHelpButton.onClick.AddListener(HideContextHelp);
            TutorialContinueButton.onClick.AddListener(HandleTutorialContinue);
            TutorialExitButton.onClick.AddListener(HandleTutorialExit);
            ContextHelpPanel.SetActive(false);
            TutorialPanel.SetActive(false);
        }

        public void Render(MatchViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            IsPresentationLocked = false;
            lastInputEnabled = model.InputEnabled;
            ResetFeedbackColors();
            StatusLabel.text = model.Status;
            OpponentHandLabel.text = model.OpponentHand;
            StockLabel.text = model.Stock;
            DiscardLabel.text = model.Discard;
            HumanHandLabel.text = model.HumanHand;
            ActionSummaryLabel.text = model.ActionSummary;
            ContextHelpLabel.text = model.ContextHelp;
            RenderActions(model);
        }

        public void ShowActionFeedback(ProductFeedbackKind kind)
        {
            ProductFeedbackPresentation feedback = ProductPresentationCatalog.Get(kind);
            ActionSummaryLabel.text = feedback.DisplayText;
            ResetFeedbackColors();
            switch (kind)
            {
                case ProductFeedbackKind.CardPlay:
                    HumanHandLabel.color = new Color(0.55f, 0.95f, 0.72f, 1f);
                    DiscardLabel.color = new Color(0.55f, 0.95f, 0.72f, 1f);
                    break;
                case ProductFeedbackKind.Draw:
                    StockLabel.color = new Color(0.48f, 0.82f, 1f, 1f);
                    HumanHandLabel.color = new Color(0.48f, 0.82f, 1f, 1f);
                    break;
                case ProductFeedbackKind.WildSuit:
                    HumanHandLabel.color = new Color(0.93f, 0.68f, 1f, 1f);
                    DiscardLabel.color = new Color(0.93f, 0.68f, 1f, 1f);
                    break;
                case ProductFeedbackKind.CpuTurn:
                    StatusLabel.color = new Color(1f, 0.82f, 0.38f, 1f);
                    break;
                case ProductFeedbackKind.Reject:
                    ActionSummaryLabel.color = new Color(1f, 0.55f, 0.45f, 1f);
                    break;
                case ProductFeedbackKind.Submit:
                    ActionSummaryLabel.color = new Color(0.62f, 0.94f, 0.7f, 1f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind,
                        "Feedback kind does not target the match surface.");
            }
        }

        public void SetPresentationLocked(bool locked)
        {
            IsPresentationLocked = locked;
            if (tutorialContinueButton != null)
                tutorialContinueButton.interactable = !locked;
            if (actionRoot != null)
            {
                foreach (Button button in actionRoot.GetComponentsInChildren<Button>(false))
                {
                    if (button != null)
                        button.interactable = !locked && lastInputEnabled;
                }
            }
        }

        public void RenderTutorial(MatchViewModel match, TutorialOverlayViewModel tutorial)
        {
            if (tutorial == null) throw new ArgumentNullException(nameof(tutorial));
            HighlightedActionId = tutorial.ExpectedActionId;
            Render(match);
            TutorialProgressLabel.text = tutorial.Progress;
            TutorialHeadingLabel.text = tutorial.Heading;
            TutorialInstructionLabel.text = tutorial.Instruction;
            TutorialGuidanceLabel.text = tutorial.Guidance;
            TutorialContinueButton.gameObject.SetActive(tutorial.ContinueVisible);
            Text? continueLabel = TutorialContinueButton.GetComponentInChildren<Text>(true);
            if (continueLabel == null)
                throw new InvalidOperationException(
                    "Tutorial continue button requires a Text label.");
            continueLabel.text = tutorial.ContinueLabel;
            TutorialPanel.SetActive(true);

            GameObject focus = TutorialExitButton.gameObject;
            if (tutorial.ContinueVisible)
                focus = TutorialContinueButton.gameObject;
            else if (HighlightedActionId != null)
            {
                Button? expected = ActionRoot.GetComponentsInChildren<Button>(false)
                    .SingleOrDefault(button => button.name == "Action_" + HighlightedActionId);
                if (expected != null) focus = expected.gameObject;
            }
            EventSystem.current?.SetSelectedGameObject(focus);
        }

        public void HideTutorial()
        {
            HighlightedActionId = null;
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
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
                button.interactable = model.InputEnabled && !IsPresentationLocked;
                Text? label = button.GetComponentInChildren<Text>(true);
                if (label == null)
                    throw new InvalidOperationException("Action button template requires a Text label.");
                bool highlighted = action.Id == HighlightedActionId;
                label.text = (highlighted ? "★ " : "✓ ") + action.Label + "\n" +
                    action.Reason;
                label.fontSize = 17;
                Image? image = button.GetComponent<Image>();
                if (image != null)
                    image.color = highlighted
                        ? new Color(0.75f, 0.52f, 0.08f, 1f)
                        : model.InputEnabled
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
            if (IsPresentationLocked || IsContextHelpVisible) return;
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
            if (rulesButton != null) rulesButton.onClick.RemoveListener(HandleRules);
            if (closeHelpButton != null) closeHelpButton.onClick.RemoveListener(HideContextHelp);
            if (tutorialContinueButton != null)
                tutorialContinueButton.onClick.RemoveListener(HandleTutorialContinue);
            if (tutorialExitButton != null)
                tutorialExitButton.onClick.RemoveListener(HandleTutorialExit);
            ClearActionButtons();
        }

        private void HandleRules()
        {
            if (!IsPresentationLocked) RulesRequested?.Invoke();
        }
        private void HandleTutorialContinue() => TutorialContinueRequested?.Invoke();
        private void HandleTutorialExit() => TutorialExitRequested?.Invoke();

        private void ResetFeedbackColors()
        {
            Color normal = new Color(0.96f, 0.94f, 0.82f, 1f);
            StatusLabel.color = normal;
            StockLabel.color = normal;
            DiscardLabel.color = normal;
            HumanHandLabel.color = normal;
            ActionSummaryLabel.color = normal;
        }

        private static Text Required(Text? value, string name) => value ??
            throw new InvalidOperationException("Match control is not configured: " + name);
        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException("Match control is not configured: " + name);
    }
}
