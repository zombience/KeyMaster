using UnityEngine;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace KeyMaster
{
    /// <summary>
    /// created to contain control key groups
    /// assuming windows, control keys are SHIFT, CTRL, ALT
    /// e.g. CTRL + SHIFT 
    /// </summary>
    [System.Serializable]
    public class KeyCombo
    {
        public KeyCombo(KeyCode[] combo)
        {
            keys = combo;
        }

        public KeyCode[] keys;

        public bool IsDown()
        {
            bool allDown = true;
            for (int i = 0; i < keys.Length; i++)
            {
                if(!keys[i].IsDown(true))
                {
                    allDown = false;
                }
            }
            return allDown;
        }

        public bool IsDownAny(bool continuous = false)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].IsDown(continuous)) return true;
            }
            return false;
        }

        static public KeyCombo shiftCtrlLeft
            = new KeyCombo(new KeyCode[]
            {
                KeyCode.LeftShift,
                KeyCode.LeftControl
            });


        static public KeyCombo shiftAltLeft
            = new KeyCombo(new KeyCode[]
            {
                KeyCode.LeftShift,
                KeyCode.LeftAlt
            });
        

        static public KeyCombo ctrlAltLeft 
            = new KeyCombo(new KeyCode[]
            {
                KeyCode.LeftControl,
                KeyCode.LeftAlt
            });
        

        static public KeyCombo shiftCtrlAltLeft 
            = new KeyCombo(new KeyCode[]
            {
                KeyCode.LeftShift,
                KeyCode.LeftAlt,
                KeyCode.LeftControl
            });

        static public KeyCombo allModKeys
            = new KeyCombo(new KeyCode[]
            {
                KeyCode.LeftShift,
                KeyCode.LeftAlt,
                KeyCode.LeftControl,
                KeyCode.RightShift,
                KeyCode.RightAlt,
                KeyCode.RightControl
            });
    }

    #region keymaster extensions
    static public partial class Utilities
    {
        static public IKeyMaster Get(this IKeyMaster empty)
        {
            return KeyMaster;
        }

        static public IKeyMaster KeyMaster
        {
            get
            {
                if(_keyMaster == null)
                {
                    _keyMaster = new Implementation.KeyMaster();
                    _keyMaster.Initialize();
                }
                return _keyMaster;
            }
        }
        static Implementation.KeyMaster _keyMaster;

        static public void RegisterKeyHolder(this IKeyHolder kh)
        {
            KeyMaster.RegisterKeyholder(kh);
        }

        static public bool IsDown(this KeyCode key, bool continuous = false)
        {
            if (!continuous)
            {
                return Input.GetKeyDown(key);
            }
            else
            {
                return Input.GetKey(key);
            }
        }

        static public bool IsUp(this KeyCode key)
        {
            return Input.GetKeyUp(key);
        }

        /// <summary>
        /// intended for use in editor, NOT fo use at runtime
        /// searches entire assembly for methods with the proper attribute
        /// </summary>
        /// <returns></returns>
        static public List<MethodInfo> GetDecoratedMethods()
        {
            return System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(asm =>
                    asm.GetTypes()
                    .Where(t => t.GetInterface("IKeyHolder") != null)
                    .SelectMany(t => t.GetRuntimeMethods())
                    .Where(m => m.GetCustomAttributes<KeyToken>(false).Count() > 0))
                .ToList();
        }
    }
    #endregion

    #region general utilities
    public static partial class Utilities
    {
        /// <summary>
        /// remove all namespaces from a type name
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static string RemoveNamespaceFromType(this System.Type t)
        {
            var tName = t.ToString().Split('.');
            return tName[tName.Length - 1];
        }

        // approximation of what unity does for serialized variables on monobehaviour inspector
        public static string FriendlyFormat(this string baseString)
        {
            string friendlyString = "";
            friendlyString += char.ToUpper(baseString[0]);

            for (int i = 1; i < baseString.Length; i++)
            {
                //Adds a space if (The current letter is uppercase OR number) AND the previous added letter was not a number and was lowercase
                if ((char.IsUpper(baseString[i]) || char.IsNumber(baseString[i]))
                    && (!char.IsNumber(friendlyString[friendlyString.Length - 1]) && char.IsLower(friendlyString[friendlyString.Length - 1])))
                    friendlyString += " ";
                friendlyString += baseString[i];
            }
            return friendlyString;
        }
        #endregion
    }
}
