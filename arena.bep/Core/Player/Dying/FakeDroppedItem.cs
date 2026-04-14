using Comfort.Common;
using EFT.InventoryLogic;
using JsonType;
using UnityEngine;

namespace ifp.arena.bep.Core.Dying;

public class FakeDroppedItem : MonoBehaviour
{
    private EItemDropSoundType dropSoundType;
    BaseBallistic.ESurfaceSound lastSurfaceSound = BaseBallistic.ESurfaceSound.Concrete;
    float timeLastPlayedDropSound;

    const float DropSoundCooldown = 0.2f;

    public void SetOriginalItem(Item item)
    {
        dropSoundType = item.DropSoundType;
        timeLastPlayedDropSound = Time.time - DropSoundCooldown / 2f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.impulse.sqrMagnitude < 1f)
            return;

        if (Time.time < timeLastPlayedDropSound + DropSoundCooldown)
            return;

        timeLastPlayedDropSound = Time.time;

        if (collision.collider.TryGetComponent<BaseBallistic>(out var surface))
        {
            lastSurfaceSound = surface.SurfaceSound;
        }

        H.BetterAudio.PlayDropItem(lastSurfaceSound, dropSoundType, transform.position, energy: 50f);
    }
}

