using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace NeroWeNeed.InputSystem
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct InputActionEventHeaderData
    {
        //Offset to start any memory copies from header data to output.
        public const int DataOffset = 40;

        public int sizeInBytes;
        public Guid actionMapId;
        public Guid actionId;
        public double startTime;
        public double time;
        public InputActionPhase phase;
        public int deviceId;
    }
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct InputActionHeaderData {
        public double startTime;
        public double time;
        public InputActionPhase phase;
        public int deviceId;
    }
}