using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Baselib.LowLevel;
using UnityEngine.Bindings;

namespace RocketScience.Bindings
{
    public class BindingsRouter
    {
        #region -- ENUMS --

        public enum Binding_ErrorCode
        {
            Success = 0,
            OutOfMemory = 16777216,
            OutOfSystemResources = 16777217,
            InvalidAddressRange = 16777218,
            InvalidArgument = 16777219,
            InvalidBufferSize = 16777220,
            InvalidState = 16777221,
            NotSupported = 16777222,
            Timeout = 16777223,
            UnsupportedAlignment = 33554432,
            InvalidPageSize = 33554433,
            InvalidPageCount = 33554434,
            UnsupportedPageState = 33554435,
            ThreadCannotJoinSelf = 50331648,
            NetworkInitializationError = 67108864,
            AddressInUse = 67108865,
            AddressUnreachable = 67108866,
            AddressFamilyNotSupported = 67108867,
            Disconnected = 67108868,
            InvalidSocketType = 67108869,
            InvalidAddressFamily = 67108870,
            InvalidPathname = 83886080,
            RequestedAccessIsNotAllowed = 83886081,
            IOError = 83886082,
            FailedToOpenDynamicLibrary = 100663296,
            FunctionNotFound = 100663297,
            NoSupportedAddressFound = 117440512,
            TryAgain = 117440513,
            UnexpectedError = -1
        }

        public enum Binding_Memory_PageState
        {
            Reserved = 0,
            NoAccess = 1,
            ReadOnly = 2,
            ReadWrite = 4,
            ReadOnly_Executable = 18,
            ReadWrite_Executable = 20
        }

        public enum Binding_NetworkAddress_Family
        {
            Invalid,
            IPv4,
            IPv6
        }

        public enum Binding_Socket_Protocol
        {
            UDP = 1,
            TCP
        }

        public enum Binding_ErrorState_NativeErrorCodeType : byte
        {
            None,
            PlatformDefined
        }

        public enum Binding_ErrorState_ExtraInformationType : byte
        {
            None,
            StaticString,
            GenerationCounter
        } 
        #endregion

        #region -- FUNCTIONS --

        public unsafe static Binding_Socket_Handle Socket_Create(Binding_NetworkAddress_Family family, Binding_Socket_Protocol protocol, Binding_ErrorState errorState)
        {
            Binding.Baselib_NetworkAddress_Family bFamily = (Binding.Baselib_NetworkAddress_Family)family;
            Binding.Baselib_Socket_Protocol bProtocol = (Binding.Baselib_Socket_Protocol)protocol;
            Binding.Baselib_ErrorState* bErrState = (errorState).ToBaseErrorState();

            Binding.Baselib_Socket_Handle bHandle = Binding.Baselib_Socket_Create(bFamily, bProtocol, bErrState);
            Binding_Socket_Handle result = Binding_Socket_Handle.FromBaseHandle(bHandle);
            errorState = errorState.FromBaseErrorState();

            return result;
        }

        public static void Socket_Close(Binding_Socket_Handle socket)
        {
            Binding.Baselib_Socket_Close(socket.ToBaseHandle());
            socket.handle = IntPtr.Zero;
        }

        public unsafe static void NetworkAddress_Encode(Binding_NetworkAddress* dstAddress, Binding_NetworkAddress_Family family, byte* ip, ushort port, Binding_ErrorState errorState)
        {
            Binding.Baselib_NetworkAddress baselib_NetworkAddress = (*dstAddress).ToBaseNetworkAddress();
            Binding.Baselib_NetworkAddress_Family bFamily = (Binding.Baselib_NetworkAddress_Family)family;
            Binding.Baselib_ErrorState* bErrState = (errorState).ToBaseErrorState();

            Binding.Baselib_NetworkAddress_Encode(&baselib_NetworkAddress, bFamily, ip, port, bErrState);

            *dstAddress = Binding_NetworkAddress.FromBaseNetworkAddress(baselib_NetworkAddress);
            errorState = errorState.FromBaseErrorState();
        }

        public unsafe static uint Socket_UDP_Send(Binding_Socket_Handle socket, Binding_Socket_Message messages, uint messagesCount, Binding_ErrorState errorState)
        {
            Binding.Baselib_Socket_Handle bHandle = socket.ToBaseHandle();
            Binding.Baselib_Socket_Message* bMessage = messages.ToBaseSocketMessage();
            Binding.Baselib_ErrorState* bErrState = (errorState).ToBaseErrorState();

            uint result = Binding.Baselib_Socket_UDP_Send(bHandle, bMessage, messagesCount, bErrState);

            messages = messages.FromBaseSocketMessage();
            errorState = errorState.FromBaseErrorState();
            return result;
        }

        public unsafe static uint Socket_UDP_Recv(Binding_Socket_Handle socket, Binding_Socket_Message messages, uint messagesCount, Binding_ErrorState errorState)
        {
            Binding.Baselib_Socket_Handle baselib_Socket = socket.ToBaseHandle();
            Binding.Baselib_Socket_Message* bMessages = (messages).ToBaseSocketMessage();
            Binding.Baselib_ErrorState* bErrState = (errorState).ToBaseErrorState();

            uint result = Binding.Baselib_Socket_UDP_Recv(baselib_Socket, bMessages, messagesCount, bErrState);

            messages = messages.FromBaseSocketMessage();
            errorState = errorState.FromBaseErrorState();

            return result;
        } 
        #endregion

        #region -- DATA STRUCTURES --
        public struct Binding_Socket_Handle
        {
            public IntPtr handle;

            internal static Binding_Socket_Handle FromBaseHandle(Binding.Baselib_Socket_Handle handle)
            {
                return new Binding_Socket_Handle() { handle = handle.handle };
            }

            internal Binding.Baselib_Socket_Handle ToBaseHandle()
            {
                return new Binding.Baselib_Socket_Handle() { handle = this.handle };
            }
        }

        public class Binding_Socket_Message
        {
            public Binding_NetworkAddress address;
            public IntPtr data;
            public uint dataLen;

            internal IntPtr _bAddressMemory;
            internal unsafe Binding.Baselib_NetworkAddress* _addressPtr;

            internal IntPtr _bMessageMemory;
            internal unsafe Binding.Baselib_Socket_Message* _messagePtr;

            public unsafe Binding_Socket_Message()
            {
                _bAddressMemory = Marshal.AllocHGlobal(sizeof(Binding.Baselib_NetworkAddress));
                _addressPtr = (Binding.Baselib_NetworkAddress*)_bAddressMemory.ToPointer();

                _bMessageMemory = Marshal.AllocHGlobal(sizeof(Binding.Baselib_Socket_Message));
                _messagePtr = (Binding.Baselib_Socket_Message*)_bMessageMemory.ToPointer();
                *_messagePtr = new Binding.Baselib_Socket_Message();
            }

            ~Binding_Socket_Message()
            {
                Marshal.FreeHGlobal(_bAddressMemory);
                Marshal.FreeHGlobal(_bMessageMemory);
                _bAddressMemory = _bMessageMemory = IntPtr.Zero;
            }

            internal unsafe Binding.Baselib_Socket_Message* ToBaseSocketMessage()
            {
                *_addressPtr = address.ToBaseNetworkAddress();
                _messagePtr->address = _addressPtr;
                _messagePtr->data = data;
                _messagePtr->dataLen = dataLen;

                return _messagePtr;
            }

            internal unsafe Binding_Socket_Message FromBaseSocketMessage(Binding.Baselib_Socket_Message socketMessage)
            {
                Binding_NetworkAddress bAddress = Binding_NetworkAddress.FromBaseNetworkAddress((*socketMessage.address));
                address = bAddress;
                data = socketMessage.data;
                dataLen = socketMessage.dataLen;
                return this;
            }

            internal unsafe Binding_Socket_Message FromBaseSocketMessage()
            {
                Binding_NetworkAddress bAddress = Binding_NetworkAddress.FromBaseNetworkAddress((*_messagePtr->address));
                address = bAddress;
                data = _messagePtr->data;
                dataLen = _messagePtr->dataLen;
                return this;
            }
        }

        public struct Binding_SourceLocation
        {
            public unsafe byte* file;

            public unsafe byte* function;

            public uint lineNumber;

            internal unsafe Binding.Baselib_SourceLocation ToBaseSourceLocation()
            {
                return new Binding.Baselib_SourceLocation()
                {
                    file = this.file,
                    function = this.function,
                    lineNumber = this.lineNumber,
                };
            }

            internal unsafe static Binding_SourceLocation FromBaseSourceLocation(Binding.Baselib_SourceLocation srcLoc)
            {
                return new Binding_SourceLocation()
                {
                    file = srcLoc.file,
                    function = srcLoc.function,
                    lineNumber = srcLoc.lineNumber,
                };
            }
        }

        public class Binding_ErrorState
        {
            public Binding_SourceLocation sourceLocation;

            public ulong nativeErrorCode;

            public ulong extraInformation;

            public Binding_ErrorCode code;

            public Binding_ErrorState_NativeErrorCodeType nativeErrorCodeType;

            public Binding_ErrorState_ExtraInformationType extraInformationType;

            internal IntPtr _bErrorStateMemory;
            internal unsafe Binding.Baselib_ErrorState* _errorStatePtr;

            public unsafe Binding_ErrorState()
            {
                _bErrorStateMemory = Marshal.AllocHGlobal(sizeof(Binding.Baselib_ErrorState));
                _errorStatePtr = (Binding.Baselib_ErrorState*)_bErrorStateMemory.ToPointer();
                *_errorStatePtr = default(Binding.Baselib_ErrorState);
            }

            ~Binding_ErrorState()
            {
                Marshal.FreeHGlobal(_bErrorStateMemory);
                _bErrorStateMemory = IntPtr.Zero;
            }

            internal unsafe Binding.Baselib_ErrorState* ToBaseErrorState()
            {
                _errorStatePtr->sourceLocation = sourceLocation.ToBaseSourceLocation();
                _errorStatePtr->nativeErrorCode = nativeErrorCode;
                _errorStatePtr->extraInformation = extraInformation;
                _errorStatePtr->code = (Binding.Baselib_ErrorCode)code;
                _errorStatePtr->nativeErrorCodeType = (Binding.Baselib_ErrorState_NativeErrorCodeType)nativeErrorCodeType;
                _errorStatePtr->extraInformationType = (Binding.Baselib_ErrorState_ExtraInformationType)extraInformationType;

                return _errorStatePtr;
            }

            internal unsafe Binding_ErrorState FromBaseErrorState()
            {
                sourceLocation = Binding_SourceLocation.FromBaseSourceLocation(_errorStatePtr->sourceLocation);
                nativeErrorCode = _errorStatePtr->nativeErrorCode;
                extraInformation = _errorStatePtr->extraInformation;
                code = (Binding_ErrorCode)_errorStatePtr->code;
                nativeErrorCodeType = (Binding_ErrorState_NativeErrorCodeType)_errorStatePtr->nativeErrorCodeType;
                extraInformationType = (Binding_ErrorState_ExtraInformationType)_errorStatePtr->extraInformationType;

                return this;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Binding_NetworkAddress
        {
            [FieldOffset(0)]
            public byte data0;

            [FieldOffset(1)]
            public byte data1;

            [FieldOffset(2)]
            public byte data2;

            [FieldOffset(3)]
            public byte data3;

            [FieldOffset(4)]
            public byte data4;

            [FieldOffset(5)]
            public byte data5;

            [FieldOffset(6)]
            public byte data6;

            [FieldOffset(7)]
            public byte data7;

            [FieldOffset(8)]
            public byte data8;

            [FieldOffset(9)]
            public byte data9;

            [FieldOffset(10)]
            public byte data10;

            [FieldOffset(11)]
            public byte data11;

            [FieldOffset(12)]
            public byte data12;

            [FieldOffset(13)]
            public byte data13;

            [FieldOffset(14)]
            public byte data14;

            [FieldOffset(15)]
            public byte data15;

            [FieldOffset(0)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_0;

            [FieldOffset(1)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_1;

            [FieldOffset(2)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_2;

            [FieldOffset(3)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_3;

            [FieldOffset(4)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_4;

            [FieldOffset(5)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_5;

            [FieldOffset(6)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_6;

            [FieldOffset(7)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_7;

            [FieldOffset(8)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_8;

            [FieldOffset(9)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_9;

            [FieldOffset(10)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_10;

            [FieldOffset(11)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_11;

            [FieldOffset(12)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_12;

            [FieldOffset(13)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_13;

            [FieldOffset(14)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_14;

            [FieldOffset(15)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv6_15;

            [FieldOffset(0)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv4_0;

            [FieldOffset(1)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv4_1;

            [FieldOffset(2)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv4_2;

            [FieldOffset(3)]
            [Ignore(DoesNotContributeToSize = true)]
            public byte ipv4_3;

            [FieldOffset(16)]
            public byte port0;

            [FieldOffset(17)]
            public byte port1;

            [FieldOffset(18)]
            public byte family;

            [FieldOffset(19)]
            public byte _padding;

            [FieldOffset(20)]
            public uint ipv6_scope_id;

            internal Binding.Baselib_NetworkAddress ToBaseNetworkAddress()
            {
                return new Binding.Baselib_NetworkAddress()
                {
                    data0 = this.data0,
                    data1 = this.data1,
                    data2 = this.data2,
                    data3 = this.data3,
                    data4 = this.data4,
                    data5 = this.data5,
                    data6 = this.data6,
                    data7 = this.data7,
                    data8 = this.data8,
                    data9 = this.data9,
                    data10 = this.data10,
                    data11 = this.data11,
                    data12 = this.data12,
                    data13 = this.data13,
                    data14 = this.data14,
                    data15 = this.data15,
                    ipv6_0 = this.ipv6_0,
                    ipv6_1 = this.ipv6_1,
                    ipv6_2 = this.ipv6_2,
                    ipv6_3 = this.ipv6_3,
                    ipv6_4 = this.ipv6_4,
                    ipv6_5 = this.ipv6_5,
                    ipv6_6 = this.ipv6_6,
                    ipv6_7 = this.ipv6_7,
                    ipv6_8 = this.ipv6_8,
                    ipv6_9 = this.ipv6_9,
                    ipv6_10 = this.ipv6_10,
                    ipv6_11 = this.ipv6_11,
                    ipv6_12 = this.ipv6_12,
                    ipv6_13 = this.ipv6_13,
                    ipv6_14 = this.ipv6_14,
                    ipv6_15 = this.ipv6_15,
                    ipv4_0 = this.ipv4_0,
                    ipv4_1 = this.ipv4_1,
                    ipv4_2 = this.ipv4_2,
                    ipv4_3 = this.ipv4_3,
                    port0 = this.port0,
                    port1 = this.port1,
                    family = this.family,
                    _padding = this._padding,
                    ipv6_scope_id = this.ipv6_scope_id
                };
            }


            internal static Binding_NetworkAddress FromBaseNetworkAddress(Binding.Baselib_NetworkAddress netAddress)
            {
                return new Binding_NetworkAddress()
                {
                    data0 = netAddress.data0,
                    data1 = netAddress.data1,
                    data2 = netAddress.data2,
                    data3 = netAddress.data3,
                    data4 = netAddress.data4,
                    data5 = netAddress.data5,
                    data6 = netAddress.data6,
                    data7 = netAddress.data7,
                    data8 = netAddress.data8,
                    data9 = netAddress.data9,
                    data10 = netAddress.data10,
                    data11 = netAddress.data11,
                    data12 = netAddress.data12,
                    data13 = netAddress.data13,
                    data14 = netAddress.data14,
                    data15 = netAddress.data15,
                    ipv6_0 = netAddress.ipv6_0,
                    ipv6_1 = netAddress.ipv6_1,
                    ipv6_2 = netAddress.ipv6_2,
                    ipv6_3 = netAddress.ipv6_3,
                    ipv6_4 = netAddress.ipv6_4,
                    ipv6_5 = netAddress.ipv6_5,
                    ipv6_6 = netAddress.ipv6_6,
                    ipv6_7 = netAddress.ipv6_7,
                    ipv6_8 = netAddress.ipv6_8,
                    ipv6_9 = netAddress.ipv6_9,
                    ipv6_10 = netAddress.ipv6_10,
                    ipv6_11 = netAddress.ipv6_11,
                    ipv6_12 = netAddress.ipv6_12,
                    ipv6_13 = netAddress.ipv6_13,
                    ipv6_14 = netAddress.ipv6_14,
                    ipv6_15 = netAddress.ipv6_15,
                    ipv4_0 = netAddress.ipv4_0,
                    ipv4_1 = netAddress.ipv4_1,
                    ipv4_2 = netAddress.ipv4_2,
                    ipv4_3 = netAddress.ipv4_3,
                    port0 = netAddress.port0,
                    port1 = netAddress.port1,
                    family = netAddress.family,
                    _padding = netAddress._padding,
                    ipv6_scope_id = netAddress.ipv6_scope_id
                };

            }
        }

    } 
    #endregion
}
