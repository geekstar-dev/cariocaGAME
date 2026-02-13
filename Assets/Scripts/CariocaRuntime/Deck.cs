using System;
using System.Collections.Generic;

namespace CariocaRuntime
{
    // ✅ Deck estable: 52 + 2 jokers = 54
    public sealed class Deck
    {
        private readonly List<Card> _cards = new List<Card>();
        private readonly System.Random _rng = new System.Random();

        public int Count => _cards.Count;

        // ✅ Constructor vacío (necesario)
        public Deck() { }

        public void Build54With2Jokers()
        {
            _cards.Clear();

            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                for (int r = 1; r <= 13; r++)
                {
                    var rank = (Rank)r;
                    _cards.Add(new Card(suit, rank));
                }
            }

            _cards.Add(new Card(null, Rank.Joker));
            _cards.Add(new Card(null, Rank.Joker));
        }

        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                var tmp = _cards[i];
                _cards[i] = _cards[j];
                _cards[j] = tmp;
            }
        }

        public Card Draw()
        {
            if (_cards.Count == 0) throw new InvalidOperationException("Deck vacío");
            int last = _cards.Count - 1;
            var top = _cards[last];
            _cards.RemoveAt(last);
            return top;
        }

        public void AddRange(IEnumerable<Card> cards)
        {
            _cards.AddRange(cards);
        }
    }
}