using System;
using System.Collections.Generic;

namespace CariocaRuntime
{
    public sealed class Deck
    {
        private readonly List<Card> _cards = new();
        private readonly Random _rng = new();

        public int Count => _cards.Count;

        // ✅ Necesario para que Activator.CreateInstance no falle
        public Deck() { }

        public void Build54With2Jokers()
        {
            _cards.Clear();

            // Si tu enum Suit tiene solo 4 palos (sin None), esto funciona perfecto.
            // Si tuviera otros valores, filtramos para quedarnos con los 4 típicos.
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                var name = suit.ToString().ToLower();
                bool looksLikeRealSuit =
                    name.Contains("club") || name.Contains("diamond") ||
                    name.Contains("heart") || name.Contains("spade");

                // Si detectamos nombres típicos, filtramos.
                // Si tu enum se llama distinto, se dejará pasar igual.
                if (!looksLikeRealSuit && Enum.GetValues(typeof(Suit)).Length > 4)
                    continue;

                for (int r = 1; r <= 13; r++)
                {
                    var rank = (Rank)r;
                    _cards.Add(new Card((Suit?)suit, rank));
                }
            }

            // Jokers: suit = null
            _cards.Add(new Card(null, Rank.Joker));
            _cards.Add(new Card(null, Rank.Joker));
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