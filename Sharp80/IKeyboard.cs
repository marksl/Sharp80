﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp80
{
    /// <summary>
    /// This interface is used to allow for future non-DirectInput based implementations
    /// </summary>
    internal interface IKeyboard : IDisposable
    {
        Task Start(float PollRateInHz, KeyPressedDelegate Callback, CancellationToken StopToken);
        bool Enabled { get; set; }
        bool IsShifted { get; }
        bool LeftShiftPressed { get; }
        bool RightShiftPressed { get; }
        bool IsControlPressed { get; }
        bool IsAltPressed { get; }

        void Refresh();
    }
}