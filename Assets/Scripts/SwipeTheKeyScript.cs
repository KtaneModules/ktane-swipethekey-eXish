using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class SwipeTheKeyScript : MonoBehaviour
{
    [SerializeField]
    private CardFolder _folder;
    [SerializeField]
    private Material[] _lightMats;
    [SerializeField]
    private Renderer _led;
    [SerializeField]
    private Texture[] _symbols;
    [SerializeField]
    private Renderer _symbol;

    private bool _unSolved = true;
    private int _id;
    private static int _idc = 1;

    private int _symbolIx;

    private void Start()
    {
        _id = _idc++;
        EnsureFolder(_folder);

        GetComponent<KMSelectable>().OnFocus += () => { GetComponentInChildren<KeyCardAcceptor>().Active = _unSolved; };
        GetComponent<KMSelectable>().OnDefocus += () => { GetComponentInChildren<KeyCardAcceptor>().Active = false; };

        GetComponentInChildren<KeyCardAcceptor>().OnCollide += CardCollide;

        _symbolIx = Random.Range(0, 16);
        _symbol.material.mainTexture = _symbols[_symbolIx];
        Log("The displayed symbol is #" + _symbolIx);
        _symbol.transform.Rotate(transform.up, Random.Range(0f, 360f), Space.World);
    }

    private int _scanDir;
    private float _firstTime;

    private void CardCollide(Transform card)
    {
        if(card.localPosition.x < -0.5f)
            card.localPosition = new Vector3(-0.5f, card.localPosition.y, card.localPosition.z);

        if(card.localPosition.x < -0.25f)
        {
            if(card.localPosition.z > 0.5f || card.localPosition.z < -0.5f)
            {
                int newdir = card.localPosition.z > 0 ? 1 : 2;
                if(newdir == 1 && _scanDir == 2 || newdir == 2 && _scanDir == 1)
                    CheckScan(_scanDir, card.localPosition.z, Time.time);

                _scanDir = newdir;

                _firstTime = Time.time;
            }
        }
        else
            _scanDir = 0;
    }

    private void CheckScan(int mode, float lastPos, float lastTime)
    {
        float time = lastTime - _firstTime;
        if(time > 0.2f)
            return;

        Log("Scanned a card (number " + Card.Held.Number + ") in a " + (mode == 1 ? "downward" : "upward") + " direction.");
        GetComponent<KMAudio>().PlaySoundAtTransform("Beep", transform);

        if(_symbolIx < 8 && mode == 2 || _symbolIx > 7 && mode == 1)
        {
            Strike(true);
            return;
        }

        IEnumerable<int> sn = GetComponent<KMBombInfo>().QueryWidgets(KMBombInfo.QUERYKEY_GET_SERIAL_NUMBER, null).First()
            .Where(c => "0123456789".Contains(c)).Select(c => int.Parse(c.ToString()));
        int order = sn.First() > sn.Last() ? 1 : -1;

        Card correct = CardFolder.Instance.GetAvailableCards()
            .OrderByDescending(c =>
            {
                IEnumerable<char> chr = c.Number.Where(ch => "0123456789".Contains(ch));
                int i = int.Parse(chr.Skip(_symbolIx % 8).Concat(chr.Take(_symbolIx % 8)).Join(""));
                return order * i;
            })
            .First();

        if(correct == Card.Held)
            Solve();
        else
            Strike(false);
    }

    private void Solve()
    {
        if(!_unSolved)
            return;

        _led.material = _lightMats[1];
        _unSolved = false;

        Log("Correct card scanned; Module solved.");
        GetComponent<KMBombModule>().HandlePass();
    }

    private void Log(string v)
    {
        Debug.Log("[Swipe The Key #" + _id + "] " + v);
    }

    private void Strike(bool dir)
    {
        if(!_unSolved)
            return;

        StartCoroutine(Flash());

        Log("Incorrect " + (dir ? "scan direction" : "card scanned") + "; Strike issued.");
        GetComponent<KMBombModule>().HandleStrike();
    }

    private IEnumerator Flash()
    {
        _led.material = _lightMats[2];
        yield return new WaitForSeconds(1f);
        if(_unSolved)
            _led.material = _lightMats[0];
    }

    internal static void EnsureFolder(CardFolder folder)
    {
        if(CardFolder.Instance != null)
            return;

        MonoBehaviour room = (MonoBehaviour)FindObjectOfType(ReflectionHelper.FindTypeInGame("GameplayRoom"));
        Transform[] modholdables = FindObjectsOfType(ReflectionHelper.FindTypeInGame("ModHoldable")).Cast<MonoBehaviour>().Select(m => m.transform).ToArray();
        IList spawns = room.GetType().Field<IList>("HoldableSpawnPoints", room);
        MonoBehaviour hsp = spawns.Cast<MonoBehaviour>().FirstOrDefault(hspt => !modholdables.Any(tr => (tr.position - hspt.transform.position).magnitude < 0.01f));
        Type mht = ReflectionHelper.FindTypeInGame("ModHoldable");
        bool flag;
        if(flag = hsp == null)
        {
            GameObject sp = Instantiate(((MonoBehaviour)spawns[spawns.Count - 1]).gameObject);
            Component origh = ((MonoBehaviour)spawns[spawns.Count - 1]);
            object target = origh.GetType().Field<object>("HoldableTarget", origh);

            //sp.transform.position += new Vector3(1f, 0f, 0f);
            hsp = (MonoBehaviour)sp.GetComponent(ReflectionHelper.FindTypeInGame("HoldableSpawnPoint"));
            hsp.GetType().SetField("HoldableTarget", hsp, target);
        }
        GameObject mho = Instantiate(folder.gameObject, hsp.transform.position, hsp.transform.rotation);
        Component mh;

        Type mselt = ReflectionHelper.FindTypeInGame("ModSelectable");

        if(!(mh = mho.GetComponent(mht)))
            mh = mho.AddComponent(mht);
        if(!mho.GetComponent(mselt))
            mho.AddComponent(mselt);

        Type selt = ReflectionHelper.FindTypeInGame("Selectable");
        selt.SetField("Parent", mh.GetComponent(selt), room.GetComponent(selt));
        mh.transform.parent = room.transform;
        mh.transform.localScale = Vector3.one;
        mh.GetType().SetField("HoldableTarget", mh, hsp.GetType().Field<object>("HoldableTarget", hsp));
        if(flag)
        {
            IList initarray = selt.Field<IList>("Children", room.GetComponent(selt));
            IList arraygarbage = (IList)Activator.CreateInstance(selt.MakeArrayType(), initarray.Count + 1);
            Array.Copy((Array)initarray, (Array)arraygarbage, initarray.Count);
            arraygarbage[initarray.Count] = mh.GetComponent(selt);
            selt.SetField("Children", room.GetComponent(selt), arraygarbage);
        }
        else
        {
            int ix = selt.Field<int>("ChildRowLength", room.GetComponent(selt)) * hsp.GetType().Field<int>("SelectableIndexY", hsp) + hsp.GetType().Field<int>("SelectableIndexX", hsp);
            selt.Field<IList>("Children", room.GetComponent(selt))[ix] = mh.GetComponent(selt);
        }

        object arr = Array.CreateInstance(ReflectionHelper.FindTypeInGame("Assets.Scripts.Input.FaceSelectable"), 0);
        mht.SetField("Faces", mh, arr);
    }

#pragma warning disable 414
    private const string TwitchHelpMessage = @"!{0} swipe up | !{0} swipe down | !folder help";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        Card.TPActive = true;

        if(Card.Held == null)
        {
            yield return "sendtochaterror You're not holding a card!";
            yield break;
        }

        command = command.Trim().ToLowerInvariant();
        float mult;
        if(command == "swipe up")
            mult = 8f;
        else if(command == "swipe down")
            mult = -8f;
        else
            yield break;

        yield return null;

        StartCoroutine(Swipe(mult));
        yield return new WaitForSeconds(.5f);
    }

    private IEnumerator Swipe(float mult)
    {

        if(Card.Held._scan != null)
            Card.Held.StopCoroutine(Card.Held._scan);
        Card.Held.ShowVisuals(true);
        Card.Held.transform.parent = GetComponentInChildren<KeyCardAcceptor>().transform;
        Card.Held.transform.localEulerAngles = new Vector3(-90f, 180f, 0f);
        Card.Held.transform.localScale = Vector3.one * 50;
        Vector3 point;
        float t = Time.time;
        while(Time.time - t < .5f)
        {
            point = new Vector3(-0.5f, 0f, (Time.time - t - 0.25f) * mult);

            Card.Held.transform.localPosition = point;
            CardCollide(Card.Held.transform);
            yield return null;
        }
        Card.Held.ShowVisuals(false);
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        Card.TPActive = true;

        Log("Force solved.");

        GetComponent<KMAudio>().PlaySoundAtTransform("Beep", transform);
        Solve();
        yield break;
    }
}
