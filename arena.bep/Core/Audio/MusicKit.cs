using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.Core.Audio
{

    public class MusicKit
    {
        public string Name;
        public string BasePath;

        // key: Event Type, value: List of absolute file paths
        private Dictionary<MusicEvent, List<string>> _trackPaths;

        public MusicKit(string folderPath)
        {
            BasePath = folderPath;
            Name = new DirectoryInfo(folderPath).Name;
            _trackPaths = new Dictionary<MusicEvent, List<string>>();

            ScanFiles();
        }

        private void ScanFiles()
        {
            // Ensure all enum keys exist to avoid KeyNotFound exceptions
            foreach (MusicEvent me in System.Enum.GetValues(typeof(MusicEvent)))
            {
                _trackPaths[me] = new List<string>();
            }

            if (!Directory.Exists(BasePath))
            {
                Debug.LogError($"Music Kit not found at: {BasePath}");
                return;
            }

            string[] files = Directory.GetFiles(BasePath);

            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                string extension = Path.GetExtension(file).ToLower();

                // 1. Filter junk
                if (extension == ".vsnd" || extension == ".meta") continue;
                if (extension != ".mp3" && extension != ".wav" && extension != ".ogg") continue;

                // 2. Map filename to Enum
                // The order matters slightly (e.g. check "mainmenu" before generic checks if needed)

                if (fileName.StartsWith("mainmenu")) Add(MusicEvent.MainMenu, file);
                else if (fileName.StartsWith("startround")) Add(MusicEvent.RoundStart, file);
                else if (fileName.StartsWith("startaction")) Add(MusicEvent.StartAction, file);
                else if (fileName.StartsWith("roundmvpanthem")) Add(MusicEvent.MVP, file);
                else if (fileName.StartsWith("bombplanted")) Add(MusicEvent.BombPlanted, file);
                else if (fileName.StartsWith("bombtenseccount")) Add(MusicEvent.BombTenSecCount, file);
                else if (fileName.StartsWith("roundtenseccount")) Add(MusicEvent.RoundTenSecCount, file);
                else if (fileName.StartsWith("wonround")) Add(MusicEvent.WonRound, file);
                else if (fileName.StartsWith("lostround")) Add(MusicEvent.LostRound, file);
                else if (fileName.StartsWith("deathcam")) Add(MusicEvent.DeathCam, file);
                else if (fileName.StartsWith("chooseteam")) Add(MusicEvent.ChooseTeam, file);
                else if (fileName.StartsWith("startofmatch")) Add(MusicEvent.StartMatch, file);
                else if (fileName.StartsWith("endofmatch")) Add(MusicEvent.EndMatch, file);
            }
        }

        private void Add(MusicEvent type, string path)
        {
            _trackPaths[type].Add(path);
        }

        public string GetRandomTrackPath(MusicEvent type)
        {
            if (_trackPaths[type].Count == 0) return null;
            int index = Random.Range(0, _trackPaths[type].Count);
            return _trackPaths[type][index];
        }
    }
}