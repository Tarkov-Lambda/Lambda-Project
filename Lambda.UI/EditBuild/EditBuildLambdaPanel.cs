using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lambda.UI
{
    public class EditBuildLambdaPanel : MonoBehaviour
    {
        [SerializeField] private Button ButtonEquip;
        [SerializeField] private Button ButtonNonInteractable;

        Action onClickEquip;

        void Awake()
        {
            ButtonEquip.onClick.AddListener(OnClickEquip);
        }

        public void SetEquipped(bool equipped, Action onClickEquip)
        {
            ButtonEquip.gameObject.SetActive(!equipped);
            ButtonNonInteractable.gameObject.SetActive(equipped);

            this.onClickEquip = onClickEquip;
        }

        void OnClickEquip()
        {
            onClickEquip?.Invoke();
        }

        void Update()
        {
        
        }
    }
}
