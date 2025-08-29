using System;
using System.Collections.Generic;

namespace OpenCNCPilot.Hardware.Services
{
    public enum GCodeSenderState
    {
        Idle,
        Sending,
        Paused
    }

    public interface IGCodeSender : IDisposable
    {
        GCodeSenderState State { get; }
        int FilePosition { get; }
        int FileLength { get; }
        TimeSpan Runtime { get; }
        TimeSpan EstimatedDuration { get; }
        bool PauseOnHold { get; set; }
        bool IsSending { get; }
        string CurrentLineText { get; }

        void Load(IEnumerable<string> lines);
        void Start();
        void Pause();
        void Clear();
        void Goto(int lineIndex);

        event EventHandler<GCodeSenderState> StateChanged;
        event EventHandler<int> PositionChanged;
        event EventHandler<string> ErrorOccurred;
    }
}
