using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Shops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using xTile.Dimensions;

namespace FreeLove
{
    // TODO:
    // "I married Hailey and then married another player. Now I have a wedding twice everyday."

    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {
        public const int bedSleepOffset = 76;

        public static IMonitor SMonitor { get; private set; }
        public static IModHelper SHelper { get; private set; }
        public static ModConfig Config { get; private set; }
        public static Random myRand { get; private set; }
        public static string farmHelperSpouse { get; set; } = null;


        private static readonly PerScreen<NPC> tempOfficialSpouse = new(() => null);
        /// <summary> If set, this will be the name returned in Farmer.spouse</summary>
        internal static NPC TempOfficialSpouse 
        { 
            get => tempOfficialSpouse.Value;

            set => tempOfficialSpouse.Value = value;
        }

        private static readonly PerScreen<string> spouseToDivorce = new(() => null);
        /// <summary> The spouse currently being divorced. </summary>
        internal static string SpouseToDivorce
        {
            get => spouseToDivorce.Value;

            set => spouseToDivorce.Value = value;
        }

        private static readonly PerScreen<int> divorceHeartsLost = new(() => 0);
        /// <summary> The number of hearts that should be lost thru this divorce. </summary>
        internal static int DivorceHeartsLost
        {
            get => divorceHeartsLost.Value;

            set => divorceHeartsLost.Value = value;
        }

        public static readonly Dictionary<long, Dictionary<string, NPC>> currentSpouses = new();
        public static readonly Dictionary<long, Dictionary<string, NPC>> currentUnofficialSpouses = new();

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();
            if (!Config.EnableMod)
                return;

            SMonitor = Monitor;
            SHelper = helper;

            myRand = new Random();

            helper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle; ;
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            helper.Events.GameLoop.OneSecondUpdateTicked += GameLoop_OneSecondUpdateTicked;

            helper.Events.Content.AssetRequested += Content_AssetRequested;

#if DEBUG
            helper.Events.Input.ButtonsChanged += (sender, args) =>
            {
                if(KeybindList.Parse("F").JustPressed())
                {
                    SMonitor.Log($"Valid: {CanNPCStandHere(null, Game1.player.currentLocation, Game1.player.Tile)}");
                }
            };
#endif

            PathFindControllerPatches.Initialize(Monitor, Config, helper);
            Divorce.Initialize(Monitor, Config, helper);
            NPCPatches.Initialize(Monitor, Config, helper);
            Game1Patches.Initialize(Monitor);
            LocationPatches.Initialize(Monitor, Config, helper);
            FarmerPatches.Initialize(Monitor, Config, helper);
            UIPatches.Initialize(Monitor, Config, helper);
            EventPatches.Initialize(Monitor, Config, helper);

            var harmony = new Harmony(ModManifest.UniqueID);


            // npc patches

            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.marriageDuties)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_marriageDuties_Prefix)),
               postfix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_marriageDuties_Postfix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.getSpouse)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_getSpouse_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.isRoommate)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_isRoommate_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.isMarried)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_isMarried_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.isMarriedOrEngaged)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_isMarriedOrEngaged_Prefix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.tryToReceiveActiveObject)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_tryToReceiveActiveObject_Prefix)),
               transpiler: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_tryToReceiveActiveObject_Transpiler))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), "engagementResponse"),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_engagementResponse_Prefix)),
               postfix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_engagementResponse_Postfix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.spouseObstacleCheck)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_spouseObstacleCheck_Prefix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.setUpForOutdoorPatioActivity)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_setUpForOutdoorPatioActivity_Prefix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.playSleepingAnimation)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_playSleepingAnimation_Prefix)),
               postfix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_playSleepingAnimation_Postfix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.GetDispositionModifiedString)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_GetDispositionModifiedString_Prefix)),
               postfix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_GetDispositionModifiedString_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), "loadCurrentDialogue"),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_loadCurrentDialogue_Prefix)),
               postfix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_loadCurrentDialogue_Postfix))
            );
            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.tryToRetrieveDialogue)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_tryToRetrieveDialogue_Prefix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
               prefix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_checkAction_Prefix)),
               postfix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_checkAction_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.isAdoptionSpouse)),
               transpiler: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.NPC_isAdoptionSpouse_Transpiler))
            );

            // Child patches

            harmony.Patch(
               original: typeof(Character).GetProperty("displayName").GetMethod,
               postfix: new HarmonyMethod(typeof(NPCPatches), nameof(NPCPatches.Character_displayName_Getter_Postfix))
            );

            // Path patches
            
            harmony.Patch(
               original: AccessTools.Constructor(typeof(PathFindController), new Type[] { typeof(Character), typeof(GameLocation), typeof(Point), typeof(int), typeof(bool) }),
               prefix: new HarmonyMethod(typeof(PathFindControllerPatches), nameof(PathFindControllerPatches.PathFindController_Prefix))
            );
            harmony.Patch(
               original: AccessTools.Constructor(typeof(PathFindController), new Type[] { typeof(Character), typeof(GameLocation), typeof(Point), typeof(int), typeof(PathFindController.endBehavior) }),
               prefix: new HarmonyMethod(typeof(PathFindControllerPatches), nameof(PathFindControllerPatches.PathFindController_Prefix))
            );
            harmony.Patch(
               original: AccessTools.Constructor(typeof(PathFindController), new Type[] { typeof(Character), typeof(GameLocation), typeof(Point), typeof(int), typeof(PathFindController.endBehavior), typeof(int) }),
               prefix: new HarmonyMethod(typeof(PathFindControllerPatches), nameof(PathFindControllerPatches.PathFindController_Prefix))
            );
            harmony.Patch(
               original: AccessTools.Constructor(typeof(PathFindController), new Type[] { typeof(Character), typeof(GameLocation), typeof(Point), typeof(int) }),
               prefix: new HarmonyMethod(typeof(PathFindControllerPatches), nameof(PathFindControllerPatches.PathFindController_Prefix))
            );
            

            // Location patches
            
            harmony.Patch(
               original: AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.GetSpouseBed)),
               postfix: new HarmonyMethod(typeof(LocationPatches), nameof(LocationPatches.FarmHouse_GetSpouseBed_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.getSpouseBedSpot)),
               prefix: new HarmonyMethod(typeof(LocationPatches), nameof(LocationPatches.FarmHouse_getSpouseBedSpot_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(FarmHouse), nameof(FarmHouse.HasNpcSpouseOrRoommate), new Type[] {typeof(string)}),
               postfix: new HarmonyMethod(typeof(LocationPatches), nameof(LocationPatches.FarmHouse_HasNPCSpouseOrRoommate_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Beach), nameof(Beach.checkAction)),
               prefix: new HarmonyMethod(typeof(LocationPatches), nameof(LocationPatches.Beach_checkAction_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Beach), "resetLocalState"),
               postfix: new HarmonyMethod(typeof(LocationPatches), nameof(LocationPatches.Beach_resetLocalState_Postfix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), "checkEventPrecondition", new Type[] { typeof(string), typeof(bool) }),
               prefix: new HarmonyMethod(typeof(LocationPatches), nameof(LocationPatches.GameLocation_checkEventPrecondition_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(ManorHouse), nameof(ManorHouse.performAction), new Type[] { typeof(string[]), typeof(Farmer), typeof(Location)  }),
               prefix: new HarmonyMethod(typeof(LocationPatches), nameof(LocationPatches.ManorHouse_performAction_Prefix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(ManorHouse), nameof(ManorHouse.answerDialogueAction)),
               prefix: new HarmonyMethod(typeof(LocationPatches), nameof(LocationPatches.ManorHouse_answerDialogueAction_Prefix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.answerDialogueAction)),
               prefix: new HarmonyMethod(typeof(LocationPatches), nameof(LocationPatches.GameLocation_answerDialogueAction_Prefix))
            );


            // pregnancy patches

            harmony.Patch(
               original: AccessTools.Method(typeof(Utility), nameof(Utility.pickPersonalFarmEvent)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Utility_pickPersonalFarmEvent_Prefix))
            );
            harmony.Patch(
               original: AccessTools.Method(typeof(QuestionEvent), nameof(QuestionEvent.setUp)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.QuestionEvent_setUp_Prefix)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.QuestionEvent_setUp_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(QuestionEvent), "answerPlayerPregnancyQuestion"),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.QuestionEvent_answerPregnancyQuestion_Prefix)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.QuestionEvent_answerPregnancyQuestion_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(BirthingEvent), nameof(BirthingEvent.setUp)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.BirthingEvent_setUp_Prefix)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.BirthingEvent_setUp_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(BirthingEvent), nameof(BirthingEvent.tickUpdate)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.BirthingEvent_tickUpdate_Prefix))
            );


            // Farmer patches

            harmony.Patch(
               original: AccessTools.Method(typeof(Farmer), nameof(Farmer.doDivorce)),
               prefix: new HarmonyMethod(typeof(FarmerPatches), nameof(FarmerPatches.Farmer_doDivorce_Prefix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(Farmer), nameof(Farmer.isMarriedOrRoommates)),
               prefix: new HarmonyMethod(typeof(FarmerPatches), nameof(FarmerPatches.Farmer_isMarried_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Farmer), nameof(Farmer.getSpouse)),
               postfix: new HarmonyMethod(typeof(FarmerPatches), nameof(FarmerPatches.Farmer_getSpouse_Postfix))
            );
            harmony.Patch(
               original: AccessTools.PropertyGetter(typeof(Farmer), nameof(Farmer.spouse)),
               postfix: new HarmonyMethod(typeof(FarmerPatches), nameof(FarmerPatches.Farmer_spouse_Postfix))
            );
            harmony.Patch(
               original: AccessTools.Method(typeof(Farmer), nameof(Farmer.GetSpouseFriendship)),
               prefix: new HarmonyMethod(typeof(FarmerPatches), nameof(FarmerPatches.Farmer_GetSpouseFriendship_Prefix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(Farmer), nameof(Farmer.checkAction)),
               prefix: new HarmonyMethod(typeof(FarmerPatches), nameof(FarmerPatches.Farmer_checkAction_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Farmer), nameof(Farmer.getChildren)),
               prefix: new HarmonyMethod(typeof(FarmerPatches), nameof(FarmerPatches.Farmer_getChildren_Prefix))
            );


            // UI patches

            harmony.Patch(
               original: AccessTools.Method(typeof(SocialPage), "drawNPCSlot"),
               prefix: new HarmonyMethod(typeof(UIPatches), nameof(UIPatches.SocialPage_drawNPCSlot_prefix)),
               transpiler: new HarmonyMethod(typeof(UIPatches), nameof(UIPatches.SocialPage_drawSlot_transpiler))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(SocialPage), "drawFarmerSlot"),
               transpiler: new HarmonyMethod(typeof(UIPatches), nameof(UIPatches.SocialPage_drawSlot_transpiler))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(SocialPage.SocialEntry), nameof(SocialPage.SocialEntry.IsMarriedToAnyone)),
               prefix: new HarmonyMethod(typeof(UIPatches), nameof(UIPatches.SocialPage_isMarriedToAnyone_Prefix))
            );

            harmony.Patch(
               original: typeof(DialogueBox).GetConstructor(new Type[] { typeof(List<string>) }),
               prefix: new HarmonyMethod(typeof(UIPatches), nameof(UIPatches.DialogueBox_Prefix))
            );


            // Event patches

            harmony.Patch(
               original: AccessTools.Method(typeof(Event), nameof(Event.answerDialogueQuestion)),
               prefix: new HarmonyMethod(typeof(EventPatches), nameof(EventPatches.Event_answerDialogueQuestion_Prefix)),
               postfix: new HarmonyMethod(typeof(EventPatches), nameof(EventPatches.Event_answerDialogueQuestion_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Event.DefaultCommands), nameof(Event.DefaultCommands.LoadActors)),
               transpiler: new HarmonyMethod(typeof(EventPatches), nameof(EventPatches.Event_DefaultCommands_LoadActors_Transpiler))
            );


            // Game1 patches

            harmony.Patch(
               original: AccessTools.Method(typeof(Event.DefaultCommands), nameof(Event.DefaultCommands.Dump)),
               postfix: new HarmonyMethod(typeof(EventPatches), nameof(EventPatches.Event_DefaultCommands_Dump_Postfix))
            );

        }

        public override object GetApi()
        {
            return new FreeLoveAPI();
        }

        private void Content_AssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!Config.EnableMod)
                return;
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            {
                e.Edit(delegate (IAssetData data)
                {
                    var dict = data.AsDictionary<string, ShopData>();
                    try
                    {
                        for (int i = 0; i < dict.Data["DesertTrade"].Items.Count; i++)
                        {
                            if (dict.Data["DesertTrade"].Items[i].ItemId == "(O)808") // Void ghost pendant (for asking Krobus to be roommate)
                                dict.Data["DesertTrade"].Items[i].Condition = "PLAYER_FARMHOUSE_UPGRADE Current 1, !PLAYER_HAS_ITEM Current 808"; // Removes conditions for a number of hearts or the player not to be married
                        }
                    }
                    catch
                    {

                    }
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Events/HaleyHouse")) // Edit female group 10 heart event 
            {
                e.Edit(delegate (IAssetData idata)
                {
                    IDictionary<string, string> data = idata.AsDictionary<string, string>().Data;

                    string key = "195012/f Haley 2500/f Emily 2500/f Penny 2500/f Abigail 2500/f Leah 2500/f Maru 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 38/e 2123343/e 10/e 901756/e 54/e 15/k 195019";
                    if (data.TryGetValue(key, out string value))
                    {
                        data[key] = Regex.Replace(value, "(pause 1000/speak Maru \\\")[^$]+.a\\\"", $"$1{SHelper.Translation.Get("confrontation-female")}$h\"/emote Haley 21 true/emote Emily 21 true/emote Penny 21 true/emote Maru 21 true/emote Leah 21 true/emote Abigail 21").Replace("/dump girls 3", "");
                        //data["91740942"] = data["195012/f Haley 2500/f Emily 2500/f Penny 2500/f Abigail 2500/f Leah 2500/f Maru 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 38/e 2123343/e 10/e 901756/e 54/e 15/k 195019"];
                    }
                    key = "195012/f Olivia 2500/f Sophia 2500/f Claire 2500/f Haley 2500/f Emily 2500/f Penny 2500/f Abigail 2500/f Leah 2500/f Maru 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 38/e 2123343/e 10/e 901756/e 54/e 15/k 195019";
                    if (data.TryGetValue(key, out value))
                    {
                        data[key] = Regex.Replace(value, "(pause 1000/speak Maru \\\")[^$]+.a\\\"", $"$1{SHelper.Translation.Get("confrontation-female")}$h\"/emote Haley 21 true/emote Emily 21 true/emote Penny 21 true/emote Maru 21 true/emote Leah 21 true/emote Abigail 21").Replace("/dump girls 3", "");
                        //data["91740942"] = data["195012/f Haley 2500/f Emily 2500/f Penny 2500/f Abigail 2500/f Leah 2500/f Maru 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 38/e 2123343/e 10/e 901756/e 54/e 15/k 195019"];
                    }

                    if (data.TryGetValue("choseToExplain", out value))
                    {
                        data["choseToExplain"] = Regex.Replace(value, "(pause 1000/speak Maru \\\")[^$]+.a\\\"", $"$1{SHelper.Translation.Get("confrontation-female")}$h\"/emote Haley 21 true/emote Emily 21 true/emote Penny 21 true/emote Maru 21 true/emote Leah 21 true/emote Abigail 21").Replace("/dump girls 4", "");
                    }
                    if (data.TryGetValue("lifestyleChoice", out value))
                    {
                        data["lifestyleChoice"] = Regex.Replace(value, "(pause 1000/speak Maru \\\")[^$]+.a\\\"", $"$1{SHelper.Translation.Get("confrontation-female")}$h\"/emote Haley 21 true/emote Emily 21 true/emote Penny 21 true/emote Maru 21 true/emote Leah 21 true/emote Abigail 21").Replace("/dump girls 4", "");
                    }

                });

            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Events/Saloon")) // Edit male group 10 heart event 
            {
                e.Edit(delegate (IAssetData idata)
                {
                    IDictionary<string, string> data = idata.AsDictionary<string, string>().Data;
                    string key = "195013/f Shane 2500/f Sebastian 2500/f Sam 2500/f Harvey 2500/f Alex 2500/f Elliott 2500/o Abigail/o Penny/o Leah/o Emily/o Maru/o Haley/o Shane/o Harvey/o Sebastian/o Sam/o Elliott/o Alex/e 911526/e 528052/e 9581348/e 43/e 384882/e 233104/k 195099";
                    if (!data.TryGetValue(key, out string value))
                    {
                        Monitor.Log("Missing event key for Saloon!");
                        return;
                    }
                    data[key] = Regex.Replace(value, "(pause 1000/speak Sam \\\")[^$]+.a\\\"", $"$1{SHelper.Translation.Get("confrontation-male")}$h\"/emote Shane 21 true/emote Sebastian 21 true/emote Sam 21 true/emote Harvey 21 true/emote Alex 21 true/emote Elliott 21").Replace("/dump guys 3", "");
                    //data["19501342"] = Regex.Replace(aData, "(pause 1000/speak Sam \\\")[^$]+.a\\\"",$"$1{SHelper.Translation.Get("confrontation-male")}$h\"/emote Shane 21 true/emote Sebastian 21 true/emote Sam 21 true/emote Harvey 21 true/emote Alex 21 true/emote Elliott 21").Replace("/dump guys 3", "");
                    if (data.TryGetValue("choseToExplain", out value))
                    {
                        data["choseToExplain"] = Regex.Replace(value, "(pause 1000/speak Sam \\\")[^$]+.a\\\"", $"$1{SHelper.Translation.Get("confrontation-male")}$h\"/emote Shane 21 true/emote Sebastian 21 true/emote Sam 21 true/emote Harvey 21 true/emote Alex 21 true/emote Elliott 21").Replace("/dump guys 4", "");
                    }
                    if (data.TryGetValue("crying", out value))
                    {
                        data["crying"] = Regex.Replace(value, "(pause 1000/speak Sam \\\")[^$]+.a\\\"", $"$1{SHelper.Translation.Get("confrontation-male")}$h\"/emote Shane 21 true/emote Sebastian 21 true/emote Sam 21 true/emote Harvey 21 true/emote Alex 21 true/emote Elliott 21").Replace("/dump guys 4", "");
                    }
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Strings/StringsFromCSFiles"))
            {
                e.Edit(delegate (IAssetData idata)
                {
                    // NPC.cs.3985 is where they are mad at you for giving someone else a gift
                    // ResourceCollectionQuest.cs.13681 is "Wonderful!"
                    // ResourceCollectionQuest.cs.13683 is "Thank You!"
                    // Basically just changes it to "I heard you gave X a gift; Wonderful! Thank You!"

                    IDictionary<string, string> data = idata.AsDictionary<string, string>().Data;
                    data["NPC.cs.3985"] = Regex.Replace(data["NPC.cs.3985"], @"\.\.\.\$s.+", $"$n#$b#$c 0.5#{data["ResourceCollectionQuest.cs.13681"]}#{data["ResourceCollectionQuest.cs.13683"]}");
                    Monitor.Log($"NPC.cs.3985 is set to \"{data["NPC.cs.3985"]}\"");
                });

            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/animationDescriptions"))
            {
                // for all animation data that end with _Sleep, add the same data but lowercase too
                e.Edit(delegate (IAssetData idata)
                {
                    IDictionary<string, string> data = idata.AsDictionary<string, string>().Data;
                    List<string> sleepKeys = data.Keys.ToList().FindAll((s) => s.EndsWith("_Sleep"));
                    foreach (string key in sleepKeys)
                    {
                        if (!data.ContainsKey(key.ToLower()))
                        {
                            Monitor.Log($"adding {key.ToLower()} to animationDescriptions");
                            data.Add(key.ToLower(), data[key]);
                        }
                    }
                });

            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/EngagementDialogue"))
            {
                if (!Config.RomanceAllVillagers)
                    return;
                // Add engagement dialogue for all non-romanceable villagers.
                e.Edit(delegate (IAssetData idata)
                {
                    IDictionary<string, string> data = idata.AsDictionary<string, string>().Data;
                    Farmer f = Game1.player;
                    if (f is null)
                    {
                        return;
                    }
                    foreach (string friend in f.friendshipData.Keys)
                    {
                        if (!data.ContainsKey(friend + "0"))
                        {
                            data[friend + "0"] = "";
                        }
                        if (!data.ContainsKey(friend + "1"))
                        {
                            data[friend + "1"] = "";
                        }
                    }
                });
            }
            else if (Config.RomanceAllVillagers && (e.NameWithoutLocale.StartsWith("Characters/schedules/")))
            {
                try
                {
                    // foreach item in the npc's schedule, if they don't have a marriage version of that item, add one
                    // TODO: Figure out why? (at least for npcs that are already romanceable); I suspect that it is supposed to check !Game1.characterData[npc.Name].CanBeRomanced
                    string name = e.NameWithoutLocale.BaseName.Replace("Characters/schedules/", "").Replace("Characters\\schedules\\", "");
                    NPC npc = Game1.getCharacterFromName(name);
                    if (npc != null && npc.Age < 2 && npc is not Child)
                    {
                        
                        if (Game1.characterData[npc.Name].CanBeRomanced)
                        {
                            Monitor.Log($"can edit schedule for {name}");
                            e.Edit(delegate (IAssetData idata)
                            {
                                IDictionary<string, string> data = idata.AsDictionary<string, string>().Data;
                                foreach (string key in data.Keys)
                                {
                                    if (!data.ContainsKey($"marriage_{key}"))
                                        data[$"marriage_{key}"] = data[key];
                                }
                            });
                        }
                    }
                }
                catch
                {
                }


            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Strings/Locations"))
            {
                // Change the pendant price to the config value
                e.Edit(delegate (IAssetData idata)
                {
                    IDictionary<string, string> data = idata.AsDictionary<string, string>().Data;
                    data["Beach_Mariner_PlayerBuyItem_AnswerYes"] = data["Beach_Mariner_PlayerBuyItem_AnswerYes"].Replace("5000", Config.PendantPrice.ToString());
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Characters"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, CharacterData>().Data;
                    foreach(var kvp in data)
                    {
                        kvp.Value.SpouseGiftJealousy = "FALSE";
                        if(Config.RomanceAllVillagers && kvp.Value.Age is NpcAge.Adult or NpcAge.Teen)
                        {

                        }
                    }
                }, AssetEditPriority.Late);
            }
        }
    }
}