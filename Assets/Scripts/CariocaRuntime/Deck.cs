using System;
using System.Collections.Generic;

namespace CariocaRuntime
{
    public sealed class Deck
    {
        private readonly List<Card> _cards = new();
        private readonly System.Random _rng;

        public int Count => _cards.Count;

        public Deck(int seed = 0)
        {
            _rng = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public void Build54With2Jokers()
        {
            _cards.Clear();

            foreach (Suit s in Enum.GetValues(typeof(Suit)))
            {
                _cards.Add(new Card(s, Rank.Ace));
                _cards.Add(new Card(s, Rank.Two));
                _cards.Add(new Card(s, Rank.Three));
                _cards.Add(new Card(s, Rank.Four));
                _cards.Add(new Card(s, Rank.Five));
                _cards.Add(new Card(s, Rank.Six));
                _cards.Add(new Card(s, Rank.Seven));
                _cards.Add(new Card(s, Rank.Eight));
                _cards.Add(new Card(s, Rank.Nine));
                _cards.Add(new Card(s, Rank.Ten));
                _cards.Add(new Card(s, Rank.Jack));
                _cards.Add(new Card(s, Rank.Queen));
                _cards.Add(new Card(s, Rank.King));
            }

            _cards.Add(new Card(null, Rank.Joker));
            _cards.Add(new Card(null, Rank.Joker));
        }

        public void Shuffle() => ShuffleUtils.FisherYates(_cards, _rng);

        public Card Draw()
        {
            if (_cards.Count == 0) throw new InvalidOperationException("Deck vacío");
            var c = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return c;
        }

        public void AddRange(IEnumerable<Card> cards) => _cards.AddRange(cards);
    }
}
