using System;
using System.Reflection;

public static class GameFixes
{
#pragma warning disable 649
    private static Type ModSelectableType;
    private static MethodInfo CopySettingsFromProxyMethod;
#pragma warning restore 649

    public static void UpdateChildrenProperly(this KMSelectable selectable, KMSelectable childToSelect = null)
    {
        if(selectable == null)
            return;
        foreach(var child in selectable.Children)
            child.UpdateSettings();
        selectable.UpdateSettings();
        selectable.UpdateChildren(childToSelect);
    }

    private static void UpdateSettings(this KMSelectable selectable)
    {
        if(selectable != null && CopySettingsFromProxyMethod != null)
            CopySettingsFromProxyMethod.Invoke(
                selectable.GetComponent(ModSelectableType) ?? selectable.gameObject.AddComponent(ModSelectableType),
                new object[0]);
    }

#if !UNITY_EDITOR
    static GameFixes()
    {
        ModSelectableType = ReflectionHelper.FindTypeInGame("ModSelectable");
        if(ModSelectableType != null)
            CopySettingsFromProxyMethod =
                ModSelectableType.GetMethod("CopySettingsFromProxy", BindingFlags.Public | BindingFlags.Instance);
    }
#endif
}