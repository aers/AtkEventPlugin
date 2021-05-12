using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AtkEventPlugin
{
    public unsafe class PluginAtkEventListener : IDisposable
    {
        private Plugin _p;

        private delegate IntPtr GameAlloc(ulong size, IntPtr unk, IntPtr allocator, IntPtr alignment);

        private delegate IntPtr GetGameAllocator();

        private static GameAlloc _gameAlloc;
        private static GetGameAllocator _getGameAllocator;

        private delegate void AtkEventListener_Dtor_Delegate(IntPtr thisPtr, byte free);

        private delegate void AtkEventListener_ReceiveEvent_Delegate(IntPtr thisPtr, UInt16 eventType,
            UInt32 eventParam, IntPtr eventStruct, IntPtr eventInfoStruct);

        private AtkEventListener* internalEventListener;
        private IntPtr allocVtbl;

        private AtkEventListener_ReceiveEvent_Delegate receiveEventFunc;

        private delegate void AtkResNode_AddEvent_Delegate(AtkResNode* thisPtr, UInt16 eventType, UInt32 eventParam,
            AtkEventListener* listener, AtkResNode* nodeParam, bool systemEvent);

        private delegate void AtkResNode_RemoveEvent_Delegate(AtkResNode* thisPtr, UInt16 eventType, UInt32 eventParam,
            AtkEventListener* listener, bool systemEvent);

        private AtkResNode_AddEvent_Delegate _addEvent;
        private AtkResNode_RemoveEvent_Delegate _removeEvent;

        [StructLayout(LayoutKind.Explicit, Size=0x18)]
        private struct AtkEventListenerVtbl
        {
            [FieldOffset(0x0)] public IntPtr Dtor;
            [FieldOffset(0x8)] public IntPtr ReceiveSystemEvent;
            [FieldOffset(0x10)] public IntPtr ReceiveEvent;
        }

        public struct RegisteredEvent
        {
            public AtkResNode* source;
            public UInt16 type;
            public UInt32 param;

            public RegisteredEvent(AtkResNode* source, UInt16 type, UInt32 param)
            {
                this.source = source;
                this.type = type;
                this.param = param;
            }
        }

        public Dictionary<UInt32, RegisteredEvent> eventMap;

        public PluginAtkEventListener(Plugin p)
        {
            _p = p;
            eventMap = new Dictionary<uint, RegisteredEvent>();
        }

        public void Initialize()
        {
            var gameAllocPtr = _p.pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 45 8D 67 23");
            var getGameAllocatorPtr = _p.pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 75 08");

            _gameAlloc = Marshal.GetDelegateForFunctionPointer<GameAlloc>(gameAllocPtr);
            _getGameAllocator = Marshal.GetDelegateForFunctionPointer<GetGameAllocator>(getGameAllocatorPtr);

            var addEventAddress = _p.pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? C1 E7 0C ");
            var removeEventAddress = _p.pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 44 38 7D 67");

            _addEvent = Marshal.GetDelegateForFunctionPointer<AtkResNode_AddEvent_Delegate>(addEventAddress);
            _removeEvent = Marshal.GetDelegateForFunctionPointer<AtkResNode_RemoveEvent_Delegate>(removeEventAddress);

            var atkEventListenerVtblAddress =
                _p.pluginInterface.TargetModuleScanner.GetStaticAddressFromSig("4C 8D 3D ?? ?? ?? ?? 49 8D 8E ?? ?? ?? ??");

            var atkEventListenerVtbl = (AtkEventListenerVtbl*) atkEventListenerVtblAddress.ToPointer();

            PluginLog.Log($"{(long)atkEventListenerVtbl:X}");
            PluginLog.Log($"{(long)atkEventListenerVtbl->Dtor.ToPointer():X}");
            PluginLog.Log($"{(long)atkEventListenerVtbl->ReceiveSystemEvent.ToPointer():X}");
            PluginLog.Log($"{(long)atkEventListenerVtbl->ReceiveEvent.ToPointer():X}");

            var myVtbl = new AtkEventListenerVtbl();
            myVtbl.Dtor = atkEventListenerVtbl->Dtor;
            myVtbl.ReceiveSystemEvent = atkEventListenerVtbl->ReceiveSystemEvent;
            receiveEventFunc = ReceiveEvent;
            myVtbl.ReceiveEvent =
                Marshal.GetFunctionPointerForDelegate<AtkEventListener_ReceiveEvent_Delegate>(receiveEventFunc);

            var memory = Marshal.AllocHGlobal(0x18);
            Marshal.StructureToPtr(myVtbl, memory, false);
            allocVtbl = memory;

            internalEventListener = (AtkEventListener*) Alloc(0x8).ToPointer();

            internalEventListener->vtbl = memory.ToPointer();

            PluginLog.Log($"{(long)internalEventListener:X}");
        }

        private void ReceiveEvent(IntPtr thisPtr, UInt16 eventType,
            UInt32 eventParam, IntPtr eventStruct, IntPtr eventInfoStruct)
        {
            PluginLog.Log($"event hit - type {eventType} param {eventParam}");
        }

        public static IntPtr Alloc(ulong size)
        {
            if (_gameAlloc == null || _getGameAllocator == null) return IntPtr.Zero;
            return _gameAlloc(size, IntPtr.Zero, _getGameAllocator(), IntPtr.Zero);
        }

        public void AddEvent(AtkResNode* node, UInt16 type, UInt32 param)
        {
            if (eventMap.ContainsKey(param))
            {
                PluginLog.Log("Existing event with that param found.");
                return;
            }

            var re = new RegisteredEvent(node, type, param);
            eventMap.Add(param, re);

            _addEvent(node, type, param, internalEventListener, null, false);
        }

        public void RemoveEvent(RegisteredEvent re)
        {
            _removeEvent(re.source, re.type, re.param, internalEventListener, false);
        }

        public void Dispose()
        {
            foreach (var re in eventMap)
            {
                RemoveEvent(re.Value);
            }

            eventMap.Clear();

            var vtbl = (AtkEventListenerVtbl*) internalEventListener->vtbl;
            var dtor = Marshal.GetDelegateForFunctionPointer<AtkEventListener_Dtor_Delegate>(vtbl->Dtor);
            dtor(new IntPtr(internalEventListener), 1);

            Marshal.FreeHGlobal(allocVtbl);
        }
    }
}
