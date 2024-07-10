using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;
using System.Linq;
namespace CustomSpousePatioRedux
{
    partial class ModEntry
    {
        public static Point cursorLoc;

        public static int currentPage;

        public void StartWizard()
        {
            currentPage = 0;
            if (outdoorAreas == null)
            {
                Monitor.Log("Outdoor ares is null.", LogLevel.Warn);
                return;
            }

            cursorLoc = Utility.Vector2ToPoint(Game1.GetPlacementGrabTile());
            var pairs = Game1.player.friendshipData.Pairs.Where(s => s.Value.IsMarried());
            if (!pairs.Any())
            {
                Monitor.Log("You don't have any spouses.", LogLevel.Warn);
                return;
            }

            noCustomAreaSpouses = new List<string>();
            foreach (KeyValuePair<string, Friendship> spouse in pairs)
            {
                if (!outdoorAreas.dict.ContainsKey(spouse.Key))
                    noCustomAreaSpouses.Add(spouse.Key);
            }

            List<Response> responses = new List<Response>();
            if (noCustomAreaSpouses.Any())
                responses.Add(new Response("CSP_Wizard_Questions_AddPatio", string.Format(Helper.Translation.Get("new-patio"), cursorLoc.X, cursorLoc.Y)));
            if (outdoorAreas.dict.Any())
            {
                responses.Add(new Response("CSP_Wizard_Questions_RemovePatio", Helper.Translation.Get("remove-patio")));
                responses.Add(new Response("CSP_Wizard_Questions_MovePatio", Helper.Translation.Get("move-patio")));
                responses.Add(new Response("CSP_Wizard_Questions_ListPatios", Helper.Translation.Get("list-patios")));
            }
            responses.Add(new Response("CSP_Wizard_Questions_ReloadPatios", Helper.Translation.Get("reload-patios")));
            responses.Add(new Response("cancel", Helper.Translation.Get("cancel")));
            Game1.player.currentLocation.createQuestionDialogue(Helper.Translation.Get("welcome"), responses.ToArray(), "CSP_Wizard_Questions");
        }


        public void CSPWizardDialogue(string whichQuestion, string whichAnswer)
        {
            Monitor.Log($"question: {whichQuestion}, answer: {whichAnswer}");
            if (whichAnswer == "cancel")
                return;

            List<Response> responses = new List<Response>();
            string header = "";
            string newQuestion = whichAnswer;
            switch (whichQuestion)
            {
                case "CSP_Wizard_Questions":
                    switch (whichAnswer)
                    {
                        case "CSP_Wizard_Questions_AddPatio":
                            if (cursorLoc.X > Game1.player.currentLocation.map.Layers[0].LayerWidth - 4 || cursorLoc.Y > Game1.player.currentLocation.map.Layers[0].LayerWidth - 4)
                            {
                                Game1.drawObjectDialogue(string.Format(Helper.Translation.Get("cursor-out-of-bounds"), cursorLoc.X, cursorLoc.Y));
                                return;
                            }
                            header = Helper.Translation.Get("new-patio-which");
                            if (currentPage > 0)
                                responses.Add(new Response("last", "..."));
                            foreach (string spouse in noCustomAreaSpouses.Skip(currentPage * Config.MaxSpousesPerPage).Take(Config.MaxSpousesPerPage))
                            {
                                responses.Add(new Response(spouse, spouse));
                            }
                            if (noCustomAreaSpouses.Count > (currentPage + 1) * Config.MaxSpousesPerPage)
                                responses.Add(new Response("next", "..."));
                            break;
                        case "CSP_Wizard_Questions_MovePatio":
                            header = Helper.Translation.Get("move-patio-which");
                            if (currentPage > 0)
                                responses.Add(new Response("last", "..."));
                            foreach (string spouse in outdoorAreas.dict.Keys.Skip(currentPage * Config.MaxSpousesPerPage).Take(Config.MaxSpousesPerPage))
                            {
                                responses.Add(new Response(spouse, spouse));
                            }
                            if (outdoorAreas.dict.Keys.Count > (currentPage + 1) * Config.MaxSpousesPerPage)
                                responses.Add(new Response("next", "..."));
                            break;
                        case "CSP_Wizard_Questions_RemovePatio":
                            header = Helper.Translation.Get("remove-patio-which");
                            if (currentPage > 0)
                                responses.Add(new Response("last", "..."));
                            foreach (string spouse in outdoorAreas.dict.Keys.Skip(currentPage * Config.MaxSpousesPerPage).Take(Config.MaxSpousesPerPage))
                            {
                                responses.Add(new Response(spouse, spouse));
                            }
                            if (outdoorAreas.dict.Keys.Count > (currentPage + 1) * Config.MaxSpousesPerPage)
                                responses.Add(new Response("next", "..."));
                            break;
                        case "CSP_Wizard_Questions_ListPatios":
                            Game1.drawObjectDialogue(string.Format(Helper.Translation.Get("patios-exist-for"), string.Join(", ", outdoorAreas.dict.Keys)));
                            return;
                        case "CSP_Wizard_Questions_ReloadPatios":
                            OutdoorAreaData reloadedData = Helper.Data.ReadSaveData<OutdoorAreaData>(saveKey);
                            if (reloadedData != null)
                            {
                                outdoorAreas = reloadedData;
                            }
                            else
                            {
                                Monitor.Log("Could not load data from the save file! Using old data.", LogLevel.Debug);
                            }
                            Game1.getFarm().UpdatePatio();
                            Game1.drawObjectDialogue(string.Format(Helper.Translation.Get("reloaded-patios"), outdoorAreas.dict.Count));
                            return;
                    }
                    break;
                case "CSP_Wizard_Questions_AddPatio":
                    if (whichAnswer == "next")
                    {
                        currentPage++;
                        CSPWizardDialogue("CSP_Wizard_Questions", "CSP_Wizard_Questions_AddPatio");
                        return;
                    }
                    if (whichAnswer == "last")
                    {
                        currentPage--;
                        CSPWizardDialogue("CSP_Wizard_Questions", "CSP_Wizard_Questions_AddPatio");
                        return;
                    }
                    if (cursorLoc.X > Game1.player.currentLocation.map.Layers[0].LayerWidth - 4 || cursorLoc.Y > Game1.player.currentLocation.map.Layers[0].LayerHeight - 4)
                    {
                        Game1.drawObjectDialogue(string.Format(Helper.Translation.Get("cursor-out-of-bounds"), cursorLoc.X, cursorLoc.Y));
                        return;
                    }
                    ReapplyBasePatioArea();
                    if (AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(Game1.getFarm(), "_appliedMapOverrides").Contains("spouse_patio"))
                    {
                        AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(Game1.getFarm(), "_appliedMapOverrides").Remove("spouse_patio");
                    }
                    if (AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(Game1.getFarm(), "_appliedMapOverrides").Contains(whichAnswer + "_spouse_patio"))
                    {
                        AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(Game1.getFarm(), "_appliedMapOverrides").Remove(whichAnswer + "_spouse_patio");
                    }
                    outdoorAreas.dict[whichAnswer] = new OutdoorArea() { location = Game1.player.currentLocation.Name, corner = cursorLoc.ToVector2() };
                    CacheOffBasePatioArea(whichAnswer);
                    Game1.getFarm().UpdatePatio();
                    if (Game1.getCharacterFromName(whichAnswer)?.shouldPlaySpousePatioAnimation.Value == true)
                    {
                        Game1.getCharacterFromName(whichAnswer).setUpForOutdoorPatioActivity();
                    }

                    Game1.drawObjectDialogue(string.Format(Helper.Translation.Get("created-patio"), cursorLoc.X, cursorLoc.Y));
                    return;
                case "CSP_Wizard_Questions_MovePatio":
                    if (whichAnswer == "next")
                    {
                        currentPage++;
                        CSPWizardDialogue("CSP_Wizard_Questions", "CSP_Wizard_Questions_MovePatio");
                        return;
                    }
                    if (whichAnswer == "last")
                    {
                        currentPage--;
                        CSPWizardDialogue("CSP_Wizard_Questions", "CSP_Wizard_Questions_MovePatio");
                        return;
                    }
                    header = Helper.Translation.Get("move-patio-which-way");
                    newQuestion = "CSP_Wizard_Questions_MovePatio_2";
                    responses.Add(new Response($"{whichAnswer}_cursorLoc", string.Format(Helper.Translation.Get("cursor-location"), cursorLoc.X, cursorLoc.Y)));
                    responses.Add(new Response($"{whichAnswer}_up", Helper.Translation.Get("up")));
                    responses.Add(new Response($"{whichAnswer}_down", Helper.Translation.Get("down")));
                    responses.Add(new Response($"{whichAnswer}_left", Helper.Translation.Get("left")));
                    responses.Add(new Response($"{whichAnswer}_right", Helper.Translation.Get("right")));
                    break;
                case "CSP_Wizard_Questions_MovePatio_2":
                    if (MoveSpousePatio(whichAnswer, cursorLoc))
                        Game1.drawObjectDialogue(string.Format(Helper.Translation.Get("moved-patio"), whichAnswer.Split('_')[0]));
                    else
                        Game1.drawObjectDialogue(string.Format(Helper.Translation.Get("not-moved-patio"), whichAnswer.Split('_')[0]));
                    return;
                case "CSP_Wizard_Questions_RemovePatio":
                    if (whichAnswer == "next")
                    {
                        currentPage++;
                        CSPWizardDialogue("CSP_Wizard_Questions", "CSP_Wizard_Questions_RemovePatio");
                        return;
                    }
                    if (whichAnswer == "last")
                    {
                        currentPage--;
                        CSPWizardDialogue("CSP_Wizard_Questions", "CSP_Wizard_Questions_RemovePatio");
                        return;
                    }
                    if (outdoorAreas.dict.ContainsKey(whichAnswer))
                    {
                        ReapplyBasePatioArea(whichAnswer);
                        outdoorAreas.dict.Remove(whichAnswer);
                        baseSpouseAreaTiles.Remove(whichAnswer);
                        Game1.getFarm().UpdatePatio();
                        Game1.drawObjectDialogue(string.Format(Helper.Translation.Get("removed-patio"), whichAnswer));
                    }
                    else
                        Game1.drawObjectDialogue(string.Format(Helper.Translation.Get("not-removed-patio"), whichAnswer));
                    return;
                default:
                    return;
            }
            responses.Add(new Response("cancel", Helper.Translation.Get("cancel")));
            Game1.player.currentLocation.createQuestionDialogue($"{header}", responses.ToArray(), newQuestion);
        }

        public bool MoveSpousePatio(string spouse_dir, Point cursorLoc)
        {
            string spouse = spouse_dir.Split('_')[0];
            string dir = spouse_dir.Split('_')[1];
            Vector2 outdoorArea = outdoorAreas.dict[spouse].corner;
            string location = outdoorAreas.dict[spouse].location;
            switch (dir)
            {
                case "cursorLoc":
                    outdoorArea = cursorLoc.ToVector2();
                    location = Game1.player.currentLocation.Name;
                    break;
                case "up":
                    outdoorArea.Y--;
                    break;
                case "down":
                    outdoorArea.Y++;
                    break;
                case "left":
                    outdoorArea.X--;
                    break;
                case "right":
                    outdoorArea.X++;
                    break;
            }

            if (outdoorArea.X < 0 || outdoorArea.Y < 0 || outdoorArea.Y >= Game1.getFarm().map.Layers[0].LayerHeight - 4 || outdoorArea.X >= Game1.getFarm().map.Layers[0].LayerWidth - 4)
                return false;

            ReapplyBasePatioArea(spouse);
            outdoorAreas.dict[spouse].corner = outdoorArea;
            outdoorAreas.dict[spouse].location = location;
            SMonitor.Log($"Moved spouse patio for {spouse} to {outdoorArea}");
            CacheOffBasePatioArea(spouse);
            Game1.getFarm().UpdatePatio();
            if (Game1.getCharacterFromName(spouse)?.shouldPlaySpousePatioAnimation.Value == true)
            {
                Game1.getCharacterFromName(spouse).setUpForOutdoorPatioActivity();
            }
            return true;
        }
    }
}
