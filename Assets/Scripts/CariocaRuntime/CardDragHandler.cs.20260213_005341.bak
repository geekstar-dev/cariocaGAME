using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CariocaRuntime
{
    public sealed class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action OnBegin;
        public Action<Vector2> OnDragDelta;
        public Action<PointerEventData> OnEnd;

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData) => OnBegin?.Invoke();

        public void OnDrag(PointerEventData eventData)
        {
            var scale = _canvas ? _canvas.scaleFactor : 1f;
            var delta = eventData.delta / Mathf.Max(0.0001f, scale);
            OnDragDelta?.Invoke(delta);
        }

        public void OnEndDrag(PointerEventData eventData) => OnEnd?.Invoke(eventData);
    }
}