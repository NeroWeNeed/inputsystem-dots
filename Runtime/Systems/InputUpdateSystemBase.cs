using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace NeroWeNeed.InputSystem
{
[UpdateInGroup(typeof(InputUpdateSystemGroup))]
    public abstract class InputUpdateSystemBase : SystemBase
    {
        public string ActionMapId { get; protected set; }
        public NativeHashMap<Guid, int> ActionIndices { get; protected set; }
        protected void StartActionTrace(InputActionAssetData assetData)
        {
            var actionMap = assetData.value.FindActionMap(ActionMapId);
            actionMap.Enable();
            //ActionTrace.SubscribeTo(actionMap);
        }
        protected void StopActionTrace(InputActionAssetData assetData)
        {
            var actionMap = assetData.value.FindActionMap(ActionMapId);
            actionMap.Disable();
            //ActionTrace.UnsubscribeFrom(actionMap);
        }
        protected override void OnDestroy()
        {
            ActionIndices.Dispose();
        }
        public int GetActionIndex(Guid id) {
            return ActionIndices.TryGetValue(id, out var item) ? item : -1;
        }
    }
}