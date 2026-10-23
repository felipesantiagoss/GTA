// Somente o atalho autorizado da Game Bar. Nunca força foco ou eleva privilégios.
public static class AtalhoGravacaoDiretor
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Key, Scan;
        public uint Flags, Time;
        public System.UIntPtr Extra;
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X, Y;
        public uint Data, Flags, Time;
        public System.UIntPtr Extra;
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    private struct InputUnion
    {
        [System.Runtime.InteropServices.FieldOffset(0)] public KeyboardInput Keyboard;
        [System.Runtime.InteropServices.FieldOffset(0)] public MouseInput Mouse;
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Value;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern System.IntPtr GetForegroundWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(System.IntPtr window, out uint process);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    public static bool IsGameForeground
    {
        get
        {
            uint foregroundProcess;
            GetWindowThreadProcessId(GetForegroundWindow(), out foregroundProcess);
            using (System.Diagnostics.Process current = System.Diagnostics.Process.GetCurrentProcess())
                return foregroundProcess == (uint)current.Id && current.ProcessName == "GTA5";
        }
    }

    public static void Toggle()
    {
        if (!IsGameForeground)
            throw new System.InvalidOperationException("Gravação automática: deixe o GTA Legacy em primeiro plano; use Win+Alt+R manualmente se necessário.");
        foreach (int key in new int[] { 0x5B, 0x5C, 0x12, 0x11, 0x10, 0x52 })
            if ((GetAsyncKeyState(key) & 0x8000) != 0)
                throw new System.InvalidOperationException("Gravação automática: solte Win, Alt, Ctrl, Shift e R antes da ação.");
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Input));
        if (size != (System.IntPtr.Size == 8 ? 40 : 28))
            throw new System.InvalidOperationException("Gravação automática: formato nativo de teclado incompatível.");
        Input[] sequence = new Input[] { Key(0x5B, false), Key(0x12, false), Key(0x52, false),
            Key(0x52, true), Key(0x12, true), Key(0x5B, true) };
        uint sent = SendInput((uint)sequence.Length, sequence, size);
        if (sent != sequence.Length)
        {
            // Libera somente as teclas que estavam soltas antes da tentativa;
            // não repete o atalho, que poderia inverter uma gravação existente.
            Input[] release = new Input[] { Key(0x52, true), Key(0x12, true), Key(0x5B, true) };
            SendInput((uint)release.Length, release, size);
            throw new System.InvalidOperationException("O Windows não aceitou o atalho completo. Automação suspensa; use Win+Alt+R manualmente.");
        }
        // Aceitar os eventos não comprova que Game Bar iniciou/parou. A captura
        // só é considerada salva após MP4 real, duração e hashes conferidos.
    }
    private static Input Key(ushort key, bool up)
    {
        Input result = new Input();
        result.Type = 1;
        result.Value.Keyboard.Key = key;
        result.Value.Keyboard.Flags = up ? 2u : 0u;
        return result;
    }
}
