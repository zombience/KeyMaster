using System;
using KeyMaster;
using UnityEngine;
using UnityEngine.UI;


class UITriggers : KeyholderBase
{
    [SerializeField]
    Canvas uiCanvas;

    [SerializeField]
    Text text;


    [KeyToken(ControlPage.UI, KeyCode.T)]
    void ToggleUI()
    {
        uiCanvas.enabled = !uiCanvas.enabled;
    }

    [KeyToken(ControlPage.UI, KeyCode.R)]
    void DisplayTime()
    {
        uiCanvas.enabled = true;
        text.text = DateTime.Now.ToString("yy:dd:mm:ss");
    }
}
