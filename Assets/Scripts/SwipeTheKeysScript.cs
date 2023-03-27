using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class SwipeTheKeysScript : MonoBehaviour
{
    [SerializeField]
    private CardFolder _folder;
    [SerializeField]
    private Material[] _lightMats;
    [SerializeField]
    private Renderer _led;

    private bool _unSolved = true;
    private int _id = ++_idc;
    private static int _idc;

    private int _symbolIx;
    
    private void Start()
    {
        SwipeTheKeyScript.EnsureFolder(_folder);

        GetComponent<KMSelectable>().OnFocus += () => { GetComponentInChildren<KeyCardAcceptor>().Active = _unSolved; };
        GetComponent<KMSelectable>().OnDefocus += () => { GetComponentInChildren<KeyCardAcceptor>().Active = false; };

        GetComponentInChildren<KeyCardAcceptor>().OnCollide += CardCollide;
        
        // TODO: Generate
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

        // TODO: Card scanning
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
}
