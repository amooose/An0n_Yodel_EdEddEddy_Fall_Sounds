using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using An0n_Patches.Patches;
using Debug = UnityEngine.Debug;
using PEAKLib.Core;
using System.IO;

namespace An0n_Patches
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class An0n_Patch_Plugin : BaseUnityPlugin
    {
        public const string pluginGUID = "com.an0n.patch";
        private const string pluginName = "An0n Patch";
        private const string pluginVersion = "1.0.0";
        public static ManualLogSource mls = BepInEx.Logging.Logger.CreateLogSource(pluginGUID);
        private Harmony harmony = new Harmony(pluginGUID);
        public static ConfigEntry<bool> enableFallDmgSounds;
        public static ConfigEntry<bool> allowYodel;
        public static An0n_Patch_Plugin instance;
        public static string soundLoc;
        private void Awake()
        {
            instance = this;
            enableFallDmgSounds = Config.Bind("General",    
                             "enableFallDmgSounds",  
                             true, 
                             "Enable Ed Edd and Eddy fall damage sounds");
            allowYodel = Config.Bind("General",
                             "allowYodel",
                             true,
                             "Allow yodeling or not");

            Debug.Log("[An0nPatch] Yodel/Fall Sounds Plugin Loaded!");
            

            string location = ((BaseUnityPlugin)An0n_Patch_Plugin.instance).Info.Location;
            string[] loc = location.Split('\\');
            loc = loc.Take(loc.Length - 1).ToArray();
            string text = string.Join("\\", loc);
            soundLoc = text + "\\edd\\";
            
            

            this.patcher = new Harmony(pluginGUID);
            this.patcher.PatchAll(typeof(PlayerControllerPatch));
            this.patcher.PatchAll(typeof(RunManagerPatch));
            this.patcher.PatchAll(typeof(SndPatch));
            

        }
        private Harmony patcher;
    }
}