using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Lambda.UI
{
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text textProgress;
        [SerializeField] private Spinner spinner;

        void Start()
        {

        }

        public void SetText(string text)
        {
            textProgress.text = text;
        }

        void Update()
        {
        
        }
    }
}
