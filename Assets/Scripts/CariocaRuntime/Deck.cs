using System;
using System.Collections.Generic;

namespace CariocaRuntime
{
    public sealed class Deck
    {
        private readonly List<Card> _cards = new();
        private readonly Random _rng = new();

        public int Count => _cards.Count;

        // ✅ Constructor vacío (MUY IMPORTANTE)
        public Deck() { }

        public void Build54With2Jokers()
        {
            _cards.Clear();

            // 4 palos x 13 cartas
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                if (suit == Suit.None) continue;

                for (int r = 1; r <= 13; r++)
                {
                    var rank = (Rank)r;
                    _cards.Add(new Card(rank, suit));
                }
            }

            // 2 Jokers
            _cards.Add(new Card(Rank.Joker, Suit.None));
            _cards.Add(new Card(Rank.Joker, Suit.None));
        }

        // Fisher-Yates
        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        public Card Draw()
        {
            if (_cards.Count == 0)
                throw new InvalidOperationException("Deck vacío");

            var top = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return top;
        }

        public void AddRange(IEnumerable<Card> cards)
        {
            _cards.AddRange(cards);
        }
    }
}
