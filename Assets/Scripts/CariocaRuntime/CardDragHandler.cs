using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CariocaRuntime
{
    [DisallowMultipleComponent]
    public sealed class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public event Action OnBegin;
        public event Action<Vector2> OnDragDelta;
        public event Action<PointerEventData> OnEnd;

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            OnBegin?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            float scale = _canvas ? _canvas.scaleFactor : 1f;
            var delta = eventData.delta / Mathf.Max(0.0001f, scale);
            OnDragDelta?.Invoke(delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OnEnd?.Invoke(eventData);
        }
    }
}