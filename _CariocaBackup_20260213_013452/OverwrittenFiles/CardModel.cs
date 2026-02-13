using System;

namespace CariocaRuntime
{
    public enum Suit { Clubs, Diamonds, Hearts, Spades }
    public enum Rank
    {
        Joker = 0,
        Ace = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
        Jack, Queen, King
    }

    public readonly struct Card
    {
        public readonly Suit? Suit;   // null si Joker
        public readonly Rank Rank;

        public bool IsJoker => Rank == Rank.Joker;

        public Card(Suit? suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        public override string ToString()
        {
            if (IsJoker) return "JOKER";

            string rankStr = Rank switch
            {
                Rank.Ace => "A",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                _ => ((int)Rank).ToString()
            };

            string suitStr = Suit switch
            {
                CariocaRuntime.Suit.Clubs => "♣",
                CariocaRuntime.Suit.Diamonds => "♦",
                CariocaRuntime.Suit.Hearts => "♥",
                CariocaRuntime.Suit.Spades => "♠",
                _ => ""
            };

            return $"{rankStr}{suitStr}";
        }
    }
}
