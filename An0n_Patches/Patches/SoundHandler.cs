using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine;
using Object = UnityEngine.Object;

namespace An0n_Patches.Patches
{
    internal class SoundHandler
    {
        public static AudioClip newSFX;
        public static IEnumerator LoadAudio(string url, Action<AudioClip> callback)
        {
            UnityEngine.Networking.UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, (AudioType)20);
            try
            {
                yield return www.SendWebRequest();
                if ((int)www.result == 2)
                {
                    yield break;
                }
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if ((Object)(object)clip == (Object)null)
                {
                    yield break;
                }
                callback(clip);
            }
            finally
            {
                ((IDisposable)www)?.Dispose();
            }
        }

        //TODO: make sound loading better and not rely on magic num 9 for yodel
        public static void getAudioAndPlay(Character player, int rSound)
        {
            if (!An0n_Patch_Plugin.allowYodel.Value && rSound == 9) { return; }
            if (!An0n_Patch_Plugin.enableFallDmgSounds.Value && rSound < 9) { return; }
            string edd = "ed" + rSound.ToString() + ".wav";
            string path = An0n_Patch_Plugin.soundLoc + edd;
            CoroutineHelper.StartCoroutine(SoundHandler.LoadAudio("file:///" + path, (AudioClip sound) =>
            {
                if (sound == null)
                {
                    Debug.LogError("Failed to load Edd sounds!");
                    return;
                }

                AudioSource charDmg;
                GameObject sfx = player.gameObject.transform.FindChild("Scout").FindChild("SFX").gameObject;
                charDmg = sfx.GetComponent<AudioSource>();
                if (charDmg == null)
                {
                    sfx.AddComponent<AudioSource>();

                    charDmg = sfx.GetComponent<AudioSource>();

                }

                charDmg.spatialBlend = 1f;
                charDmg.dopplerLevel = 1f;
                charDmg.minDistance = 12f;
                charDmg.maxDistance = 1000f;
                charDmg.rolloffMode = AudioRolloffMode.Logarithmic;
                charDmg.clip = sound;
                if(An0n_Patch_Plugin.useGameSFXVolume.Value )
                {
                    charDmg.volume = GameHandler.Instance.SettingsHandler.GetSetting<SFXVolumeSetting>().Value;
                }
                else
                {
                    charDmg.volume = An0n_Patch_Plugin.yodelAndFallVolume.Value;
                }
                charDmg.Play();

                //If its a yodel, show it on the other players face as well
                if (player.name != Player.localPlayer.character.name && rSound == 9)
                {
                    PlayerControllerPatch.otherFaceYodel(player);
                }

            }));

        }
    }
}
