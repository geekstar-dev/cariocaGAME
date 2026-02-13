using System;
using System.Collections.Generic;
using System.Linq;

namespace CariocaRuntime
{
    public static class RulesValidator
    {
        public static bool TryAsTrio(List<Card> selected, out string reason)
        {
            reason = "";
            if (selected.Count != 3)
            {
                reason = "Un trío son 3 cartas.";
                return false;
            }

            int jokers = selected.Count(c => c.IsJoker);
            if (jokers > 1)
            {
                reason = "Máximo 1 Joker por grupo.";
                return false;
            }

            var nonJ = selected.Where(c => !c.IsJoker).ToList();
            if (nonJ.Count == 0)
            {
                reason = "Un trío no puede ser solo Jokers.";
                return false;
            }

            var rank = nonJ[0].Rank;
            if (nonJ.Any(c => c.Rank != rank))
            {
                reason = "En trío, todas las cartas deben ser del mismo número (Rank)."; 
                return false;
            }

            // Different suits among non-jokers
            var suits = nonJ.Select(c => c.Suit.Value).ToList();
            if (suits.Distinct().Count() != suits.Count)
            {
                reason = "En trío, las pintas deben ser distintas (sin repetir)."; 
                return false;
            }

            return true;
        }

        // Run: same suit, 4+ cards, consecutive, 0-1 joker that can fill exactly one gap.
        // Ace can be low (A-2-3-4) OR high (J-Q-K-A). Not wrap (Q-K-A-2) unless you decide later.
        public static bool TryAsRun(List<Card> selected, out string reason, out List<int> resolvedRanks)
        {
            reason = "";
            resolvedRanks = null;

            if (selected.Count < 4)
            {
                reason = "Una escala es de 4 o más cartas.";
                return false;
            }

            int jokers = selected.Count(c => c.IsJoker);
            if (jokers > 1)
            {
                reason = "Máximo 1 Joker por grupo.";
                return false;
            }

            var nonJ = selected.Where(c => !c.IsJoker).ToList();
            if (nonJ.Count == 0)
            {
                reason = "Una escala no puede ser solo Jokers.";
                return false;
            }

            // Same suit for non-jokers
            var suit = nonJ[0].Suit.Value;
            if (nonJ.Any(c => c.Suit.Value != suit))
            {
                reason = "En escala, todas las cartas deben ser de la misma pinta.";
                return false;
            }

            // Convert ranks to int (Ace=1..King=13)
            var ranks = nonJ.Select(c => (int)c.Rank).ToList();
            if (ranks.Distinct().Count() != ranks.Count)
            {
                reason = "En escala, no puedes repetir el mismo número.";
                return false;
            }

            // We allow two modes: Ace low (1) or Ace high (treat Ace as 14 if needed)
            // Try best fit for sequence length with <= 1 gap filled by joker.
            if (TryResolveSequence(ranks, selected.Count, jokers, aceHigh:false, out resolvedRanks))
                return true;

            if (TryResolveSequence(ranks, selected.Count, jokers, aceHigh:true, out resolvedRanks))
                return true;

            reason = jokers == 1
                ? "La escala no es correlativa (ni con 1 Joker rellenando un solo hueco)." 
                : "La escala no es correlativa.";
            return false;
        }

        private static bool TryResolveSequence(List<int> nonJRanks, int totalCount, int jokers, bool aceHigh, out List<int> resolved)
        {
            resolved = null;

            // if aceHigh: map Ace(1) -> 14 for computation
            var r = nonJRanks.Select(v => (aceHigh && v == 1) ? 14 : v).OrderBy(x => x).ToList();

            // Determine if can form consecutive sequence length = totalCount with <=1 missing
            // Approach: pick start so that all non-joker ranks fit in [start, start+len-1]
            int len = totalCount;
            int min = r.Min();
            int max = r.Max();

            // Possible start range so interval covers [min..max]
            int startMin = max - (len - 1);
            int startMax = min;

            for (int start = startMin; start <= startMax; start++)
            {
                var seq = Enumerable.Range(start, len).ToHashSet();
                int missing = seq.Count(x => !r.Contains(x));
                if (missing == jokers)
                {
                    // Build resolved list (sorted)
                    var resolvedSeq = Enumerable.Range(start, len).ToList();

                    // Validate: if aceHigh, avoid sequences like 10-11-12-13-14 is ok (J-Q-K-A),
                    // but disallow 14-15... (not possible anyway)
                    // Also disallow sequences with values >14
                    if (resolvedSeq.Any(v => v < 1 || v > 14)) continue;

                    // If aceHigh used, ensure 14 only appears at end (typical J-Q-K-A)
                    if (aceHigh && resolvedSeq.Contains(14) && resolvedSeq.Last() != 14) continue;

                    resolved = resolvedSeq;
                    return true;
                }
            }

            return false;
        }

        public static bool TryAddToTrio(BankGroup trio, Card card, out string reason)
        {
            reason = "";
            if (trio.Type != BankGroupType.Trio) { reason = "No es trío."; return false; }

            int jokerCount = trio.Cards.Count(c => c.IsJoker);
            if (card.IsJoker && jokerCount >= 1) { reason = "Ese trío ya tiene Joker."; return false; }

            var nonJ = trio.Cards.Where(c => !c.IsJoker).ToList();
            if (nonJ.Count == 0) { reason = "Trío inválido."; return false; }

            var rank = nonJ[0].Rank;

            if (!card.IsJoker)
            {
                if (card.Rank != rank) { reason = "Ese trío es de otro número."; return false; }

                // suit must not repeat among non-jokers
                var suits = nonJ.Select(c => c.Suit.Value).ToHashSet();
                if (suits.Contains(card.Suit.Value)) { reason = "Ya existe esa pinta en el trío."; return false; }
            }
            else
            {
                // Joker ok if none already
            }

            return true;
        }

        public static bool TryAddToRun(BankGroup run, Card card, out string reason)
        {
            reason = "";
            if (run.Type != BankGroupType.Run) { reason = "No es escala."; return false; }

            int jokerCount = run.Cards.Count(c => c.IsJoker);
            if (card.IsJoker && jokerCount >= 1) { reason = "Esa escala ya tiene Joker."; return false; }

            // Determine suit from non-jokers in run
            var nonJ = run.Cards.Where(c => !c.IsJoker).ToList();
            if (nonJ.Count == 0) { reason = "Escala inválida."; return false; }
            var suit = nonJ[0].Suit.Value;

            if (!card.IsJoker && card.Suit.Value != suit)
            {
                reason = "La carta no es de la misma pinta.";
                return false;
            }

            // For simplicity (MVP): allow adding only at ends by rank (or using joker to extend one step).
            // We'll compute current resolved best sequence, then see if card can be start-1 or end+1.
            string rReason;
            List<int> resolved;
            if (!TryAsRun(run.Cards.ToList(), out rReason, out resolved))
            {
                reason = "Escala actual no es válida: " + rReason;
                return false;
            }

            int start = resolved.First();
            int end = resolved.Last();

            if (card.IsJoker)
            {
                // Joker extends one side only (start-1 or end+1), if within 1..14
                if (start > 1 || end < 14) return true;
                reason = "No hay espacio para extender con Joker.";
                return false;
            }

            int v = (int)card.Rank;
            if (v == 1)
            {
                // Ace can be 1 or 14 depending on sequence
                // If end is 13, allow Ace as 14 (J-Q-K-A)
                if (end == 13) v = 14;
            }

            if (v == start - 1 || v == end + 1)
                return true;

            reason = "Solo puedes agregar al inicio o al final de la escala.";
            return false;
        }
    }
}
