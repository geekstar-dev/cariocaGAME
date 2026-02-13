using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CariocaRuntime
{
    public sealed class GameTableController : MonoBehaviour
    {
        [Header("UI Refs (auto-set by installer)")]
        [SerializeField] private Button deckButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private Transform handLayout;
        [SerializeField] private GameObject cardPrefab;

        private readonly List<Card> _hand = new();
        private readonly List<Card> _discard = new();
        private Deck _deck;

        private bool _hasDrawnThisTurn;

        private void Start()
        {
            if (deckButton == null || discardButton == null || handLayout == null || cardPrefab == null)
            {
                Debug.LogError("GameTableController: faltan referencias UI. Vuelve a correr Tools → CARIOCA → Build GameTable Prototype.");
                enabled = false;
                return;
            }

            deckButton.onClick.RemoveAllListeners();
            discardButton.onClick.RemoveAllListeners();
            deckButton.onClick.AddListener(DrawFromDeck);
            discardButton.onClick.AddListener(DrawFromDiscard);

            NewRound();
        }

        private void NewRound()
        {
            _deck = new Deck();
            _deck.Build54With2Jokers();
            _deck.Shuffle();

            _hand.Clear();
            _discard.Clear();
            ClearHandUI();

            // Deal 12
            for (int i = 0; i < 12; i++)
                _hand.Add(_deck.Draw());

            // First discard cannot be Joker
            Card first = _deck.Draw();
            int safety = 0;
            while (first.IsJoker && safety < 200)
            {
                // put joker back into deck bottom and reshuffle lightly
                _discard.Add(first); // temporarily store, but we will recycle all except top later
                first = _deck.Draw();
                safety++;
            }

            // We don't want Joker on top. If we collected jokers in discard temp, move them back & shuffle.
            if (_discard.Count > 0)
            {
                var temp = new List<Card>(_discard);
                _discard.Clear();

                _deck.AddRange(temp);
                _deck.Shuffle();
            }

            _discard.Add(first);
            _hasDrawnThisTurn = false;

            RefreshDiscardLabel();
            RefreshHandUI();
        }

        private void DrawFromDeck()
        {
            if (_hasDrawnThisTurn) return;

            EnsureDeckHasCards();
            _hand.Add(_deck.Draw());
            _hasDrawnThisTurn = true;

            RefreshHandUI();
        }

        private void DrawFromDiscard()
        {
            if (_hasDrawnThisTurn) return;
            if (_discard.Count == 0) return;

            var top = _discard[^1];
            _discard.RemoveAt(_discard.Count - 1);
            _hand.Add(top);

            _hasDrawnThisTurn = true;

            RefreshDiscardLabel();
            RefreshHandUI();
        }

        private void OnCardClicked(CardView view)
        {
            if (!_hasDrawnThisTurn) return; // rule: must draw before discard

            // remove from hand
            int idx = _hand.FindIndex(c => CardsEqual(c, view.Card));
            if (idx >= 0) _hand.RemoveAt(idx);

            // discard it
            _discard.Add(view.Card);
            _hasDrawnThisTurn = false;

            RefreshDiscardLabel();
            RefreshHandUI();

            // win check
            if (_hand.Count == 0)
                Debug.Log("🏁 Ganaste la ronda (mano vacía). Luego agregamos puntaje.");
        }

        private static bool CardsEqual(Card a, Card b)
        {
            // Struct compare manual (Suit? + Rank)
            return a.Rank == b.Rank && a.Suit == b.Suit;
        }

        private void EnsureDeckHasCards()
        {
            if (_deck.Count > 0) return;

            if (_discard.Count <= 1)
            {
                Debug.LogWarning("No hay cartas para reciclar.");
                return;
            }

            // Keep top discard, shuffle rest back into deck
            var top = _discard[^1];
            var recycle = _discard.GetRange(0, _discard.Count - 1);

            _discard.Clear();
            _discard.Add(top);

            _deck.AddRange(recycle);
            _deck.Shuffle();

            Debug.Log("♻️ Mazo vacío: se revuelve descarte y se reutiliza como mazo (dejando 1 arriba).");
        }

        private void ClearHandUI()
        {
            for (int i = handLayout.childCount - 1; i >= 0; i--)
                Destroy(handLayout.GetChild(i).gameObject);
        }

        private void RefreshHandUI()
        {
            ClearHandUI();

            foreach (var c in _hand)
            {
                var go = Instantiate(cardPrefab, handLayout);
                var view = go.GetComponent<CardView>();
                view.Bind(c, OnCardClicked, false, false);

            }
        }

        private void RefreshDiscardLabel()
        {
            // Change text on discard button child TMP (if exists)
            var tmp = discardButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp == null) return;

            if (_discard.Count == 0) tmp.text = "DESCARTE";
            else
            {
                var top = _discard[^1];
                tmp.text = top.IsJoker ? "DESCARTE\\n(JOKER)" : "DESCARTE\\n("+top.ToString()+ ")";
            }
        }
    }
}
