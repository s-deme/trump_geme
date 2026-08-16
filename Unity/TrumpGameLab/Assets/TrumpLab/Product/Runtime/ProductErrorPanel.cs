#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TrumpLab.Product
{
    public sealed class ProductErrorPanel : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private Text? messageLabel;
        [SerializeField] private Button? dismissButton;

        private readonly List<BackgroundControlState> backgroundControls =
            new List<BackgroundControlState>();
        private GameObject? previousSelection;
        private bool backgroundControlsLocked;

        public Text MessageLabel => messageLabel ?? throw new InvalidOperationException(
            "Error message label is not configured.");
        public event System.Action? Dismissed;
        public event System.Action? Shown;

        public void Configure(Text message, Button dismiss)
        {
            messageLabel = message;
            dismissButton = dismiss;
            DisableDismissNavigation();
        }

        private void Awake()
        {
            if (messageLabel == null || dismissButton == null)
                throw new InvalidOperationException("Error panel controls are not configured.");
            DisableDismissNavigation();
            dismissButton.onClick.AddListener(HandleDismiss);
        }

        private void OnDestroy()
        {
            if (dismissButton != null) dismissButton.onClick.RemoveListener(HandleDismiss);
            RestoreBackgroundControls();
            previousSelection = null;
        }

        public void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Error message cannot be empty.", nameof(message));
            MessageLabel.text = message;
            bool opening = !gameObject.activeSelf;
            if (opening) previousSelection = EventSystem.current?.currentSelectedGameObject;
            gameObject.SetActive(true);
            if (opening) LockBackgroundControls();
            FocusDismissButton();
            Shown?.Invoke();
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            else CloseModal();
        }

        private void LateUpdate()
        {
            EventSystem? eventSystem = EventSystem.current;
            GameObject? selected = eventSystem?.currentSelectedGameObject;
            if (selected == null ||
                (selected.transform != transform && !selected.transform.IsChildOf(transform)))
                FocusDismissButton();
        }

        private void OnDisable() => CloseModal();

        public void OnCancel(BaseEventData eventData)
        {
            if (eventData == null) throw new ArgumentNullException(nameof(eventData));
            if (gameObject.activeSelf) HandleDismiss();
        }

        private void HandleDismiss() => Dismissed?.Invoke();

        private void LockBackgroundControls()
        {
            if (backgroundControlsLocked) return;
            backgroundControlsLocked = true;
            foreach (Selectable control in Resources.FindObjectsOfTypeAll<Selectable>())
            {
                if (control == null || !control.gameObject.activeInHierarchy ||
                    control.gameObject.scene != gameObject.scene ||
                    control.transform == transform || control.transform.IsChildOf(transform))
                    continue;
                backgroundControls.Add(new BackgroundControlState(
                    control, control.interactable));
                control.interactable = false;
            }
        }

        private void CloseModal()
        {
            RestoreBackgroundControls();
            RestorePreviousSelection();
        }

        private void RestoreBackgroundControls()
        {
            foreach (BackgroundControlState state in backgroundControls)
            {
                if (state.Control != null) state.Control.interactable = state.WasInteractable;
            }
            backgroundControls.Clear();
            backgroundControlsLocked = false;
        }

        private void RestorePreviousSelection()
        {
            EventSystem? eventSystem = EventSystem.current;
            GameObject? selected = previousSelection;
            previousSelection = null;
            if (eventSystem == null || selected == null || !selected.activeInHierarchy) return;
            Selectable? selectable = selected.GetComponent<Selectable>();
            if (selectable != null && (!selectable.IsActive() || !selectable.IsInteractable()))
                return;
            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(selected);
        }

        private void FocusDismissButton()
        {
            EventSystem? eventSystem = EventSystem.current;
            if (eventSystem == null || dismissButton == null ||
                !dismissButton.IsActive() || !dismissButton.IsInteractable()) return;
            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(dismissButton.gameObject);
        }

        private void DisableDismissNavigation()
        {
            if (dismissButton == null) return;
            Navigation navigation = dismissButton.navigation;
            navigation.mode = Navigation.Mode.None;
            dismissButton.navigation = navigation;
        }

        private readonly struct BackgroundControlState
        {
            public BackgroundControlState(Selectable control, bool wasInteractable)
            {
                Control = control;
                WasInteractable = wasInteractable;
            }

            public Selectable Control { get; }
            public bool WasInteractable { get; }
        }
    }
}
