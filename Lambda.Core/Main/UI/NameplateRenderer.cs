using EFT.UI;
using Lambda.UI.Nameplate;
using System.Collections.Generic;
using UnityEngine;

namespace Lambda.Core.Main.UI;

internal class NameplateRenderer : MonoBehaviour
{
    private static readonly Vector3 HEAD_OFFSET = new Vector3(0f, 0.2f, 0f);

    float _textFadeStart = 4f;
    float _textFadeEnd = 10f;

    float _triangleFadeStart = 10f;
    float _triangleFadeEnd = 30f;

    RectTransform rectTransform => transform as RectTransform;

    List<Nameplate> nameplates = new List<Nameplate>();

    Nameplate prefabNameplate;

    public void Init(CommonUI commonUI, Nameplate prefabNameplate)
    {
        rectTransform.SetParent(commonUI.EftBattleUIScreen.transform);
        rectTransform.SetAsFirstSibling();

        rectTransform.localScale = Vector3.one;
        rectTransform.localPosition = Vector3.zero;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        this.prefabNameplate = prefabNameplate;
    }

    private Nameplate GetOrCreateNameplate(int index)
    {
        while (nameplates.Count <= index)
        {
            Nameplate instance = Instantiate(prefabNameplate, rectTransform);
            instance.gameObject.SetActive(false);
            nameplates.Add(instance);
        }
        return nameplates[index];
    }

    private void DisableAll()
    {
        for (int i = 0; i < nameplates.Count; i++)
            nameplates[i].gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (!H.IsInRaid())
        {
            DisableAll();
            gameObject.SetActive(false);
            return;
        }

        if (H.MainPlayerScore == null) return;

        Faction ownFaction = H.MainPlayerScore.Faction;
        int activeCount = 0;

        Camera cam = CameraClass.Instance.Camera;

        foreach (var playerScore in H.Scoreboard.Values)
        {
            if (playerScore.player.IsYourPlayer)
                continue;

            if (playerScore.Faction != ownFaction)
                continue;

            if (playerScore.player == null || !playerScore.IsAlive)
                continue;

            Vector3 worldPos = playerScore.player.PlayerBones.Head.position + HEAD_OFFSET;
            Vector3 viewportPos = cam.WorldToViewportPoint(worldPos);

            if (viewportPos.z < 0f) // behind the camera
                continue;

            Nameplate nameplate = GetOrCreateNameplate(activeCount);
            nameplate.gameObject.SetActive(true);

            RectTransform nameplateRect = nameplate.transform as RectTransform;

            Vector2 screenPos = new Vector2(
                viewportPos.x * Screen.width,
                viewportPos.y * Screen.height
            );

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPos,
                cam: null,
                out Vector2 localPoint
            );

            nameplateRect.localPosition = localPoint;

            nameplate.Set(playerScore.player.Profile.Nickname, playerScore.Faction);

            float sqrDistance = (cam.transform.position - worldPos).sqrMagnitude;
            float distance = Mathf.Sqrt(sqrDistance);

            float textT = Mathf.InverseLerp(_textFadeStart, _textFadeEnd, distance);
            float textAlpha = 1f - textT;

            float triT = Mathf.InverseLerp(_triangleFadeStart, _triangleFadeEnd, distance);
            float triangleAlpha = Mathf.Lerp(1f, 0.23f, triT);

            nameplate.SetTextAlpha(textAlpha);
            nameplate.SetGraphicsAlpha(triangleAlpha);

            activeCount++;
        }

        // disable leftover
        for (int i = activeCount; i < nameplates.Count; i++)
            nameplates[i].gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        foreach (var item in nameplates)
        {
            Destroy(item.gameObject);
        }
    }
}
