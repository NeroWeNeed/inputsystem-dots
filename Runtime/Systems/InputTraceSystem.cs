using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(InputActionAssetLoaderSystem))]
    public class InputActionTraceSystem : SystemBase
    {
        public InputActionTrace ActionTrace { get; private set; }
        protected override void OnCreate()
        {
            base.OnCreate();
            ActionTrace = new InputActionTrace();
            ActionTrace.SubscribeToAll();
        }
        protected override void OnUpdate() { }
        protected override void OnDestroy()
        {
            ActionTrace.Dispose();
        }
    }
}