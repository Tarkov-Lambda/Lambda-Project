using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace arena.ui
{
    public class PopupMatchEnd : MonoBehaviour
    {
        [SerializeField] private Color colorWon;
        [SerializeField] private Color colorLost;
        [SerializeField] private Graphic[] coloredGraphic;
        [SerializeField] private TMP_Text textMain;
        [SerializeField] private TMP_Text textSub;

        Animator animator;

        readonly int TR_POP = Animator.StringToHash("RoundEnd");

#if UNITY_EDITOR
        [SerializeField] private bool testwin;

        private void OnValidate()
        {
            SetGraphicColor(testwin);
        }

        [ContextMenu("test pop")]
        void TestPop()
        {
            Pop(true, "tset", "subtitle");
        }
#endif

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void Pop(bool win, string main, string subtitle)
        {
            textMain.text = main;
            textSub.text = subtitle;
            SetGraphicColor(win);

            animator.SetTrigger(TR_POP);
        }

        void SetGraphicColor(bool win)
        {
            foreach (var item in coloredGraphic)
            {
                if (item != null)
                {
                    item.SetColorKeepGraphicAlpha(win ? colorWon : colorLost);
                }
            }
        }

        void Update()
        {
        
        }
    }
}
