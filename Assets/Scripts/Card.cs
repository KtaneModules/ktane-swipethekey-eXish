using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(KMSelectable))]
public class Card : MonoBehaviour
{
    [SerializeField]
    private TextMesh _numberText;
    [SerializeField]
    private Material _cursorMaterial;

    public static bool TPActive = false;

    private string _number;
    public string Number
    {
        get
        {
            return _number;
        }
        private set
        {
            _number = value;
            _numberText.text = value.Replace("-", " - ");
        }
    }

    public static Card Held
    {
        get;
        private set;
    }

    private object _rootParent;
    private object GetRootParent(object sel)
    {
        if(_rootParent != null)
            return _rootParent;

        object tmp;

        Type _selt = sel.GetType();
        do
        {
            tmp = _selt.Field<object>("Parent", sel);
            if(tmp != null)
                sel = tmp;
        }
        while(tmp != null);

        return _rootParent = sel;
    }

    private Transform _originalParent;
    internal Coroutine _scan;

    public void Init()
    {
        List<string> cards = CardFolder.Instance.GetAvailableCards().Select(c => c.Number).ToList();
        do
            Number = string.Format("{0:D4}-{1:D4}", Random.Range(0, 10000), Random.Range(0, 10000));
        while(cards.Contains(Number));

        GetComponent<KMSelectable>().OnInteract += () => { Hold(); return true; };

        _originalParent = transform.parent;

        ShowVisuals(true);
    }

    internal void Hold()
    {
        if(Held != null)
            throw new DuplicateException("Attempted to hold a Card while another is Held.");

        ++_override;
        _managerType.MethodCall("HandleCancel", GetManager(), new object[0]);
        --_override;

        Held = this;

        ShowVisuals(false);

        if(!TPActive)
            _scan = StartCoroutine(MouseScan());
    }

    internal void LetGo()
    {
        if(Held != this)
            throw new InvalidOperationException("Attempted to let go of a Card not being held.");

        ShowVisuals(true);
        transform.parent = _originalParent;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        transform.localScale = Vector3.one;

        Held = null;

        if(_scan != null)
            StopCoroutine(_scan);
    }

    private object GetManager()
    {
        return _inputManagerType.GetProperty("SelectableManager", ReflectionHelper.Flags).GetValue(FindObjectOfType(_inputManagerType), new object[0]);
    }

    private void OnDestroy()
    {
        if(Held == this)
            LetGo();
    }

    private IEnumerator MouseScan()
    {
        while(true)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

            if(!hits.Any(rh => rh.collider.GetComponent<KeyCardAcceptor>() && rh.collider.GetComponent<KeyCardAcceptor>().Active))
            {
                ShowVisuals(false);
                yield return null;
                continue;
            }

            ShowVisuals(true);

            RaycastHit hit = hits.First(rh => rh.collider.GetComponent<KeyCardAcceptor>() && rh.collider.GetComponent<KeyCardAcceptor>().Active);

            transform.parent = hit.collider.transform;
            transform.position = hit.point;
            transform.localEulerAngles = new Vector3(-90f, 180f, 0f);
            transform.localScale = Vector3.one * 50;
            hit.collider.GetComponent<KeyCardAcceptor>().Collisssssssssssssssssion(transform);

            yield return null;
        }
    }

    internal void ShowVisuals(bool on)
    {
        foreach(Renderer r in GetComponentsInChildren<Renderer>())
            r.enabled = on;
    }

    [Serializable]
    private class DuplicateException : Exception
    {
        public DuplicateException()
        {
        }

        public DuplicateException(string message) : base(message)
        {
        }

        public DuplicateException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DuplicateException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    private static Type _managerType;
    private static Type _inputManagerType;
    private static int _override;
    private static object _lastSelected;

    static Card()
    {
#if !UNITY_EDITOR
        _managerType = ReflectionHelper.FindTypeInGame("SelectableManager");
        _inputManagerType = ReflectionHelper.FindTypeInGame("KTInputManager");
        Harmony harm = new Harmony("BakersDozenBagels.SwipeTheKey");

        harm.Patch(_managerType.Method("Select"), prefix: new HarmonyMethod(typeof(Card).Method("SelectPrefix")));
        harm.Patch(_managerType.Method("HandleInteract"), prefix: new HarmonyMethod(typeof(Card).Method("InteractPrefix")), postfix: new HarmonyMethod(typeof(Card).Method("InteractPostfix")));

        harm.Patch(_managerType.Method("HandleCancel"), prefix: new HarmonyMethod(typeof(Card).Method("CancelPrefix")));

        harm.Patch(ReflectionHelper.FindTypeInGame("KTMouseCursor").Method("SetCursor"), prefix: new HarmonyMethod(typeof(Card).Method("CursorPrefix")));
#endif
    }

    private static bool InteractPrefix(object __instance)
    {
        DebugLog("IFix");
        if(Held == null || TPActive)
            return true;

        if(_lastSelected != null && _lastSelected.GetType().Field<bool>("IsPassThrough", _lastSelected))
            return true;

        MonoBehaviour cursel = _managerType.MethodCall<MonoBehaviour>("GetCurrentSelectable", __instance, new object[0]);
        _override += (cursel.GetComponentInChildren<KeyCardAcceptor>() != null) ? 1 : 0;

        return _override != 0;
    }

    private static bool SelectPrefix(object newSelectable)
    {
        DebugLog("SFix {0}", newSelectable);
        if(Held == null || TPActive)
            return true;

        _lastSelected = newSelectable;

        MonoBehaviour cursel = (MonoBehaviour)newSelectable;

        return cursel.transform.root.GetComponentInChildren<KeyCardAcceptor>() != null;
    }

    private static void InteractPostfix()
    {
        if(TPActive)
            return;

        if(_override != 0)
            _override -= 1;
    }

    private static bool CancelPrefix()
    {
        if(_override != 0 || Held == null || TPActive)
            return true;

        Held.LetGo();

        return false;
    }

    private static bool CursorPrefix(object __instance, bool ___UseHardwareCursor, ref int ___currentMode)
    {
        if(Held == null || TPActive)
            return true;

        if(___UseHardwareCursor)
            Cursor.SetCursor((Texture2D)Held._cursorMaterial.mainTexture, Vector2.zero, CursorMode.Auto);
        else
        {
            if(___currentMode == 2)
                return false;
            MonoBehaviour inst = (MonoBehaviour)__instance;
            inst.transform.localRotation = Quaternion.Euler(Vector3.zero);
            ___currentMode = 2;
            inst.GetComponent<Renderer>().sharedMaterial = Held._cursorMaterial;
        }

        return false;
    }

    private static void DebugLog(string str, params object[] args)
    {
        //Debug.LogFormat(str, args);
    }
}
