using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ifp.arena.bep.Core.Audio
{

    public class MusicKit
    {
        public string Name;
        private Dictionary<MusicEvent, List<string>> _trackPaths;

        public MusicKit(string kitPath)
        {
            Name = new DirectoryInfo(kitPath).Name;
            _trackPaths = new Dictionary<MusicEvent, List<string>>();
            ScanFiles(kitPath);
        }

        private void ScanFiles(string path)
        {
            // Initialize Lists
            foreach (MusicEvent me in Enum.GetValues(typeof(MusicEvent)))
                _trackPaths[me] = new List<string>();

            if (!Directory.Exists(path))
            {
                H.Notify($"[CS2 Music] Kit path not found: {path}");
                return;
            }

            // Get all audio files
            var files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                .Where(s => s.EndsWith(".mp3") || s.EndsWith(".wav"));

            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file).ToLower();

                // Mapping Logic
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

        private void Add(MusicEvent type, string path) => _trackPaths[type].Add(path);

        public string GetRandomTrack(MusicEvent type)
        {
            var list = _trackPaths[type];
            if (list.Count == 0) return null;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }
    }
}