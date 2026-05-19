using static VoiceRelay.NativeMethods;

namespace VoiceRelay;

static class Program
{
    [STAThread]
    static void Main()
    {
        NET_DVR_Init();
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
        NET_DVR_Cleanup();
    }
}
