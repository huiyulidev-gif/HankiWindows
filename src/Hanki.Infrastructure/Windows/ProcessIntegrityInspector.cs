using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Hanki.Core.Diagnostics;

namespace Hanki.Infrastructure.Windows;

public enum ProcessInspectionStatus
{
    Available,
    NoForegroundWindow,
    ProcessExited,
    AccessDenied,
    ProtectedOrSystemProcess,
    InformationUnavailable
}

public sealed record ProcessIntegrityInfo(
    int ProcessId,
    string? ProcessName,
    ProcessIntegrityLevel Integrity,
    ProcessInspectionStatus Status,
    bool IsProtected);

public sealed record IntegrityInspection(
    ProcessIntegrityInfo Hanki,
    ProcessIntegrityInfo Target,
    IntegrityComparison Comparison,
    bool IsSecureDesktop);

public sealed class ProcessIntegrityInspector
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenIntegrityLevel = 25;
    private const int SecurityMandatoryUntrustedRid = 0x00000000;
    private const int SecurityMandatoryLowRid = 0x00001000;
    private const int SecurityMandatoryMediumRid = 0x00002000;
    private const int SecurityMandatoryMediumPlusRid = 0x00002100;
    private const int SecurityMandatoryHighRid = 0x00003000;
    private const int SecurityMandatorySystemRid = 0x00004000;
    private const int ErrorAccessDenied = 5;
    private const uint DesktopReadObjects = 0x0001;
    private const uint DesktopSwitchDesktop = 0x0100;
    private const int UoiName = 2;

    public IntegrityInspection InspectForeground()
    {
        var hanki = InspectProcess(Environment.ProcessId);
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return new IntegrityInspection(
                hanki,
                new ProcessIntegrityInfo(
                    0,
                    null,
                    ProcessIntegrityLevel.Unknown,
                    ProcessInspectionStatus.NoForegroundWindow,
                    false),
                IntegrityComparison.Unknown,
                IsSecureDesktopActive());
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        var target = processId == 0
            ? new ProcessIntegrityInfo(
                0,
                null,
                ProcessIntegrityLevel.Unknown,
                ProcessInspectionStatus.InformationUnavailable,
                false)
            : InspectProcess((int)processId);
        return new IntegrityInspection(hanki, target, Compare(hanki.Integrity, target.Integrity), IsSecureDesktopActive());
    }

    public ProcessIntegrityInfo InspectProcess(int processId)
    {
        string? processName = null;
        try
        {
            using var process = Process.GetProcessById(processId);
            processName = process.ProcessName + ".exe";
        }
        catch (ArgumentException)
        {
            return new ProcessIntegrityInfo(
                processId,
                null,
                ProcessIntegrityLevel.Unknown,
                ProcessInspectionStatus.ProcessExited,
                false);
        }
        catch (Exception)
        {
            // The token inspection below can still distinguish access denial.
        }

        var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
        if (processHandle == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            return new ProcessIntegrityInfo(
                processId,
                processName,
                ProcessIntegrityLevel.Unknown,
                error == ErrorAccessDenied
                    ? ProcessInspectionStatus.ProtectedOrSystemProcess
                    : ProcessInspectionStatus.InformationUnavailable,
                error == ErrorAccessDenied);
        }

        try
        {
            if (!OpenProcessToken(processHandle, TokenQuery, out var token))
            {
                var error = Marshal.GetLastWin32Error();
                return new ProcessIntegrityInfo(
                    processId,
                    processName,
                    ProcessIntegrityLevel.Unknown,
                    error == ErrorAccessDenied
                        ? ProcessInspectionStatus.AccessDenied
                        : ProcessInspectionStatus.InformationUnavailable,
                    error == ErrorAccessDenied);
            }

            try
            {
                _ = GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out var required);
                if (required <= 0)
                {
                    return new ProcessIntegrityInfo(
                        processId,
                        processName,
                        ProcessIntegrityLevel.Unknown,
                        ProcessInspectionStatus.InformationUnavailable,
                        false);
                }

                var buffer = Marshal.AllocHGlobal(required);
                try
                {
                    if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, required, out _))
                    {
                        return new ProcessIntegrityInfo(
                            processId,
                            processName,
                            ProcessIntegrityLevel.Unknown,
                            ProcessInspectionStatus.InformationUnavailable,
                            false);
                    }

                    var label = Marshal.PtrToStructure<TokenMandatoryLabel>(buffer);
                    var subAuthorityCount = Marshal.ReadByte(GetSidSubAuthorityCount(label.Label.Sid));
                    if (subAuthorityCount == 0)
                    {
                        return new ProcessIntegrityInfo(
                            processId,
                            processName,
                            ProcessIntegrityLevel.Unknown,
                            ProcessInspectionStatus.InformationUnavailable,
                            false);
                    }

                    var ridPointer = GetSidSubAuthority(label.Label.Sid, (uint)(subAuthorityCount - 1));
                    var rid = Marshal.ReadInt32(ridPointer);
                    return new ProcessIntegrityInfo(
                        processId,
                        processName,
                        MapIntegrity(rid),
                        ProcessInspectionStatus.Available,
                        false);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    public static IntegrityComparison Compare(
        ProcessIntegrityLevel hanki,
        ProcessIntegrityLevel target)
    {
        if (hanki == ProcessIntegrityLevel.Unknown || target == ProcessIntegrityLevel.Unknown)
            return IntegrityComparison.Unknown;
        if (hanki == target)
            return IntegrityComparison.Same;
        return Rank(hanki) > Rank(target)
            ? IntegrityComparison.HankiHigher
            : IntegrityComparison.TargetHigher;
    }

    private static ProcessIntegrityLevel MapIntegrity(int rid) => rid switch
    {
        < SecurityMandatoryLowRid => ProcessIntegrityLevel.Untrusted,
        < SecurityMandatoryMediumRid => ProcessIntegrityLevel.Low,
        < SecurityMandatoryMediumPlusRid => ProcessIntegrityLevel.Medium,
        < SecurityMandatoryHighRid => ProcessIntegrityLevel.MediumPlus,
        < SecurityMandatorySystemRid => ProcessIntegrityLevel.High,
        _ => ProcessIntegrityLevel.System
    };

    private static int Rank(ProcessIntegrityLevel level) => level switch
    {
        ProcessIntegrityLevel.Untrusted => 0,
        ProcessIntegrityLevel.Low => 1,
        ProcessIntegrityLevel.Medium => 2,
        ProcessIntegrityLevel.MediumPlus => 3,
        ProcessIntegrityLevel.High => 4,
        ProcessIntegrityLevel.System => 5,
        ProcessIntegrityLevel.Protected => 6,
        _ => -1
    };

    private static bool IsSecureDesktopActive()
    {
        var desktop = OpenInputDesktop(0, false, DesktopReadObjects | DesktopSwitchDesktop);
        if (desktop == IntPtr.Zero)
            return true;

        try
        {
            _ = GetUserObjectInformation(desktop, UoiName, IntPtr.Zero, 0, out var needed);
            if (needed <= 0)
                return false;
            var buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!GetUserObjectInformation(desktop, UoiName, buffer, needed, out _))
                    return false;
                var name = Marshal.PtrToStringUni(buffer);
                return !string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseDesktop(desktop);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenMandatoryLabel
    {
        public SidAndAttributes Label;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthority);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(
        uint flags,
        [MarshalAs(UnmanagedType.Bool)] bool inherit,
        uint desiredAccess);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(IntPtr desktop);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformation(
        IntPtr handle,
        int index,
        IntPtr information,
        uint length,
        out uint needed);
}
