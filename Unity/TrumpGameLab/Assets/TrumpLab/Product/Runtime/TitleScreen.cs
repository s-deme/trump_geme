#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class TitleScreen : ProductScreen, IPreferredFocusProvider
    {
        [SerializeField] private Button? tutorialButton;
        [SerializeField] private Button? playButton;
        [SerializeField] private Button? sessionsButton;
        [SerializeField] private Button? settingsButton;
        [SerializeField] private Button? quitButton;

        public override ScreenId Id => ScreenId.Title;
        public Button TutorialButton => tutorialButton ?? throw new InvalidOperationException(
            "Title tutorial button is not configured.");
        public Button PlayButton => playButton ?? throw new InvalidOperationException(
            "Title play button is not configured.");
        public Button SettingsButton => settingsButton ?? throw new InvalidOperationException(
            "Title settings button is not configured.");
        public bool TutorialCompleted { get; private set; }
        public Selectable? PreferredFocus => TutorialCompleted ? PlayButton : TutorialButton;
        public event System.Action? TutorialRequested;
        public event System.Action? PlayRequested;
        public event System.Action? SessionsRequested;
        public event System.Action? SettingsRequested;
        public event System.Action? QuitRequested;

        public void Configure(Button tutorial, Button play, Button sessions, Button settings,
            Button quit)
        {
            tutorialButton = tutorial;
            playButton = play;
            sessionsButton = sessions;
            settingsButton = settings;
            quitButton = quit;
        }

        private void Awake()
        {
            if (tutorialButton == null || playButton == null || sessionsButton == null ||
                settingsButton == null || quitButton == null)
                throw new InvalidOperationException("Title screen buttons are not configured.");
            tutorialButton.onClick.AddListener(HandleTutorial);
            playButton.onClick.AddListener(HandlePlay);
            sessionsButton.onClick.AddListener(HandleSessions);
            settingsButton.onClick.AddListener(HandleSettings);
            quitButton.onClick.AddListener(HandleQuit);
        }

        private void OnDestroy()
        {
            if (tutorialButton != null)
                tutorialButton.onClick.RemoveListener(HandleTutorial);
            if (playButton != null) playButton.onClick.RemoveListener(HandlePlay);
            if (sessionsButton != null) sessionsButton.onClick.RemoveListener(HandleSessions);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(HandleSettings);
            if (quitButton != null) quitButton.onClick.RemoveListener(HandleQuit);
        }

        public void SetTutorialCompleted(bool completed)
        {
            TutorialCompleted = completed;
            Text? label = TutorialButton.GetComponentInChildren<Text>(true);
            if (label == null)
                throw new InvalidOperationException("Title tutorial button requires a Text label.");
            label.text = completed ? "How to play" : "Tutorial";
        }

        private void HandleTutorial() => TutorialRequested?.Invoke();
        private void HandlePlay() => PlayRequested?.Invoke();
        private void HandleSessions() => SessionsRequested?.Invoke();
        private void HandleSettings() => SettingsRequested?.Invoke();
        private void HandleQuit() => QuitRequested?.Invoke();
    }
}
