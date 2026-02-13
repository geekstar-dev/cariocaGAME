using System.Collections.Generic;

namespace CariocaRuntime
{
    public enum BankGroupType { Trio, Run }

    public sealed class BankGroup
    {
        public BankGroupType Type;
        public readonly List<Card> Cards = new();

        public BankGroup(BankGroupType type, IEnumerable<Card> cards)
        {
            Type = type;
            Cards.AddRange(cards);
        }

        public int JokerCount()
        {
            int n = 0;
            for (int i = 0; i < Cards.Count; i++)
                if (Cards[i].IsJoker) n++;
            return n;
        }
    }
}
