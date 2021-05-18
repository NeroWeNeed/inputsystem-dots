using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using NeroWeNeed.InputSystem;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;


namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputSystemGroup))]
    public sealed class InputActionProcessorSystem : SystemBase
    {
        [BurstCompile]
        private unsafe struct PartitionInputBufferJob : IJob
        {
            [ReadOnly]
            public NativeInputActionBuffer buffer;
            [WriteOnly]
            public NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle> handles;
            public void Execute()
            {
                var enumerator = buffer.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var item = enumerator.Current;
                    handles.Add(item.Header->actionMapId, item);
                }

            }
        }
        private NativeInputActionBuffer buffer;
        public NativeInputActionBuffer Buffer { get => buffer; }
        private NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle> handles;
        public NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle> Handles { get => handles; }
        public NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle>.Enumerator GetActionUpdates(Guid id)
        {
            return handles.GetValuesForKey(id);
        }
        private Dictionary<Guid, InputActionMap> actionMaps;
        protected override void OnCreate()
        {
            buffer = new NativeInputActionBuffer(512, Allocator.Persistent);
            handles = new NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle>(8, Allocator.Persistent);
            actionMaps = new Dictionary<Guid, InputActionMap>();
            foreach (var actionMap in UnityEngine.InputSystem.InputSystem.ListEnabledActions().Select(action => action.actionMap).Distinct())
            {
                actionMaps[actionMap.id] = actionMap;
                actionMap.actionTriggered += WriteToBuffer;
            }
            UnityEngine.InputSystem.InputSystem.onActionChange += OnActionChange;
            base.OnCreate();
        }
        protected unsafe override void OnUpdate()
        {
            UnityEngine.InputSystem.InputSystem.Update();
            var job = new PartitionInputBufferJob
            {
                buffer = buffer,
                handles = handles
            };
            job.Schedule().Complete();
        }
        protected override void OnDestroy()
        {
            buffer.Dispose();
            handles.Dispose();
            foreach (var actionMap in actionMaps.Values)
            {
                actionMap.actionTriggered -= WriteToBuffer;
            }
            actionMaps.Clear();
        }
        private void EnableActionMap(InputActionMap actionMap)
        {
            Debug.Log("e");
            if (!actionMaps.ContainsKey(actionMap.id))
            {
                actionMaps[actionMap.id] = actionMap;
                actionMap.actionTriggered += WriteToBuffer;
            }
        }
        private void DisableActionMap(InputActionMap actionMap)
        {
            Debug.Log("f");
            if (actionMaps.Remove(actionMap.id))
            {
                actionMap.actionTriggered -= WriteToBuffer;
            }

        }
        private unsafe void WriteToBuffer(InputAction.CallbackContext context)
        {
            buffer.WriteAction(context);
        }
        private unsafe void ClearAction(InputAction action)
        {
            buffer.ClearAction(action);
        }
        private void OnActionChange(object actionOrMap, InputActionChange change)
        {
            switch (change)
            {
                case InputActionChange.ActionEnabled:
                    break;
                case InputActionChange.ActionDisabled:
                    break;
                case InputActionChange.ActionMapEnabled:
                    EnableActionMap((InputActionMap)actionOrMap);
                    break;
                case InputActionChange.ActionMapDisabled:
                    DisableActionMap((InputActionMap)actionOrMap);
                    break;
                case InputActionChange.ActionStarted:
                    break;
                case InputActionChange.ActionPerformed:
                    break;
                case InputActionChange.ActionCanceled:
                    ClearAction((InputAction)actionOrMap);
                    break;
                case InputActionChange.BoundControlsAboutToChange:
                    break;
                case InputActionChange.BoundControlsChanged:
                    break;
                default:
                    break;
            }

        }
    }



}