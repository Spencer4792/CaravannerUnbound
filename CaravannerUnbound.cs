// CaravannerUnbound — an Outward Definitive Edition mod.
//
// Removes the location restrictions on the Soroborean Caravanner's fast-travel
// service: destinations are offered from anywhere, not just when standing in a
// city. Which destinations appear is configurable.
//
// Design notes (how this differs from prior mods with a similar goal):
//   * Additive strategy. The vanilla RefreshTravelDestinations logic runs
//     untouched; a postfix then appends any eligible city the vanilla rules
//     withheld. Nothing is cleared or recomputed, so vanilla behavior
//     (multiplayer host sync, origin-city detection, banner selection) is
//     reused rather than re-implemented.
//   * Story-event UIDs are read out of the game's own constants
//     (MerchantFastTravel.CIERZODESTROYED_EVENTID etc.) at startup, so a game
//     patch that changes them can't silently desync this mod.
//   * All reflection handles are resolved once at startup and validated,
//     with a clear log line if the game update removed something.
//   * Fully config-driven via BepInEx's ConfigFile (F5 config manager aware).

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CaravannerUnbound
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class CaravannerUnboundPlugin : BaseUnityPlugin
    {
        
        public const string GUID = "com.spencer4792.caravannerunbound";
        public const string NAME = "CaravannerUnbound";
        public const string VERSION = "1.0.2";

        internal static ManualLogSource Log;
        internal static ModSettings Settings;

        internal void Awake()
        {
            Log = Logger;
            Settings = new ModSettings(Config);

            if (!GameApi.Initialize())
            {
                Log.LogError("Game API validation failed — see warnings above. Mod disabled.");
                return;
            }

            new Harmony(GUID).PatchAll(typeof(DestinationExpander));
            Log.LogMessage($"{NAME} {VERSION} ready.");
        }
    }

    /// <summary>All user-facing configuration, bound once at startup.</summary>
    internal class ModSettings
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<bool> RespectStoryEvents;
        public readonly ConfigEntry<bool> VanillaCityRule;
        readonly Dictionary<City, ConfigEntry<bool>> m_cityToggles = new Dictionary<City, ConfigEntry<bool>>();

        public ModSettings(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true,
                "Master switch. When off, the caravanner behaves exactly like vanilla.");
            RespectStoryEvents = config.Bind("General", "RespectStoryEvents", true,
                "Keep destinations consistent with your story: destroyed cities stay unavailable, " +
                "New Sirocco appears only once the caravan trader has set up there.");
            VanillaCityRule = config.Bind("General", "OnlyOfferTravelInCities", false,
                "Restore the vanilla rule that the full destination list is only offered while " +
                "inside a city. Off = travel from anywhere.");

            foreach (City city in Enum.GetValues(typeof(City)))
            {
                m_cityToggles[city] = config.Bind("Destinations", city.ToString(), true,
                    $"Offer {city} as a travel destination.");
            }
        }

        public bool IsDestinationWanted(City city) => m_cityToggles[city].Value;
    }

    /// <summary>The cities a caravanner can service, with their AreaEnum ids.</summary>
    internal enum City
    {
        Cierzo = 100,
        Monsoon = 200,
        Levant = 300,
        Harmattan = 400,
        Berg = 500,
        NewSirocco = 601,
    }

    /// <summary>
    /// One-time, validated bridge to the pieces of the game this mod needs.
    /// Everything private is resolved here and nowhere else.
    /// </summary>
    internal static class GameApi
    {
        // private state inside MerchantFastTravel (static fields in DE)
        static FieldInfo s_travelDataList;     // static List<TravelData> tmpCurrentTravelData
        static FieldInfo s_regionIdList;       // instance List<int> m_travelToRegion
        static FieldInfo s_originCity;         // static AreaManager.AreaEnum m_currentAreaAssociatedCity
        static MethodInfo s_levantAccessible;  // private bool CheckIfLevantBlocked() — true = Levant reachable

        // story-event UIDs, read from the game's own constants
        public static string CierzoDestroyedEvent { get; private set; }
        public static string BergExpulsedEvent { get; private set; }
        public static string SiroccoTraderEvent { get; private set; }

        public static bool Initialize()
        {
            var t = typeof(MerchantFastTravel);
            s_travelDataList = Find(t.GetField("tmpCurrentTravelData", AccessTools.all), "tmpCurrentTravelData");
            s_regionIdList = Find(t.GetField("m_travelToRegion", AccessTools.all), "m_travelToRegion");
            s_originCity = Find(t.GetField("m_currentAreaAssociatedCity", AccessTools.all), "m_currentAreaAssociatedCity");
            s_levantAccessible = Find(t.GetMethod("CheckIfLevantBlocked", AccessTools.all), "CheckIfLevantBlocked");

            CierzoDestroyedEvent = ReadGameConstant(t, "CIERZODESTROYED_EVENTID", "lDHL_XMS7kKEs0uOqrLQjw");
            BergExpulsedEvent = ReadGameConstant(t, "BERGEXPULSED_EVENTID", "vW4sarzBGkalTwy_KhGI6A");
            SiroccoTraderEvent = ReadGameConstant(t, "CARAVAN_TRADER_IN_NEWSIROCCO_EVENT_ID", "eYmZGb_BJ0qAtpcwrndhTg");

            return s_travelDataList != null && s_regionIdList != null
                && s_originCity != null && s_levantAccessible != null;
        }

        static T Find<T>(T member, string name) where T : class
        {
            if (member == null)
                CaravannerUnboundPlugin.Log.LogWarning($"MerchantFastTravel.{name} not found in this game version.");
            return member;
        }

        static string ReadGameConstant(Type type, string constName, string fallback)
        {
            try
            {
                var f = type.GetField(constName, AccessTools.all);
                var v = f?.GetRawConstantValue() as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            catch { /* fall through to fallback */ }
            CaravannerUnboundPlugin.Log.LogWarning($"Could not read {constName} from the game; using known value.");
            return fallback;
        }

        // ---- typed accessors used by the patch ----

        public static List<TravelData> TravelDataList(MerchantFastTravel m)
            => (List<TravelData>)s_travelDataList.GetValue(m);

        public static List<int> RegionIdList(MerchantFastTravel m)
            => (List<int>)s_regionIdList.GetValue(m);

        public static City OriginCity(MerchantFastTravel m)
            => (City)(int)s_originCity.GetValue(m);

        public static bool LevantAccessible(MerchantFastTravel m)
            => (bool)s_levantAccessible.Invoke(m, null);
    }

    /// <summary>
    /// Postfix: after vanilla has built (or declined to build) its destination
    /// list, append every configured city it left out.
    /// </summary>
    [HarmonyPatch(typeof(MerchantFastTravel), nameof(MerchantFastTravel.RefreshTravelDestinations), typeof(bool))]
    internal static class DestinationExpander
    {
        [HarmonyPostfix]
        static void ExpandDestinations(MerchantFastTravel __instance, bool _forceRefresh)
        {
            try
            {
                var cfg = CaravannerUnboundPlugin.Settings;
                if (!cfg.Enabled.Value)
                    return;

                // In multiplayer, non-host clients receive the list from the host;
                // vanilla already sent that request. Only the host expands.
                if (PhotonNetwork.isNonMasterClientInRoom && !_forceRefresh)
                    return;

                // Vanilla-style gate, opt-in via config.
                if (cfg.VanillaCityRule.Value && !AreaManager.Instance.GetIsCurrentAreaTownOrCity())
                    return;

                var offeredData = GameApi.TravelDataList(__instance);
                var offeredIds = GameApi.RegionIdList(__instance);
                City origin = GameApi.OriginCity(__instance);

                int added = 0;
                foreach (City city in Enum.GetValues(typeof(City)))
                {
                    if (city == origin) continue;                    // no round trips to here
                    if (AlreadyOffered(offeredData, city)) continue; // vanilla's rolled pick, or an earlier pass
                    if (!cfg.IsDestinationWanted(city)) continue;    // user turned it off
                    if (!StoryAllows(__instance, city)) continue;    // burned down, blockaded, unbuilt

                    var route = AreaManager.Instance.GetMerchantTravelData(
                        (AreaManager.AreaEnum)(int)origin, (AreaManager.AreaEnum)(int)city);
                    if (route == null) continue;                     // game has no route data for this pair

                    // Note: in DE, vanilla only *offers* one random pick from its candidate
                    // pool (m_travelToRegion), re-rolled every 72 game hours. We bypass the
                    // roll by appending directly to the offered list; the pool is kept in
                    // step so vanilla's stored-destination bookkeeping stays consistent.
                    if (!offeredIds.Contains((int)city))
                        offeredIds.Add((int)city);
                    offeredData.Add(route);
                    added++;
                }

                // Harmattan is additionally gated by a global flag in the travel UI.
                if (cfg.IsDestinationWanted(City.Harmattan) && origin != City.Harmattan)
                    MerchantFastTravel.CanTravelToHarmattan = true;

                if (added > 0)
                    MerchantFastTravel.SyncTravelDestinations();
            }
            catch (Exception ex)
            {
                CaravannerUnboundPlugin.Log.LogError($"ExpandDestinations: {ex}");
            }
        }

        /// <summary>Is this city already in the offered list (by destination, not reference)?</summary>
        static bool AlreadyOffered(List<TravelData> offered, City city)
        {
            var dest = (AreaManager.AreaEnum)(int)city;
            for (int i = 0; i < offered.Count; i++)
                if (offered[i] != null && offered[i].Destination == dest)
                    return true;
            return false;
        }

        /// <summary>Story consistency: is this destination currently part of the world?</summary>
        static bool StoryAllows(MerchantFastTravel instance, City city)
        {
            if (!CaravannerUnboundPlugin.Settings.RespectStoryEvents.Value)
                return true;

            switch (city)
            {
                case City.Cierzo:
                    return !QuestEventManager.Instance.HasQuestEvent(GameApi.CierzoDestroyedEvent);
                case City.Berg:
                    return !QuestEventManager.Instance.HasQuestEvent(GameApi.BergExpulsedEvent);
                case City.Levant:
                    return GameApi.LevantAccessible(instance);
                case City.NewSirocco:
                    return QuestEventManager.Instance.HasQuestEvent(GameApi.SiroccoTraderEvent);
                default:
                    return true;
            }
        }
    }
}
