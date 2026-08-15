#nullable enable

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class ProductErrorPanel : MonoBehaviour
    {
        [SerializeField] private Text? messageLabel;
        [SerializeField] private Button? dismissButton;

        public Text MessageLabel => messageLabel ?? throw new InvalidOperationException(
            "Error message label is not configured.");
        public event System.Action? Dismissed;

        public void Configure(Text message, Button dismiss)
        {
            messageLabel = message;
            dismissButton = dismiss;
        }

        private void Awake()
        {
            if (messageLabel == null || dismissButton == null)
                throw new InvalidOperationException("Error panel controls are not configured.");
            dismissButton.onClick.AddListener(HandleDismiss);
        }

        private void OnDestroy()
        {
            if (dismissButton != null) dismissButton.onClick.RemoveListener(HandleDismiss);
        }

        public void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Error message cannot be empty.", nameof(message));
            MessageLabel.text = message;
            gameObject.SetActive(true);
            if (EventSystem.current != null && dismissButton != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(dismissButton.gameObject);
            }
        }

        public void Hide() => gameObject.SetActive(false);

        private void HandleDismiss() => Dismissed?.Invoke();
    }
}
