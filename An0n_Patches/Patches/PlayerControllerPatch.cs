
using HarmonyLib;
using System;
using System.Text;
using UnityEngine;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Harmony;
using BepInEx;
using System.Collections;
using BepInEx.Logging;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Photon.Pun;
using System.IO;
using Object = UnityEngine.Object;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using PEAKLib.Core;
using static DynamicBoneColliderBase;
using Zorro.Core;
using Zorro.Core.CLI;
using UnityEngine.InputSystem.Utilities;
namespace An0n_Patches.Patches
{

    public static class CoroutineHelper
    {
        private class CoroutineRunner : MonoBehaviour
        {
        }

        private static GameObject coroutineObject;

        public static void StartCoroutine(IEnumerator coroutine)
        {
            if ((Object)(object)coroutineObject == (Object)null)
            {
                coroutineObject = new GameObject("CoroutineHelper");
                Object.DontDestroyOnLoad((Object)(object)coroutineObject);
            }
            ((MonoBehaviour)coroutineObject.AddComponent<CoroutineRunner>()).StartCoroutine(coroutine);
        }
    }


    [HarmonyPatch(typeof(RunManager))]
    internal class RunManagerPatch : MonoBehaviour
    {
        [HarmonyPatch(typeof(RunManager), "StartRun")]
        [HarmonyPrefix()]
        static void Prefix()
        {
            PlayerControllerPatch.yodelEnable(0f);
        }
    }


    public static class SndPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "Awake")]
        public static void AwakePatch(Character __instance)
        {
            ((Component)__instance).gameObject.AddComponent<PlayerControllerPatch>();
           //Debug.Log((object)("Added Component to character: " + __instance.characterName));
        }
    }


    internal class PlayerControllerPatch : MonoBehaviour
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

        public static IEnumerator otherFaceYodelEnable(Character player)
        {
            yield return new WaitForSeconds((float)1.7f);
            AnimatedMouth pmouth = player.GetComponent<AnimatedMouth>();
            pmouth.enabled = true;
        }

        public static void otherFaceYodel(Character player)
        {
            AnimatedMouth pmouth = player.GetComponent<AnimatedMouth>();
            pmouth.enabled = false;
            pmouth.mouthRenderer.material.SetInt("_UseTalkSprites", 1);
            pmouth.isSpeaking = true;
            pmouth.mouthRenderer.material.SetTexture("_TalkSprite", pmouth.mouthTextures[2]);
            RunManager.Instance.StartCoroutine(otherFaceYodelEnable(player));
        }


        public static void getAudioAndPlay(Character player, int rSound)
        {
            if(!An0n_Patch_Plugin.allowYodel.Value && rSound == 9) {  return; }
            if (!An0n_Patch_Plugin.enableFallDmgSounds.Value && rSound < 9) { return; }
            string edd = "ed" + rSound.ToString() + ".wav";
            string path = An0n_Patch_Plugin.soundLoc + edd;
            CoroutineHelper.StartCoroutine(LoadAudio("file:///" + path, (AudioClip sound) =>
            {
                if (sound == null){
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
                charDmg.Play();

                //If its a yodel, show it on the other players face as well
                if (player.name != Player.localPlayer.character.name && rSound == 9)
                {
                    otherFaceYodel(player);
                }

            }));

        }



        static int lastPlayed = 0;
        public static bool yodel = false;
        public static bool yJump = false;
        static float force = 0f;


        //TODO: make it more future-proof instead of injecting at line specific in function
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(CharacterMovement), "CheckFallDamage")]
        public static IEnumerable<CodeInstruction> FallDamageHook(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            if (An0n_Patch_Plugin.enableFallDmgSounds.Value)
            {
                list.Insert(53, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerControllerPatch), nameof(selfDmgFallPlayAudio))));
            }
            return list;
        }

        static void selfDmgFallPlayAudio()
        {
            System.Random randomDirection = new System.Random();

            int rSound = randomDirection.Next(1, 9);
            if(rSound == lastPlayed)
            {
                while(rSound==lastPlayed)
                {
                    rSound = randomDirection.Next(1, 9);
                }
            }
            getAudioAndPlay(Player.localPlayer.character, rSound);
            Player.localPlayer.character.refs.view.RPC("playPlayerSound", RpcTarget.Others, Player.localPlayer.character.refs.view.ViewID,rSound);
            lastPlayed = rSound;
        }

        [HarmonyPatch(typeof(CharacterMovement), "TryToJump")]
        [HarmonyPrefix()]
        static bool stopYodelJump(object __instance)
        {
            if (yJump)
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(CharacterMovement), "Update")]
        [HarmonyPostfix()]
        static void yodelKey(object __instance)
        {
            bool flag4 = Input.GetKeyDown(KeyCode.Y);
            if (flag4 && !yodel && An0n_Patch_Plugin.allowYodel.Value)
            {
                Character locPlayer = Player.localPlayer.character;
                CharacterData data = locPlayer.GetComponent<CharacterData>();
                
                bool canYodel = !data.passedOut && !data.dead && !data.fullyPassedOut;
                if (!canYodel) {  return; }

                Player.localPlayer.character.refs.view.RPC("RPCA_PlayRemove", RpcTarget.All, new object[]{
                    "A_Scout_Emote_Shrug"
                });
             
                getAudioAndPlay(locPlayer, 9);
                Player.localPlayer.character.refs.view.RPC("playPlayerSound", RpcTarget.Others, locPlayer.refs.view.ViewID, 9);

                AnimatedMouth pmouth = locPlayer.GetComponent<AnimatedMouth>();
                pmouth.enabled = false;
                pmouth.isSpeaking = true;
                pmouth.mouthRenderer.material.SetInt("_UseTalkSprites", 1);
                pmouth.mouthRenderer.material.SetTexture("_TalkSprite", pmouth.mouthTextures[2]);

                yJump = true;
                force = locPlayer.GetComponent<CharacterMovement>().movementForce;
                locPlayer.GetComponent<CharacterMovement>().movementForce = 0;
                locPlayer.GetComponent<CharacterAnimations>().enabled = false;
                RunManager.Instance.StartCoroutine(yodelEnable(1f));
                yodel = true;

            }
        }

        public static IEnumerator yodelEnable(float mult)
        {
            Character locPlayer = Player.localPlayer.character;
            yield return new WaitForSeconds((float)1.7f*mult);
            yJump = false;
            Player.localPlayer.character.refs.animator.SetBool("Emote", false);
            locPlayer.GetComponent<AnimatedMouth>().enabled = true;
            locPlayer.GetComponent<CharacterMovement>().movementForce = force;
            locPlayer.GetComponent<CharacterAnimations>().enabled = true;
            yield return new WaitForSeconds((float)10f * mult);
            yodel = false;
        }

        [PunRPC]
        private void playPlayerSound(int name, int rSound)
        {
            Character senderCharacter = null;
            Character.GetCharacterWithPhotonID(name, out senderCharacter);
            Debug.Log($"Received message: {senderCharacter.name} sound: {rSound.ToString()}");
            getAudioAndPlay(senderCharacter, rSound);
        }
    }
}
