// CariocaPrototypeBootstrap.cs
// Drop-in "todo en uno" para: deck+shuffle+deal+hand fan+card text skin + steam stub.
// Requiere TextMeshPro. DOTween es opcional (animaciones suaves si está instalado).

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if DOTWEEN
using DG.Tweening;
#endif

#if STEAMWORKS_NET
using Steamworks;
#endif

namespace CariocaPrototype
{
    // -----------------------------
    // DATA
    // -----------------------------
    public enum Suit { Clubs, Diamonds, Hearts, Spades, None }
    public enum Rank
    {
        Joker = 0,
        Ace = 1, Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7,
        Eight = 8, Nine = 9, Ten = 10, Jack = 11, Queen = 12, King = 13
    }

    [Serializable]
    public struct CardData
    {
        public Suit Suit;
        public Rank Rank;

        public CardData(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = rank == Rank.Joker ? Suit.None : suit;
        }

        public bool IsJoker => Rank == Rank.Joker;
        public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;

        // ID estable para mods/skins: "AS", "10H", "QD", "JOKER"
        public string StableId
        {
            get
            {
                if (IsJoker) return "JOKER";
                return $"{RankToId(Rank)}{SuitToId(Suit)}";
            }
        }

        public static string RankToId(Rank r) => r switch
        {
            Rank.Ace => "A",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ten => "10",
            Rank.Nine => "9",
            Rank.Eight => "8",
            Rank.Seven => "7",
            Rank.Six => "6",
            Rank.Five => "5",
            Rank.Four => "4",
            Rank.Three => "3",
            Rank.Two => "2",
            _ => ((int)r).ToString()
        };

        public static string SuitToId(Suit s) => s switch
        {
            Suit.Spades => "S",
            Suit.Hearts => "H",
            Suit.Diamonds => "D",
            Suit.Clubs => "C",
            _ => ""
        };
    }

    public sealed class DeckModel
    {
        private readonly List<CardData> _cards = new(128);
        private readonly System.Random _rng = new();

        public int Count => _cards.Count;

        public void BuildStandardCarioca()
        {
            _cards.Clear();

            // Carioca típico: 2 barajas inglesas (52) + comodines (2 por mazo) => 108
            for (int deck = 0; deck < 2; deck++)
            {
                foreach (Suit s in new[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades })
                {
                    for (int rv = 1; rv <= 13; rv++)
                        _cards.Add(new CardData((Rank)rv, s));
                }

                // 2 jokers por mazo
                _cards.Add(new CardData(Rank.Joker, Suit.None));
                _cards.Add(new CardData(Rank.Joker, Suit.None));
            }
        }

        public void ShuffleFisherYates()
        {
            // Fisher–Yates in-place
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        public CardData DrawTop()
        {
            if (_cards.Count == 0) throw new InvalidOperationException("Deck vacío.");
            int last = _cards.Count - 1;
            var c = _cards[last];
            _cards.RemoveAt(last);
            return c;
        }
    }

    // -----------------------------
    // SKIN SYSTEM (texto ahora, sprites después)
    // -----------------------------
    public interface ICardSkin
    {
        void Apply(CardViewText view, CardData data);
    }

    public sealed class TextSkin : ICardSkin
    {
        private readonly Color _red = new(0.75f, 0.12f, 0.12f);
        private readonly Color _black = new(0.10f, 0.10f, 0.10f);

        public void Apply(CardViewText view, CardData data)
        {
            view.ApplyText(data, _red, _black);
        }
    }

    public sealed class SkinManager : MonoBehaviour
    {
        public static SkinManager Instance { get; private set; }
        private ICardSkin _activeSkin;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Hoy: texto. Mañana: cargar JSON del Workshop y elegir TextSkin/SpriteSkin.
            _activeSkin = new TextSkin();
        }

        public void ApplySkin(CardViewText view, CardData data) => _activeSkin.Apply(view, data);
    }

    // -----------------------------
    // CARD VIEW (UI + TMP)
    // -----------------------------
    public sealed class CardViewText : MonoBehaviour
    {
        [Header("UI")]
        public RectTransform rect;
        public GameObject frontRoot;
        public GameObject backRoot;
        public Image frontBg;

        [Header("TMP")]
        public TMP_Text rankText;
        public TMP_Text suitText;
        public TMP_Text cornerText;

        [NonSerialized] public CardData Data;

        public void SetData(CardData data)
        {
            Data = data;
            if (SkinManager.Instance != null) SkinManager.Instance.ApplySkin(this, data);
            else ApplyText(data, Color.red, Color.black); // fallback
        }

        public void SetFace(bool front)
        {
            if (frontRoot) frontRoot.SetActive(front);
            if (backRoot) backRoot.SetActive(!front);
        }

        public void ApplyText(CardData data, Color red, Color black)
        {
            if (data.IsJoker)
            {
                if (rankText) rankText.text = "JOKER";
                if (suitText) suitText.text = "";
                if (cornerText) cornerText.text = "JOKER";
                SetTextColor(Color.black);
                return;
            }

            string r = CardData.RankToId(data.Rank);
            string s = SuitToGlyph(data.Suit);

            if (rankText) rankText.text = r;
            if (suitText) suitText.text = s;
            if (cornerText) cornerText.text = $"{r}{s}";

            SetTextColor(data.IsRed ? red : black);
        }

        private void SetTextColor(Color c)
        {
            if (rankText) rankText.color = c;
            if (suitText) suitText.color = c;
            if (cornerText) cornerText.color = c;
        }

        private static string SuitToGlyph(Suit s) => s switch
        {
            Suit.Spades => "♠",
            Suit.Hearts => "♥",
            Suit.Diamonds => "♦",
            Suit.Clubs => "♣",
            _ => ""
        };
    }

    // -----------------------------
    // HAND VIEW (abanico tipo Hearthstone)
    // -----------------------------
    public sealed class HandView : MonoBehaviour
    {
        [Header("Layout")]
        public float maxAngle = 36f;
        public float radius = 520f;         // si lo quieres en arco real, úsalo para calcular y
        public float spacing = 90f;         // separación base
        public float verticalCurve = 40f;   // curva Y
        public float tiltStrength = 1f;

        [Header("Animation (DOTween opcional)")]
        public float moveDuration = 0.18f;

        private readonly List<CardViewText> _cards = new();

        public void Add(CardViewText card)
        {
            _cards.Add(card);
            card.transform.SetParent(transform, worldPositionStays: false);
        }

        public void Reflow(bool animated)
        {
            int n = _cards.Count;
            if (n == 0) return;

            float totalAngle = Mathf.Min(maxAngle, 10f + n * 3.2f);
            float step = (n == 1) ? 0f : totalAngle / (n - 1);
            float start = -totalAngle * 0.5f;

            float spread = Mathf.Min(spacing, 600f / Mathf.Max(1, n));

            for (int i = 0; i < n; i++)
            {
                float t = (n == 1) ? 0.5f : (float)i / (n - 1);
                float ang = start + step * i;

                float x = (i - (n - 1) * 0.5f) * spread;
                float y = -Mathf.Abs((t - 0.5f) * 2f) * verticalCurve;
                float zRot = ang * tiltStrength;

                var tr = _cards[i].rect != null ? _cards[i].rect : (RectTransform)_cards[i].transform;

#if DOTWEEN
                if (animated)
                {
                    tr.DOLocalMove(new Vector3(x, y, 0), moveDuration).SetEase(Ease.OutCubic);
                    tr.DOLocalRotate(new Vector3(0, 0, zRot), moveDuration).SetEase(Ease.OutCubic);
                }
                else
#endif
                {
                    tr.localPosition = new Vector3(x, y, 0);
                    tr.localRotation = Quaternion.Euler(0, 0, zRot);
                }

                // orden visual
                _cards[i].transform.SetSiblingIndex(i);
            }
        }
    }

    [Serializable]
    public class PlayerSeat
    {
        public bool isLocalPlayer;
        public Transform dealTarget; // punto cerca de su mano
        public HandView hand;
    }

    // -----------------------------
    // GAME SEQUENCE (shuffle + deal 12 a 2–4 jugadores)
    // -----------------------------
    public sealed class GameSequenceController : MonoBehaviour
    {
        [Header("Refs")]
        public Transform deckTransform;
        public GameObject cardPrefab; // debe tener CardViewText
        public List<PlayerSeat> seats = new();

        [Header("Timings")]
        public float shuffleDuration = 0.9f;
        public int shuffleShakes = 14;
        public float dealCardDuration = 0.25f;
        public float dealInterval = 0.05f;

        private DeckModel _deck;

        private void Start()
        {
            // Asegura SkinManager
            if (SkinManager.Instance == null)
            {
                var go = new GameObject("SkinManager");
                go.AddComponent<SkinManager>();
            }

            _deck = new DeckModel();
            _deck.BuildStandardCarioca();

            StartCoroutine(BeginMatch());
        }

        private IEnumerator BeginMatch()
        {
            // 1) Shuffle lógico + animación mazo
            _deck.ShuffleFisherYates();
            yield return ShuffleAnim();

            // 2) Deal 12 a 2–4 jugadores
            int players = Mathf.Clamp(seats.Count, 2, 4);
            int cardsPerPlayer = 12;

            for (int i = 0; i < cardsPerPlayer; i++)
            {
                for (int p = 0; p < players; p++)
                {
                    var seat = seats[p];
                    var data = _deck.DrawTop();

                    var go = Instantiate(cardPrefab, deckTransform.position, Quaternion.identity, deckTransform.parent);
                    var view = go.GetComponent<CardViewText>();
                    view.SetData(data);

                    // Local: front. Otros: back (por ahora)
                    view.SetFace(seat.isLocalPlayer);

                    yield return DealAnim(view, seat);
                    yield return new WaitForSeconds(dealInterval);
                }
            }

            // Reflow final
            for (int p = 0; p < players; p++)
                seats[p].hand.Reflow(animated: true);
        }

        private IEnumerator ShuffleAnim()
        {
#if DOTWEEN
            Vector3 basePos = deckTransform.position;
            Quaternion baseRot = deckTransform.rotation;

            var seq = DOTween.Sequence();
            seq.Append(deckTransform.DOShakePosition(shuffleDuration, strength: 12f, vibrato: shuffleShakes, randomness: 70f));
            seq.Join(deckTransform.DOShakeRotation(shuffleDuration, strength: new Vector3(0, 0, 12f), vibrato: shuffleShakes, randomness: 60f));
            seq.Append(deckTransform.DOMove(basePos, 0.05f));
            seq.Join(deckTransform.DORotateQuaternion(baseRot, 0.05f));
            yield return seq.WaitForCompletion();
#else
            // Sin DOTween: pausa simple (igual “se siente” que baraja)
            yield return new WaitForSeconds(shuffleDuration);
#endif
        }

        private IEnumerator DealAnim(CardViewText view, PlayerSeat seat)
        {
            // mover al dealTarget y luego parent a la hand
            Transform tr = view.transform;

#if DOTWEEN
            var seq = DG.Tweening.DOTween.Sequence();
            seq.Append(tr.DOMove(seat.dealTarget.position, dealCardDuration).SetEase(Ease.OutCubic));
            seq.Join(tr.DORotate(new Vector3(0, 0, UnityEngine.Random.Range(-10f, 10f)), dealCardDuration));
            yield return seq.WaitForCompletion();
#else
            // Lerp simple
            Vector3 start = tr.position;
            Vector3 end = seat.dealTarget.position;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, dealCardDuration);
                tr.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
#endif
            // ahora la carta vive en la mano
            seat.hand.Add(view);
            seat.hand.Reflow(animated: true);
        }
    }

    // -----------------------------
    // STEAM (stub) - inicializa Steam y corre callbacks
    // -----------------------------
    public sealed class SteamBootstrap : MonoBehaviour
    {
        public static SteamBootstrap Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if STEAMWORKS_NET
            try
            {
                if (!SteamAPI.Init())
                {
                    Debug.LogError("SteamAPI.Init() falló. ¿AppID correcto? ¿Corriendo desde Steam?");
                }
                else
                {
                    Debug.Log("Steamworks inicializado OK.");
                }
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError($"Steamworks DLL no encontrada: {e.Message}");
            }
#else
            Debug.Log("SteamBootstrap: Steamworks.NET no está instalado (define STEAMWORKS_NET ausente).");
#endif
        }

        private void Update()
        {
#if STEAMWORKS_NET
            SteamAPI.RunCallbacks();
#endif
        }

        private void OnDestroy()
        {
#if STEAMWORKS_NET
            SteamAPI.Shutdown();
#endif
        }
    }
}
