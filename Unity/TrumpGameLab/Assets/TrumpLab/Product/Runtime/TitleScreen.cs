#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class TitleScreen : ProductScreen
    {
        [SerializeField] private Button? playButton;
        [SerializeField] private Button? sessionsButton;
        [SerializeField] private Button? quitButton;

        public override ScreenId Id => ScreenId.Title;
        public event System.Action? PlayRequested;
        public event System.Action? SessionsRequested;
        public event System.Action? QuitRequested;

        public void Configure(Button play, Button sessions, Button quit)
        {
            playButton = play;
            sessionsButton = sessions;
            quitButton = quit;
        }

        private void Awake()
        {
            if (playButton == null || sessionsButton == null || quitButton == null)
                throw new InvalidOperationException("Title screen buttons are not configured.");
            playButton.onClick.AddListener(HandlePlay);
            sessionsButton.onClick.AddListener(HandleSessions);
            quitButton.onClick.AddListener(HandleQuit);
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(HandlePlay);
            if (sessionsButton != null) sessionsButton.onClick.RemoveListener(HandleSessions);
            if (quitButton != null) quitButton.onClick.RemoveListener(HandleQuit);
        }

        private void HandlePlay() => PlayRequested?.Invoke();
        private void HandleSessions() => SessionsRequested?.Invoke();
        private void HandleQuit() => QuitRequested?.Invoke();
    }
}
