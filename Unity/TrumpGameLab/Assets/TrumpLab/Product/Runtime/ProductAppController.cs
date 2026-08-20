#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TrumpLab;
using UnityEngine;

namespace TrumpLab.Product
{
    public sealed class ProductAppController : MonoBehaviour
    {
        [SerializeField] private ScreenRouter? router;
        [SerializeField] private TitleScreen? titleScreen;
        [SerializeField] private GameSettingsScreen? settingsScreen;
        [SerializeField] private ProductSettingsScreen? productSettingsScreen;
        [SerializeField] private SessionLibraryScreen? sessionLibraryScreen;
        [SerializeField] private MatchScreen? matchScreen;
        [SerializeField] private ReplayScreen? replayScreen;
        [SerializeField] private ResultScreen? resultScreen;
        [SerializeField] private HowToPlayScreen? howToPlayScreen;
        [SerializeField] private ProductInputController? inputController;
        [SerializeField] private ProductPresentationController? presentationController;
        [SerializeField] private ProductErrorPanel? errorPanel;
        [SerializeField] private ProductLocalizationController? localizationController;
        [SerializeField] private ProductAccessibilityController? accessibilityController;

        private GameSessionController? activeSession;
        private TutorialSessionController? activeTutorial;
        private Coroutine? cpuTurnCoroutine;
        private Coroutine? actionPresentationCoroutine;
        private SessionActionRecord? pendingSessionAction;
        private SessionActionRecord? pendingTutorialAction;
        private ResultViewModel? pendingResult;
        private int presentationGeneration;
        private int lastSessionCpuCueActionCount = -1;
        private int lastTutorialCpuCueActionCount = -1;
        private GameStartRequest? lastRequest;
        private ISessionStore? sessionStore;
        private IProductProgressStore? progressStore;
        private IProductSettingsStore? productSettingsStore;
        private IProductSettingsApplier? productSettingsApplier;
        private ProductSettingsService? productSettingsService;
        private string? activeSlotId;
        private ScreenId errorReturnScreen = ScreenId.Title;
        private ScreenId howToPlayReturnScreen = ScreenId.Title;
        private ScreenId settingsReturnScreen = ScreenId.Title;
        private string settingsLoadFeedbackKey = string.Empty;
        private bool gamepadDisconnectNoticeVisible;
        private string shownStartupFontWarning = string.Empty;
        private bool awakeComplete;

        public ScreenRouter Router => router ?? throw new InvalidOperationException(
            "Screen router is not configured.");
        public IGame? ActiveGame => activeSession?.Game ?? activeTutorial?.Game;
        public GameSessionController? ActiveSession => activeSession;
        public TutorialSessionController? ActiveTutorial => activeTutorial;
        public string? ActiveSlotId => activeSlotId;
        public ProductErrorPanel ErrorPanel => errorPanel ?? throw new InvalidOperationException(
            "Product error panel is not configured.");
        public GameStartRequest? LastRequest => lastRequest;
        public ISessionStore SessionStore => sessionStore ?? throw new InvalidOperationException(
            "Session store is not configured.");
        public IProductProgressStore ProgressStore => progressStore ??
            throw new InvalidOperationException("Product progress store is not configured.");
        public IProductSettingsStore ProductSettingsStore => productSettingsStore ??
            throw new InvalidOperationException("Product settings store is not configured.");
        public ProductSettings CurrentProductSettings => productSettingsService?.Current ??
            throw new InvalidOperationException("Product settings are not initialized.");
        public ProductInputController InputController => inputController ??
            throw new InvalidOperationException("Product input controller is not configured.");
        public ProductPresentationController PresentationController => presentationController ??
            throw new InvalidOperationException(
                "Product presentation controller is not configured.");
        public IProductText Text => localizationController ?? ProductTextCatalog.English;
        public ProductLocalizationController? LocalizationController => localizationController;
        public ProductAccessibilityController? AccessibilityController => accessibilityController;

        public void Configure(ScreenRouter configuredRouter, TitleScreen title,
            GameSettingsScreen settings, ProductSettingsScreen productSettings,
            SessionLibraryScreen library, MatchScreen match, ReplayScreen replay,
            ResultScreen result, HowToPlayScreen howToPlay, ProductInputController input,
            ProductPresentationController presentation, ProductErrorPanel errors)
        {
            router = configuredRouter;
            titleScreen = title;
            settingsScreen = settings;
            productSettingsScreen = productSettings;
            sessionLibraryScreen = library;
            matchScreen = match;
            replayScreen = replay;
            resultScreen = result;
            howToPlayScreen = howToPlay;
            inputController = input;
            presentationController = presentation;
            errorPanel = errors;
        }

        public void Configure(ScreenRouter configuredRouter, TitleScreen title,
            GameSettingsScreen settings, ProductSettingsScreen productSettings,
            SessionLibraryScreen library, MatchScreen match, ReplayScreen replay,
            ResultScreen result, HowToPlayScreen howToPlay, ProductInputController input,
            ProductPresentationController presentation, ProductErrorPanel errors,
            ProductLocalizationController localization,
            ProductAccessibilityController accessibility)
        {
            Configure(configuredRouter, title, settings, productSettings, library, match,
                replay, result, howToPlay, input, presentation, errors);
            localizationController = localization ??
                throw new ArgumentNullException(nameof(localization));
            accessibilityController = accessibility ??
                throw new ArgumentNullException(nameof(accessibility));
        }

        public void SetSessionStore(ISessionStore store) =>
            sessionStore = store ?? throw new ArgumentNullException(nameof(store));

        public void SetProgressStore(IProductProgressStore store)
        {
            progressStore = store ?? throw new ArgumentNullException(nameof(store));
            RefreshTutorialProgress();
        }

        public void SetProductSettingsStore(IProductSettingsStore store)
        {
            productSettingsStore = store ?? throw new ArgumentNullException(nameof(store));
            if (awakeComplete) InitializeProductSettings();
        }

        public void SetProductSettingsApplier(IProductSettingsApplier applier)
        {
            productSettingsApplier = applier ?? throw new ArgumentNullException(nameof(applier));
            if (awakeComplete) InitializeProductSettings();
        }

        private void Awake()
        {
            if (router == null || titleScreen == null || settingsScreen == null ||
                productSettingsScreen == null || inputController == null ||
                sessionLibraryScreen == null || matchScreen == null || replayScreen == null ||
                resultScreen == null || howToPlayScreen == null ||
                presentationController == null || errorPanel == null)
                throw new InvalidOperationException("Product app controller is not configured.");

            if (sessionStore == null) sessionStore = new FileSessionStore(Application.persistentDataPath);
            if (progressStore == null)
                progressStore = new FileProductProgressStore(Application.persistentDataPath);
            if (productSettingsApplier == null)
                productSettingsApplier = new UnityProductSettingsApplier();
            inputController.Initialize();
            if (productSettingsStore == null)
                productSettingsStore = new FileProductSettingsStore(
                    Application.persistentDataPath, validator: inputController);
            InitializeProductSettings();
            RefreshTutorialProgress();
            RefreshLocalizedUi();

            titleScreen.TutorialRequested += HandleTitleTutorialRequested;
            titleScreen.PlayRequested += HandlePlayRequested;
            titleScreen.SessionsRequested += HandleSessionsRequested;
            titleScreen.SettingsRequested += HandleTitleSettingsRequested;
            titleScreen.QuitRequested += HandleQuitRequested;
            settingsScreen.StartRequested += HandleStartRequested;
            settingsScreen.HowToPlayRequested += HandleSettingsHowToPlayRequested;
            settingsScreen.BackRequested += HandleTitleRequested;
            settingsScreen.ValidationRejected += HandleValidationRejected;
            productSettingsScreen.ApplyRequested += HandleProductSettingsApplyRequested;
            productSettingsScreen.ResetRequested += HandleProductSettingsResetRequested;
            productSettingsScreen.BackRequested += HandleProductSettingsBackRequested;
            productSettingsScreen.RebindRequested += HandleRebindRequested;
            productSettingsScreen.CancelRebindRequested += HandleCancelRebindRequested;
            productSettingsScreen.ValidationRejected += HandleValidationRejected;
            sessionLibraryScreen.ResumeRequested += HandleResumeRequested;
            sessionLibraryScreen.ReplayRequested += HandleReplayRequested;
            sessionLibraryScreen.DeleteRequested += HandleDeleteRequested;
            sessionLibraryScreen.BackRequested += HandleTitleRequested;
            matchScreen.ActionRequested += HandleActionRequested;
            matchScreen.ContextHelpOpened += HandleContextHelpOpened;
            matchScreen.ContextHelpClosed += HandleContextHelpClosed;
            matchScreen.SettingsRequested += HandleMatchSettingsRequested;
            matchScreen.RulesRequested += HandleMatchRulesRequested;
            matchScreen.TutorialContinueRequested += HandleTutorialContinueRequested;
            matchScreen.TutorialExitRequested += HandleTutorialExitRequested;
            replayScreen.BackRequested += HandleSessionsRequested;
            resultScreen.RematchRequested += HandleRematchRequested;
            resultScreen.DetailsRequested += HandleResultDetailsRequested;
            resultScreen.TitleRequested += HandleTitleRequested;
            howToPlayScreen.BackRequested += HandleHowToPlayBackRequested;
            howToPlayScreen.StartTutorialRequested += HandleStartTutorialRequested;
            inputController.HelpRequested += HandleHelpRequested;
            inputController.GamepadDisconnected += HandleGamepadDisconnected;
            inputController.GamepadReconnected += HandleGamepadReconnected;
            settingsScreen.CancelRequested += HandleTitleRequested;
            productSettingsScreen.CancelRequested += HandleProductSettingsCancelRequested;
            sessionLibraryScreen.CancelRequested += HandleTitleRequested;
            matchScreen.CancelRequested += HandleMatchCancelRequested;
            replayScreen.CancelRequested += HandleSessionsRequested;
            resultScreen.CancelRequested += HandleTitleRequested;
            howToPlayScreen.CancelRequested += HandleHowToPlayBackRequested;
            errorPanel.Dismissed += HandleErrorDismissed;
            errorPanel.Shown += HandleErrorShown;
            errorPanel.Hidden += HandleErrorHidden;
            router.ScreenChanged += HandleScreenChanged;
            errorPanel.Hide();
            router.Show(ScreenId.Title);
            awakeComplete = true;
            ShowStartupFontWarningIfNeeded();
        }

        private void OnDestroy()
        {
            if (titleScreen != null)
            {
                titleScreen.TutorialRequested -= HandleTitleTutorialRequested;
                titleScreen.PlayRequested -= HandlePlayRequested;
                titleScreen.SessionsRequested -= HandleSessionsRequested;
                titleScreen.SettingsRequested -= HandleTitleSettingsRequested;
                titleScreen.QuitRequested -= HandleQuitRequested;
            }
            if (settingsScreen != null)
            {
                settingsScreen.StartRequested -= HandleStartRequested;
                settingsScreen.HowToPlayRequested -= HandleSettingsHowToPlayRequested;
                settingsScreen.BackRequested -= HandleTitleRequested;
                settingsScreen.CancelRequested -= HandleTitleRequested;
                settingsScreen.ValidationRejected -= HandleValidationRejected;
            }
            if (productSettingsScreen != null)
            {
                productSettingsScreen.ApplyRequested -= HandleProductSettingsApplyRequested;
                productSettingsScreen.ResetRequested -= HandleProductSettingsResetRequested;
                productSettingsScreen.BackRequested -= HandleProductSettingsBackRequested;
                productSettingsScreen.RebindRequested -= HandleRebindRequested;
                productSettingsScreen.CancelRebindRequested -= HandleCancelRebindRequested;
                productSettingsScreen.CancelRequested -= HandleProductSettingsCancelRequested;
                productSettingsScreen.ValidationRejected -= HandleValidationRejected;
            }
            if (sessionLibraryScreen != null)
            {
                sessionLibraryScreen.ResumeRequested -= HandleResumeRequested;
                sessionLibraryScreen.ReplayRequested -= HandleReplayRequested;
                sessionLibraryScreen.DeleteRequested -= HandleDeleteRequested;
                sessionLibraryScreen.BackRequested -= HandleTitleRequested;
                sessionLibraryScreen.CancelRequested -= HandleTitleRequested;
            }
            if (matchScreen != null)
            {
                matchScreen.ActionRequested -= HandleActionRequested;
                matchScreen.ContextHelpOpened -= HandleContextHelpOpened;
                matchScreen.ContextHelpClosed -= HandleContextHelpClosed;
                matchScreen.SettingsRequested -= HandleMatchSettingsRequested;
                matchScreen.RulesRequested -= HandleMatchRulesRequested;
                matchScreen.TutorialContinueRequested -= HandleTutorialContinueRequested;
                matchScreen.TutorialExitRequested -= HandleTutorialExitRequested;
                matchScreen.CancelRequested -= HandleMatchCancelRequested;
            }
            if (replayScreen != null)
            {
                replayScreen.BackRequested -= HandleSessionsRequested;
                replayScreen.CancelRequested -= HandleSessionsRequested;
            }
            if (resultScreen != null)
            {
                resultScreen.RematchRequested -= HandleRematchRequested;
                resultScreen.DetailsRequested -= HandleResultDetailsRequested;
                resultScreen.TitleRequested -= HandleTitleRequested;
                resultScreen.CancelRequested -= HandleTitleRequested;
            }
            if (howToPlayScreen != null)
            {
                howToPlayScreen.BackRequested -= HandleHowToPlayBackRequested;
                howToPlayScreen.StartTutorialRequested -= HandleStartTutorialRequested;
                howToPlayScreen.CancelRequested -= HandleHowToPlayBackRequested;
            }
            if (inputController != null)
            {
                inputController.HelpRequested -= HandleHelpRequested;
                inputController.GamepadDisconnected -= HandleGamepadDisconnected;
                inputController.GamepadReconnected -= HandleGamepadReconnected;
            }
            if (errorPanel != null)
            {
                errorPanel.Dismissed -= HandleErrorDismissed;
                errorPanel.Shown -= HandleErrorShown;
                errorPanel.Hidden -= HandleErrorHidden;
            }
            if (router != null) router.ScreenChanged -= HandleScreenChanged;
            EndSession();
            EndTutorial();
        }

        private void InitializeProductSettings()
        {
            if (productSettingsStore == null || productSettingsApplier == null ||
                inputController == null || presentationController == null) return;
            var appliers = new List<IProductSettingsApplier>
            {
                productSettingsApplier,
                presentationController
            };
            if (localizationController != null) appliers.Add(localizationController);
            if (accessibilityController != null) appliers.Add(accessibilityController);
            var combinedApplier = new CompositeProductSettingsApplier(appliers.ToArray());
            productSettingsService = new ProductSettingsService(
                productSettingsStore, combinedApplier, validator: inputController);
            ProductSettingsLoadResult result = productSettingsService.Initialize();
            inputController.ApplyBindings(productSettingsService.Current.InputBindings);
            settingsLoadFeedbackKey = result.Status switch
            {
                ProductSettingsLoadStatus.Missing =>
                    "settings.feedback_load_defaults",
                ProductSettingsLoadStatus.Invalid =>
                    "settings.feedback_load_invalid",
                _ => string.Empty
            };
            RefreshLocalizedUi();
            if (awakeComplete) ShowStartupFontWarningIfNeeded();
        }

        private void Update()
        {
            if (productSettingsService != null &&
                productSettingsApplier is IProductDisplayGuard displayGuard)
                displayGuard.MaintainValidDisplay(productSettingsService.Current);
        }

        private void HandleScreenChanged(ScreenId _)
        {
            try
            {
                PresentationController.BeginScreenTransition();
                accessibilityController?.RefreshNavigation();
            }
            catch (Exception exception)
            {
                Debug.LogError("Screen transition feedback failed: " + exception.Message);
            }
        }

        private void HandleErrorShown()
        {
            matchScreen?.SetExternalModalLocked(true);
            TryPlayFeedback(ProductFeedbackKind.Error);
            accessibilityController?.RefreshNavigation();
        }

        private void HandleErrorHidden()
        {
            matchScreen?.SetExternalModalLocked(false);
            accessibilityController?.RefreshNavigation();
        }

        private void HandleValidationRejected() =>
            TryPlayFeedback(ProductFeedbackKind.Reject);

        private void HandleTitleSettingsRequested()
        {
            OpenProductSettings(ScreenId.Title);
        }

        private void HandleMatchSettingsRequested()
        {
            if (Router.Current != ScreenId.Match ||
                matchScreen?.IsPresentationLocked == true ||
                matchScreen?.IsContextHelpVisible == true ||
                (activeSession == null && activeTutorial == null)) return;
            StopCpuTurn();
            OpenProductSettings(ScreenId.Match);
        }

        private void OpenProductSettings(ScreenId returnScreen)
        {
            if (productSettingsScreen == null || productSettingsService == null) return;
            settingsReturnScreen = returnScreen;
            productSettingsScreen.SetValues(productSettingsService.Current,
                SettingsLoadFeedback(), feedbackIsError:
                    localizationController?.LastWarning != null);
            Router.Show(ScreenId.ProductSettings);
        }

        private void HandleProductSettingsApplyRequested(ProductSettings settings)
        {
            if (productSettingsScreen == null || productSettingsService == null) return;
            ProductSettingsSaveResult result = productSettingsService.SaveAndApply(settings);
            if (!result.Succeeded)
            {
                Debug.LogWarning("Product settings were not saved: " + result.Error);
                productSettingsScreen.SetFeedback(
                    Text.Get("settings.error_save_failed"),
                    isError: true);
                TryPlayFeedback(ProductFeedbackKind.Error);
                return;
            }
            try
            {
                InputController.ApplyBindings(productSettingsService.Current.InputBindings);
                settingsLoadFeedbackKey = string.Empty;
                RefreshLocalizedUi();
                RefreshActivePresentation();
                string feedback = result.InvalidArchivePath == null
                    ? Text.Get("settings.feedback_applied")
                    : Text.Get("settings.feedback_applied_preserved");
                feedback = AppendFontWarning(feedback);
                productSettingsScreen.SetValues(productSettingsService.Current, feedback,
                    feedbackIsError: localizationController?.LastWarning != null);
            }
            catch (Exception exception)
            {
                Debug.LogError("Saved input settings could not be applied: " +
                    exception.Message);
                productSettingsScreen.SetFeedback(
                    Text.Get("settings.error_input_apply_failed"),
                    isError: true);
                TryPlayFeedback(ProductFeedbackKind.Error);
            }
        }

        private void HandleProductSettingsResetRequested()
        {
            if (productSettingsScreen == null || productSettingsService == null) return;
            ProductSettingsSaveResult result = productSettingsService.ResetToDefaults();
            if (!result.Succeeded)
            {
                Debug.LogWarning("Product settings defaults were not saved: " + result.Error);
                productSettingsScreen.SetFeedback(
                    Text.Get("settings.error_defaults_failed"),
                    isError: true);
                TryPlayFeedback(ProductFeedbackKind.Error);
                return;
            }
            InputController.CancelRebind();
            InputController.ApplyBindings(productSettingsService.Current.InputBindings);
            settingsLoadFeedbackKey = string.Empty;
            RefreshLocalizedUi();
            RefreshActivePresentation();
            string feedback = result.InvalidArchivePath == null
                ? Text.Get("settings.feedback_defaults")
                : Text.Get("settings.feedback_defaults_preserved");
            feedback = AppendFontWarning(feedback);
            productSettingsScreen.SetValues(productSettingsService.Current, feedback,
                feedbackIsError: localizationController?.LastWarning != null);
        }

        private void HandleRebindRequested(ProductInputScheme scheme,
            ProductInputCommand command)
        {
            if (productSettingsScreen == null) return;
            string device = Text.Get(scheme == ProductInputScheme.Keyboard
                ? "settings.keyboard"
                : "settings.gamepad");
            productSettingsScreen.SetRebindState(true,
                Text.Get("settings.feedback_rebind_prompt", device,
                    Text.Get(InputCommandKey(command))));
            bool started;
            try
            {
                started = InputController.BeginRebind(scheme, command,
                    path => HandleRebindCompleted(scheme, command, path),
                    HandleRebindCancelled);
            }
            catch (Exception exception)
            {
                Debug.LogError("Product input rebinding could not start: " +
                    exception.Message);
                productSettingsScreen.SetRebindState(false, string.Empty);
                productSettingsScreen.SetFeedback(
                    Text.Get("settings.error_rebind_start_failed"), isError: true);
                TryPlayFeedback(ProductFeedbackKind.Error);
                return;
            }
            if (!started)
            {
                productSettingsScreen.SetRebindState(true,
                    Text.Get("settings.feedback_rebind_busy"));
                TryPlayFeedback(ProductFeedbackKind.Reject);
            }
        }

        private void HandleRebindCompleted(ProductInputScheme scheme,
            ProductInputCommand command, string path)
        {
            if (productSettingsScreen == null) return;
            productSettingsScreen.SetRebindState(false, string.Empty);
            productSettingsScreen.TrySetBinding(scheme, command, path, out _);
            Router.RestoreFocus();
        }

        private void HandleCancelRebindRequested() => InputController.CancelRebind();

        private void HandleRebindCancelled()
        {
            if (productSettingsScreen == null) return;
            productSettingsScreen.SetRebindState(false,
                Text.Get("settings.feedback_rebind_cancelled"));
            Router.RestoreFocus();
        }

        private void HandleProductSettingsCancelRequested()
        {
            if (InputController.IsRebinding)
                InputController.CancelRebind();
            else
                HandleProductSettingsBackRequested();
        }

        private void HandleProductSettingsBackRequested()
        {
            if (InputController.IsRebinding)
            {
                InputController.CancelRebind();
                return;
            }
            if (settingsReturnScreen != ScreenId.Match ||
                (activeSession == null && activeTutorial == null))
            {
                HandleTitleRequested();
                return;
            }

            RefreshActivePresentation();
            Router.Show(ScreenId.Match);
            if (activeSession != null) ScheduleCpuTurn();
            else ScheduleTutorialCpuTurn();
        }

        private void HandleMatchCancelRequested()
        {
            if (matchScreen?.IsPresentationLocked == true) return;
            if (matchScreen?.IsContextHelpVisible == true)
            {
                matchScreen.HideContextHelp();
                return;
            }
            if (activeTutorial != null)
                HandleTutorialExitRequested();
            else
                HandleTitleRequested();
        }

        private void HandleHelpRequested()
        {
            if (errorPanel?.gameObject.activeSelf == true) return;
            if (!Router.Current.HasValue || Router.Current == ScreenId.HowToPlay) return;
            if (Router.Current == ScreenId.Match)
            {
                if (matchScreen?.IsPresentationLocked == true) return;
                matchScreen?.ShowContextHelp();
                return;
            }
            if (Router.Current == ScreenId.Result)
            {
                HandleResultDetailsRequested();
                return;
            }
            ShowHowToPlay(Router.Current.Value, presentation: null);
        }

        private void HandleGamepadDisconnected()
        {
            Debug.LogWarning("Gamepad disconnected; keyboard and mouse remain available.");
            if (errorPanel == null || errorPanel.gameObject.activeSelf) return;
            errorReturnScreen = Router.Current ?? ScreenId.Title;
            gamepadDisconnectNoticeVisible = true;
            errorPanel.ShowKey("error.gamepad_disconnected");
            accessibilityController?.RefreshNavigation();
        }

        private void HandleGamepadReconnected()
        {
            Debug.Log("Gamepad reconnected.");
            if (errorPanel != null && errorPanel.gameObject.activeSelf &&
                gamepadDisconnectNoticeVisible)
            {
                gamepadDisconnectNoticeVisible = false;
                errorPanel.MessageLabel.text = Text.Get("error.gamepad_reconnected");
                return;
            }
            Router.RestoreFocus();
        }

        private void HandlePlayRequested()
        {
            if (settingsScreen == null) return;
            Router.Show(ScreenId.GameSettings);
            settingsScreen.SetValues(lastRequest ?? new GameStartRequest(seed: 1, wildRank: 8));
        }

        private void HandleTitleTutorialRequested()
        {
            if (titleScreen?.TutorialCompleted == true)
                ShowHowToPlay(ScreenId.Title, presentation: null);
            else
                BeginTutorial();
        }

        private void HandleStartRequested(GameStartRequest request)
        {
            if (matchScreen == null) throw new InvalidOperationException("Match screen is not configured.");
            if (request == null) throw new ArgumentNullException(nameof(request));
            EndTutorial();
            EndSession();
            ErrorPanel.Hide();
            lastRequest = request;
            var session = new GameSessionController(
                request.Seed, request.WildRank, request.Difficulty);
            BeginSession(session, SessionSlotIds.Create());
        }

        private void BeginSession(GameSessionController session, string slotId)
        {
            if (matchScreen == null) throw new InvalidOperationException("Match screen is not configured.");
            EndTutorial();
            EndSession();
            activeSession = session;
            activeSlotId = SessionSlotIds.Require(slotId);
            lastSessionCpuCueActionCount = -1;
            session.ActionApplied += HandleSessionActionApplied;
            session.SnapshotChanged += HandleSnapshotChanged;
            session.Finished += HandleSessionFinished;
            session.Faulted += HandleSessionFaulted;
            session.Begin();
            if (activeSession != session || session.State == MatchSessionState.Faulted) return;
            if (session.State != MatchSessionState.Finished)
            {
                Router.Show(ScreenId.Match);
                ScheduleCpuTurn();
            }
        }

        private void HandleSessionsRequested()
        {
            EndTutorial();
            EndSession();
            ErrorPanel.Hide();
            try
            {
                RefreshSessionLibrary();
                Router.Show(ScreenId.SessionLibrary);
            }
            catch (Exception)
            {
                ShowSafeError("error.session_list", ScreenId.Title);
            }
        }

        private void HandleResumeRequested(string slotId)
        {
            try
            {
                SessionArchive archive = SessionStore.Load(slotId);
                if (!archive.Configuration.Options.TryGetValue("wild_rank", out string? wildValue) ||
                    !int.TryParse(wildValue, NumberStyles.None, CultureInfo.InvariantCulture,
                        out int wildRank))
                    throw new InvalidOperationException("Saved options are unavailable.");
                var session = new GameSessionController(archive);
                lastRequest = new GameStartRequest(
                    archive.Configuration.Seed, wildRank, archive.Configuration.Difficulty);
                ErrorPanel.Hide();
                BeginSession(session, slotId);
            }
            catch (Exception)
            {
                ShowSafeError("error.resume", ScreenId.SessionLibrary);
            }
        }

        private void HandleReplayRequested(string slotId)
        {
            if (replayScreen == null) return;
            try
            {
                SessionArchive archive = SessionStore.Load(slotId);
                SessionReplayResult replay = SessionReplayer.Replay(archive, viewer: 0);
                ReplayCheckpoint checkpoint = replay.Checkpoints[replay.Checkpoints.Count - 1];
                replayScreen.Render(checkpoint.Presentation ?? throw new InvalidOperationException(
                    "Replay did not provide a viewer-safe presentation."), archive.Actions.Count);
                ErrorPanel.Hide();
                Router.Show(ScreenId.Replay);
            }
            catch (Exception)
            {
                ShowSafeError("error.replay", ScreenId.SessionLibrary);
            }
        }

        private void HandleDeleteRequested(string slotId)
        {
            try
            {
                SessionStore.Delete(slotId);
                RefreshSessionLibrary();
            }
            catch (Exception)
            {
                ShowSafeError("error.delete", ScreenId.SessionLibrary);
            }
        }

        private void RefreshSessionLibrary()
        {
            if (sessionLibraryScreen == null)
                throw new InvalidOperationException("Session library is not configured.");
            sessionLibraryScreen.SetSlots(SessionStore.List());
        }

        private void HandleRematchRequested()
        {
            if (lastRequest == null)
            {
                ShowSafeError("error.rematch_unavailable");
                return;
            }
            HandleStartRequested(lastRequest);
        }

        private void HandleTitleRequested()
        {
            if (inputController?.IsRebinding == true) inputController.CancelRebind();
            EndTutorial();
            EndSession();
            if (errorPanel != null) errorPanel.Hide();
            Router.Show(ScreenId.Title);
        }

        private void HandleActionRequested(string actionId)
        {
            TutorialSessionController? tutorial = activeTutorial;
            if (tutorial != null)
            {
                if (Router.Current != ScreenId.Match ||
                    matchScreen?.IsContextHelpVisible == true ||
                    matchScreen?.IsExternalModalLocked == true ||
                    matchScreen?.IsPresentationLocked == true) return;
                bool applied = tutorial.TryApplyHumanAction(actionId);
                if (!applied)
                {
                    if (tutorial.FeedbackKey != null &&
                        tutorial.FeedbackKey.StartsWith("tutorial.feedback.expected_",
                            StringComparison.Ordinal))
                        PresentMatchFeedback(ProductFeedbackKind.Reject);
                    return;
                }
                ScheduleTutorialCpuTurn();
                return;
            }
            GameSessionController? session = activeSession;
            if (session == null || Router.Current != ScreenId.Match ||
                matchScreen?.IsContextHelpVisible == true ||
                matchScreen?.IsExternalModalLocked == true ||
                matchScreen?.IsPresentationLocked == true ||
                !session.TryApplyHumanAction(actionId)) return;
            ScheduleCpuTurn();
        }

        private void HandleContextHelpOpened() => StopCpuTurn();
        private void HandleContextHelpClosed()
        {
            ScheduleCpuTurn();
            ScheduleTutorialCpuTurn();
        }

        private void HandleSettingsHowToPlayRequested() =>
            ShowHowToPlay(ScreenId.GameSettings, presentation: null);

        private void HandleMatchRulesRequested()
        {
            GamePresentation? presentation = activeSession?.Snapshot ?? activeTutorial?.Snapshot;
            if (presentation == null) return;
            StopCpuTurn();
            ShowHowToPlay(ScreenId.Match, presentation);
        }

        private void HandleResultDetailsRequested()
        {
            GamePresentation? presentation = activeSession?.Snapshot;
            if (presentation?.Result == null) return;
            ShowHowToPlay(ScreenId.Result, presentation);
        }

        private void ShowHowToPlay(ScreenId returnScreen, GamePresentation? presentation)
        {
            if (howToPlayScreen == null)
                throw new InvalidOperationException("How-to-play screen is not configured.");
            howToPlayReturnScreen = returnScreen;
            howToPlayScreen.Render(CrazyEightsHowToPlayPresenter.Create(
                presentation, text: Text));
            Router.Show(ScreenId.HowToPlay);
        }

        private void HandleHowToPlayBackRequested()
        {
            if (Router.Current != ScreenId.HowToPlay) return;
            ScreenId destination = howToPlayReturnScreen;
            Router.Show(destination);
            if (destination == ScreenId.Match) ScheduleCpuTurn();
            if (destination == ScreenId.Match) ScheduleTutorialCpuTurn();
        }

        private void HandleStartTutorialRequested() => BeginTutorial();

        private void BeginTutorial()
        {
            if (matchScreen == null)
                throw new InvalidOperationException("Match screen is not configured.");
            EndSession();
            EndTutorial();
            ErrorPanel.Hide();
            var tutorial = new TutorialSessionController();
            activeTutorial = tutorial;
            lastTutorialCpuCueActionCount = -1;
            tutorial.ActionApplied += HandleTutorialActionApplied;
            tutorial.Changed += HandleTutorialChanged;
            tutorial.Completed += HandleTutorialCompleted;
            tutorial.Faulted += HandleTutorialFaulted;
            Router.Show(ScreenId.Match);
            tutorial.Begin();
            if (activeTutorial != tutorial || tutorial.State == TutorialSessionState.Faulted)
                return;
        }

        private void HandleTutorialChanged()
        {
            TutorialSessionController? tutorial = activeTutorial;
            if (tutorial == null || matchScreen == null ||
                tutorial.State == TutorialSessionState.Faulted) return;
            bool inputEnabled = tutorial.State == TutorialSessionState.AwaitingHuman;
            matchScreen.RenderTutorial(
                CrazyEightsMatchPresenter.Create(tutorial.Snapshot, inputEnabled, Text),
                TutorialOverlayPresenter.Create(tutorial, Text));
            accessibilityController?.RefreshNavigation();
            SessionActionRecord? appliedAction = pendingTutorialAction;
            pendingTutorialAction = null;
            if (appliedAction != null)
                StartActionPresentation(appliedAction);
            else if (Router.Current == ScreenId.Match)
                Router.RestoreFocus();
            ScheduleTutorialCpuTurn();
        }

        private void HandleTutorialActionApplied(SessionActionRecord action) =>
            pendingTutorialAction = action ?? throw new ArgumentNullException(nameof(action));

        private void HandleTutorialContinueRequested()
        {
            TutorialSessionController? tutorial = activeTutorial;
            if (tutorial == null || Router.Current != ScreenId.Match ||
                matchScreen?.IsPresentationLocked == true) return;
            if (tutorial.State == TutorialSessionState.AwaitingIntro)
                tutorial.AcknowledgeIntro();
            else if (tutorial.State == TutorialSessionState.AwaitingResultConfirmation)
                tutorial.ConfirmResult();
        }

        private void HandleTutorialExitRequested()
        {
            if (activeTutorial == null) return;
            EndTutorial();
            RefreshTutorialProgress();
            Router.Show(ScreenId.Title);
        }

        private void HandleTutorialCompleted()
        {
            TutorialSessionController? tutorial = activeTutorial;
            if (tutorial == null) return;
            try
            {
                ProgressStore.SaveTutorialCompleted(tutorial.Definition);
            }
            catch (Exception)
            {
                ShowSafeError("error.tutorial_progress_save");
                return;
            }
            EndTutorial();
            RefreshTutorialProgress();
            Router.Show(ScreenId.Title);
        }

        private void HandleTutorialFaulted(string _)
        {
            Debug.LogError("Crazy Eights tutorial stopped safely.");
            ShowSafeError("error.tutorial_stopped");
        }

        private void HandleSnapshotChanged(GamePresentation presentation)
        {
            if (matchScreen == null || activeSession == null) return;
            if (activeSlotId == null)
                throw new InvalidOperationException("Active session has no save slot.");
            try
            {
                SessionStore.Save(activeSlotId, activeSession.Archive);
            }
            catch (Exception)
            {
                throw new InvalidOperationException("Autosave could not be completed safely.");
            }
            matchScreen.Render(CrazyEightsMatchPresenter.Create(
                presentation,
                activeSession.State == MatchSessionState.AwaitingHuman,
                Text));
            accessibilityController?.RefreshNavigation();
            SessionActionRecord? appliedAction = pendingSessionAction;
            pendingSessionAction = null;
            if (appliedAction != null)
                StartActionPresentation(appliedAction);
            else if (Router.Current == ScreenId.Match)
                Router.RestoreFocus();
        }

        private void HandleSessionActionApplied(SessionActionRecord action) =>
            pendingSessionAction = action ?? throw new ArgumentNullException(nameof(action));

        private void HandleSessionFinished(GameResultPresentation result)
        {
            StopCpuTurn();
            pendingResult = CrazyEightsResultPresenter.Create(result, text: Text);
            if (actionPresentationCoroutine == null) ShowPendingResult();
        }

        private void StartActionPresentation(SessionActionRecord action)
        {
            IReadOnlyList<ProductFeedbackKind> sequence;
            try
            {
                sequence = ProductActionFeedback.ClassifyActionSequence(action);
            }
            catch (Exception exception)
            {
                Debug.LogError("Action feedback classification failed: " + exception.Message);
                matchScreen?.SetPresentationLocked(false);
                return;
            }

            CancelActionPresentation(unlockMatch: false, clearPendingResult: false);
            matchScreen?.SetPresentationLocked(true);
            if (Router.Current == ScreenId.Match) Router.RestoreFocus();
            int generation = ++presentationGeneration;
            actionPresentationCoroutine = StartCoroutine(RunActionPresentation(
                sequence, generation, activeSession, activeTutorial));
        }

        private IEnumerator RunActionPresentation(
            IReadOnlyList<ProductFeedbackKind> sequence, int generation,
            GameSessionController? session, TutorialSessionController? tutorial)
        {
            foreach (ProductFeedbackKind kind in sequence)
            {
                if (!IsCurrentPresentation(generation, session, tutorial)) yield break;
                PresentMatchFeedback(kind);
                yield return WaitForCurrentCue(generation, session, tutorial);
            }

            if (!IsCurrentPresentation(generation, session, tutorial)) yield break;
            actionPresentationCoroutine = null;
            if (session != null && pendingResult != null)
            {
                ShowPendingResult();
                yield break;
            }

            matchScreen?.SetPresentationLocked(false);
            if (Router.Current == ScreenId.Match) Router.RestoreFocus();
            if (session != null) ScheduleCpuTurn();
            if (tutorial != null) ScheduleTutorialCpuTurn();
        }

        private IEnumerator WaitForCurrentCue(int generation,
            GameSessionController? session, TutorialSessionController? tutorial)
        {
            float elapsed = 0f;
            bool yielded = false;
            while (IsCurrentPresentation(generation, session, tutorial))
            {
                ProductPresentationPolicy policy = PresentationController.Policy;
                float duration = policy.CueEnterSeconds + policy.CueHoldSeconds +
                    policy.CueExitSeconds;
                if (yielded && elapsed >= duration) yield break;
                yield return null;
                yielded = true;
                elapsed += Time.unscaledDeltaTime;
            }
        }

        private bool IsCurrentPresentation(int generation, GameSessionController? session,
            TutorialSessionController? tutorial) =>
            generation == presentationGeneration &&
            (session == null || activeSession == session) &&
            (tutorial == null || activeTutorial == tutorial);

        private void ShowPendingResult()
        {
            ResultViewModel? model = pendingResult;
            pendingResult = null;
            if (model == null || resultScreen == null) return;
            matchScreen?.SetPresentationLocked(false);
            resultScreen.Render(model);
            Router.Show(ScreenId.Result);
            if (model.Outcome == ProductResultOutcome.Win)
                TryPlayFeedback(ProductFeedbackKind.Win);
            else if (model.Outcome == ProductResultOutcome.Loss)
                TryPlayFeedback(ProductFeedbackKind.Lose);
        }

        private void PresentMatchFeedback(ProductFeedbackKind kind)
        {
            try
            {
                matchScreen?.ShowActionFeedback(kind);
            }
            catch (Exception exception)
            {
                Debug.LogError("Match feedback failed: " + exception.Message);
            }
            TryPlayFeedback(kind);
        }

        private bool TryPlayFeedback(ProductFeedbackKind kind)
        {
            try
            {
                PresentationController.Play(kind);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Product feedback failed: " + exception.Message);
                return false;
            }
        }

        private void HandleSessionFaulted(string _)
        {
            StopCpuTurn();
            Debug.LogError("Crazy Eights session stopped safely.");
            ShowSafeError("error.match_stopped");
        }

        private void ShowSafeError(string key, ScreenId returnScreen = ScreenId.Title)
        {
            EndTutorial();
            EndSession();
            gamepadDisconnectNoticeVisible = false;
            errorReturnScreen = returnScreen;
            Router.Show(returnScreen);
            ErrorPanel.ShowKey(key);
            accessibilityController?.RefreshNavigation();
        }

        private void HandleErrorDismissed()
        {
            gamepadDisconnectNoticeVisible = false;
            ErrorPanel.Hide();
            Router.Show(errorReturnScreen);
            accessibilityController?.RefreshNavigation();
        }

        private void ScheduleCpuTurn()
        {
            if (activeSession?.State != MatchSessionState.WaitingForCpu ||
                cpuTurnCoroutine != null || actionPresentationCoroutine != null ||
                Router.Current != ScreenId.Match ||
                matchScreen?.IsContextHelpVisible == true) return;
            if (lastSessionCpuCueActionCount != activeSession.Archive.Actions.Count)
            {
                lastSessionCpuCueActionCount = activeSession.Archive.Actions.Count;
                PresentMatchFeedback(ProductFeedbackKind.CpuTurn);
            }
            cpuTurnCoroutine = StartCoroutine(RunCpuTurn(activeSession));
        }

        private void ScheduleTutorialCpuTurn()
        {
            if (activeTutorial?.State != TutorialSessionState.WaitingForCpu ||
                cpuTurnCoroutine != null || actionPresentationCoroutine != null ||
                Router.Current != ScreenId.Match ||
                matchScreen?.IsContextHelpVisible == true) return;
            if (lastTutorialCpuCueActionCount != activeTutorial.Archive.Actions.Count)
            {
                lastTutorialCpuCueActionCount = activeTutorial.Archive.Actions.Count;
                PresentMatchFeedback(ProductFeedbackKind.CpuTurn);
            }
            cpuTurnCoroutine = StartCoroutine(RunTutorialCpuTurn(activeTutorial));
        }

        private IEnumerator RunTutorialCpuTurn(TutorialSessionController tutorial)
        {
            yield return WaitForDynamicCpuDelay();
            cpuTurnCoroutine = null;
            if (activeTutorial != tutorial ||
                tutorial.State != TutorialSessionState.WaitingForCpu ||
                Router.Current != ScreenId.Match) yield break;
            tutorial.TryApplyCpuAction();
            ScheduleTutorialCpuTurn();
        }

        private IEnumerator RunCpuTurn(GameSessionController session)
        {
            yield return WaitForDynamicCpuDelay();
            cpuTurnCoroutine = null;
            if (activeSession != session || session.State != MatchSessionState.WaitingForCpu)
                yield break;
            session.TryApplyCpuAction();
            ScheduleCpuTurn();
        }

        private IEnumerator WaitForDynamicCpuDelay()
        {
            float elapsed = 0f;
            do
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            while (elapsed < CpuTurnDelaySeconds);
        }

        private void StopCpuTurn()
        {
            if (cpuTurnCoroutine == null) return;
            StopCoroutine(cpuTurnCoroutine);
            cpuTurnCoroutine = null;
        }

        private void CancelActionPresentation(bool unlockMatch, bool clearPendingResult)
        {
            presentationGeneration++;
            if (actionPresentationCoroutine != null)
            {
                StopCoroutine(actionPresentationCoroutine);
                actionPresentationCoroutine = null;
            }
            if (unlockMatch && matchScreen != null)
                matchScreen.SetPresentationLocked(false);
            if (clearPendingResult)
            {
                pendingResult = null;
                if (presentationController != null) presentationController.Cancel();
            }
        }

        private void EndSession()
        {
            StopCpuTurn();
            if (matchScreen != null) matchScreen.HideContextHelp(notify: false);
            if (activeSession == null) return;
            CancelActionPresentation(unlockMatch: true, clearPendingResult: true);
            pendingSessionAction = null;
            activeSession.ActionApplied -= HandleSessionActionApplied;
            activeSession.SnapshotChanged -= HandleSnapshotChanged;
            activeSession.Finished -= HandleSessionFinished;
            activeSession.Faulted -= HandleSessionFaulted;
            activeSession = null;
            activeSlotId = null;
            lastSessionCpuCueActionCount = -1;
        }

        private void EndTutorial()
        {
            StopCpuTurn();
            if (matchScreen != null)
            {
                matchScreen.HideContextHelp(notify: false);
                matchScreen.HideTutorial();
            }
            TutorialSessionController? tutorial = activeTutorial;
            if (tutorial == null) return;
            CancelActionPresentation(unlockMatch: true, clearPendingResult: true);
            pendingTutorialAction = null;
            activeTutorial = null;
            tutorial.ActionApplied -= HandleTutorialActionApplied;
            tutorial.Changed -= HandleTutorialChanged;
            tutorial.Completed -= HandleTutorialCompleted;
            tutorial.Faulted -= HandleTutorialFaulted;
            tutorial.Cancel();
            lastTutorialCpuCueActionCount = -1;
        }

        private void RefreshTutorialProgress()
        {
            if (titleScreen == null || progressStore == null) return;
            bool completed = false;
            try
            {
                completed = progressStore.Load().IsTutorialCompleted(
                    TutorialDefinition.CrazyEightsBasic);
            }
            catch (Exception)
            {
                Debug.LogWarning("Product progress could not be read safely.");
            }
            titleScreen.SetTutorialCompleted(completed);
            if (router?.Current == ScreenId.Title) router.Show(ScreenId.Title);
        }

        private void RefreshLocalizedUi()
        {
            IProductText currentText = Text;
            titleScreen?.SetText(currentText);
            settingsScreen?.SetText(currentText);
            productSettingsScreen?.SetText(currentText);
            sessionLibraryScreen?.SetText(currentText);
            matchScreen?.SetText(currentText);
            replayScreen?.SetText(currentText);
            resultScreen?.SetText(currentText);
            howToPlayScreen?.SetText(currentText);
            errorPanel?.SetText(currentText);
            presentationController?.SetText(currentText);
            accessibilityController?.RefreshNavigation();
        }

        private void RefreshActivePresentation()
        {
            if (matchScreen == null) return;
            if (activeTutorial != null &&
                activeTutorial.State != TutorialSessionState.Faulted)
            {
                bool inputEnabled = activeTutorial.State ==
                    TutorialSessionState.AwaitingHuman;
                matchScreen.RenderTutorial(
                    CrazyEightsMatchPresenter.Create(activeTutorial.Snapshot,
                        inputEnabled, Text),
                    TutorialOverlayPresenter.Create(activeTutorial, Text));
                accessibilityController?.RefreshNavigation();
                return;
            }
            if (activeSession != null &&
                activeSession.State != MatchSessionState.Faulted)
            {
                matchScreen.Render(CrazyEightsMatchPresenter.Create(
                    activeSession.Snapshot,
                    activeSession.State == MatchSessionState.AwaitingHuman,
                    Text));
                accessibilityController?.RefreshNavigation();
            }
        }

        private string SettingsLoadFeedback()
        {
            string feedback = string.IsNullOrEmpty(settingsLoadFeedbackKey)
                ? string.Empty
                : Text.Get(settingsLoadFeedbackKey);
            return AppendFontWarning(feedback);
        }

        private string AppendFontWarning(string feedback)
        {
            string? warning = localizationController?.LastWarning;
            if (string.IsNullOrWhiteSpace(warning)) return feedback;
            return string.IsNullOrWhiteSpace(feedback)
                ? warning
                : feedback + "\n" + warning;
        }

        private void ShowStartupFontWarningIfNeeded()
        {
            string? warning = localizationController?.LastWarning;
            if (string.IsNullOrWhiteSpace(warning))
            {
                shownStartupFontWarning = string.Empty;
                return;
            }
            if (string.Equals(shownStartupFontWarning, warning,
                    StringComparison.Ordinal) || errorPanel == null ||
                errorPanel.gameObject.activeSelf || router?.Current == null ||
                router.Current == ScreenId.ProductSettings)
                return;

            shownStartupFontWarning = warning;
            errorReturnScreen = router.Current.Value;
            errorPanel.Show(warning);
        }

        private static string InputCommandKey(ProductInputCommand command) => command switch
        {
            ProductInputCommand.Up => "settings.command_up",
            ProductInputCommand.Down => "settings.command_down",
            ProductInputCommand.Left => "settings.command_left",
            ProductInputCommand.Right => "settings.command_right",
            ProductInputCommand.Submit => "settings.command_submit",
            ProductInputCommand.Cancel => "settings.command_back",
            ProductInputCommand.Help => "settings.command_help",
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };

        private float CpuTurnDelaySeconds => productSettingsService?.Current.PresentationSpeed switch
        {
            ProductPresentationSpeed.Reduced => 0.1f,
            ProductPresentationSpeed.Fast => 0.05f,
            _ => 0.35f
        };

        private static void HandleQuitRequested() => Application.Quit();
    }
}
