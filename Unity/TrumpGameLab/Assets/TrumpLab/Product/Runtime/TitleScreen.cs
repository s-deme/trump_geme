#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class TitleScreen : ProductScreen
    {
        [SerializeField] private Button? playButton;
        [SerializeField] private Button? quitButton;

        public override ScreenId Id => ScreenId.Title;
        public event System.Action? PlayRequested;
        public event System.Action? QuitRequested;

        public void Configure(Button play, Button quit)
        {
            playButton = play;
            quitButton = quit;
        }

        private void Awake()
        {
            if (playButton == null || quitButton == null)
                throw new InvalidOperationException("Title screen buttons are not configured.");
            playButton.onClick.AddListener(HandlePlay);
            quitButton.onClick.AddListener(HandleQuit);
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(HandlePlay);
            if (quitButton != null) quitButton.onClick.RemoveListener(HandleQuit);
        }

        private void HandlePlay() => PlayRequested?.Invoke();
        private void HandleQuit() => QuitRequested?.Invoke();
    }
}
