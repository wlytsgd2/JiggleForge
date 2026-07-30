using System;
using System.Collections.Generic;
using System.Threading;

internal sealed class KeyPulseQueue : IDisposable
{
    private readonly Queue<byte> queue = new();
    private readonly AutoResetEvent wake = new(false);
    private readonly Thread worker;
    private readonly int pulseMilliseconds;
    private volatile bool stopping;

    internal KeyPulseQueue(int pulseMilliseconds)
    {
        this.pulseMilliseconds = pulseMilliseconds;
        worker = new Thread(WorkLoop) { IsBackground = true, Name = "JiggleForge Wheel Key Pulse" };
        worker.Start();
    }

    internal void Enqueue(byte virtualKey)
    {
        lock (queue)
        {
            queue.Enqueue(virtualKey);
        }
        wake.Set();
    }

    private void WorkLoop()
    {
        while (!stopping)
        {
            byte virtualKey = 0;
            lock (queue)
            {
                if (queue.Count > 0)
                {
                    virtualKey = queue.Dequeue();
                }
            }

            if (virtualKey == 0)
            {
                wake.WaitOne(250);
                continue;
            }

            NativeMethods.keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
            Thread.Sleep(pulseMilliseconds);
            NativeMethods.keybd_event(virtualKey, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(8);
        }

        NativeMethods.keybd_event(NativeMethods.VK_F13, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_F14, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public void Dispose()
    {
        stopping = true;
        wake.Set();
        worker.Join(500);
        wake.Dispose();
    }
}
