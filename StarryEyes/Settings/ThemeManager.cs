﻿using System;
using System.Collections.Generic;
using System.IO;
using StarryEyes.Albireo;
using StarryEyes.Models;
using StarryEyes.Models.Backstages.SystemEvents;
using StarryEyes.Nightmare.Windows;
using StarryEyes.Settings.Themes;

namespace StarryEyes.Settings
{
    public static class ThemeManager
    {
        private static readonly IDictionary<string, ThemeProfile> Profiles =
            new Dictionary<string, ThemeProfile>();

        public static string ThemeProfileDirectoryPath
        {
            get { return Path.Combine(App.ConfigurationDirectoryPath, App.ThemeProfilesDirectory); }
        }

        public static event Action ThemeChanged;

        internal static void Initialize()
        {
            Directory.CreateDirectory(ThemeProfileDirectoryPath);

            ReloadCandidates();

            Setting.Theme.ValueChanged += _ => ThemeChanged.SafeInvoke();
        }

        private static void Load(string file)
        {
            if (!File.Exists(file)) return;
            try
            {
                var profile = ThemeProfile.Load(file);
                Profiles[profile.Name] = profile;
            }
            catch (Exception ex)
            {
                MainWindowModel.ShowTaskDialog(
                    new TaskDialogOptions
                    {
                        Title = "テーマ エラー",
                        MainIcon = VistaTaskDialogIcon.Error,
                        MainInstruction = "テーマ ファイルを読み込めませんでした。",
                        Content = "XMLの記述に誤りがあります:" + Environment.NewLine +
                                  file,
                        ExpandedInfo = ex.Message,
                        CustomButtons = new[] { "再読込", "無視" },
                        Callback = (dlg, args, _) =>
                        {
                            if (args.ButtonId == 0)
                            {
                                Load(file);
                            }
                            return true;
                        },
                    });
            }
        }

        private static void CheckSetting()
        {
            // check assign is existed
            var group = Setting.Theme.Value ?? DefaultThemeProvider.DefaultThemeName;
            if (Profiles.ContainsKey(group)) return;
            // load default
            Setting.KeyAssign.Value = DefaultThemeProvider.DefaultThemeName;
            if (Profiles.ContainsKey(DefaultThemeProvider.DefaultThemeName)) return;
            // default binding is not found
            // make default
            var deftheme = DefaultThemeProvider.GetDefault();
            deftheme.Save(ThemeProfileDirectoryPath);
            Profiles.Add(deftheme.Name, deftheme);
        }

        public static void ReloadCandidates()
        {
            var path = ThemeProfileDirectoryPath;

            // load all assigns.
            foreach (var file in Directory.EnumerateFiles(path, "*.xml"))
            {
                Load(file);
            }
            CheckSetting();
        }

        public static ThemeProfile CurrentProfile
        {
            get
            {
                var profileId = Setting.KeyAssign.Value ?? DefaultThemeProvider.DefaultThemeName;
                if (Profiles.ContainsKey(profileId))
                {
                    return Profiles[profileId];
                }

                // not found
                BackstageModel.RegisterEvent(new ThemeProfileNotFoundEvent(profileId));
                return DefaultThemeProvider.GetDefault();
            }
        }
    }
}