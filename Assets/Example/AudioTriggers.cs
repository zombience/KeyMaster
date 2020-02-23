using UnityEngine;
using KeyMaster;

public class AudioTriggers : KeyholderBase
{
    [KeyToken(ControlPage.Audio, KeyCode.G)]
    void GenericAudioTrigger()
    {
        Debug.LogFormat("#KEYMASTER# generic audio trigger");
    }

    [KeyToken(ControlPage.Audio, KeyCode.M)]
    void MultiUseExampleNoCombo()
    {
        Debug.LogFormat("#KEYMASTER# audio page multi-use example with WITHOUT COMBO was triggered");
    }

    [KeyToken(ControlPage.Audio, KeyCode.M, new KeyCode[] { KeyCode.LeftShift, })]
    void MultiUseExampleWithCombo()
    {
        Debug.LogFormat("#KEYMASTER# audio page multi-use example WITH COMBO was triggered");
    }

    [KeyToken(ControlPage.Audio, KeyCode.B)]
    void PlayBGMusic()
    {
        Debug.LogFormat("#KEYMASTER# rick astley intensifies");
    }

    [KeyToken(ControlPage.Audio, KeyCode.E, new KeyCode[] { KeyCode.LeftAlt, KeyCode.LeftShift})]
    void KeyComboExample()
    {
        Debug.LogFormat("#KEYMASTER# key combo success");
    }
}
