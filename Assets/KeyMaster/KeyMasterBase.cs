using System;
using UnityEngine;

namespace KeyMaster
{
    public enum ControlPage
    {
        None            = 0,
        /// <summary>
        /// page selection will always be active
        /// </summary>
        PageSelection   = 1, 
        Dev             = 2,
        Scene           = 3, 
        UI              = 4,
        Audio           = 5,
    }

    /// <summary>
    /// Interface used to indicate key objects
    /// key objects do not necessarily need to inherit monobehaviour 
    /// </summary>
    public interface IKeyHolder
    {
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false), System.Serializable]
    public class KeyToken : Attribute
    {
        public KeyToken(ControlPage page, KeyCode key, KeyCode[] combo = null)
        {
            this.key = key;
            if (combo != null)
            {
                keyCombo = new KeyCombo(combo);
            }
            this.page = page;
        }
        public KeyCode key = KeyCode.None;
        public KeyCombo keyCombo;
        public ControlPage page;
        /// <summary>
        /// label with spaces etc. for button UI
        /// </summary>
        public string label;
        /// <summary>
        /// for convenience whem viewing command map
        /// shows class association
        /// </summary>
        public string type;
        /// <summary>
        /// for runtime use only
        /// action will be assigned by ExternalTriggerService
        /// once it receives the method that this attribute is decorating
        /// </summary>
        public System.Action action;

        public bool AllowTrigger()
        {
            bool allowTrigger = false;
          
            if (key != KeyCode.None)
            {
                if(keyCombo != null)
                {
                    allowTrigger = key.IsDown() && keyCombo.IsDown();
                }
                else
                {
                    // if this command does not have a key combo assigned, 
                    // prevent triggering it if a modifier key is being held down
                    // this will allow, for example, the key 'a' to be assigned 
                    // without a key combo, and also with a key combo, without a double trigger
                    allowTrigger = key.IsDown() && !KeyCombo.allModKeys.IsDownAny(true); ;
                }
            }
            return allowTrigger;
        }
    }

    public class KeyholderBase : MonoBehaviour, IKeyHolder
    {
        /// <summary>
        /// keys are assumed to live throughout play mode
        /// currently destroying them will cause null refs
        /// </summary>
        protected virtual void Start()
        {
            this.RegisterKeyHolder();
            Debug.LogFormat("#KEYMASTER# {0}| registered with keymaster", gameObject.name);   
        }
    }
}
