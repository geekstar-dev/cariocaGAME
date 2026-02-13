using System;
using System.Collections.Generic;

namespace CariocaRuntime
{
    // Deck estable para tu modelo actual:
    // Card(Suit? suit, Rank rank)
    // Joker => suit = null, rank = Rank.Joker
    public sealed class Deck
    {
        private readonly List<Card> _cards = new();
        private readonly Random _rng = new();

        public int Count => _cards.Count;

        public Deck() { }

        public void Build54With2Jokers()
        {
            _cards.Clear();

            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                for (int r = 1; r <= 13; r++)
                {
                    var rank = (Rank)r;
                    _cards.Add(new Card((Suit?)suit, rank));
                }
            }

            _cards.Add(new Card((Suit?)null, Rank.Joker));
            _cards.Add(new Card((Suit?)null, Rank.Joker));
        }

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
            if (_cards.Count == 0) throw new InvalidOperationException("Deck vacío");

            var top = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return top;
        }

        public void AddRange(IEnumerable<Card> cards) => _cards.AddRange(cards);
    }
}