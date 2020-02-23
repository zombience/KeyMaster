using UnityEngine;
using KeyMaster;


public class DevTriggers : KeyholderBase
{
    bool 
        isDevMode,
        allowConsoleOutput;

    [KeyToken(ControlPage.Dev, KeyCode.D)]
    void DevModeTrigger()
    {
        isDevMode = !isDevMode;
        Debug.LogFormat("#KEYMASTER# developer mode set to: {0}", isDevMode);
    }

    [KeyToken(ControlPage.Dev, KeyCode.B)]
    void ToggleConsoleOutput()
    {
        allowConsoleOutput = !allowConsoleOutput;
        if(!allowConsoleOutput)
        {
            Debug.LogFormat("#KEYMASTER# console logging will be disabled");
        }
        Debug.unityLogger.logEnabled = allowConsoleOutput;
        if(allowConsoleOutput)
        {
            Debug.LogFormat("#KEYMASTER# console logging has been re-enabled");
        }
    }

    [KeyToken(ControlPage.Dev, KeyCode.E, new KeyCode[] { KeyCode.LeftAlt, KeyCode.LeftShift })]
    void KeyComboExample()
    {
        Debug.LogFormat("#KEYMASTER# dev  key combo success");
    }
}


