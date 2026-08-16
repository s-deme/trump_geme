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
        [SerializeField] private ProductErrorPanel? errorPanel;

        private GameSessionController? activeSession;
        private Coroutine? cpuTurnCoroutine;
        private GameStartRequest? lastRequest;
        private ISessionStore? sessionStore;
        private string? activeSlotId;
        private ScreenId errorReturnScreen = ScreenId.Title;

        public ScreenRouter Router => router ?? throw new InvalidOperationException(
            "Screen router is not configured.");
        public IGame? ActiveGame => activeSession?.Game;
        public GameSessionController? ActiveSession => activeSession;
        public string? ActiveSlotId => activeSlotId;
        public ProductErrorPanel ErrorPanel => errorPanel ?? throw new InvalidOperationException(
            "Product error panel is not configured.");
        public GameStartRequest? LastRequest => lastRequest;
        public ISessionStore SessionStore => sessionStore ?? throw new InvalidOperationException(
            "Session store is not configured.");

        public void Configure(ScreenRouter configuredRouter, TitleScreen title,
            GameSettingsScreen settings, SessionLibraryScreen library, MatchScreen match,
            ReplayScreen replay, ResultScreen result, ProductErrorPanel errors)
        {
            router = configuredRouter;
            titleScreen = title;
            settingsScreen = settings;
            sessionLibraryScreen = library;
            matchScreen = match;
            replayScreen = replay;
            resultScreen = result;
            errorPanel = errors;
        }

        public void SetSessionStore(ISessionStore store) =>
            sessionStore = store ?? throw new ArgumentNullException(nameof(store));

        private void Awake()
        {
            if (router == null || titleScreen == null || settingsScreen == null ||
                sessionLibraryScreen == null || matchScreen == null || replayScreen == null ||
                resultScreen == null || errorPanel == null)
                throw new InvalidOperationException("Product app controller is not configured.");

            if (sessionStore == null) sessionStore = new FileSessionStore(Application.persistentDataPath);

            titleScreen.PlayRequested += HandlePlayRequested;
            titleScreen.SessionsRequested += HandleSessionsRequested;
            titleScreen.QuitRequested += HandleQuitRequested;
            settingsScreen.StartRequested += HandleStartRequested;
            settingsScreen.BackRequested += HandleTitleRequested;
            sessionLibraryScreen.ResumeRequested += HandleResumeRequested;
            sessionLibraryScreen.ReplayRequested += HandleReplayRequested;
            sessionLibraryScreen.DeleteRequested += HandleDeleteRequested;
            sessionLibraryScreen.BackRequested += HandleTitleRequested;
            matchScreen.ActionRequested += HandleActionRequested;
            replayScreen.BackRequested += HandleSessionsRequested;
            resultScreen.RematchRequested += HandleRematchRequested;
            resultScreen.TitleRequested += HandleTitleRequested;
            errorPanel.Dismissed += HandleErrorDismissed;
            errorPanel.Hide();
            router.Show(ScreenId.Title);
        }

        private void OnDestroy()
        {
            if (titleScreen != null)
            {
                titleScreen.PlayRequested -= HandlePlayRequested;
                titleScreen.SessionsRequested -= HandleSessionsRequested;
                titleScreen.QuitRequested -= HandleQuitRequested;
            }
            if (settingsScreen != null)
            {
                settingsScreen.StartRequested -= HandleStartRequested;
                settingsScreen.BackRequested -= HandleTitleRequested;
            }
            if (sessionLibraryScreen != null)
            {
                sessionLibraryScreen.ResumeRequested -= HandleResumeRequested;
                sessionLibraryScreen.ReplayRequested -= HandleReplayRequested;
                sessionLibraryScreen.DeleteRequested -= HandleDeleteRequested;
                sessionLibraryScreen.BackRequested -= HandleTitleRequested;
            }
            if (matchScreen != null) matchScreen.ActionRequested -= HandleActionRequested;
            if (replayScreen != null) replayScreen.BackRequested -= HandleSessionsRequested;
            if (resultScreen != null)
            {
                resultScreen.RematchRequested -= HandleRematchRequested;
                resultScreen.TitleRequested -= HandleTitleRequested;
            }
            if (errorPanel != null) errorPanel.Dismissed -= HandleErrorDismissed;
            EndSession();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
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

        private void HandleStartRequested(GameStartRequest request)
        {
            if (matchScreen == null) throw new InvalidOperationException("Match screen is not configured.");
            if (request == null) throw new ArgumentNullException(nameof(request));
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
            EndSession();
            if (errorPanel != null) errorPanel.Hide();
            Router.Show(ScreenId.Title);
        }

        private void HandleActionRequested(string actionId)
        {
            GameSessionController? session = activeSession;
            if (session == null || !session.TryApplyHumanAction(actionId)) return;
            ScheduleCpuTurn();
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
                cpuTurnCoroutine != null) return;
            cpuTurnCoroutine = StartCoroutine(RunCpuTurn(activeSession));
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
            if (activeSession == null) return;
            activeSession.SnapshotChanged -= HandleSnapshotChanged;
            activeSession.Finished -= HandleSessionFinished;
            activeSession.Faulted -= HandleSessionFaulted;
            activeSession = null;
            activeSlotId = null;
        }
        private static void HandleQuitRequested() => Application.Quit();
    }
}
