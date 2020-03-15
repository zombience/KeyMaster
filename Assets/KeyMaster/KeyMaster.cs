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

        void HandleRemoteCommand(RemoteKeymasterCommand command);

        void EnqueueAction(System.Action action);
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
        GateKeeperRemote remoteKeeper;

        event System.Action<ControlPage> OnPageActivation;


        public void Initialize()
        {
            gateKeeper = new GameObject("gatekeeper").AddComponent<GateKeeper>();
            // immortal gatekeeper
            GameObject.DontDestroyOnLoad(gateKeeper.gameObject);
            gateKeeper.RunRoutine(GateKeeperRoutine());
            var remoteConfig = Utilities.LoadRemoteConfig();
            if (remoteConfig == null) return;

            remoteKeeper = new GameObject("remoteKeeper").AddComponent<GateKeeperRemote>();
            GameObject.DontDestroyOnLoad(remoteKeeper);
            Utilities.StartKeymasterRemote(remoteConfig.port);
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

        
        #region network listener
        public void HandleRemoteCommand(RemoteKeymasterCommand command)
        {
            remoteKeeper?.Enqueue(() => HandleCommand(command));
        }

        public void EnqueueAction(System.Action action)
        {
            remoteKeeper?.Enqueue(action);
        }

        void HandleCommand(RemoteKeymasterCommand command)
        {   
            //Debug.LogFormat("#COMMAND# {0}| received raw command packet {1}", this, command.ToString());
            SetActivePage(command.page);
            List<KeyToken> list = null;
            
            bool success = keyCommandMap.TryGetValue(currentActivePage, out list);
            
            if (success)
            {
                foreach (var item in list)
                {
                    //Debug.LogFormat("#COMMAND# comparing label {0} to received command.label {1}", item.label, command.label);
                    if(item.label == command.label)
                    {
                        Debug.LogFormat("#KEYMASTER# invoking: {0}| label {1} method {2} object: {3}", this, command.label, item.action?.Method.Name, item.action?.Target);
                        item.action?.Invoke();
                        break;
                    }
                    else
                    {
                        //Debug.LogFormat("#COMMAND# {0}| failed to find registered method for page {1} label {2}", this, command.page, command.label);
                    }
                }
                
            }
            else
            {
                Debug.LogFormat("#COMMAND# {0}| failed to find any registered commands for page {1}", this, command.page);
            }
        }
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

            /****************************************************************************************************\
             *                                             VERY IMPORTANT:                                      *
             * if you plan on using the example Touchdesigner project for remote triggering                     *
             * then these page-selection method names MUST have EXACT same names as ControlPage enum values     *
             * The TD project expects this.                                                                     *
             * If you are not using remote triggering, or if you are building your own, then do as you please   *
             *                                                                                                  *
            \****************************************************************************************************/

            [KeyToken(ControlPage.PageSelection, KeyCode.Alpha0)]
            void None()
            {
                KeyMaster.SetActivePage(ControlPage.None);
            }

            [KeyToken(ControlPage.PageSelection, KeyCode.Alpha1)]
            void Dev()
            {
                KeyMaster.SetActivePage(ControlPage.Dev);
            }

            [KeyToken(ControlPage.PageSelection, KeyCode.Alpha2)]
            void Scene()
            {
                KeyMaster.SetActivePage(ControlPage.Scene);
            }

            [KeyToken(ControlPage.PageSelection, KeyCode.Alpha3)]
            void UI()
            {
                KeyMaster.SetActivePage(ControlPage.UI);
            }

            [KeyToken(ControlPage.PageSelection, KeyCode.Alpha4)]
            void Audio()
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

        public class GateKeeperRemote : MonoBehaviour
        {
            Queue<System.Action> actions = new Queue<Action>();

            public void Enqueue(System.Action action)
            {
                lock(actions)
                {
                    actions.Enqueue(action);
                }
            }

            void Update()
            {
                lock (actions)
                {
                    while (actions.Count > 0)
                    {
                        actions.Dequeue()?.Invoke();
                    }
                }
            }
        }

        #endregion
    }
}