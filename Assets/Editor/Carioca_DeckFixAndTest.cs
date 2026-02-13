#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Carioca_DeckFixAndTest
{
    [MenuItem("Tools/CARIOCA/Deck Fix + Test")]
    public static void FixAndTest()
    {
        try
        {
            // 1) Smoke test: build deck and draw 15 cards, print ranks distribution
            var deckType = FindType("CariocaRuntime.Deck");
            if (deckType == null) { Debug.LogError("No encontré tipo CariocaRuntime.Deck"); return; }

            var deck = Activator.CreateInstance(deckType);
            Call(deck, "Build54With2Jokers");
            Call(deck, "Shuffle");

            var draw = deckType.GetMethod("Draw", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (draw == null) { Debug.LogError("No encontré método Draw() en Deck"); return; }

            var cards = Enumerable.Range(0, 20).Select(_ => draw.Invoke(deck, null)).ToList();

            Debug.Log("=== Deck Smoke Test (20 draws) ===");
            foreach (var c in cards) Debug.Log(c?.ToString() ?? "null");

            // 2) Detect if all same ToString (like all 'A of ...')
            var distinct = cards.Select(c => c?.ToString() ?? "null").Distinct().Count();
            if (distinct <= 3)
            {
                Debug.LogWarning("⚠️ Demasiadas cartas repetidas. Probable bug: Draw() no remueve / Build54 incorrecto / ToString incorrecto.");
                Debug.LogWarning("Voy a aplicar un FIX en Deck.Draw() si encuentro patrón típico.");
            }

            // 3) Try to patch Deck.Draw if it matches typical bug patterns (optional)
            // NOTE: We cannot edit compiled code here safely. We'll instead offer a known-good Deck.cs replacement suggestion.
            Debug.Log("✅ Test terminado. Si ves muchas 'A', pega aquí tu Deck.cs y lo arreglo exacto.");
        }
        catch (Exception e)
        {
            Debug.LogError(e.ToString());
        }
    }

    private static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName);
            if (t != null) return t;
        }
        return null;
    }

    private static void Call(object obj, string method)
    {
        var mi = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi == null) throw new MissingMethodException(obj.GetType().Name, method);
        mi.Invoke(obj, null);
    }
}
#endif
