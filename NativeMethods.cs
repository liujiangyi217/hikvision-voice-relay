using System.Runtime.InteropServices;

namespace VoiceRelay;

public static class NativeMethods
{
    // Audio: 8kHz mono 16-bit (matching Linux code)
    public const int SAMPLE_RATE = 8000;
    public const int CHANNELS = 1;
    public const int BITS_PER_SAMPLE = 16;
    public const int FRAME_SAMPLES = 160;   // 20ms @ 8kHz
    public const int FRAME_BYTES = 320;     // PCM: 160*2
    public const int G711_FRAME = 160;      // G.711A: 160 bytes
    public const int BUF_NUM = 8;

    public const int WAVE_FORMAT_PCM = 1;
    public const int CALLBACK_FUNCTION = 0x30000;
    public const int WIM_DATA = 0x3C0;
    public const int WHDR_INQUEUE = 0x10;

    // Max lengths
    public const int NET_DVR_DEV_ADDRESS_MAX_LEN = 129;
    public const int NET_DVR_LOGIN_USERNAME_MAX_LEN = 64;
    public const int NET_DVR_LOGIN_PASSWD_MAX_LEN = 64;

    // ==================== HCNetSDK ====================

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern bool NET_DVR_Init();

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern bool NET_DVR_Cleanup();

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int NET_DVR_Login_V40(
        ref NET_DVR_USER_LOGIN_INFO pLoginInfo,
        ref NET_DVR_DEVICEINFO_V40 lpDeviceInfo);

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int NET_DVR_Login_V30(
        string sDVRIP, ushort wDVRPort, string sUserName, string sPassword,
        ref NET_DVR_DEVICEINFO_V30 lpDeviceInfo);

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern bool NET_DVR_Logout(int lUserID);

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern bool NET_DVR_Logout_V30(int lUserID);

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern uint NET_DVR_GetLastError();

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern int NET_DVR_StartVoiceCom_MR_V30(
        int lUserID, uint dwVoiceChan,
        VoiceDataCallback? fVoiceDataCallBack, IntPtr pUser);

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern bool NET_DVR_StopVoiceCom(int lVoiceComHandle);

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern bool NET_DVR_VoiceComSendData(
        int lVoiceComHandle, byte[] pSendBuf, uint dwBufSize);

    [DllImport("HCNetSDK.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern bool NET_DVR_SetLogToFile(int nLogLevel, string strLogDir, int bAutoDel);

    // ==================== WinMM Audio ====================

    [DllImport("winmm.dll")]
    public static extern int waveInOpen(
        out IntPtr phwi, uint uDeviceID, ref WAVEFORMATEX pwfx,
        WaveInProc dwCallback, IntPtr dwInstance, uint fdwOpen);

    [DllImport("winmm.dll")]
    public static extern int waveInStart(IntPtr hwi);
    [DllImport("winmm.dll")]
    public static extern int waveInReset(IntPtr hwi);
    [DllImport("winmm.dll")]
    public static extern int waveInClose(IntPtr hwi);
    [DllImport("winmm.dll")]
    public static extern int waveInPrepareHeader(IntPtr hwi, IntPtr pwh, uint cbwh);
    [DllImport("winmm.dll")]
    public static extern int waveInUnprepareHeader(IntPtr hwi, IntPtr pwh, uint cbwh);
    [DllImport("winmm.dll")]
    public static extern int waveInAddBuffer(IntPtr hwi, IntPtr pwh, uint cbwh);
}

// ==================== Structures ====================

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct NET_DVR_USER_LOGIN_INFO
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
    public string sDeviceAddress;
    public byte byUseTransport;
    public ushort wPort;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string sUserName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string sPassword;
    public IntPtr cbLoginResult;
    public IntPtr pUser;
    public int bUseAsynLogin;
    public byte byProxyType;
    public byte byUseUTCTime;
    public byte byLoginMode;
    public byte byHttps;
    public int iProxyID;
    public byte byVerifyMode;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 119)]
    public byte[] byRes3;
}

[StructLayout(LayoutKind.Sequential)]
public struct NET_DVR_DEVICEINFO_V40
{
    public NET_DVR_DEVICEINFO_V30 struDeviceV30;
    public byte bySupportLock;
    public byte byRetryLoginTime;
    public byte byPasswordLevel;
    public byte byProxyType;
    public uint dwSurplusLockTime;
    public byte byCharEncodeType;
    public byte bySupportDev5;
    public byte bySupport;
    public byte byLoginMode;
    public uint dwOEMCode;
    public int iResidualValidity;
    public byte byResidualValidity;
    public byte bySingleStartDTalkChan;
    public byte bySingleDTalkChanNums;
    public byte byPassWordResetLevel;
    public byte bySupportStreamEncrypt;
    public byte byMarketType;
    public byte byTLSCap;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 237)]
    public byte[] byRes2;
}

[StructLayout(LayoutKind.Sequential)]
public struct NET_DVR_DEVICEINFO_V30
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
    public byte[] sSerialNumber;
    public byte byAlarmInPortNum;
    public byte byAlarmOutPortNum;
    public byte byDiskNum;
    public byte byDVRType;
    public byte byChanNum;
    public byte byStartChan;
    public byte byAudioChanNum;
    public byte byIPChanNum;
    public byte byZeroChanNum;
    public byte byMainProto;
    public byte bySubProto;
    public byte bySupport;
    public byte bySupport1;
    public byte bySupport2;
    public ushort wDevType;
    public byte bySupport3;
    public byte byMultiStreamProto;
    public byte byStartDChan;
    public byte byStartDTalkChan;
    public byte byHighDChanNum;
    public byte bySupport4;
    public byte byLanguageType;
    public byte byVoiceInChanNum;
    public byte byStartVoiceInChanNo;
    public byte bySupport5;
    public byte bySupport6;
    public byte byMirrorChanNum;
    public ushort wStartMirrorChanNo;
    public byte bySupport7;
    public byte byRes2;
}

[StructLayout(LayoutKind.Sequential)]
public struct WAVEFORMATEX
{
    public ushort wFormatTag;
    public ushort nChannels;
    public uint nSamplesPerSec;
    public uint nAvgBytesPerSec;
    public ushort nBlockAlign;
    public ushort wBitsPerSample;
    public ushort cbSize;
}

[StructLayout(LayoutKind.Sequential)]
public struct WAVEHDR
{
    public IntPtr lpData;
    public uint dwBufferLength;
    public uint dwBytesRecorded;
    public IntPtr dwUser;
    public uint dwFlags;
    public uint dwLoops;
    public IntPtr lpNext;
    public IntPtr reserved;
}

// ==================== Delegates ====================

public delegate void VoiceDataCallback(
    int lVoiceComHandle, IntPtr pRecvDataBuffer,
    uint dwBufSize, byte byAudioFlag, IntPtr pUser);

public delegate void WaveInProc(
    IntPtr hwi, uint uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);
