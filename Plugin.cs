using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace HalloweenAction
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "butterystancakes.lethalcompany.halloweenaction", PLUGIN_NAME = "Halloween Action", PLUGIN_VERSION = "1.0.0";
        internal static new ManualLogSource Logger;

        internal static ConfigEntry<float> configChance, configEclipseChance;

        void Awake()
        {
            Logger = base.Logger;

            AcceptableValueRange<float> percentage = new(0f, 1f);
            string chanceHint = " (0 = never, 1 = guaranteed, or anything in between - 0.5 = 50% chance)";

            configChance = Config.Bind(
                "Random",
                "Chance",
                1f,
                new ConfigDescription("The percentage chance for the Halloween ambience to replace the original." + chanceHint, percentage));

            configEclipseChance = Config.Bind(
                "Random",
                "EclipseChance",
                1f,
                new ConfigDescription("The percentage chance for the Halloween ambience to replace the original during Eclipsed weather." + chanceHint, percentage));

            try
            {
                AssetBundle halloweenBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "halloweenaction"));
                HalloweenActionPatches.lowActionHW = halloweenBundle.LoadAsset<AudioClip>("LowActionHalloween");
                HalloweenActionPatches.highActionHW = halloweenBundle.LoadAsset<AudioClip>("HighAction2Halloween");
                halloweenBundle.Unload(false);
            }
            catch
            {
                Logger.LogError("Encountered some error loading asset bundle. Did you install the plugin correctly?");
                return;
            }

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }

    [HarmonyPatch]
    class HalloweenActionPatches
    {
        internal static AudioClip lowActionHW, highActionHW;

        static AudioClip lowActionOG, highActionOG;
        static bool randomizeThisFrame;

        [HarmonyPatch(typeof(SoundManager), "Awake")]
        [HarmonyPostfix]
        static void SoundManagerPostAwake(SoundManager __instance)
        {
            if (lowActionOG == null && !__instance.lowAction.clip.name.Contains("Halloween"))
                lowActionOG = __instance.lowAction.clip;
            if (highActionOG == null && !__instance.highAction2.clip.name.Contains("Halloween"))
                highActionOG = __instance.highAction2.clip;
            Plugin.Logger.LogDebug("Cached original clips");
        }

        [HarmonyPatch(typeof(SoundManager), "Start")]
        [HarmonyPostfix]
        static void SoundManagerPostStart(SoundManager __instance)
        {
            ChangeFearAudio();
            // for changing weather types
            StartOfRound.Instance.StartNewRoundEvent.AddListener(delegate
            {
                if (StartOfRound.Instance.fearLevel <= 0.1f)
                    ChangeFearAudio();
            });
        }

        [HarmonyPatch(typeof(SoundManager), "SetFearAudio")]
        [HarmonyPrefix]
        static void SoundManagerPreSetFearAudio(bool ___lowActionAudible, bool ___highAction2audible)
        {
            randomizeThisFrame = !GameNetworkManager.Instance.localPlayerController.isPlayerDead && (___lowActionAudible || ___highAction2audible);
        }

        [HarmonyPatch(typeof(SoundManager), "SetFearAudio")]
        [HarmonyPostfix]
        static void SoundManagerPostSetFearAudio(SoundManager __instance, bool ___lowActionAudible, bool ___highAction2audible)
        {
            if (randomizeThisFrame && !___lowActionAudible && !___highAction2audible)
                ChangeFearAudio();
        }

        static void ChangeFearAudio()
        {
            float chance = GetSpookyChance();
            if (chance > 0f && Random.value <= chance)
            {
                if (SoundManager.Instance.lowAction.clip != lowActionHW || SoundManager.Instance.highAction2.clip != highActionHW)
                {
                    SoundManager.Instance.lowAction.Stop();
                    SoundManager.Instance.lowAction.clip = lowActionHW;
                    SoundManager.Instance.lowAction.Play();
                    SoundManager.Instance.highAction2.Stop();
                    SoundManager.Instance.highAction2.clip = highActionHW;
                    SoundManager.Instance.highAction2.Play();
                    Plugin.Logger.LogDebug("SpoOoky");
                }
            }
            else
            {
                if (SoundManager.Instance.lowAction.clip != lowActionOG || SoundManager.Instance.highAction2.clip != highActionOG)
                {
                    SoundManager.Instance.lowAction.Stop();
                    SoundManager.Instance.lowAction.clip = lowActionOG;
                    SoundManager.Instance.lowAction.Play();
                    SoundManager.Instance.highAction2.Stop();
                    SoundManager.Instance.highAction2.clip = highActionOG;
                    SoundManager.Instance.highAction2.Play();
                    Plugin.Logger.LogDebug("classic");
                }
            }
        }

        static float GetSpookyChance()
        {
            return StartOfRound.Instance.currentLevel.currentWeather == LevelWeatherType.Eclipsed ? Plugin.configEclipseChance.Value : Plugin.configChance.Value;
        }
    }
}