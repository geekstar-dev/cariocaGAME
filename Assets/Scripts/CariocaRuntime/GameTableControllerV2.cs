using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace CariocaRuntime
{
    public sealed class GameTableControllerV2 : MonoBehaviour
    {
        [Header("UI (auto) — piles/hand")]
        [SerializeField] private Button deckButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private Transform handLayout;
        [SerializeField] private GameObject cardPrefab;

        [Header("UI (auto) — bank/actions")]
        [SerializeField] private Transform bankLayout;
        [SerializeField] private Button dropButton;
        [SerializeField] private Button addButton;
        [SerializeField] private Button sortButton;

        [Header("UI (optional) — labels")]
        [SerializeField] private TextMeshProUGUI roundLabel;

        [Header("Game")]
        [SerializeField] private RoundType currentRound = RoundType.TwoTrios;

        private readonly List<Card> _hand = new();
        private readonly List<Card> _discard = new();
        private readonly List<BankGroup> _bank = new();

        private readonly HashSet<Card> _selected = new();

        private Deck _deck;
        private bool _hasDrawnThisTurn;

        private void Start()
        {
            if (!AllRefsOk())
            {
                Debug.LogError("GameTableControllerV2: referencias UI incompletas.");
                enabled = false;
                return;
            }

            deckButton.onClick.RemoveAllListeners();
            discardButton.onClick.RemoveAllListeners();
            deckButton.onClick.AddListener(DrawFromDeck);
            discardButton.onClick.AddListener(DrawFromDiscard);

            dropButton.onClick.RemoveAllListeners();
            addButton.onClick.RemoveAllListeners();
            sortButton.onClick.RemoveAllListeners();

            dropButton.onClick.AddListener(OnDropPressed);
            addButton.onClick.AddListener(DiscardSelectedOne);
            sortButton.onClick.AddListener(() => { SmartSortHand(); RefreshHandUI(true); });

            NewRound();
        }

        private void Update()
        {
            if (CariocaDragBus.LastDrop.HasValue)
            {
                var info = CariocaDragBus.LastDrop.Value;
                CariocaDragBus.Clear();

                if (info.view == null || info.zone == null) return;

                if (info.zone.zoneType == DropZone.ZoneType.Discard)
                {
                    SelectOnly(info.view.Card);
                    DiscardSelectedOne();
                }
                else
                {
                    if (_selected.Count == 0) SelectOnly(info.view.Card);

                    if (CanDropSelectionNow()) OnDropPressed();
                    else if (_selected.Count == 1) TryAddSelectedOneToExistingBank();
                }
            }
        }

        private bool AllRefsOk()
        {
            return deckButton && discardButton && handLayout && cardPrefab && bankLayout && dropButton && addButton && sortButton;
        }

        private void NewRound()
        {
            _deck = new Deck();
            _deck.Build54With2Jokers();
            _deck.Shuffle();

            _hand.Clear();
            _discard.Clear();
            _bank.Clear();
            _selected.Clear();

            ClearChildren(handLayout);
            ClearChildren(bankLayout);

            for (int i = 0; i < 12; i++) _hand.Add(_deck.Draw());

            Card first = _deck.Draw();
            int safety = 0;
            var temp = new List<Card>();
            while (first.IsJoker && safety < 200)
            {
                temp.Add(first);
                first = _deck.Draw();
                safety++;
            }
            if (temp.Count > 0) { _deck.AddRange(temp); _deck.Shuffle(); }

            _discard.Add(first);
            _hasDrawnThisTurn = false;

            if (roundLabel) roundLabel.text = "Ronda " + (int)currentRound;

            RefreshDiscardLabel();
            RefreshBankUI();
            RefreshHandUI(false);
            UpdateDropButtonVisibility();
        }

        private void DrawFromDeck()
        {
            if (_hasDrawnThisTurn) return;
            EnsureDeckHasCards();
            _hand.Add(_deck.Draw());
            _hasDrawnThisTurn = true;
            RefreshHandUI(true);
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
            RefreshHandUI(true);
        }

        private void OnCardClicked(CardView view)
        {
            var c = view.Card;
            if (_selected.Contains(c)) _selected.Remove(c);
            else _selected.Add(c);
            view.SetSelected(_selected.Contains(c));
            UpdateDropButtonVisibility();
        }

        private void OnDropPressed()
        {
            if (_selected.Count == 0) return;
            var selectedCards = _selected.ToList();

            string reason;
            if (RulesValidator.TryAsTrio(selectedCards, out reason))
            {
                _bank.Add(new BankGroup(BankGroupType.Trio, selectedCards));
                RemoveSelectedFromHand();
            }
            else
            {
                List<int> resolved;
                if (RulesValidator.TryAsRun(selectedCards, out reason, out resolved))
                {
                    _bank.Add(new BankGroup(BankGroupType.Run, selectedCards));
                    RemoveSelectedFromHand();
                }
                else
                {
                    Debug.LogWarning("❌ No válido para bajar: " + reason);
                    return;
                }
            }

            _selected.Clear();
            RefreshBankUI();
            RefreshHandUI(true);
            UpdateDropButtonVisibility();
        }

        private bool CanDropSelectionNow()
        {
            if (_selected.Count == 0) return false;
            var selectedCards = _selected.ToList();
            string reason;
            List<int> resolved;
            return RulesValidator.TryAsTrio(selectedCards, out reason) ||
                   RulesValidator.TryAsRun(selectedCards, out reason, out resolved);
        }

        private void UpdateDropButtonVisibility()
        {
            if (!dropButton) return;
            dropButton.gameObject.SetActive(CanDropSelectionNow());
        }

        private void DiscardSelectedOne()
        {
            if (!_hasDrawnThisTurn)
            {
                Debug.LogWarning("Primero roba (MAZO o DESCARTE). Luego descarta 1.");
                return;
            }

            if (_selected.Count != 1)
            {
                Debug.LogWarning("Selecciona EXACTAMENTE 1 carta para DESCARTAR.");
                return;
            }

            var card = _selected.First();
            if (!_hand.Contains(card))
            {
                _selected.Clear();
                RefreshHandUI(true);
                return;
            }

            _hand.Remove(card);
            _discard.Add(card);

            _selected.Clear();
            _hasDrawnThisTurn = false;

            RefreshDiscardLabel();
            RefreshHandUI(true);
            UpdateDropButtonVisibility();
        }

        private void TryAddSelectedOneToExistingBank()
        {
            if (_selected.Count != 1) return;
            if (_bank.Count == 0) return;

            var card = _selected.First();
            if (!_hand.Contains(card)) return;

            for (int g = 0; g < _bank.Count; g++)
            {
                var group = _bank[g];
                string reason;

                bool ok = group.Type == BankGroupType.Trio
                    ? RulesValidator.TryAddToTrio(group, card, out reason)
                    : RulesValidator.TryAddToRun(group, card, out reason);

                if (ok)
                {
                    group.Cards.Add(card);
                    _hand.Remove(card);
                    _selected.Clear();
                    RefreshBankUI();
                    RefreshHandUI(true);
                    return;
                }
            }
        }

        private void SelectOnly(Card card)
        {
            _selected.Clear();
            _selected.Add(card);
        }

        private void RemoveSelectedFromHand()
        {
            foreach (var c in _selected)
                _hand.Remove(c);
        }

        private void SmartSortHand()
        {
            // Simple MVP: sort for triads
            _hand.Sort((a, b) =>
            {
                if (a.IsJoker && !b.IsJoker) return 1;
                if (!a.IsJoker && b.IsJoker) return -1;
                if (a.IsJoker && b.IsJoker) return 0;

                int r = ((int)a.Rank).CompareTo((int)b.Rank);
                if (r != 0) return r;

                return a.Suit.Value.CompareTo(b.Suit.Value);
            });

            _selected.Clear();
        }

        private void EnsureDeckHasCards()
        {
            if (_deck.Count > 0) return;
            if (_discard.Count <= 1) return;

            var top = _discard[^1];
            var recycle = _discard.GetRange(0, _discard.Count - 1);

            _discard.Clear();
            _discard.Add(top);

            _deck.AddRange(recycle);
            _deck.Shuffle();
        }

        private void RefreshHandUI(bool animate)
        {
            ClearChildren(handLayout);

            for (int i = 0; i < _hand.Count; i++)
            {
                var c = _hand[i];
                var go = Object.Instantiate(cardPrefab, handLayout);
                var view = go.GetComponent<CardView>();

                bool selected = _selected.Contains(c);
                view.Bind(c, OnCardClicked, selected, false);

                if (animate)
                {
                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        var start = rt.anchoredPosition + new Vector2(0, -70);
                        rt.anchoredPosition = start;
                        rt.DOAnchorPosY(start.y + 70, 0.16f);
                    }
                }
            }
        }

        private void RefreshBankUI()
        {
            ClearChildren(bankLayout);
            // Mantén tu UI de banca actual si ya la tienes, aquí está mínimo.
        }

        private void RefreshDiscardLabel()
        {
            var tmp = discardButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp == null) return;

            if (_discard.Count == 0) tmp.text = "DESCARTE";
            else
            {
                var top = _discard[^1];
                tmp.text = top.IsJoker ? "DESCARTE\n(JOKER)" : "DESCARTE\n(" + top.ToString() + ")";
            }
        }

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}