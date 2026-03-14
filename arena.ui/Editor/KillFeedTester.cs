using arena.ui.killfeed;
using ifp.arena.shared.Models;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KillFeedTester : MonoBehaviour
{
    [SerializeField] private KillFeed killfeed;
    [SerializeField] private Sprite placeholdergun;

    [SerializeField] private PlayerStats playerLeft;
    [SerializeField] private PlayerStats playerRight;

    float t;

    int counter;

    void Start()
    {
        
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t > 0.5f)
        {
            counter++;
            playerLeft.Name = $"player left {counter}";
            playerRight.Name = $"player right {counter}";
            t = 0f;
            killfeed.Add(playerLeft, playerRight, placeholdergun, Random.value > 0.9f);
        }
    }
}
