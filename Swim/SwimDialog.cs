using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Collections.Generic;

namespace Swim
{
    internal class SwimDialog
    {
        private static IMonitor SMonitor;
        private static ModConfig Config => ModEntry.Config;
        private static IModHelper SHelper;
        private static readonly PerScreen<List<int>> marinerQuestions = new(() => new());
        private static List<int> MarinerQuestions => marinerQuestions.Value;

        public static void Initialize(IMonitor monitor, IModHelper helper)
        {
            SMonitor = monitor;
            SHelper = helper;
        }

        internal static void OldMarinerDialogue(string whichAnswer)
        {
            string playerTerm = Game1.content.LoadString("Strings\\Locations:Beach_Mariner_Player_" + (Game1.player.IsMale ? "Male" : "Female"));
            SMonitor.Log("answer " + whichAnswer);
            if (whichAnswer == "SwimMod_Mariner_Questions_Yes")
            {
                CreateMarinerQuestions();
                TryGetTranslation(Game1.player.mailReceived.Contains("SwimMod_Mariner_Already") ? "SwimMod_Mariner_Questions_Yes_Old" : "SwimMod_Mariner_Questions_Yes", out string preface);
                Game1.player.mailReceived.Add("SwimMod_Mariner_Already");
                ShowNextQuestion(preface, 0);
            }
            else if (whichAnswer == "SwimMod_Mariner_Questions_No")
            {
                TryGetTranslation(Game1.player.mailReceived.Contains("SwimMod_Mariner_Already") ? "SwimMod_Mariner_Questions_No_Old" : "SwimMod_Mariner_Questions_No", out string preface);
                Game1.player.mailReceived.Add("SwimMod_Mariner_Already");
                Game1.drawObjectDialogue(preface);
            }
            else if (whichAnswer.StartsWith("SwimMod_Mariner_Question_"))
            {
                SMonitor.Log($"answered question {whichAnswer}");
                string[] keys = whichAnswer.Split('_');
                string preface = "";
                switch(keys[keys.Length-1])
                {
                    case "Y":
                        TryGetTranslation($"SwimMod_Mariner_Answer_Y_{keys[keys.Length - 3]}", out string preface1);
                        preface = string.Format(preface1, playerTerm);
                        break;
                    case "N":
                        TryGetTranslation("SwimMod_Mariner_Answer_N", out string preface2);
                        preface = string.Format(preface2, playerTerm);
                        Game1.drawObjectDialogue(preface);
                        ModEntry.marinerQuestionsWrongToday.Value = true;
                        return;
                    case "S":
                        TryGetTranslation("SwimMod_Mariner_Answer_S", out string preface3);
                        preface = string.Format(preface3, playerTerm);
                        Game1.drawObjectDialogue(preface);
                        ModEntry.marinerQuestionsWrongToday.Value = true;
                        return;
                }
                int next = int.Parse(keys[keys.Length - 2]) + 1;
                if(MarinerQuestions.Count > next)
                {
                    ShowNextQuestion(preface, next);
                }
                else
                {
                    CompleteEvent();
                }
            }
        }

        private static void CreateMarinerQuestions()
        {
            if (MarinerQuestions.Count == 0)
            {
                int i = 1;
                while (true)
                {
                    if (!TryGetTranslation($"SwimMod_Mariner_Question_{i}", out string _))
                        break;
                    MarinerQuestions.Add(i++);
                }
            }
            int n = MarinerQuestions.Count;
            while (n > 1)
            {
                n--;
                int k = ModEntry.myRand.Value.Next(n + 1);
                (MarinerQuestions[n], MarinerQuestions[k]) = (MarinerQuestions[k], MarinerQuestions[n]);
            }
        }

        private static void ShowNextQuestion(string preface, int index)
        {
            int qi = MarinerQuestions[index];
            if (!TryGetTranslation($"SwimMod_Mariner_Question_{qi}", out string s2))
            {
                SMonitor.Log("no dialogue: " + s2, LogLevel.Error);
                return;
            }
            //Monitor.Value.Log("has dialogue: " + s2.ToString());
            List<Response> responses = new List<Response>();
            int i = 1;
            while (true)
            {
                if (!TryGetTranslation($"SwimMod_Mariner_Question_{qi}_{i}", out string r))
                    break;
                string str = r.Split('#')[0];
                SMonitor.Log(str);

                responses.Add(new Response($"SwimMod_Mariner_Question_{qi}_{index}_{r.ToString().Split('#')[1]}", str));
                i++;
            }
            //Monitor.Value.Log($"next question: {preface}{s2}");
            Game1.player.currentLocation.createQuestionDialogue($"{preface}{s2}", responses.ToArray(), $"SwimMod_Mariner_Question_{qi}");
        }
        private static void CompleteEvent()
        {
            string playerTerm = Game1.content.LoadString("Strings\\Locations:Beach_Mariner_Player_" + (Game1.player.IsMale ? "Male" : "Female"));
            TryGetTranslation("SwimMod_Mariner_Completed", out string preface);
            Game1.drawObjectDialogue(string.Format(preface, playerTerm));
            Game1.stopMusicTrack(StardewValley.GameData.MusicContext.Default);
            Game1.playSound("Cowboy_Secret");
            Game1.player.mailReceived.Add("SwimMod_Mariner_Completed");
            Game1.player.currentLocation.resetForPlayerEntry();
            SwimMaps.AddScubaChest(Game1.player.currentLocation, new Vector2(10,6), "ScubaTank");
        }

        internal static bool TryGetTranslation(string key, out string translation)
        {
            bool hasCPTranslation = GetTranslationDictionary().TryGetValue(key, out var translation1);

            if (hasCPTranslation)
            {
                translation = translation1;
                return true;
            }

            Translation translation2 = SHelper.Translation.Get(key);

            translation = translation2;

            return translation2.HasValue();
        }

        private static Dictionary<string, string> GetTranslationDictionary()
        {
            return Game1.content.Load<Dictionary<string, string>>("Mods/FlyingTNT.Swim/i18n");
        }
    }
}