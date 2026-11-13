using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public static class ItchDeployUI
    {
        public enum State { Ok, Warn, Err, Muted }

        public static Label CardTitle(string text, string icon = null)
        {
            var label = new Label(string.IsNullOrEmpty(icon) ? text : icon + "  " + text);
            label.AddToClassList("card-title");
            return label;
        }

        public static Button SecondaryButton(string text, Action onClick) =>
            StyledButton(text, onClick, "btn");

        private static Button StyledButton(
            string text,
            Action onClick,
            params string[] classes)
        {
            var button = new Button(onClick) { text = text };
            foreach (var className in classes)
                button.AddToClassList(className);
            return button;
        }

        public static string StateToClass(State state)
        {
            switch (state)
            {
                case State.Ok: return "ok";
                case State.Warn: return "warn";
                case State.Err: return "err";
                default: return "muted";
            }
        }

        public static Color StateToColor(State state)
        {
            switch (state)
            {
                case State.Ok: return new Color(0.24f, 0.86f, 0.52f);
                case State.Warn: return new Color(1f, 0.71f, 0.33f);
                case State.Err: return new Color(1f, 0.36f, 0.36f);
                default: return new Color(0.6f, 0.63f, 0.66f);
            }
        }

        public static TextField TextField(
            string label,
            string value,
            Action<string> onChanged,
            bool password = false)
        {
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isPasswordField = password,
            };
            field.labelElement.style.width = 150;
            field.labelElement.style.minWidth = 150;
            field.RegisterValueChangedCallback(ev => onChanged?.Invoke(ev.newValue));
            return field;
        }

        public static VisualElement LabeledToggleRow(
            string text,
            bool value,
            out Toggle toggle,
            string toggleName = null)
        {
            var row = new VisualElement();
            row.AddToClassList("settings-toggle-row");

            var label = new Label(text);
            label.AddToClassList("settings-toggle-label");
            row.Add(label);

            var control = new Toggle
            {
                name = toggleName,
                value = value,
            };
            control.AddToClassList("settings-compact-toggle");
            row.Add(control);
            toggle = control;

            label.RegisterCallback<ClickEvent>(evt =>
            {
                control.value = !control.value;
                control.Focus();
                evt.StopPropagation();
            });

            return row;
        }
    }

    public sealed class ItchDeployChoiceField : VisualElement
    {
        private readonly Label _label;
        private readonly VisualElement _body;
        private readonly Button _input;
        private readonly Label _valueLabel;
        private readonly Label _caret;
        private readonly ScrollView _menu;
        private readonly List<string> _choices = new();
        private VisualElement _panelRoot;
        private string _placeholder = string.Empty;
        private int _index = -1;
        private bool _isOpen;

        public event Action<int, string> SelectionChanged;

        public Label labelElement => _label;
        public Button inputElement => _input;
        public ScrollView menuElement => _menu;
        public IReadOnlyList<string> choices => _choices;
        public bool isOpen => _isOpen;

        public string placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value ?? string.Empty;
                RefreshValue();
            }
        }

        public int index
        {
            get => _index;
            set => SetIndex(value, true);
        }

        public string value
        {
            get => _index >= 0 && _index < _choices.Count
                ? _choices[_index]
                : string.Empty;
            set
            {
                var nextIndex = _choices.IndexOf(value ?? string.Empty);
                if (nextIndex >= 0)
                    SetIndex(nextIndex, true);
            }
        }

        public ItchDeployChoiceField(
            string label,
            IEnumerable<string> choices,
            int selectedIndex = 0)
        {
            AddToClassList("choice-field");

            _label = new Label(label ?? string.Empty)
            {
                pickingMode = PickingMode.Ignore
            };
            _label.AddToClassList("choice-field-label");
            _label.style.display = string.IsNullOrWhiteSpace(label)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            Add(_label);

            _body = new VisualElement();
            _body.AddToClassList("choice-field-body");
            Add(_body);

            _input = new Button(ToggleMenu)
            {
                name = "choice-field-input"
            };
            _input.AddToClassList("choice-field-input");
            _valueLabel = new Label();
            _valueLabel.AddToClassList("choice-field-value");
            _valueLabel.pickingMode = PickingMode.Ignore;
            _input.Add(_valueLabel);

            _caret = new Label("v")
            {
                pickingMode = PickingMode.Ignore
            };
            _caret.AddToClassList("choice-field-caret");
            _input.Add(_caret);
            _body.Add(_input);

            _menu = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "choice-field-menu"
            };
            _menu.AddToClassList("choice-field-menu");
            _menu.style.display = DisplayStyle.None;
            _body.Add(_menu);

            _input.RegisterCallback<KeyDownEvent>(OnInputKeyDown);
            RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);

            SetChoices(choices, selectedIndex);
        }

        public void SetChoices(
            IEnumerable<string> choices,
            int selectedIndex = 0)
        {
            var incomingChoices = choices?
                .Where(choice => choice != null)
                .ToList() ?? new List<string>();
            _choices.Clear();
            _choices.AddRange(incomingChoices);

            _index = _choices.Count == 0 || selectedIndex < 0
                ? -1
                : Mathf.Clamp(selectedIndex, 0, _choices.Count - 1);
            RebuildOptions();
            RefreshValue();
            CloseMenu();
        }

        public void SetIndexWithoutNotify(int selectedIndex) =>
            SetIndex(selectedIndex, false);

        public void SetValueWithoutNotify(string selectedValue)
        {
            var selectedIndex = _choices.IndexOf(selectedValue ?? string.Empty);
            if (selectedIndex >= 0)
                SetIndex(selectedIndex, false);
        }

        internal void OpenMenu()
        {
            if (_isOpen || !enabledInHierarchy || _choices.Count == 0)
                return;

            _isOpen = true;
            AddToClassList("open");
            _input.AddToClassList("open");
            _menu.style.display = DisplayStyle.Flex;
            _caret.text = "^";
            schedule.Execute(ScrollSelectedOptionIntoView).StartingIn(1);
        }

        internal void CloseMenu()
        {
            if (!_isOpen && _menu.style.display.value == DisplayStyle.None)
                return;

            _isOpen = false;
            RemoveFromClassList("open");
            _input.RemoveFromClassList("open");
            _menu.style.display = DisplayStyle.None;
            _caret.text = "v";
        }

        private void ToggleMenu()
        {
            if (_isOpen)
                CloseMenu();
            else
                OpenMenu();
        }

        private void SetIndex(int selectedIndex, bool notify)
        {
            if (_choices.Count == 0)
            {
                _index = -1;
                RefreshValue();
                return;
            }

            var nextIndex = selectedIndex < 0
                ? -1
                : Mathf.Clamp(selectedIndex, 0, _choices.Count - 1);
            var changed = nextIndex != _index;
            _index = nextIndex;
            RefreshValue();
            RefreshOptionStates();

            if (notify && changed)
                SelectionChanged?.Invoke(_index, value);
        }

        private void RebuildOptions()
        {
            _menu.Clear();
            for (var optionIndex = 0; optionIndex < _choices.Count; optionIndex++)
            {
                var capturedIndex = optionIndex;
                var option = new Button(() =>
                {
                    SetIndex(capturedIndex, true);
                    CloseMenu();
                    _input.Focus();
                })
                {
                    text = _choices[optionIndex]
                };
                option.AddToClassList("choice-field-option");
                option.EnableInClassList(
                    "last",
                    optionIndex == _choices.Count - 1);
                option.EnableInClassList("selected", optionIndex == _index);
                _menu.Add(option);
            }
        }

        private void RefreshValue()
        {
            _valueLabel.text = _index >= 0 ? value : _placeholder;
            _input.EnableInClassList("empty", _index < 0);
        }

        private void OnOpenMenuWheel(WheelEvent evt)
        {
            if (!_isOpen)
                return;

            const float wheelStep = 18f;
            _menu.verticalScroller.value += evt.delta.y * wheelStep;

            evt.StopPropagation();
        }

        private void RefreshOptionStates()
        {
            var options = _menu.Query<Button>(className: "choice-field-option")
                .ToList();
            for (var optionIndex = 0; optionIndex < options.Count; optionIndex++)
                options[optionIndex].EnableInClassList(
                    "selected",
                    optionIndex == _index);
        }

        private void ScrollSelectedOptionIntoView()
        {
            if (!_isOpen || _index < 0)
                return;

            var options = _menu.Query<Button>(className: "choice-field-option")
                .ToList();
            if (_index < options.Count)
                _menu.ScrollTo(options[_index]);
        }

        private void OnInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                CloseMenu();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.DownArrow)
            {
                SetIndex(Mathf.Min(_index + 1, _choices.Count - 1), true);
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.UpArrow)
            {
                SetIndex(Mathf.Max(_index - 1, 0), true);
                evt.StopPropagation();
            }
        }

        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            _panelRoot = evt.destinationPanel?.visualTree;
            _panelRoot?.RegisterCallback<PointerDownEvent>(
                OnPanelPointerDown,
                TrickleDown.TrickleDown);
            _panelRoot?.RegisterCallback<WheelEvent>(
                OnOpenMenuWheel,
                TrickleDown.TrickleDown);
        }

        private void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            _panelRoot?.UnregisterCallback<PointerDownEvent>(
                OnPanelPointerDown,
                TrickleDown.TrickleDown);
            _panelRoot?.UnregisterCallback<WheelEvent>(
                OnOpenMenuWheel,
                TrickleDown.TrickleDown);
            _panelRoot = null;
        }

        private void OnPanelPointerDown(PointerDownEvent evt)
        {
            if (!_isOpen || evt.target is not VisualElement target)
                return;

            if (!Contains(target))
                CloseMenu();
        }
    }
}
