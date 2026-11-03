using System;
using System.Globalization;
using System.IO;
using Unravel.Core;

namespace Sample2048
{
    /// <summary>The best score, kept in the player's data folder (Application.persistentDataPath) between sessions.</summary>
    public static class SaveData
    {
        private static string FilePath => Path.Combine(Application.persistentDataPath, "best.txt");

        public static long LoadBest()
        {
            try
            {
                return File.Exists(FilePath) && long.TryParse(File.ReadAllText(FilePath), NumberStyles.Integer, CultureInfo.InvariantCulture, out long best)
                           ? best
                           : 0;
            }
            catch (Exception e)
            {
                Log.Warning($"Could not read the best score: {e.Message}");
                return 0;
            }
        }

        public static void SaveBest(long best)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, best.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                Log.Warning($"Could not save the best score: {e.Message}");
            }
        }
    }
}
