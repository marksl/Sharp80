﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp80
{
    internal interface IScreen : ISerializable, IDisposable
    {
        Task Start(float RefreshRateHz, CancellationToken StopToken);
        bool Suspend { set; }

        bool IsFullScreen { get; set; }
        bool AdvancedView { get; }
        bool WideCharMode { get; set; }
        bool AltCharMode { get; set; }
        string StatusMessage { set; }
        
        void Reset();
        void Initialize(IAppWindow Parent);
        void Reinitialize(Computer Computer);
    }
}
