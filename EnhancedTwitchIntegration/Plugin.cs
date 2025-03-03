﻿using IPA;
using IPALogger = IPA.Logging.Logger;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SongRequestManager.UI;
using BeatSaberMarkupLanguage.Settings;
using IPA.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace SongRequestManager
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public string Name => "Song Request Manager";
        public static SemVer.Version Version => IPA.Loader.PluginManager.GetPluginFromId("SongRequestManager").Version;

        public static IPALogger Logger { get; internal set; }

        internal static WebClient WebClient;

        public static UdpListener UdpListener;

        public bool IsApplicationExiting = false;
        public static Plugin Instance { get; private set; }
        
        private RequestBotConfig RequestBotConfig;

        internal static GameMode gameMode;

        //DataPath is typically "C:\Program Files (x86)\Steam\steamapps\common\Beat Saber\UserData\SRM"
        public static string DataPath = Path.Combine(UnityGame.UserDataPath, "SRM");
        public static string OldDataPath = Path.Combine(UnityGame.UserDataPath, "StreamCore");
        public static bool SongBrowserPluginPresent;
        public static bool ChatCorePluginPresent;

        public static bool _pre1_29;
        public static bool _pre1_28;

        [Init]
        public void Init(IPALogger log)
        {
            Logger = log;
        }

        public static void Log(string text,
                        [CallerFilePath] string file = "",
                        [CallerMemberName] string member = "",
                        [CallerLineNumber] int line = 0)
        {
            Logger.Info($"{Path.GetFileName(file)}->{member}({line}): {text}");
        }

        [OnStart]
        public void OnStart()
        {
            if (Instance != null) return;
            Instance = this;

            // create SRM UserDataFolder folder if needed, or rename old streamcore folder
            if (!Directory.Exists(DataPath))
            {
                if (Directory.Exists(OldDataPath))
                {
                    Directory.Move(OldDataPath, DataPath);
                }
                else
                {
                    Directory.CreateDirectory(DataPath);
                }
            }

            // initialize config
            RequestBotConfig = new RequestBotConfig();

            Dispatcher.Initialize();

            // create our internal webclient
            WebClient = new WebClient();

            // create udp listener
            UdpListener = new UdpListener();

            SongBrowserPluginPresent = IPA.Loader.PluginManager.GetPlugin("Song Browser") != null;
            ChatCorePluginPresent = IPA.Loader.PluginManager.GetPlugin("ChatCore") != null;
            _pre1_29 = IPA.Utilities.UnityGame.GameVersion < new IPA.Utilities.AlmostVersion("1.29.0");
            _pre1_28 = IPA.Utilities.UnityGame.GameVersion < new IPA.Utilities.AlmostVersion("1.28.0");
            Plugin.Log("catcore?" + (IPA.Loader.PluginManager.GetPlugin("CatCore") != null));

            // setup handle for fresh menu scene changes
            BS_Utils.Utilities.BSEvents.OnLoad();
            BS_Utils.Utilities.BSEvents.lateMenuSceneLoadedFresh += OnLateMenuSceneLoadedFresh;

            // init sprites
            Base64Sprites.Init();
        }

        private void OnLateMenuSceneLoadedFresh(ScenesTransitionSetupDataSO scenesTransitionSetupData)
        {
            // setup settings ui
            BSMLSettings.instance.AddSettingsMenu("SRM", "SongRequestManager.Views.SongRequestManagerSettings.bsml", SongRequestManagerSettings.instance);

            var onlinePlayButton = Resources.FindObjectsOfTypeAll<Button>().First(x => x.name == "OnlineButton");
            var soloFreePlayButton = Resources.FindObjectsOfTypeAll<Button>().First(x => x.name == "SoloButton");
            var partyFreePlayButton = Resources.FindObjectsOfTypeAll<Button>().First(x => x.name == "PartyButton");
            var campaignButton = Resources.FindObjectsOfTypeAll<Button>().First(x => x.name == "CampaignButton");

            onlinePlayButton.onClick.AddListener(() => { gameMode = GameMode.Online; });
            soloFreePlayButton.onClick.AddListener(() => { gameMode = GameMode.Solo; });
            partyFreePlayButton.onClick.AddListener(() => { gameMode = GameMode.Solo; });
            campaignButton.onClick.AddListener(() => { gameMode = GameMode.Solo; });

            // main load point
            RequestBot.OnLoad();
            RequestBotConfig.Save(true);
        }

        internal enum GameMode
        {
            Solo,
            Online
        }

        public static void SongBrowserCancelFilter()
        {
            if (SongBrowserPluginPresent)
            {
                var _songBrowserUI = SongBrowser.SongBrowserApplication.Instance.GetField<SongBrowser.UI.SongBrowserUI, SongBrowser.SongBrowserApplication>("_songBrowserUI");
                if (_songBrowserUI)
                {
                    //if (_songBrowserUI.Model.Settings.filterMode != SongBrowser.Configuration.SongFilterMode.None && _songBrowserUI.Model.Settings.sortMode != SongBrowser.Configuration.SongSortMode.Original)
                    //{
                        _songBrowserUI.CancelFilter();
                    //}
                }
                else
                {
                    Plugin.Log("There was a problem obtaining SongBrowserUI object, unable to reset filters");
                }
            }
        }

        [OnExit]
        public void OnExit()
        {
            UdpListener?.Shutdown();

            IsApplicationExiting = true;
        }
    }
}
