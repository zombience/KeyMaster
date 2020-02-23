using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace KeyMaster
{
    public interface IKeyMaster
    {
        void SetActivePage(ControlPage page);

        void RegisterKeyholder(IKeyHolder obj);

        /// <summary>
        /// can be used to turn on/off objects or take other actions when ControlPage focus is changed
        /// </summary>
        /// <param name="listener"></param>
        void AddControlPageListener(System.Action<ControlPage> listener);
        void RemoveControlPageListener(System.Action<ControlPage> listener);
    }
}
namespace KeyMaster.Implementation
{ 
    public class KeyMaster : IKeyMaster
    {
        /// <summary>
        /// used for matching network commands 
        /// listens for string values mathing label contained in <see cref="KeyToken"/>
        /// </summary>
        Dictionary<ControlPage, Dictionary<string, KeyToken>> remoteKeyCommandMap = new Dictionary<ControlPage, Dictionary<string, KeyToken>>();

        /// <summary>
        /// used for iterating through currently active control page
        /// </summary>
        Dictionary<ControlPage, List<KeyToken>> keyCommandMap = new Dictionary<ControlPage, List<KeyToken>>();

        List<KeyToken> pageSelectionControl = new List<KeyToken>();

        ControlPage currentActivePage;
        ControlPage prevPage;
        static PageSelector pageSelector;
        GateKeeper gateKeeper;

        event System.Action<ControlPage> OnPageActivation;

        public void Initialize()
        {
            gateKeeper = new GameObject("gatekeeper").AddComponent<GateKeeper>();
            // immortal gatekeeper
            GameObject.DontDestroyOnLoad(gateKeeper.gameObject);
            gateKeeper.RunRoutine(GateKeeperRoutine());
        }

        #region page activation
        public void AddControlPageListener(System.Action<ControlPage> listener)
        {
            if (OnPageActivation != null && OnPageActivation.GetInvocationList().Contains(listener)) return;
            OnPageActivation += listener;
        }

        public void RemoveControlPageListener(System.Action<ControlPage> listener)
        {
            if (!OnPageActivation.GetInvocationList().Contains(listener)) return;
            OnPageActivation -= listener;
        }

        public void SetActivePage(ControlPage page)
        {
            if (page == currentActivePage)
            {
                return;
            }
            prevPage = currentActivePage;
            currentActivePage = page;
            OnPageActivation?.Invoke(currentActivePage);
        }
        #endregion

        #region command map
        //public Dictionary<ControlPage, List<string>> GetCommandMap()
        //{
        //    var methods = GetDecoratedMethods();
        //    Dictionary<ControlPage, List<string>> commandMap = new Dictionary<ControlPage, List<string>>();

        //    var attributes = new List<KeyMasterToken>();
        //    methods.ForEach((m) =>  
        //    {
        //        var atts = m.GetCustomAttributes<KeyMasterToken>();
        //        foreach (var a in atts)
        //        {
        //            a.label = m.Name.FriendlyFormat();
        //            attributes.Add(a);
        //        }
        //    });
            
        //    foreach (var attrib in attributes)
        //    {
        //        if (commandMap.ContainsKey(attrib.page)) commandMap[attrib.page].Add(attrib.label);
        //        else
        //        {
        //            var list = new List<string>();
                    
        //            list.Add(attrib.label);
        //            commandMap.Add(attrib.page, list);
        //        }
        //    }

        //    return commandMap;
        //}


        public void RegisterKeyholder(IKeyHolder obj)
        {
            // move monobehaviour objects into DontDestroyOnLoad scene under GateKeeper
            var keyBase = obj as MonoBehaviour;
            if (keyBase != null) keyBase.transform.parent = gateKeeper.transform;

            var methods = GetDecoratedMethods(obj);

            //Debug.LogFormat("#KEYMASTER# {0}| found {1} methods on object {2}", this, methods.Length, obj.GetType());
            for (int i = 0; i < methods.Length; i++)
            {
                //Debug.LogFormat("#KEYMASTER# {0}| registering method {1} ", this, methods[i].Name);
                RegisterMethod(methods[i], obj);
            }
        }

        // TODO: fix Unregister
        // objects are null if unregister is called inside OnDestroy
        //public void UnRegisterKeyholder(object obj)
        //{
        //    var methods = GetDecoratedMethods(obj);
        //    for (int i = 0; i < methods.Length; i++)
        //    {
        //        var attrib = methods[i].GetCustomAttribute<KeyMasterToken>();
        //        Dictionary<string, KeyMasterToken> dict;
        //        bool exists = remoteKeyCommandMap.TryGetValue(attrib.page, out dict);
        //        if (exists)
        //        {
        //            if (dict.ContainsKey(attrib.label))
        //            {
        //                dict.Remove(attrib.label);
        //                //Debug.LogFormat("#KEYMASTER# {0}| UnRegistered obj {1} with label {2}", this, obj.GetType(), attrib.label);
        //            }
        //            else
        //            {
        //                Debug.LogFormat("#KEYMASTER# {0}| received UnRegister request for unregistered obj {1} with label {2}", this, obj.GetType(), attrib.label);
        //            }
        //        }

        //        List<KeyMasterToken> controls;
        //        exists = keyCommandMap.TryGetValue(attrib.page, out controls);
        //        if (exists)
        //        {
        //            KeyMasterToken staleObject = null;
        //            foreach (var c in controls)
        //            {
        //                if (attrib.label == c.label)
        //                {
        //                    staleObject = c;
        //                    break;
        //                }
        //            }
        //            controls.Remove(staleObject);
        //        }
        //    }
        //}
        #endregion

            
        #region reflection and registration
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnStartup()
        {
            Debug.LogFormat("#KEYMASTER# initializing keymaster");
            pageSelector = new PageSelector();
        }

        void RegisterMethod(MethodInfo method, object obj)
        {
            var attrib = method.GetCustomAttribute<KeyToken>();
            attrib.label = method.Name.FriendlyFormat();
            //Debug.LogFormat("#KEYMASTER# {0}| processing method with label {1}", this, attrib.label);

            attrib.action = Delegate.CreateDelegate(typeof(Action), obj, method.Name, false) as Action;

            // split page selection commands off into its own command set
            // that should always be listening for key commands
            // these exist in parallel dict for label lookup
            if (attrib.page == ControlPage.PageSelection)
            {
                if (!pageSelectionControl.Contains(attrib))
                {
                    pageSelectionControl.Add(attrib);
                }
            }

            Dictionary<string, KeyToken> dict;
            bool exists = remoteKeyCommandMap.TryGetValue(attrib.page, out dict);
            if (exists)
            {
                if (dict.ContainsKey(attrib.label))
                {
                    Debug.LogFormat("#KEYMASTER# | COLLISION: a method using label {1} is already registered. attempted to register method {2}, found existing method {3}", 
                        this, attrib.label, 
                        attrib.action.Method.Name, 
                        dict[attrib.label].action.Method.Name);
                    return;
                }

                dict.Add(attrib.label, attrib);
            }
            else
            {
                dict = new Dictionary<string, KeyToken>();
                dict.Add(attrib.label, attrib);
                remoteKeyCommandMap.Add(attrib.page, dict);
            }
            List<KeyToken> controls;
            exists = keyCommandMap.TryGetValue(attrib.page, out controls);
            if (exists)
            {
                controls.Add(attrib);
            }
            else
            {
                controls = new List<KeyToken>();
                controls.Add(attrib);
                keyCommandMap.Add(attrib.page, controls);
            }
        }

        /// <summary>
        /// for use with object instances
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        MethodInfo[] GetDecoratedMethods(object obj)
        {
            return obj
                .GetType()
                .GetRuntimeMethods()
                .Where(m => m.GetCustomAttributes<KeyToken>(false).ToArray().Length > 0)
                .ToArray();
        }
        #endregion


        IEnumerator GateKeeperRoutine()
        {
            Debug.LogFormat("#KEYMASTER# the Gatekeeper awaits the Keymaster");
            var list = new List<KeyToken>();
            while (true)
            {
                foreach (var attrib in pageSelectionControl)
                {
                    if (attrib.AllowTrigger())
                    {
                        Debug.LogFormat("#KEYMASTER# {0}| acivated control page method: {1}", this, attrib.label);
                        attrib.action?.Invoke();
                    }
                }

                if (prevPage != currentActivePage)
                {
                    list = null;
                    if (currentActivePage != ControlPage.None)
                    {
                        keyCommandMap.TryGetValue(currentActivePage, out list);
                    }
                    prevPage = currentActivePage;
                }

                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (item.AllowTrigger())
                        {
                            Debug.LogFormat("#KEYMASTER# {0}| invoking method {1} for label {2}", this, item.action?.Method.Name, item.label);
                            item.action?.Invoke();
                        }
                    }
                }

                yield return null;
            }
        }

        // TODO: migrate network listener from StrangeIoC project
        #region network listener
        //void HandleCommand(NetworkMessage msg)
        //{
        //    //Debug.LogFormat("#COMMAND# received {0} | {1} ", msg.topic, msg.payload);

        //    var command = JsonUtility.FromJson<NetworkCommand>(msg.payload);
        //    if (command == null)
        //    {
        //        Debug.LogFormat("#COMMAND# {0}| FAIL: network message payload was empty OR failed to deserialize: {1}", this, msg.payload);
        //        return;
        //    }
        //    //Debug.LogFormat("#COMMAND# {0}| received external command Page: {1} Label: {2}", this, command.page, command.label);


        //    //Debug.LogFormat("#COMMAND# {0}| received raw command packet {1}", this, command.ToString());
        //    SetActivePage(command.page);

        //    Dictionary<string, ExternalControl> dict;
        //    bool success = runtimeCommandMap.TryGetValue(command.page, out dict);
        //    if (success)
        //    {
        //        ExternalControl control;
        //        success = dict.TryGetValue(command.label, out control);
        //        if (success)
        //        {
        //            Debug.LogFormat("#COMMAND# {0}| label {1} method {2} object: {3}", this, command.label, control.action.Method.Name, control.action.Target);
        //            control.action?.Invoke();
        //            DisplayDebugNotification(command.label);

        //        }
        //        else
        //        {
        //            Debug.LogFormat("#COMMAND# {0}| failed to find registered method for page {1} label {2}", this, command.page, command.label);
        //        }
        //    }
        //    else
        //    {
        //        Debug.LogFormat("#COMMAND# {0}| failed to find any registered commands for page {1}", this, command.page);
        //    }
        //}
        #endregion


        #region page switching and monobehaviour
        internal class PageSelector : IKeyHolder
        {
            public ControlPage Page => ControlPage.PageSelection;
            IKeyMaster KeyMaster => _keyMaster ?? (_keyMaster = _keyMaster.Get());
            IKeyMaster _keyMaster;

            internal PageSelector()
            {
                KeyMaster.RegisterKeyholder(this);
            }

            [KeyToken(ControlPage.PageSelection, KeyCode.Alpha0)]
            void DeactivateAllKeyControls()
            {
                KeyMaster.SetActivePage(ControlPage.None);
            }

            [KeyToken(ControlPage.PageSelection, KeyCode.Alpha1)]
            void SetDevActive()
            {
                KeyMaster.SetActivePage(ControlPage.Dev);
            }

            [KeyToken(ControlPage.PageSelection, KeyCode.Alpha2)]
            void SetSceneActive()
            {
                KeyMaster.SetActivePage(ControlPage.Scene);
            }

            [KeyToken(ControlPage.PageSelection, KeyCode.Alpha3)]
            void SetUIActive()
            {
                KeyMaster.SetActivePage(ControlPage.UI);
            }

            [KeyToken(ControlPage.PageSelection, KeyCode.Alpha4)]
            void SetAudioActive()
            {
                KeyMaster.SetActivePage(ControlPage.Audio);
            }
        }

        public class GateKeeper : MonoBehaviour
        {
            Coroutine gatekeeperRoutine;

            public void RunRoutine(IEnumerator routine)
            {
                if (gatekeeperRoutine != null) StopCoroutine(gatekeeperRoutine);
                gatekeeperRoutine = StartCoroutine(routine);
            }
        }
        #endregion
    }
}