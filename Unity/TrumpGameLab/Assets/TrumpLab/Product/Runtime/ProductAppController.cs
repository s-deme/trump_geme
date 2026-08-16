#nullable enable

using System;
using System.Collections;
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
        [SerializeField] private SessionLibraryScreen? sessionLibraryScreen;
        [SerializeField] private MatchScreen? matchScreen;
        [SerializeField] private ReplayScreen? replayScreen;
        [SerializeField] private ResultScreen? resultScreen;
        [SerializeField] private HowToPlayScreen? howToPlayScreen;
        [SerializeField] private ProductErrorPanel? errorPanel;

        private GameSessionController? activeSession;
        private TutorialSessionController? activeTutorial;
        private Coroutine? cpuTurnCoroutine;
        private GameStartRequest? lastRequest;
        private ISessionStore? sessionStore;
        private IProductProgressStore? progressStore;
        private string? activeSlotId;
        private ScreenId errorReturnScreen = ScreenId.Title;
        private ScreenId howToPlayReturnScreen = ScreenId.Title;

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

        public void Configure(ScreenRouter configuredRouter, TitleScreen title,
            GameSettingsScreen settings, SessionLibraryScreen library, MatchScreen match,
            ReplayScreen replay, ResultScreen result, HowToPlayScreen howToPlay,
            ProductErrorPanel errors)
        {
            router = configuredRouter;
            titleScreen = title;
            settingsScreen = settings;
            sessionLibraryScreen = library;
            matchScreen = match;
            replayScreen = replay;
            resultScreen = result;
            howToPlayScreen = howToPlay;
            errorPanel = errors;
        }

        public void SetSessionStore(ISessionStore store) =>
            sessionStore = store ?? throw new ArgumentNullException(nameof(store));

        public void SetProgressStore(IProductProgressStore store)
        {
            progressStore = store ?? throw new ArgumentNullException(nameof(store));
            RefreshTutorialProgress();
        }

        private void Awake()
        {
            if (router == null || titleScreen == null || settingsScreen == null ||
                sessionLibraryScreen == null || matchScreen == null || replayScreen == null ||
                resultScreen == null || howToPlayScreen == null || errorPanel == null)
                throw new InvalidOperationException("Product app controller is not configured.");

            if (sessionStore == null) sessionStore = new FileSessionStore(Application.persistentDataPath);
            if (progressStore == null)
                progressStore = new FileProductProgressStore(Application.persistentDataPath);
            RefreshTutorialProgress();

            titleScreen.TutorialRequested += HandleTitleTutorialRequested;
            titleScreen.PlayRequested += HandlePlayRequested;
            titleScreen.SessionsRequested += HandleSessionsRequested;
            titleScreen.QuitRequested += HandleQuitRequested;
            settingsScreen.StartRequested += HandleStartRequested;
            settingsScreen.HowToPlayRequested += HandleSettingsHowToPlayRequested;
            settingsScreen.BackRequested += HandleTitleRequested;
            sessionLibraryScreen.ResumeRequested += HandleResumeRequested;
            sessionLibraryScreen.ReplayRequested += HandleReplayRequested;
            sessionLibraryScreen.DeleteRequested += HandleDeleteRequested;
            sessionLibraryScreen.BackRequested += HandleTitleRequested;
            matchScreen.ActionRequested += HandleActionRequested;
            matchScreen.ContextHelpOpened += HandleContextHelpOpened;
            matchScreen.ContextHelpClosed += HandleContextHelpClosed;
            matchScreen.RulesRequested += HandleMatchRulesRequested;
            matchScreen.TutorialContinueRequested += HandleTutorialContinueRequested;
            matchScreen.TutorialExitRequested += HandleTutorialExitRequested;
            replayScreen.BackRequested += HandleSessionsRequested;
            resultScreen.RematchRequested += HandleRematchRequested;
            resultScreen.DetailsRequested += HandleResultDetailsRequested;
            resultScreen.TitleRequested += HandleTitleRequested;
            howToPlayScreen.BackRequested += HandleHowToPlayBackRequested;
            howToPlayScreen.StartTutorialRequested += HandleStartTutorialRequested;
            errorPanel.Dismissed += HandleErrorDismissed;
            errorPanel.Hide();
            router.Show(ScreenId.Title);
        }

        private void OnDestroy()
        {
            if (titleScreen != null)
            {
                titleScreen.TutorialRequested -= HandleTitleTutorialRequested;
                titleScreen.PlayRequested -= HandlePlayRequested;
                titleScreen.SessionsRequested -= HandleSessionsRequested;
                titleScreen.QuitRequested -= HandleQuitRequested;
            }
            if (settingsScreen != null)
            {
                settingsScreen.StartRequested -= HandleStartRequested;
                settingsScreen.HowToPlayRequested -= HandleSettingsHowToPlayRequested;
                settingsScreen.BackRequested -= HandleTitleRequested;
            }
            if (sessionLibraryScreen != null)
            {
                sessionLibraryScreen.ResumeRequested -= HandleResumeRequested;
                sessionLibraryScreen.ReplayRequested -= HandleReplayRequested;
                sessionLibraryScreen.DeleteRequested -= HandleDeleteRequested;
                sessionLibraryScreen.BackRequested -= HandleTitleRequested;
            }
            if (matchScreen != null)
            {
                matchScreen.ActionRequested -= HandleActionRequested;
                matchScreen.ContextHelpOpened -= HandleContextHelpOpened;
                matchScreen.ContextHelpClosed -= HandleContextHelpClosed;
                matchScreen.RulesRequested -= HandleMatchRulesRequested;
                matchScreen.TutorialContinueRequested -= HandleTutorialContinueRequested;
                matchScreen.TutorialExitRequested -= HandleTutorialExitRequested;
            }
            if (replayScreen != null) replayScreen.BackRequested -= HandleSessionsRequested;
            if (resultScreen != null)
            {
                resultScreen.RematchRequested -= HandleRematchRequested;
                resultScreen.DetailsRequested -= HandleResultDetailsRequested;
                resultScreen.TitleRequested -= HandleTitleRequested;
            }
            if (howToPlayScreen != null)
            {
                howToPlayScreen.BackRequested -= HandleHowToPlayBackRequested;
                howToPlayScreen.StartTutorialRequested -= HandleStartTutorialRequested;
            }
            if (errorPanel != null) errorPanel.Dismissed -= HandleErrorDismissed;
            EndSession();
            EndTutorial();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (Router.Current == ScreenId.HowToPlay)
            {
                HandleHowToPlayBackRequested();
                return;
            }
            if (Router.Current == ScreenId.Match && matchScreen != null &&
                matchScreen.IsContextHelpVisible)
            {
                matchScreen.HideContextHelp();
                return;
            }
            if (Router.Current == ScreenId.Match && activeTutorial != null)
            {
                HandleTutorialExitRequested();
                return;
            }
            if (errorPanel != null && errorPanel.gameObject.activeSelf)
            {
                HandleErrorDismissed();
                return;
            }
            if (Router.Current == ScreenId.GameSettings || Router.Current == ScreenId.SessionLibrary ||
                Router.Current == ScreenId.Replay || Router.Current == ScreenId.Result)
                HandleTitleRequested();
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
                ShowSafeError("Saved sessions could not be listed safely.", ScreenId.Title);
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
                ShowSafeError("The selected save could not be resumed safely.",
                    ScreenId.SessionLibrary);
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
                ShowSafeError("The selected replay could not be opened safely.",
                    ScreenId.SessionLibrary);
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
                ShowSafeError("The selected save could not be deleted safely.",
                    ScreenId.SessionLibrary);
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
                ShowSafeError("No previous match settings are available for a rematch.");
                return;
            }
            HandleStartRequested(lastRequest);
        }

        private void HandleTitleRequested()
        {
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
                    !tutorial.TryApplyHumanAction(actionId)) return;
                ScheduleTutorialCpuTurn();
                return;
            }
            GameSessionController? session = activeSession;
            if (session == null || Router.Current != ScreenId.Match ||
                matchScreen?.IsContextHelpVisible == true ||
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
            howToPlayScreen.Render(CrazyEightsHowToPlayPresenter.Create(presentation));
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
                CrazyEightsMatchPresenter.Create(tutorial.Snapshot, inputEnabled),
                TutorialOverlayPresenter.Create(tutorial));
            ScheduleTutorialCpuTurn();
        }

        private void HandleTutorialContinueRequested()
        {
            TutorialSessionController? tutorial = activeTutorial;
            if (tutorial == null || Router.Current != ScreenId.Match) return;
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
                ShowSafeError("Tutorial completed, but progress could not be saved safely.");
                return;
            }
            EndTutorial();
            RefreshTutorialProgress();
            Router.Show(ScreenId.Title);
        }

        private void HandleTutorialFaulted(string _)
        {
            Debug.LogError("Crazy Eights tutorial stopped safely.");
            ShowSafeError("The tutorial stopped safely.");
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
                activeSession.State == MatchSessionState.AwaitingHuman));
        }

        private void HandleSessionFinished(GameResultPresentation result)
        {
            StopCpuTurn();
            if (resultScreen == null) return;
            resultScreen.Render(CrazyEightsResultPresenter.Create(result));
            Router.Show(ScreenId.Result);
        }

        private void HandleSessionFaulted(string _)
        {
            StopCpuTurn();
            Debug.LogError("Crazy Eights session stopped safely.");
            ShowSafeError("The match stopped safely.");
        }

        private void ShowSafeError(string message, ScreenId returnScreen = ScreenId.Title)
        {
            EndTutorial();
            EndSession();
            errorReturnScreen = returnScreen;
            Router.Show(returnScreen);
            ErrorPanel.Show(message);
        }

        private void HandleErrorDismissed()
        {
            ErrorPanel.Hide();
            Router.Show(errorReturnScreen);
        }

        private void ScheduleCpuTurn()
        {
            if (activeSession?.State != MatchSessionState.WaitingForCpu ||
                cpuTurnCoroutine != null || matchScreen?.IsContextHelpVisible == true) return;
            cpuTurnCoroutine = StartCoroutine(RunCpuTurn(activeSession));
        }

        private void ScheduleTutorialCpuTurn()
        {
            if (activeTutorial?.State != TutorialSessionState.WaitingForCpu ||
                cpuTurnCoroutine != null || Router.Current != ScreenId.Match ||
                matchScreen?.IsContextHelpVisible == true) return;
            cpuTurnCoroutine = StartCoroutine(RunTutorialCpuTurn(activeTutorial));
        }

        private IEnumerator RunTutorialCpuTurn(TutorialSessionController tutorial)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            cpuTurnCoroutine = null;
            if (activeTutorial != tutorial ||
                tutorial.State != TutorialSessionState.WaitingForCpu ||
                Router.Current != ScreenId.Match) yield break;
            tutorial.TryApplyCpuAction();
            ScheduleTutorialCpuTurn();
        }

        private IEnumerator RunCpuTurn(GameSessionController session)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            cpuTurnCoroutine = null;
            if (activeSession != session || session.State != MatchSessionState.WaitingForCpu)
                yield break;
            session.TryApplyCpuAction();
            ScheduleCpuTurn();
        }

        private void StopCpuTurn()
        {
            if (cpuTurnCoroutine == null) return;
            StopCoroutine(cpuTurnCoroutine);
            cpuTurnCoroutine = null;
        }

        private void EndSession()
        {
            StopCpuTurn();
            matchScreen?.HideContextHelp(notify: false);
            if (activeSession == null) return;
            activeSession.SnapshotChanged -= HandleSnapshotChanged;
            activeSession.Finished -= HandleSessionFinished;
            activeSession.Faulted -= HandleSessionFaulted;
            activeSession = null;
            activeSlotId = null;
        }

        private void EndTutorial()
        {
            StopCpuTurn();
            matchScreen?.HideContextHelp(notify: false);
            matchScreen?.HideTutorial();
            TutorialSessionController? tutorial = activeTutorial;
            if (tutorial == null) return;
            activeTutorial = null;
            tutorial.Changed -= HandleTutorialChanged;
            tutorial.Completed -= HandleTutorialCompleted;
            tutorial.Faulted -= HandleTutorialFaulted;
            tutorial.Cancel();
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
        private static void HandleQuitRequested() => Application.Quit();
    }
}
