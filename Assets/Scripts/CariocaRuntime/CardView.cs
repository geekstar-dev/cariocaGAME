using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CariocaRuntime
{
    [DisallowMultipleComponent]
    public sealed class CardView : MonoBehaviour
    {
        [Header("UI refs (optional)")]
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private GameObject glow;

        private RectTransform _rt;
        private CanvasGroup _cg;
        private Vector2 _homePos;

        private Card _card;
        private Action<CardView> _onClick;

        private CardDragHandler _drag;

        public Card Card => _card;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _cg = GetComponent<CanvasGroup>();
            if (!_cg) _cg = gameObject.AddComponent<CanvasGroup>();

            _drag = GetComponent<CardDragHandler>();
            if (_drag == null) _drag = gameObject.AddComponent<CardDragHandler>();

            _drag.OnBegin += HandleBeginDrag;
            _drag.OnDragDelta += HandleDragDelta;
            _drag.OnEnd += HandleEndDrag;

            if (_rt != null) _homePos = _rt.anchoredPosition;

            var btn = GetComponent<Button>();
            if (btn == null) btn = gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => _onClick?.Invoke(this));
        }

        // Compatibilidad (2/3/4 parámetros)
        public void Bind(Card card, Action<CardView> onClick) => Bind(card, onClick, false, false);
        public void Bind(Card card, Action<CardView> onClick, bool selected) => Bind(card, onClick, selected, false);

        public void Bind(Card card, Action<CardView> onClick, bool selected, bool hint)
        {
            _card = card;
            _onClick = onClick;

            if (label != null)
                label.text = card.ToString();

            SetSelected(selected);
            SetHint(hint);

            if (_rt != null) _homePos = _rt.anchoredPosition;
        }

        public void SetSelected(bool selected)
        {
            if (_rt != null)
                _rt.localScale = selected ? Vector3.one * 1.08f : Vector3.one;
        }

        public void SetHint(bool on)
        {
            if (glow != null) glow.SetActive(on);
        }

        private void HandleBeginDrag()
        {
            if (_cg != null) _cg.blocksRaycasts = false;
        }

        private void HandleDragDelta(Vector2 delta)
        {
            if (_rt != null) _rt.anchoredPosition += delta;
        }

        private void HandleEndDrag(PointerEventData eventData)
        {
            if (_cg != null) _cg.blocksRaycasts = true;

            var zones = RaycastDropZones(eventData);
            if (zones.Count == 0)
            {
                SnapHome();
                return;
            }

            var dz = zones.Find(z => z.zoneType == DropZone.ZoneType.Discard) ?? zones[0];
            CariocaDragBus.LastDrop = new CariocaDragBus.DropInfo { view = this, zone = dz };
            SnapHome();
        }

        private void SnapHome()
        {
            if (_rt != null) _rt.anchoredPosition = _homePos;
        }

        private static List<DropZone> RaycastDropZones(PointerEventData eventData)
        {
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            var zones = new List<DropZone>();
            foreach (var r in results)
            {
                var dz = r.gameObject.GetComponentInParent<DropZone>();
                if (dz != null && !zones.Contains(dz))
                    zones.Add(dz);
            }
            return zones;
        }
    }
}