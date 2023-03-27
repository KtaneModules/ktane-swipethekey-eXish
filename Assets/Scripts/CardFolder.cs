using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

[RequireComponent(typeof(KMSelectable), typeof(KMHoldable), typeof(Animator))]
public class CardFolder : MonoBehaviour
{
    [SerializeField]
    private Transform[] _slots;
    [SerializeField]
    private Card _cardPerfab;

    private KMSelectable[] _children;

    public static CardFolder Instance
    {
        get;
        private set;
    }

    private readonly List<Card> _cards = new List<Card>();

    public List<Card> GetAvailableCards()
    {
        return _cards;
    }

    private void Awake()
    {
        if(Instance == null)
            Instance = this;
        else
        {
            Debug.LogError("[Swipe The Key] A CardFolder was created, but one already exists! No new folder will be created.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        KMSelectable self = GetComponent<KMSelectable>();
        List<KMSelectable> children = new List<KMSelectable>();
        foreach(Transform slot in _slots)
        {
            Card c = Instantiate(_cardPerfab.gameObject, slot, false).GetComponent<Card>();
            c.Init();
            children.Add(c.GetComponent<KMSelectable>());
            children.Last().Parent = self;
            _cards.Add(c);
        }
        _children = self.Children = children.ToArray();
        self.UpdateChildrenProperly();

        Debug.Log("[Swipe The Key] Available card numbers are: " + _cards.Select(c => c.Number).Join(" "));

        Type fht = ReflectionHelper.FindTypeInGame("FloatingHoldable");
        Component fh = GetComponent(fht);
        Action hold = fht.Field<Action>("OnHold", fh);
        Action letGo = fht.Field<Action>("OnLetGo", fh);

        fht.SetField<Action>("OnHold", fh, () => { GetComponent<Animator>().SetBool("Open", true); if(hold != null) hold(); });
        fht.SetField<Action>("OnLetGo", fh, () => { GetComponent<Animator>().SetBool("Open", false); if(letGo != null) letGo(); });
    }

    private void OnDestroy()
    {
        if(Instance == this)
        {
            Debug.Log("[Swipe The Key] CardFolder destroyed. Card numbers are reset.");
            Instance = null;
        }
    }

#pragma warning disable 414
    private const string TwitchHelpMessage = @"Use ""!{0} grab 1"" to grab the first card. Use ""!{0} grab 0"" to drop whatever card you're holding.";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        Card.TPActive = true;
        command = command.Trim().ToLowerInvariant();

        Match m;
        if((m = Regex.Match(command, "grab ([0-6])")).Success)
        {
            int v = int.Parse(m.Groups[1].Value);
            if(v == 0 && Card.Held!=null)
            {
                yield return null;
                Card.Held.LetGo();
                yield break;
            }

            if(v != 0 && Card.Held == null)
                yield return new KMSelectable[] { _children[v - 1] };
        }
    }
}
