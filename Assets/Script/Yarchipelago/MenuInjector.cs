using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using YARG.Localization;
using YARG.Menu.Main;
using YARG.Menu.Navigation;

namespace YARG.Assets.Script.Yarchipelago
{
    public static class MenuInjector
    {
        private const string ENTRY_NAME = "AP_MenuEntry";
        private static readonly FieldInfo OnClickField =
            typeof(NavigatableButton).GetField("_onClick", BindingFlags.Instance | BindingFlags.NonPublic);

        /// Archipelago integration:
        /// Injects the Archipelago menu entry into the main menu during startup.
        /// <see cref="YARG.Menu.Main.MainMenu.Start"/>
        /// <code>
        /// private void Start()
        /// {
        ///     MenuInjector.Inject(this); // Archipelago
        ///     _versionText.text = GlobalVariables.Instance.CurrentVersion;
        /// }
        /// </code>
        public static void Inject(MainMenu menu)
        {
            var template = FindTemplate(menu.gameObject, "Profiles") ?? menu.GetComponentInChildren<NavigatableButton>(true);
            var parent = template.transform.parent;
            if (parent.Find(ENTRY_NAME) != null) return;

            var go = Object.Instantiate(template.gameObject, parent);
            go.name = ENTRY_NAME;
            go.transform.SetSiblingIndex(template.transform.GetSiblingIndex() - 1);
            go.SetActive(true);

            var btn = go.GetComponent<NavigatableButton>();

            var localizer = go.GetComponentInChildren<LocalizeText>(true);
            if (localizer != null) UnityEngine.Object.Destroy(localizer);

            WipePersistentOnClick(btn);
            AddOnClick(btn, ArchipelagoConnectionDialog.ToggleArchipelagoDialog);

            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = "Archipelago";

            var group = template.NavigationGroup ?? template.GetComponentInParent<NavigationGroup>();
            RebuildGroupFromChildren(group);
        }

        private static NavigatableButton FindTemplate(GameObject root, string methodName)
        {
            foreach (var b in root.GetComponentsInChildren<NavigatableButton>(true))
            {
                var ev = GetOnClick(b);
                for (int i = 0, n = ev.GetPersistentEventCount(); i < n; i++)
                    if (ev.GetPersistentMethodName(i) == methodName) return b;
            }
            return null;
        }

        private static void WipePersistentOnClick(NavigatableButton btn) =>
            OnClickField.SetValue(btn, new UnityEngine.UI.Button.ButtonClickedEvent());

        private static void AddOnClick(NavigatableButton btn, UnityAction action)
            => GetOnClick(btn).AddListener(action);

        private static UnityEngine.UI.Button.ButtonClickedEvent GetOnClick(NavigatableButton btn)
            => (UnityEngine.UI.Button.ButtonClickedEvent) OnClickField.GetValue(btn);

        private static void RebuildGroupFromChildren(NavigationGroup group)
        {
            group.ClearNavigatables();
            foreach (var n in group.GetComponentsInChildren<NavigatableBehaviour>(true).OrderBy(x => x.transform.GetSiblingIndex()))
                group.AddNavigatable(n);
        }
    }
}