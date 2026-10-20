using UnityEngine;
using UnityEngine.UIElements;

namespace Anachronism.UI
{
    // Hors Archi : vue de la barre d'items sortie de HudController pour l'alléger. Elle reçoit le restant de
    // FugitivePlacer.CooldownChanged, en déduit la durée totale et décompte entre deux events.
    public sealed class ItemBarView
    {
        public const int SlotCount = 3;

        private const string SelectedClass = "item-slot--selected";
        private const string CoolingClass = "item-slot--cooling";

        private static readonly string[] SlotNames = { "item-slot-small", "item-slot-medium", "item-slot-large" };

        private readonly Slot[] _slots = new Slot[SlotCount];
        private int _selectedIndex = -1;

        public ItemBarView(VisualElement root)
        {
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = new Slot(root.Q<VisualElement>(SlotNames[i]));
        }

        public void SetSelected(int index)
        {
            if (index == _selectedIndex)
                return;

            _selectedIndex = index;
            for (int i = 0; i < SlotCount; i++)
                _slots[i].SetSelected(i == index);
        }

        public void SetCooldown(int index, float remainingSeconds)
        {
            if (index >= 0 && index < SlotCount)
                _slots[index].SetCooldown(remainingSeconds);
        }

        public void Tick(float deltaTime)
        {
            foreach (Slot slot in _slots)
                slot.Tick(deltaTime);
        }

        public void ClearCooldowns()
        {
            foreach (Slot slot in _slots)
                slot.SetCooldown(0f);
        }

        private sealed class Slot
        {
            private readonly VisualElement _root;
            private readonly VisualElement _gaugeFill;
            private readonly Label _timer;

            private float _remaining;
            private float _total;
            private int _shownSeconds = -1;
            private float _shownProgress = -1f;

            private readonly bool _isValid;

            public Slot(VisualElement root)
            {
                _root = root;
                _gaugeFill = root?.Q<VisualElement>("item-gauge-fill");
                _timer = root?.Q<Label>("item-timer");
                _isValid = _root != null && _gaugeFill != null && _timer != null;

                if (!_isValid)
                    Debug.LogError($"[ItemBarView] Emplacement « {root?.name ?? "?"} » incomplet dans Hud.uxml : « item-gauge-fill » ou « item-timer » manquant.");
            }

            public void SetSelected(bool isSelected) => _root?.EnableInClassList(SelectedClass, isSelected);

            public void SetCooldown(float remainingSeconds)
            {
                remainingSeconds = Mathf.Max(0f, remainingSeconds);
                if (remainingSeconds > _remaining + 0.01f)
                    _total = remainingSeconds;

                _remaining = remainingSeconds;
                Refresh();
            }

            public void Tick(float deltaTime)
            {
                if (_remaining <= 0f)
                    return;

                _remaining = Mathf.Max(0f, _remaining - deltaTime);
                Refresh();
            }

            private void Refresh()
            {
                if (!_isValid)
                    return;

                float progress = _total > 0f ? 1f - Mathf.Clamp01(_remaining / _total) : 1f;
                if (Mathf.Abs(progress - _shownProgress) > 0.001f)
                {
                    _shownProgress = progress;
                    _gaugeFill.style.height = Length.Percent(progress * 100f);
                }

                int seconds = Mathf.CeilToInt(_remaining);
                if (seconds == _shownSeconds)
                    return;

                _shownSeconds = seconds;
                _timer.text = seconds > 0 ? seconds.ToString() : string.Empty;
                _root.EnableInClassList(CoolingClass, seconds > 0);
            }
        }
    }
}
