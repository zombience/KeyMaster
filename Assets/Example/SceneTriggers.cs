using UnityEngine;
using KeyMaster;


public class SceneTriggers : KeyholderBase
{
    [SerializeField]
    Light sceneLight;

    [KeyToken(ControlPage.Scene, KeyCode.G)]
    void GenericAudioTrigger()
    {
        Debug.LogFormat("#KEYMASTER# generic Scene trigger");
    }

    [KeyToken(ControlPage.Scene, KeyCode.T)]
    void ToggleSpotlight()
    {
        sceneLight.enabled = !sceneLight.enabled;
    }

    [KeyToken(ControlPage.Scene, KeyCode.R, new KeyCode[] { KeyCode.LeftAlt, KeyCode.LeftShift })]
    void RandomRotateLight()
    {
        sceneLight.transform.rotation = Random.rotationUniform;
        Debug.LogFormat("#KEYMASTER# key combo success");
    }
}
