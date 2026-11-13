using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ItchDeploy.Editor
{
    public static class MultiAccount
    {
        private const string PrefKey = "ItchDeploy_Accounts";

        [System.Serializable]
        public class Account
        {
            public string name;
            public string credentialId;
            public string defaultUser;
        }

        [Serializable]
        private class LegacyAccount
        {
            public string apiKeyEncrypted;
        }

        [Serializable]
        private class LegacyAccountList
        {
            public List<LegacyAccount> accounts = new();
        }

        [System.Serializable]
        private class AccountList
        {
            public List<Account> accounts = new();
        }

        private static List<Account> _accounts;
        private static int _selected;

        public static List<Account> Accounts
        {
            get
            {
                if (_accounts == null) Load();
                return _accounts;
            }
        }

        public static void Load()
        {
            var json = EditorPrefs.GetString(PrefKey, "{}");
            try
            {
                _accounts = JsonUtility.FromJson<AccountList>(json)?.accounts ?? new List<Account>();
                var legacy = JsonUtility.FromJson<LegacyAccountList>(json)?.accounts;
                var migrated = false;

                for (var i = 0; i < _accounts.Count; i++)
                {
                    var account = _accounts[i];
                    if (string.IsNullOrEmpty(account.credentialId))
                    {
                        account.credentialId = Guid.NewGuid().ToString("N");
                        migrated = true;
                    }

                    if (legacy != null && i < legacy.Count &&
                        !string.IsNullOrEmpty(legacy[i].apiKeyEncrypted))
                    {
                        ItchDeploySecureStorage.SaveAccountApiKey(
                            account.credentialId,
                            legacy[i].apiKeyEncrypted);
                        migrated = true;
                    }
                }

                if (migrated)
                    Save();
            }
            catch { _accounts = new List<Account>(); }
        }

        public static void Save()
        {
            EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(new AccountList { accounts = Accounts }));
        }

        public static Account Current
        {
            get => _selected >= 0 && _selected < Accounts.Count ? Accounts[_selected] : null;
        }

        public static void Apply(ItchDeploySettings settings)
        {
            var acc = Current;
            if (acc == null) return;

            var apiKey = ItchDeploySecureStorage.LoadAccountApiKey(acc.credentialId);
            if (string.IsNullOrEmpty(apiKey))
                ItchDeploySecureStorage.DeleteApiKey();
            else
                ItchDeploySecureStorage.SaveApiKey(apiKey);
            ItchDeployCredentials.Invalidate();
            if (!string.IsNullOrEmpty(acc.defaultUser))
                settings.itchUserName = acc.defaultUser;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        public static VisualElement CreateUI(ItchDeploySettings settings)
        {
            var root = new VisualElement();

            var title = new Label(ItchDeployLocale._(
                "Multiple accounts", "Кілька акаунтів"));
            title.AddToClassList("settings-card-title");
            root.Add(title);

            var help = new Label(ItchDeployLocale._(
                "Save several itch.io profiles with protected local credentials.",
                "Зберігайте кілька профілів itch.io із захищеними локальними ключами."));
            help.AddToClassList("settings-card-help");
            root.Add(help);

            var noAccounts = ItchDeployLocale._(
                "No accounts", "Немає збережених акаунтів");
            var choices = Accounts.Select(AccountLabel).ToList();
            if (choices.Count == 0)
                choices.Add(noAccounts);

            var accountPopup = new ItchDeployChoiceField(
                ItchDeployLocale._("Account", "Акаунт"),
                choices,
                Mathf.Clamp(_selected, 0, choices.Count - 1));
            accountPopup.style.flexGrow = 1;
            accountPopup.SelectionChanged += (_, selectedValue) =>
            {
                _selected = Accounts.FindIndex(a => AccountLabel(a) == selectedValue);
                if (_selected >= 0) Apply(settings);
            };
            root.Add(accountPopup);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.AddToClassList("settings-action-row");

            var saveBtn = new Button(() =>
            {
                var accName = EditorUtility.DisplayDialogComplex("Save Account",
                    "Save current credentials as a new account?", "Save", "Cancel", "");
                if (accName == 0)
                {
                    SaveCurrentAccount(settings);
                    var updatedChoices = Accounts.Select(AccountLabel).ToList();
                    accountPopup.SetChoices(
                        updatedChoices,
                        updatedChoices.Count - 1);
                    accountPopup.index = updatedChoices.Count - 1;
                }
            });
            saveBtn.text = ItchDeployLocale._(
                "Save current account",
                "Зберегти поточний акаунт");
            btnRow.Add(saveBtn);

            var deleteBtn = new Button(() =>
            {
                if (Current != null)
                {
                    ItchDeploySecureStorage.DeleteAccountApiKey(Current.credentialId);
                    Accounts.RemoveAt(_selected);
                    _selected = Mathf.Max(0, _selected - 1);
                    Save();
                    var updatedChoices = Accounts.Select(AccountLabel).ToList();
                    if (updatedChoices.Count > 0)
                        accountPopup.SetChoices(updatedChoices, _selected);
                    else
                        accountPopup.SetChoices(new[] { noAccounts }, 0);
                }
            });
            deleteBtn.text = ItchDeployLocale._(
                "Delete", "Видалити");
            btnRow.Add(deleteBtn);

            root.Add(btnRow);
            return root;
        }

        private static string AccountLabel(Account account) =>
            $"{account.name} ({account.defaultUser})";

        private static void SaveCurrentAccount(ItchDeploySettings settings)
        {
            var account = new Account
            {
                name = $"{settings.itchUserName}@{DateTime.Now:HHmm}",
                credentialId = Guid.NewGuid().ToString("N"),
                defaultUser = settings.itchUserName
            };

            Accounts.Add(account);
            ItchDeploySecureStorage.SaveAccountApiKey(
                account.credentialId,
                settings.EffectiveApiKey);
            _selected = Accounts.Count - 1;
            Save();
        }
    }
}
