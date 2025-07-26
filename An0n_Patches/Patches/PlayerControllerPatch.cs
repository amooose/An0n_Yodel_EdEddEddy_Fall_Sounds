
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


    public static class PlayerSndComponent
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), "Awake")]
        public static void AwakePatch(Character __instance)
        {
            ((Component)__instance).gameObject.AddComponent<PlayerControllerPatch>();
        }
    }


    internal class PlayerControllerPatch : MonoBehaviour
    {

        static int lastPlayed = 0;
        public static bool yodel = false;
        public static bool yJump = false;
        static float force = 0f;

        //Handle other player's faces yodeling: re-enable face
        public static IEnumerator otherFaceYodelEnable(Character player)
        {
            yield return new WaitForSeconds((float)1.7f);
            AnimatedMouth pmouth = player.GetComponent<AnimatedMouth>();
            pmouth.enabled = true;
        }

        //Handle other player's faces yodeling: open mouth+disable face
        public static void otherFaceYodel(Character player)
        {
            AnimatedMouth pmouth = player.GetComponent<AnimatedMouth>();
            pmouth.enabled = false;
            pmouth.mouthRenderer.material.SetInt("_UseTalkSprites", 1);
            pmouth.isSpeaking = true;
            pmouth.mouthRenderer.material.SetTexture("_TalkSprite", pmouth.mouthTextures[2]);
            RunManager.Instance.StartCoroutine(otherFaceYodelEnable(player));
        }

        

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

        //Play damage sound and send out RPC
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
            SoundHandler.getAudioAndPlay(Player.localPlayer.character, rSound);
            Player.localPlayer.character.refs.view.RPC("playPlayerSound", RpcTarget.Others, Player.localPlayer.character.refs.view.ViewID,rSound);
            lastPlayed = rSound;
        }

        //Stop Jump while Yodel
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

        //Handle Yodel key, yodel sound, and mouth movement for client and host.
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
             
                SoundHandler.getAudioAndPlay(locPlayer, 9);
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

        //Apply yodel effect for yourself.
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

        //Send sound RPC
        [PunRPC]
        private void playPlayerSound(int name, int rSound)
        {
            Character senderCharacter = null;
            Character.GetCharacterWithPhotonID(name, out senderCharacter);
            Debug.Log($"Received message: {senderCharacter.name} sound: {rSound.ToString()}");
            SoundHandler.getAudioAndPlay(senderCharacter, rSound);
        }
    }
}
