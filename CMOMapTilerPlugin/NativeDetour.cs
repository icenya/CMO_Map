using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CMOMapTilerPlugin
{
    public class NativeDetour : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr hProcess, IntPtr lpBaseAddress, UIntPtr dwSize);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        private readonly IntPtr _targetSite;
        private readonly IntPtr _targetBody;
        private readonly byte[] _originalBytesSite;
        private readonly byte[] _originalBytesBody;
        private bool _isHooked;

        public MethodBase SourceMethod { get; }
        public MethodBase DestinationMethod { get; }

        private NativeDetour(MethodBase source, MethodBase destination, IntPtr targetSite, byte[] originalBytesSite, IntPtr targetBody, byte[] originalBytesBody)
        {
            SourceMethod = source;
            DestinationMethod = destination;
            _targetSite = targetSite;
            _originalBytesSite = originalBytesSite;
            _targetBody = targetBody;
            _originalBytesBody = originalBytesBody;
            _isHooked = true;
        }

        public static NativeDetour Create(MethodBase source, MethodBase destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("NativeDetour currently supports Windows x64 only.");
            }

            if (IntPtr.Size != 8)
            {
                throw new PlatformNotSupportedException("NativeDetour requires 64-bit architecture (x64).");
            }

            // 1. Force JIT compilation of both methods
            // For .NET Reactor (NecroBit), this triggers the clrjit.dll compileMethod hook to decrypt real IL
            RuntimeHelpers.PrepareMethod(source.MethodHandle);
            RuntimeHelpers.PrepareMethod(destination.MethodHandle);

            IntPtr pSourceSite = source.MethodHandle.GetFunctionPointer();
            IntPtr pDestSite = destination.MethodHandle.GetFunctionPointer();

            IntPtr pSourceBody = ResolveCodePointer(pSourceSite);
            IntPtr pDestBody = ResolveCodePointer(pDestSite);

            Logger.Info($"Hooking '{source.DeclaringType?.FullName}.{source.Name}' -> '{destination.DeclaringType?.FullName}.{destination.Name}'");
            Logger.Info($"  Source: Site=0x{pSourceSite.ToInt64():X16}, Body=0x{pSourceBody.ToInt64():X16}");
            Logger.Info($"  Destination: Site=0x{pDestSite.ToInt64():X16}, Body=0x{pDestBody.ToInt64():X16}");

            // Generate 12-byte x64 jump:
            // 48 B8 <8-byte addr> : mov rax, pDestBody
            // FF E0               : jmp rax
            byte[] jmpToDest = GenerateX64Jump(pDestBody);

            // Backup original bytes
            byte[] originalSiteBytes = new byte[jmpToDest.Length];
            Marshal.Copy(pSourceSite, originalSiteBytes, 0, originalSiteBytes.Length);

            byte[] originalBodyBytes = null;
            if (pSourceBody != pSourceSite)
            {
                originalBodyBytes = new byte[jmpToDest.Length];
                Marshal.Copy(pSourceBody, originalBodyBytes, 0, originalBodyBytes.Length);
            }

            // Write jump to function entry
            PatchMemory(pSourceSite, jmpToDest);

            // Also patch resolved body if different
            if (pSourceBody != pSourceSite)
            {
                PatchMemory(pSourceBody, jmpToDest);
            }

            Logger.Info($"Successfully installed detour for {source.Name}");
            return new NativeDetour(source, destination, pSourceSite, originalSiteBytes, pSourceBody, originalBodyBytes);
        }

        private static byte[] GenerateX64Jump(IntPtr destination)
        {
            byte[] code = new byte[12];
            code[0] = 0x48; // mov rax, imm64
            code[1] = 0xB8;
            ulong target = (ulong)destination.ToInt64();
            for (int i = 0; i < 8; i++)
            {
                code[2 + i] = (byte)((target >> (i * 8)) & 0xFF);
            }
            code[10] = 0xFF; // jmp rax
            code[11] = 0xE0;
            return code;
        }

        public static IntPtr ResolveCodePointer(IntPtr ptr)
        {
            unsafe
            {
                byte* p = (byte*)ptr;
                for (int i = 0; i < 8; i++)
                {
                    // E9 <rel32> : jmp rel32 (FixupPrecode or JIT jump)
                    if (*p == 0xE9)
                    {
                        int rel32 = *(int*)(p + 1);
                        p = p + 5 + rel32;
                        continue;
                    }
                    // EB <rel8> : jmp rel8 (short relative jump)
                    if (*p == 0xEB)
                    {
                        sbyte rel8 = *(sbyte*)(p + 1);
                        p = p + 2 + rel8;
                        continue;
                    }
                    // FF 25 <rel32> : jmp qword ptr [rip + rel32]
                    if (*p == 0xFF && *(p + 1) == 0x25)
                    {
                        int rel32 = *(int*)(p + 2);
                        byte** pTarget = (byte**)(p + 6 + rel32);
                        p = *pTarget;
                        continue;
                    }
                    // 48 B8 <imm64> FF E0 : mov rax, imm64; jmp rax
                    if (*p == 0x48 && *(p + 1) == 0xB8 && *(p + 10) == 0xFF && *(p + 11) == 0xE0)
                    {
                        ulong addr = *(ulong*)(p + 2);
                        p = (byte*)addr;
                        continue;
                    }
                    break;
                }
                return (IntPtr)p;
            }
        }

        private static void PatchMemory(IntPtr address, byte[] data)
        {
            UIntPtr size = (UIntPtr)data.Length;
            if (!VirtualProtect(address, size, PAGE_EXECUTE_READWRITE, out uint oldProtect))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"VirtualProtect failed at 0x{address.ToInt64():X16} (Error: {error})");
            }

            Marshal.Copy(data, 0, address, data.Length);

            VirtualProtect(address, size, oldProtect, out _);
            FlushInstructionCache(GetCurrentProcess(), address, size);
        }

        public void Undo()
        {
            if (!_isHooked) return;

            try
            {
                PatchMemory(_targetSite, _originalBytesSite);
                if (_targetBody != IntPtr.Zero && _targetBody != _targetSite && _originalBytesBody != null)
                {
                    PatchMemory(_targetBody, _originalBytesBody);
                }
                _isHooked = false;
                Logger.Info($"Detour for {SourceMethod.Name} has been undone.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to undo detour for {SourceMethod.Name}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Undo();
        }
    }
}
