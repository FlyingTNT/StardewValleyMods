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

            cursorLoc = Utility.Vector2ToPoint(Game1.GetPlacementGrabTile());
            var pairs = Game1.player.friendshipData.Pairs.Where(s => s.Value.IsMarried());
            if (!pairs.Any())
            {
                Monitor.Log("You don't have any spouses.", LogLevel.Warn);
                return;
            }

            noCustomAreaSpouses.Clear();
            foreach (KeyValuePair<string, Friendship> spouse in pairs)
            {
                if (!OutdoorAreas.ContainsKey(spouse.Key))
                    noCustomAreaSpouses.Add(spouse.Key);
            }

            List<Response> responses = new List<Response>();
            if (noCustomAreaSpouses.Any())
                responses.Add(new Response("CSP_Wizard_Questions_AddPatio", string.Format(SHelper.Translation.Get("new-patio"), cursorLoc.X, cursorLoc.Y)));
            if (OutdoorAreas.Any())
            {
                responses.Add(new Response("CSP_Wizard_Questions_RemovePatio", SHelper.Translation.Get("remove-patio")));
                responses.Add(new Response("CSP_Wizard_Questions_MovePatio", SHelper.Translation.Get("move-patio")));
                responses.Add(new Response("CSP_Wizard_Questions_ListPatios", SHelper.Translation.Get("list-patios")));
            }
            responses.Add(new Response("CSP_Wizard_Questions_ReloadPatios", SHelper.Translation.Get("reload-patios")));
            responses.Add(new Response("cancel", SHelper.Translation.Get("cancel")));
            Game1.player.currentLocation.createQuestionDialogue(SHelper.Translation.Get("welcome"), responses.ToArray(), "CSP_Wizard_Questions");
        }


        public static void CSPWizardDialogue(string whichQuestion, string whichAnswer)
        {
            SMonitor.Log($"question: {whichQuestion}, answer: {whichAnswer}");
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
                                Game1.drawObjectDialogue(string.Format(SHelper.Translation.Get("cursor-out-of-bounds"), cursorLoc.X, cursorLoc.Y));
                                return;
                            }
                            header = SHelper.Translation.Get("new-patio-which");
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
                            header = SHelper.Translation.Get("move-patio-which");
                            if (currentPage > 0)
                                responses.Add(new Response("last", "..."));
                            foreach (string spouse in OutdoorAreas.Keys.Skip(currentPage * Config.MaxSpousesPerPage).Take(Config.MaxSpousesPerPage))
                            {
                                responses.Add(new Response(spouse, spouse));
                            }
                            if (OutdoorAreas.Keys.Count > (currentPage + 1) * Config.MaxSpousesPerPage)
                                responses.Add(new Response("next", "..."));
                            break;
                        case "CSP_Wizard_Questions_RemovePatio":
                            header = SHelper.Translation.Get("remove-patio-which");
                            if (currentPage > 0)
                                responses.Add(new Response("last", "..."));
                            foreach (string spouse in OutdoorAreas.Keys.Skip(currentPage * Config.MaxSpousesPerPage).Take(Config.MaxSpousesPerPage))
                            {
                                responses.Add(new Response(spouse, spouse));
                            }
                            if (OutdoorAreas.Keys.Count > (currentPage + 1) * Config.MaxSpousesPerPage)
                                responses.Add(new Response("next", "..."));
                            break;
                        case "CSP_Wizard_Questions_ListPatios":
                            Game1.drawObjectDialogue(string.Format(SHelper.Translation.Get("patios-exist-for"), string.Join(", ", OutdoorAreas.Keys)));
                            return;
                        case "CSP_Wizard_Questions_ReloadPatios":
                            if (!Context.IsMainPlayer)
                                return;

                            // Remove all the patios and tell all farmhands to do the same
                            foreach(var kvp in OutdoorAreas)
                            {
                                RemoveSpousePatio(kvp.Key);
                                SendModMessage(kvp.Key, "", Vector2.Zero, PatioChange.Remove);
                            }

                            OutdoorAreas.Clear();

                            // Reload OutdoorAreas from the save data (automatiacally caches the base tiles)
                            LoadSpouseAreaData();

                            // Tell all farmhands to re-add the patios
                            foreach (var kvp in OutdoorAreas)
                            {
                                SendModMessage(kvp.Key, kvp.Value.location, kvp.Value.corner, PatioChange.Add);
                            }

                            // Reload all patios (has the concequence of adding them all back)
                            Game1.getFarm().UpdatePatio();
                            Game1.drawObjectDialogue(string.Format(SHelper.Translation.Get("reloaded-patios"), OutdoorAreas.Count));
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
                        Game1.drawObjectDialogue(string.Format(SHelper.Translation.Get("cursor-out-of-bounds"), cursorLoc.X, cursorLoc.Y));
                        return;
                    }
                    AddSpousePatio(whichAnswer, Game1.player.currentLocation.Name, cursorLoc.ToVector2());
                    SendModMessage(whichAnswer, Game1.player.currentLocation.Name, cursorLoc.ToVector2(), PatioChange.Add);

                    Game1.drawObjectDialogue(string.Format(SHelper.Translation.Get("created-patio"), cursorLoc.X, cursorLoc.Y));
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
                    header = SHelper.Translation.Get("move-patio-which-way");
                    newQuestion = "CSP_Wizard_Questions_MovePatio_2";
                    responses.Add(new Response($"{whichAnswer}_cursorLoc", string.Format(SHelper.Translation.Get("cursor-location"), cursorLoc.X, cursorLoc.Y)));
                    responses.Add(new Response($"{whichAnswer}_up", SHelper.Translation.Get("up")));
                    responses.Add(new Response($"{whichAnswer}_down", SHelper.Translation.Get("down")));
                    responses.Add(new Response($"{whichAnswer}_left", SHelper.Translation.Get("left")));
                    responses.Add(new Response($"{whichAnswer}_right", SHelper.Translation.Get("right")));
                    break;
                case "CSP_Wizard_Questions_MovePatio_2":
                    if (MoveSpousePatio(whichAnswer, cursorLoc))
                        Game1.drawObjectDialogue(string.Format(SHelper.Translation.Get("moved-patio"), whichAnswer.Split('_')[0]));
                    else
                        Game1.drawObjectDialogue(string.Format(SHelper.Translation.Get("not-moved-patio"), whichAnswer.Split('_')[0]));
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
                    if (OutdoorAreas.ContainsKey(whichAnswer))
                    {
                        RemoveSpousePatio(whichAnswer);
                        SendModMessage(whichAnswer, "", Vector2.Zero, PatioChange.Remove);
                        Game1.drawObjectDialogue(string.Format(SHelper.Translation.Get("removed-patio"), whichAnswer));
                    }
                    else
                        Game1.drawObjectDialogue(string.Format(SHelper.Translation.Get("not-removed-patio"), whichAnswer));
                    return;
                default:
                    return;
            }
            responses.Add(new Response("cancel", SHelper.Translation.Get("cancel")));
            Game1.player.currentLocation.createQuestionDialogue($"{header}", responses.ToArray(), newQuestion);
        }

        public static bool MoveSpousePatio(string spouse_dir, Point cursorLoc)
        {
            string spouse = spouse_dir.Split('_')[0];
            string dir = spouse_dir.Split('_')[1];
            Vector2 outdoorArea = new(OutdoorAreas[spouse].corner.X, OutdoorAreas[spouse].corner.Y);
            string location = OutdoorAreas[spouse].location;
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

            MoveSpousePatio(spouse, location, outdoorArea);
            SendModMessage(spouse, location, outdoorArea, PatioChange.Move);
            return true;
        }
    }
}
