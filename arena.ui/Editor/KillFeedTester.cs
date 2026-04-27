using arena.ui.killfeed;
using ifp.arena.shared;
using ifp.arena.shared.Models;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KillFeedTester : MonoBehaviour
{
    [SerializeField] private KillFeed killfeed;
    [SerializeField] private Sprite placeholdergun;

    [SerializeField] private string playerLeft;
    [SerializeField] private Faction factionLeft;
    [SerializeField] private Faction factionRight;
    [SerializeField] private string playerRight;

    float t;

    int counter;

    void OnEnable()
    {
        t = 999;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t > 0.5f)
        {
            counter++;
            t = 0f;
            killfeed.Pop(playerLeft, factionLeft, playerRight, factionRight, Random.value > 0.9f);
        }
    }
}
