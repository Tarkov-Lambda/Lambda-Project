#if UNITY_EDITOR
using System.Collections.Generic;
using ifp.arena.shared;
using ifp.arena.shared.Models;
using UnityEditor;
using UnityEngine;

namespace arena.ui.scoreboard.Editor
{
    [CustomEditor(typeof(Scoreboard))]
    public class ScoreboardEditor : UnityEditor.Editor
    {
        private int playerCount = 8;
        private bool includeFactionA = true;
        private bool includeFactionB = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);

            playerCount = EditorGUILayout.IntSlider("Player Count", playerCount, 1, 20);
            includeFactionA = EditorGUILayout.Toggle("Include Faction 0", includeFactionA);
            includeFactionB = EditorGUILayout.Toggle("Include Faction 1", includeFactionB);

            GUI.enabled = Application.isPlaying;

            if (GUILayout.Button("Generate Fake Data", GUILayout.Height(30)))
            {
                GenerateFakeData();
            }

            if (GUILayout.Button("Clear Scoreboard", GUILayout.Height(25)))
            {
                ClearScoreboard();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use debug tools.", MessageType.Info);
            }

            GUI.enabled = true;
        }

        private void GenerateFakeData()
        {
            Scoreboard scoreboard = (Scoreboard)target;

            List<Faction> activeFactions = new List<Faction>();
            if (includeFactionA) activeFactions.Add((Faction)0);
            if (includeFactionB) activeFactions.Add((Faction)1);

            if (activeFactions.Count == 0)
            {
                Debug.LogWarning("[ScoreboardEditor] No factions selected.");
                return;
            }

            string[] names = {
                "Alpha", "Bravo", "Charlie", "Delta", "Echo",
                "Foxtrot", "Golf", "Hotel", "India", "Juliet",
                "Kilo", "Lima", "Mike", "November", "Oscar",
                "Papa", "Quebec", "Romeo", "Sierra", "Tango"
            };

            PlayerStats[] players = new PlayerStats[playerCount];
            Dictionary<Faction, int> teamScores = new Dictionary<Faction, int>();

            foreach (Faction f in activeFactions)
            {
                teamScores[f] = Random.Range(0, 100);
            }

            for (int i = 0; i < playerCount; i++)
            {
                Faction faction = activeFactions[i % activeFactions.Count];

                players[i] = new PlayerStats
                {
                    Id = i + 1,
                    Faction = faction,
                    Name = names[i % names.Length],
                    Kills = Random.Range(0, 30),
                    Deaths = Random.Range(0, 20),
                    Assists = Random.Range(0, 15),
                    Ping = Random.Range(10, 200)
                };
            }

            Debug.Log($"[ScoreboardEditor] Calling SetPlayers with {playerCount} players across {activeFactions.Count} faction(s).");

            for (int i = 0; i < players.Length; i++)
            {
                PlayerStats p = players[i];
                Debug.Log($"  [{i}] Id={p.Id} Faction={p.Faction} Name={p.Name} K={p.Kills} D={p.Deaths} A={p.Assists} Ping={p.Ping}");
            }

            foreach (var kvp in teamScores)
            {
                Debug.Log($"  TeamScore: {kvp.Key} = {kvp.Value}");
            }

            scoreboard.SetPlayers(players, teamScores, Faction.None);
        }

        private void ClearScoreboard()
        {
            Scoreboard scoreboard = (Scoreboard)target;
            scoreboard.SetPlayers(new PlayerStats[0], new Dictionary<Faction, int>(), Faction.None);
            Debug.Log("[ScoreboardEditor] Scoreboard cleared.");
        }
    }
}

#endif