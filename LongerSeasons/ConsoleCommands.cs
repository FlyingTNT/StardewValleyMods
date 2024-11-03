using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using System.Threading;

namespace LongerSeasons
{
    partial class ModEntry
    {
        private void SetupConsoleCommands()
        {
            SHelper.ConsoleCommands.Add("ls_setmonth", "Sets the current season's month.\n\nUsage: ls_setmonth <value>\n- value: a number 1-(the number of months in the config).", SetMonth);
            SHelper.ConsoleCommands.Add("ls_setday", "Sets the current day.\n\nUsage: ls_setday <value>\n- value: a number 1-(the number of days in the config).", SetDay);
            SHelper.ConsoleCommands.Add("ls_dat", "Days after today.\n\nUsage: ls_dat <value>\n- value: a number at least 0", DaysAfterToday);
        }

        private void SetMonth(string command, string[] args)
        {
            if(!Context.IsWorldReady)
            {
                SMonitor.Log("This command cannot be used until a save is loaded.", LogLevel.Info);
                return;
            }

            if(!currentSeasonMonth.IsReady && !Context.IsOnHostComputer)
            {
                SMonitor.Log("The data has not been synced with the host yet.", LogLevel.Info);
                return;
            }

            if (!ArgUtility.TryGetInt(args, 0, out int month, out string error))
            {
                SMonitor.Log($"Invalid argument: {error}", LogLevel.Info);
                return;
            }

            if(month < 0 || month > GetMonthsInCurrentSeason())
            {
                SMonitor.Log($"The month must be between 0 and {GetMonthsInCurrentSeason()}.", LogLevel.Info);
                return;
            }

            CurrentSeasonMonth = month;
            SMonitor.Log($"The current season month is now {Game1.currentSeason} {CurrentSeasonMonth}", LogLevel.Info);
        }

        private void SetDay(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                SMonitor.Log("This command cannot be used until a save is loaded.", LogLevel.Info);
                return;
            }

            if (!ArgUtility.TryGetInt(args, 0, out int day, out string error))
            {
                SMonitor.Log($"Invalid argument: {error}", LogLevel.Info);
                return;
            }

            if(day < 1 || day > DaysPerMonth)
            {
                SMonitor.Log($"Invalid day {day}; the day must be between 1 and {DaysPerMonth}, inclusive.", LogLevel.Info);
                return;
            }

            // The below is just copied from the Console Commands mod.
            Game1.dayOfMonth = day;
            Game1.stats.DaysPlayed = (uint)SDate.Now().DaysSinceStart;
            SMonitor.Log($"OK, the date is now {Game1.currentSeason} {Game1.dayOfMonth}.", LogLevel.Info);
            SHelper.GameContent.InvalidateCache("LooseSprites/Billboard");
        }

        private void DaysAfterToday(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                SMonitor.Log("This command cannot be used until a save is loaded.", LogLevel.Info);
                return;
            }

            if (!ArgUtility.TryGetInt(args, 0, out int days, out string error))
            {
                SMonitor.Log($"Invalid argument: {error}", LogLevel.Info);
                return;
            }

            WorldDate newDay = new WorldDate();
            ApplyDayOffset(WorldDate.Now(), CurrentSeasonMonth, days, newDay);

            SMonitor.Log($"It will be {newDay} in {days} days.", LogLevel.Info);
        }
    }
}
