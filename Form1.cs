using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using static VoiceRelay.NativeMethods;

namespace VoiceRelay;

public partial class Form1 : Form
{
    // --- State ---
    private int _lUserID = -1;
    private bool _bLoggedIn = false;
    private bool _bIsTalking = false;
    private int _hVoiceHandle = -1;

    // WaveIn
    private IntPtr _hWaveIn = IntPtr.Zero;
    private WaveInProc? _waveInProc;
    private IntPtr[] _waveInHdrs = Array.Empty<IntPtr>();
    private IntPtr[] _waveInBufs = Array.Empty<IntPtr>();
    private bool _bWaveInOpen = false;
    private WAVEFORMATEX _waveFormat;

    // HTTP API (TcpListener-based, no admin required)
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _httpCts;
    private int _apiPort = 8888;

    // ==================== G.711 A-law ====================

    private static readonly short[] _segEnd = {
        0xFF, 0x1FF, 0x3FF, 0x7FF, 0xFFF, 0x1FFF, 0x3FFF, 0x7FFF
    };

    private static int Search(int val, short[] table, int size)
    {
        for (int i = 0; i < size; i++)
            if (val <= table[i]) return i;
        return size;
    }

    private static byte Linear2Alaw(short pcm)
    {
        int mask = (pcm >= 0) ? 0xD5 : 0x55;
        if (pcm < 0) pcm = (short)(-pcm - 1);
        if (pcm > 0x7FFF) pcm = 0x7FFF;

        int seg = Search(pcm, _segEnd, 8);
        if (seg >= 8) return (byte)(0x7F ^ mask);

        int aval = seg << 4;
        if (seg < 2)
            aval |= (pcm >> 4) & 0xF;
        else
            aval |= (pcm >> (seg + 3)) & 0xF;

        return (byte)(aval ^ mask);
    }

    // ==================== UI ====================
    public Form1()
    {
        InitializeComponent();
        CreateUI();
    }

    private TableLayoutPanel _root = null!;
    private TextBox tbIP = null!, tbPort = null!, tbUser = null!, tbPwd = null!;
    private TextBox tbApiPort = null!;
    private Button btnLogin = null!, btnTalk = null!;
    private RichTextBox tbLog = null!;

    private void CreateUI()
    {
        Text = "Voice Relay - PC to Camera Audio";
        ClientSize = new Size(500, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        MinimumSize = new Size(400, 360);

        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(8),
            ColumnCount = 4, RowCount = 6,
            ColumnStyles = {
                new ColumnStyle(SizeType.Absolute, 55),
                new ColumnStyle(SizeType.Percent, 100),
                new ColumnStyle(SizeType.Absolute, 55),
                new ColumnStyle(SizeType.AutoSize),
            },
            RowStyles = {
                new RowStyle(SizeType.AutoSize),
                new RowStyle(SizeType.AutoSize),
                new RowStyle(SizeType.AutoSize),
                new RowStyle(SizeType.AutoSize),
                new RowStyle(SizeType.Absolute, 36),
                new RowStyle(SizeType.Percent, 100),
            }
        };

        // Row 0: IP + Port
        _root.Controls.Add(new Label { Text = "IP:", TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill }, 0, 0);
        tbIP = new TextBox { Text = "10.0.10.74", Dock = DockStyle.Fill, Margin = new Padding(2, 4, 6, 4) };
        _root.Controls.Add(tbIP, 1, 0);
        _root.Controls.Add(new Label { Text = "Port:", TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill }, 2, 0);
        tbPort = new TextBox { Text = "8000", Dock = DockStyle.Fill, Margin = new Padding(2, 4, 2, 4),
            Width = 60 };
        _root.Controls.Add(tbPort, 3, 0);

        // Row 1: User + Pwd
        _root.Controls.Add(new Label { Text = "User:", TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill }, 0, 1);
        tbUser = new TextBox { Text = "admin", Dock = DockStyle.Fill, Margin = new Padding(2, 2, 6, 2) };
        _root.Controls.Add(tbUser, 1, 1);
        _root.Controls.Add(new Label { Text = "Pwd:", TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill }, 2, 1);
        tbPwd = new TextBox { Text = "dxwx12345", Dock = DockStyle.Fill,
            Margin = new Padding(2, 2, 2, 2), PasswordChar = '*', Width = 80 };
        _root.Controls.Add(tbPwd, 3, 1);

        // Row 2: API Port
        _root.Controls.Add(new Label { Text = "API:", TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill, ForeColor = Color.DarkBlue }, 0, 2);
        tbApiPort = new TextBox { Text = "8888", Dock = DockStyle.Fill, Margin = new Padding(2, 2, 6, 2),
            Width = 60 };
        _root.Controls.Add(tbApiPort, 1, 2);
        var lblApi = new Label
        {
            Text = "127.0.0.1:8888/open  /close",
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            ForeColor = Color.Gray,
            AutoSize = true
        };
        _root.SetColumnSpan(lblApi, 2);
        _root.Controls.Add(lblApi, 2, 2);

        // Row 3: Buttons
        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 0, 6) };
        btnLogin = new Button { Text = "Login", Width = 90, Height = 32 };
        btnLogin.Click += BtnLogin_Click;
        btnTalk = new Button { Text = "Start Relay", Width = 120, Height = 32, Enabled = false };
        btnTalk.Click += BtnTalk_Click;
        btnPanel.Controls.Add(btnLogin);
        btnPanel.Controls.Add(btnTalk);
        _root.SetColumnSpan(btnPanel, 4);
        _root.Controls.Add(btnPanel, 0, 3);

        // Row 4: Label
        var lblHint = new Label { Text = "Status (errors will show here):",
            Dock = DockStyle.Fill, ForeColor = Color.Gray };
        _root.SetColumnSpan(lblHint, 4);
        _root.Controls.Add(lblHint, 0, 4);

        // Row 5: Log
        tbLog = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true,
            BackColor = Color.White, Font = new Font("Consolas", 9),
            WordWrap = false, ScrollBars = RichTextBoxScrollBars.Both };
        _root.SetColumnSpan(tbLog, 4);
        _root.Controls.Add(tbLog, 0, 5);

        Controls.Add(_root);
        FormClosing += Form1_FormClosing;
        Load += Form1_Load;
    }

    private void Form1_Load(object? sender, EventArgs e)
    {
        StartHttpServer();
    }

    private void Log(string msg)
    {
        if (InvokeRequired)
            Invoke(() => LogInternal(msg));
        else LogInternal(msg);
    }

    private void LogInternal(string msg)
    {
        string ts = DateTime.Now.ToString("HH:mm:ss");
        tbLog.AppendText($"[{ts}] {msg}\r\n");
        tbLog.ScrollToCaret();
    }

    // ==================== HTTP API (TcpListener, no admin) ====================

    private void StartHttpServer()
    {
        _apiPort = int.TryParse(tbApiPort.Text, out int p) ? p : 8888;

        try
        {
            _tcpListener = new TcpListener(IPAddress.Loopback, _apiPort);
            _tcpListener.Start();
            _httpCts = new CancellationTokenSource();

            Task.Run(() => TcpListenLoop(_httpCts.Token));

            Log($"API: http://127.0.0.1:{_apiPort}/open  /close  /status");
        }
        catch (Exception ex)
        {
            Log($"WARN: API start failed: {ex.Message}");
        }
    }

    private async Task TcpListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient? client = null;
            try { client = await _tcpListener!.AcceptTcpClientAsync().WaitAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch { continue; }

            _ = Task.Run(() => HandleTcpClient(client));
        }
    }

    private void HandleTcpClient(TcpClient client)
    {
        try
        {
            using var stream = client.GetStream();
            byte[] buf = new byte[4096];
            int n = stream.Read(buf, 0, buf.Length);
            if (n == 0) return;

            string request = Encoding.ASCII.GetString(buf, 0, n);
            string firstLine = request.Split('\r', '\n')[0];
            string[] parts = firstLine.Split(' ');
            string path = parts.Length > 1 ? parts[1] : "/status";

            string result;
            if (path.StartsWith("/open"))
                result = ApiOpen();
            else if (path.StartsWith("/close"))
                result = ApiClose();
            else
                result = ApiStatus();

            byte[] body = Encoding.UTF8.GetBytes(result);
            byte[] header = Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                "Connection: close\r\n\r\n");

            stream.Write(header, 0, header.Length);
            stream.Write(body, 0, body.Length);
        }
        catch { /* client disconnected */ }
        finally { try { client.Close(); } catch { } }
    }

    private string ApiOpen()
    {
        if (_bIsTalking)
            return "{\"ok\":true,\"action\":\"open\",\"msg\":\"already open\"}";

        // Auto-login if not logged in
        if (!_bLoggedIn)
        {
            Invoke(() => DoLogin());
            if (!_bLoggedIn)
                return "{\"ok\":false,\"action\":\"open\",\"msg\":\"login failed\"}";
        }

        // Start voice relay
        Invoke(() => StartVoiceRelay());

        if (_bIsTalking)
        {
            Log("API: /open -> channel opened");
            return "{\"ok\":true,\"action\":\"open\",\"msg\":\"channel opened\"}";
        }
        return "{\"ok\":false,\"action\":\"open\",\"msg\":\"start failed\"}";
    }

    private string ApiClose()
    {
        if (!_bIsTalking)
            return "{\"ok\":true,\"action\":\"close\",\"msg\":\"already closed\"}";

        Invoke(() => StopVoiceRelay());

        Log("API: /close -> channel closed");
        return "{\"ok\":true,\"action\":\"close\",\"msg\":\"channel closed\"}";
    }

    private string ApiStatus()
    {
        string state = _bIsTalking ? "open" : "closed";
        string logged = _bLoggedIn ? "true" : "false";
        return $"{{\"ok\":true,\"state\":\"{state}\",\"loggedIn\":{logged}}}";
    }

    private void StopHttpServer()
    {
        _httpCts?.Cancel();
        _tcpListener?.Stop();
    }

    // ==================== Login ====================

    private void BtnLogin_Click(object? sender, EventArgs e)
    {
        if (!_bLoggedIn) DoLogin();
        else DoLogout();
    }

    private void DoLogin()
    {
        string ip = tbIP.Text.Trim();
        ushort port = ushort.TryParse(tbPort.Text, out ushort p) ? p : (ushort)8000;
        string user = tbUser.Text;
        string pwd = tbPwd.Text;

        // Try V40 first
        var loginInfo = new NET_DVR_USER_LOGIN_INFO
        {
            sDeviceAddress = ip,
            wPort = port,
            sUserName = user,
            sPassword = pwd,
            bUseAsynLogin = 0,
            byRes3 = new byte[119]
        };

        var devInfo40 = new NET_DVR_DEVICEINFO_V40();
        _lUserID = NET_DVR_Login_V40(ref loginInfo, ref devInfo40);

        if (_lUserID < 0)
        {
            uint errV40 = NET_DVR_GetLastError();
            Log($"V40 login failed, err={errV40}. Trying V30...");

            // Fallback to V30
            var devInfo30 = new NET_DVR_DEVICEINFO_V30();
            _lUserID = NET_DVR_Login_V30(ip, port, user, pwd, ref devInfo30);

            if (_lUserID < 0)
            {
                uint errV30 = NET_DVR_GetLastError();
                Log($"Login FAILED - V40 err={errV40}, V30 err={errV30}");
                return;
            }
        }

        _bLoggedIn = true;
        btnLogin.Text = "Logout";
        btnTalk.Enabled = true;

        Log($"Login OK - userID={_lUserID}");
    }

    private void DoLogout()
    {
        if (_bIsTalking) StopVoiceRelay();
        if (_lUserID >= 0)
        {
            NET_DVR_Logout_V30(_lUserID);
            NET_DVR_Logout(_lUserID);
            _lUserID = -1;
        }
        _bLoggedIn = false;
        btnLogin.Text = "Login";
        btnTalk.Enabled = false;
        Log("Logged out");
    }

    // ==================== Voice Relay ====================

    private void BtnTalk_Click(object? sender, EventArgs e)
    {
        if (!_bIsTalking) StartVoiceRelay();
        else StopVoiceRelay();
    }

    private void StartVoiceRelay()
    {
        try
        {
            _waveFormat = new WAVEFORMATEX
            {
                wFormatTag = WAVE_FORMAT_PCM,
                nChannels = CHANNELS,
                nSamplesPerSec = SAMPLE_RATE,
                wBitsPerSample = BITS_PER_SAMPLE,
                nBlockAlign = (ushort)(CHANNELS * BITS_PER_SAMPLE / 8),
                nAvgBytesPerSec = SAMPLE_RATE * (uint)(CHANNELS * BITS_PER_SAMPLE / 8),
                cbSize = 0
            };

            _hVoiceHandle = NET_DVR_StartVoiceCom_MR_V30(
                _lUserID, 1, null, IntPtr.Zero);

            if (_hVoiceHandle < 0)
            {
                uint err = NET_DVR_GetLastError();
                Log($"StartVoiceCom FAILED - err={err}");
                return;
            }
            Log($"Voice channel started - handle={_hVoiceHandle}");

            if (!OpenWaveIn())
            {
                Log("Mic capture FAILED");
                NET_DVR_StopVoiceCom(_hVoiceHandle);
                _hVoiceHandle = -1;
                return;
            }

            _bIsTalking = true;
            btnTalk.Text = "Stop Relay";
            Log("Talking - speak into microphone");
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
            StopVoiceRelay();
        }
    }

    private void StopVoiceRelay()
    {
        _bIsTalking = false;

        if (_bWaveInOpen)
        {
            waveInReset(_hWaveIn);
            FreeWaveInBuffers();
            waveInClose(_hWaveIn);
            _hWaveIn = IntPtr.Zero;
            _bWaveInOpen = false;
        }

        if (_hVoiceHandle >= 0)
        {
            NET_DVR_StopVoiceCom(_hVoiceHandle);
            _hVoiceHandle = -1;
        }

        btnTalk.Text = "Start Relay";
        Log("Stopped");
    }

    // ==================== WaveIn ====================

    private bool OpenWaveIn()
    {
        _waveInProc = WaveInCallback;
        int mmr = waveInOpen(out _hWaveIn, 0, ref _waveFormat,
            _waveInProc, IntPtr.Zero, CALLBACK_FUNCTION);
        if (mmr != 0)
        {
            Log($"waveInOpen failed, mmr={mmr}");
            return false;
        }

        int hdrSize = Marshal.SizeOf<WAVEHDR>();
        _waveInHdrs = new IntPtr[BUF_NUM];
        _waveInBufs = new IntPtr[BUF_NUM];

        for (int i = 0; i < BUF_NUM; i++)
        {
            _waveInBufs[i] = Marshal.AllocHGlobal(FRAME_BYTES);
            var hdr = new WAVEHDR
            {
                lpData = _waveInBufs[i],
                dwBufferLength = FRAME_BYTES
            };
            IntPtr ptr = Marshal.AllocHGlobal(hdrSize);
            Marshal.StructureToPtr(hdr, ptr, false);
            _waveInHdrs[i] = ptr;

            waveInPrepareHeader(_hWaveIn, ptr, (uint)hdrSize);
            waveInAddBuffer(_hWaveIn, ptr, (uint)hdrSize);
        }

        waveInStart(_hWaveIn);
        _bWaveInOpen = true;
        Log("Mic capture started");
        return true;
    }

    private void FreeWaveInBuffers()
    {
        int hdrSize = Marshal.SizeOf<WAVEHDR>();
        for (int i = 0; i < _waveInHdrs.Length; i++)
        {
            if (_waveInHdrs[i] != IntPtr.Zero)
            {
                waveInUnprepareHeader(_hWaveIn, _waveInHdrs[i], (uint)hdrSize);
                Marshal.FreeHGlobal(_waveInHdrs[i]);
            }
            if (_waveInBufs[i] != IntPtr.Zero)
                Marshal.FreeHGlobal(_waveInBufs[i]);
        }
    }

    private void WaveInCallback(IntPtr hwi, uint uMsg, IntPtr dwInstance,
        IntPtr dwParam1, IntPtr dwParam2)
    {
        if (uMsg != WIM_DATA || !_bIsTalking) return;

        var hdr = Marshal.PtrToStructure<WAVEHDR>(dwParam1);
        if (hdr.lpData == IntPtr.Zero || hdr.dwBytesRecorded < FRAME_BYTES)
        {
            if (_bIsTalking)
                waveInAddBuffer(_hWaveIn, dwParam1, (uint)Marshal.SizeOf<WAVEHDR>());
            return;
        }

        byte[] rawBytes = new byte[FRAME_BYTES];
        Marshal.Copy(hdr.lpData, rawBytes, 0, FRAME_BYTES);
        short[] pcmBuf = new short[FRAME_SAMPLES];
        Buffer.BlockCopy(rawBytes, 0, pcmBuf, 0, FRAME_BYTES);

        byte[] g711Buf = new byte[G711_FRAME];
        for (int i = 0; i < FRAME_SAMPLES; i++)
            g711Buf[i] = Linear2Alaw(pcmBuf[i]);

        if (!NET_DVR_VoiceComSendData(_hVoiceHandle, g711Buf, G711_FRAME))
        {
            uint err = NET_DVR_GetLastError();
            Log($"Send FAILED - err={err}");
        }

        if (_bIsTalking)
            waveInAddBuffer(_hWaveIn, dwParam1, (uint)Marshal.SizeOf<WAVEHDR>());
    }

    // ==================== Cleanup ====================

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        StopHttpServer();
        if (_bIsTalking) StopVoiceRelay();
        if (_bLoggedIn) DoLogout();
    }
}
