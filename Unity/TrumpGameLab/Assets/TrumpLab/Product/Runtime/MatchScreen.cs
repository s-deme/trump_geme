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
        [SerializeField] private Button? settingsButton;
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
        private IProductText text = ProductTextCatalog.English;
        private readonly Dictionary<Selectable, bool> contextHelpBackgroundStates =
            new Dictionary<Selectable, bool>();
        private GameObject? contextHelpPriorFocus;

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
        public Button SettingsButton => settingsButton ?? throw Missing(nameof(settingsButton));
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
        public bool IsExternalModalLocked { get; private set; }
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
        public event System.Action? SettingsRequested;
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
            RefreshStaticButtonText();
        }

        public void Configure(Text status, Text opponentHand, Text stock, Text discard,
            Text humanHand, Text actionSummary, RectTransform actions, Button actionTemplate,
            Button help, Button settings, Button rules, GameObject helpPanel, Text helpText,
            Button closeHelp, GameObject guidedPanel, Text guidedProgress,
            Text guidedHeading, Text guidedInstruction, Text guidedGuidance,
            Button guidedContinue, Button guidedExit)
        {
            Configure(status, opponentHand, stock, discard, humanHand, actionSummary,
                actions, actionTemplate, help, rules, helpPanel, helpText, closeHelp,
                guidedPanel, guidedProgress, guidedHeading, guidedInstruction,
                guidedGuidance, guidedContinue, guidedExit);
            ConfigureSettings(settings);
        }

        public void ConfigureSettings(Button settings)
        {
            settingsButton = settings ?? throw new ArgumentNullException(nameof(settings));
            RefreshStaticButtonText();
        }

        public void SetText(IProductText configuredText)
        {
            text = configuredText ?? throw new ArgumentNullException(nameof(configuredText));
            RefreshStaticButtonText();
        }

        private void Awake()
        {
            HelpButton.onClick.AddListener(ShowContextHelp);
            settingsButton?.onClick.AddListener(HandleSettings);
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
            StatusLabel.text = model.Status;
            OpponentHandLabel.text = model.OpponentHand;
            StockLabel.text = model.Stock;
            DiscardLabel.text = model.Discard;
            HumanHandLabel.text = model.HumanHand;
            ActionSummaryLabel.text = model.ActionSummary;
            ContextHelpLabel.text = model.ContextHelp;
            RenderActions(model);
            ApplyInteractionState();
        }

        public void ShowActionFeedback(ProductFeedbackKind kind)
        {
            ProductFeedbackPresentation feedback = ProductPresentationCatalog.Get(kind);
            ActionSummaryLabel.text = feedback.Symbol + "  " + text.Get(feedback.Key);
            switch (kind)
            {
                case ProductFeedbackKind.CardPlay:
                case ProductFeedbackKind.Draw:
                case ProductFeedbackKind.WildSuit:
                case ProductFeedbackKind.CpuTurn:
                case ProductFeedbackKind.Reject:
                case ProductFeedbackKind.Submit:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind,
                        "Feedback kind does not target the match surface.");
            }
        }

        public void SetPresentationLocked(bool locked)
        {
            IsPresentationLocked = locked;
            ApplyInteractionState();
        }

        public void SetExternalModalLocked(bool locked)
        {
            IsExternalModalLocked = locked;
            ApplyInteractionState();
        }

        private void ApplyInteractionState()
        {
            bool backgroundEnabled = !IsExternalModalLocked && !IsContextHelpVisible;
            if (settingsButton != null)
                settingsButton.interactable = backgroundEnabled && !IsPresentationLocked;
            if (tutorialContinueButton != null)
                tutorialContinueButton.interactable =
                    backgroundEnabled && !IsPresentationLocked;
            if (actionRoot != null)
            {
                foreach (Button button in actionRoot.GetComponentsInChildren<Button>(false))
                {
                    if (button != null)
                        button.interactable = backgroundEnabled &&
                            !IsPresentationLocked && lastInputEnabled;
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
            ApplyInteractionState();

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
                button.interactable = model.InputEnabled && !IsPresentationLocked &&
                    !IsExternalModalLocked && !IsContextHelpVisible;
                Text? label = button.GetComponentInChildren<Text>(true);
                if (label == null)
                    throw new InvalidOperationException("Action button template requires a Text label.");
                bool highlighted = action.Id == HighlightedActionId;
                string marker = text.Get(highlighted
                    ? "match.marker_expected"
                    : "match.marker_legal");
                label.text = text.Get("match.action_button",
                    marker, action.Label, action.Reason);
                button.GetComponent<ProductAccessibleControl>()?.SetRuntimeLabel(
                    "match.action_button", marker, action.Label, action.Reason);
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
            if (IsPresentationLocked || IsExternalModalLocked || IsContextHelpVisible) return;
            contextHelpPriorFocus = EventSystem.current?.currentSelectedGameObject;
            LockContextHelpBackgroundControls();
            ContextHelpPanel.SetActive(true);
            ApplyInteractionState();
            CloseHelpButton.interactable = !IsExternalModalLocked;
            EventSystem.current?.SetSelectedGameObject(CloseHelpButton.gameObject);
            ContextHelpOpened?.Invoke();
        }

        public void HideContextHelp() => HideContextHelp(notify: true);

        public void HideContextHelp(bool notify)
        {
            if (!IsContextHelpVisible) return;
            ContextHelpPanel.SetActive(false);
            RestoreContextHelpBackgroundControls();
            EventSystem.current?.SetSelectedGameObject(PreferredFocusAfterContextHelp());
            contextHelpPriorFocus = null;
            if (notify) ContextHelpClosed?.Invoke();
        }

        private void LockContextHelpBackgroundControls()
        {
            contextHelpBackgroundStates.Clear();
            Transform modalRoot = ContextHelpPanel.transform;
            foreach (Selectable selectable in GetComponentsInChildren<Selectable>(true))
            {
                if (selectable == null || selectable.transform.IsChildOf(modalRoot)) continue;
                contextHelpBackgroundStates.Add(selectable, selectable.interactable);
                selectable.interactable = false;
            }
        }

        private void RestoreContextHelpBackgroundControls()
        {
            foreach (KeyValuePair<Selectable, bool> state in contextHelpBackgroundStates)
            {
                if (state.Key != null) state.Key.interactable = state.Value;
            }
            contextHelpBackgroundStates.Clear();
        }

        private GameObject? PreferredFocusAfterContextHelp()
        {
            if (contextHelpPriorFocus != null &&
                contextHelpPriorFocus.TryGetComponent(out Selectable prior) && IsEligible(prior))
                return contextHelpPriorFocus;

            Selectable? action = ActionRoot.GetComponentsInChildren<Button>(false)
                .FirstOrDefault(IsEligible);
            if (action != null) return action.gameObject;

            var fallbacks = new List<Selectable>();
            if (IsTutorialVisible)
            {
                if (tutorialContinueButton != null) fallbacks.Add(tutorialContinueButton);
                if (tutorialExitButton != null) fallbacks.Add(tutorialExitButton);
            }
            if (helpButton != null) fallbacks.Add(helpButton);
            if (settingsButton != null) fallbacks.Add(settingsButton);
            if (rulesButton != null) fallbacks.Add(rulesButton);
            return fallbacks.FirstOrDefault(IsEligible)?.gameObject;
        }

        private static bool IsEligible(Selectable selectable) =>
            selectable != null && selectable.gameObject.activeInHierarchy &&
            selectable.IsActive() && selectable.IsInteractable();

        private void OnDestroy()
        {
            if (helpButton != null) helpButton.onClick.RemoveListener(ShowContextHelp);
            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(HandleSettings);
            if (rulesButton != null) rulesButton.onClick.RemoveListener(HandleRules);
            if (closeHelpButton != null) closeHelpButton.onClick.RemoveListener(HideContextHelp);
            if (tutorialContinueButton != null)
                tutorialContinueButton.onClick.RemoveListener(HandleTutorialContinue);
            if (tutorialExitButton != null)
                tutorialExitButton.onClick.RemoveListener(HandleTutorialExit);
            contextHelpBackgroundStates.Clear();
            contextHelpPriorFocus = null;
            ClearActionButtons();
        }

        private void HandleRules()
        {
            if (!IsPresentationLocked && !IsExternalModalLocked &&
                !IsContextHelpVisible) RulesRequested?.Invoke();
        }
        private void HandleSettings()
        {
            if (!IsPresentationLocked && !IsExternalModalLocked &&
                !IsContextHelpVisible) SettingsRequested?.Invoke();
        }
        private void HandleTutorialContinue()
        {
            if (!IsPresentationLocked && !IsExternalModalLocked &&
                !IsContextHelpVisible) TutorialContinueRequested?.Invoke();
        }
        private void HandleTutorialExit()
        {
            if (!IsExternalModalLocked && !IsContextHelpVisible)
                TutorialExitRequested?.Invoke();
        }

        private static Text Required(Text? value, string name) => value ??
            throw new InvalidOperationException("Match control is not configured: " + name);
        private static InvalidOperationException Missing(string name) =>
            new InvalidOperationException("Match control is not configured: " + name);

        private void RefreshStaticButtonText()
        {
            SetButtonText(helpButton, "common.help");
            SetButtonText(settingsButton, "common.settings");
            SetButtonText(rulesButton, "common.rules");
            SetButtonText(closeHelpButton, "common.close");
            SetButtonText(tutorialContinueButton, "tutorial.continue_start");
            SetButtonText(tutorialExitButton, "tutorial.exit");
        }

        private void SetButtonText(Button? button, string key)
        {
            Text? label = button?.GetComponentInChildren<Text>(true);
            if (label != null) label.text = text.Get(key);
        }
    }
}
