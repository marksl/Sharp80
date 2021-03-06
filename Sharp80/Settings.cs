﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;

namespace Sharp80
{
    internal static class Settings
    {
        private static bool? advancedView;
        private static bool? altKeyboardLayout;
        private static bool? autoStartOnReset;
        private static ushort? breakpoint;
        private static bool? breakpointOn;
        private static string defaultFloppyDirectory;
        private static string disk0Filename;
        private static string disk1Filename;
        private static string disk2Filename;
        private static string disk3Filename;
        private static bool? diskEnabled;
        private static bool? driveNoise;
        private static bool? greenScreen;
        private static string lastAsmFile;
        private static string lastCmdFile;
        private static string lastTapeFile;
        private static string lastSnapshotFile;
        private static bool? soundOn;
        private static bool? normalSpeed;

        private static bool? fullScreen;
        private static int? windowX;
        private static int? windowY;
        private static int? windowWidth;
        private static int? windowHeight;


        static Settings()
        {
#if DEBUG
         //   Properties.Settings.Default.Reset();
#endif
        }

        public static bool AdvancedView
        {
            get => advancedView ?? (advancedView = Properties.Settings.Default.advanced_view).Value;
            set => advancedView = value;
        }
        public static bool AltKeyboardLayout
        {
            get => altKeyboardLayout ?? (altKeyboardLayout = Properties.Settings.Default.alt_keyboard_layout).Value;
            set => altKeyboardLayout = value;
        }
        public static bool AutoStartOnReset
        {
            get => autoStartOnReset ?? (autoStartOnReset = Properties.Settings.Default.auto_start_on_reset).Value;
            set => autoStartOnReset = value;
        }
        public static ushort Breakpoint
        {
            get => breakpoint ?? (breakpoint = Properties.Settings.Default.breakpoint).Value;
            set => breakpoint = value;
        }
        public static bool BreakpointOn
        {
            get => breakpointOn ?? (breakpointOn = Properties.Settings.Default.breakpoint_on).Value;
            set => breakpointOn = value;
        }
        public static string DefaultFloppyDirectory
        {
            get
            {
                defaultFloppyDirectory = defaultFloppyDirectory ?? Properties.Settings.Default.default_floppy_directory;

                if (String.IsNullOrWhiteSpace(defaultFloppyDirectory) || !System.IO.Directory.Exists(defaultFloppyDirectory))
                    defaultFloppyDirectory = Storage.DocsPath;

                return defaultFloppyDirectory;
            }
            set => defaultFloppyDirectory = value;
        }
        public static string Disk0Filename
        {
            get
            {
                disk0Filename = disk0Filename ?? Properties.Settings.Default.disk0;

                if (String.IsNullOrWhiteSpace(disk0Filename))
                    disk0Filename = System.IO.Path.Combine(Storage.AppDataPath, @"Disks\");

                return disk0Filename;
            }
            set { disk0Filename = value; }
        }
        public static string Disk1Filename
        {
            get
            {
                disk1Filename = disk1Filename ?? Properties.Settings.Default.disk1;

                if (String.IsNullOrWhiteSpace(disk1Filename))
                    disk1Filename = System.IO.Path.Combine(Storage.AppDataPath, @"Disks\");

                return disk1Filename;
            }
            set { disk1Filename = value; }
        }
        public static string Disk2Filename
        {
            get
            {
                disk2Filename = disk2Filename ?? Properties.Settings.Default.disk2;

                if (String.IsNullOrWhiteSpace(disk2Filename))
                    disk2Filename = System.IO.Path.Combine(Storage.AppDataPath, @"Disks\");

                return disk2Filename;
            }
            set { disk2Filename = value; }
        }
        public static string Disk3Filename
        {
            get
            {
                disk3Filename = disk3Filename ?? Properties.Settings.Default.disk3;

                if (String.IsNullOrWhiteSpace(disk3Filename))
                    disk3Filename = System.IO.Path.Combine(Storage.AppDataPath, @"Disks\");

                return disk3Filename;
            }
            set { disk3Filename = value; }
        }
        public static bool DiskEnabled
        {
            get => diskEnabled ?? (diskEnabled = Properties.Settings.Default.disk_enabled).Value;
            set => diskEnabled = value;
        }
        public static bool DriveNoise
        {
            get => driveNoise ?? (driveNoise = Properties.Settings.Default.drive_noise).Value;
            set => driveNoise = value;
        }
        public static bool GreenScreen
        {
            get => greenScreen ?? (greenScreen = Properties.Settings.Default.green_screen).Value;
            set => greenScreen = value;
        }
        public static string LastAsmFile
        {
            get
            {
                lastAsmFile = lastAsmFile ?? Properties.Settings.Default.last_asm_file;

                if (String.IsNullOrWhiteSpace(lastAsmFile))
                    lastAsmFile = System.IO.Path.Combine(Storage.AppDataPath, @"ASM Files\");

                return lastAsmFile;
            }
            set { lastAsmFile = value; }
        }
        public static string LastCmdFile
        {
            get
            {
                lastCmdFile = lastCmdFile ?? Properties.Settings.Default.last_cmd_file;

                if (String.IsNullOrWhiteSpace(lastCmdFile))
                    lastCmdFile = System.IO.Path.Combine(Storage.AppDataPath, @"CMD Files\");

                return lastCmdFile;
            }
            set { lastCmdFile = value; }
        }
        public static string LastSnapshotFile
        {
            get => lastSnapshotFile ?? (lastSnapshotFile = Properties.Settings.Default.last_snapshot_file);
            set => lastSnapshotFile = value;
        }
        public static string LastTapeFile
        {
            get
            {
                lastTapeFile = lastTapeFile ?? Properties.Settings.Default.last_tape_file;

                if (String.IsNullOrWhiteSpace(lastTapeFile))
                    lastTapeFile = System.IO.Path.Combine(Storage.AppDataPath, @"Tapes\");

                return lastTapeFile;
            }
            set { lastTapeFile = value; }
        }
        public static bool SoundOn
        {
            get => soundOn ?? (soundOn = Properties.Settings.Default.sound).Value;
            set => soundOn = value;
        }
        public static bool NormalSpeed
        {
            get => normalSpeed ?? (normalSpeed = Properties.Settings.Default.normal_speed).Value;
            set => normalSpeed = value;
        }
        public static bool FullScreen
        {
            get => fullScreen ?? (fullScreen = Properties.Settings.Default.full_screen).Value;
            set => fullScreen = value;
        }
        public static int WindowX
        {
            get => windowX ?? (windowX = Properties.Settings.Default.window_x).Value;
            set => windowX = value;
        }
        public static int WindowY
        {
            get => windowY ?? (windowY = Properties.Settings.Default.window_y).Value;
            set => windowY = value;
        }
        public static int WindowWidth
        {
            get => windowWidth ?? (windowWidth = Properties.Settings.Default.window_width).Value;
            set => windowWidth = value;
        }
        public static int WindowHeight
        {
            get => windowHeight ?? (windowHeight = Properties.Settings.Default.window_height).Value;
            set => windowHeight = value;
        }
        public static void Save()
        {
            var psd = Properties.Settings.Default;

            if (advancedView.HasValue)
                psd.advanced_view = advancedView.Value;
            if (autoStartOnReset.HasValue)
                psd.auto_start_on_reset = autoStartOnReset.Value;
            if (breakpoint.HasValue)
                psd.breakpoint = breakpoint.Value;
            if (breakpointOn.HasValue)
                psd.breakpoint_on = breakpointOn.Value;
            if (!String.IsNullOrWhiteSpace(defaultFloppyDirectory))
                psd.default_floppy_directory = defaultFloppyDirectory;
            if (disk0Filename != null)
                psd.disk0 = disk0Filename;
            if (disk1Filename != null)
                psd.disk1 = disk1Filename;
            if (disk2Filename != null)
                psd.disk2 = disk2Filename;
            if (disk3Filename != null)
                psd.disk3 = disk3Filename;
            if (diskEnabled.HasValue)
                psd.disk_enabled = diskEnabled.Value;
            if (driveNoise.HasValue)
                psd.drive_noise = driveNoise.Value;
            if (greenScreen.HasValue)
                psd.green_screen = greenScreen.Value;
            if (!String.IsNullOrWhiteSpace(lastAsmFile))
                psd.last_asm_file = lastAsmFile;
            if (!String.IsNullOrWhiteSpace(lastCmdFile))
                psd.last_cmd_file = lastCmdFile;
            if (!String.IsNullOrWhiteSpace(lastSnapshotFile))
                psd.last_snapshot_file = lastSnapshotFile;
            if (!String.IsNullOrWhiteSpace(lastTapeFile))
                psd.last_tape_file = lastTapeFile;
            if (soundOn.HasValue)
                psd.sound = soundOn.Value;
            if (normalSpeed.HasValue)
                psd.normal_speed = normalSpeed.Value;
            if (fullScreen.HasValue)
                psd.full_screen = fullScreen.Value;
            if (windowX.HasValue)
                psd.window_x = windowX.Value;
            if (windowY.HasValue)
                psd.window_y = windowY.Value;
            if (windowWidth.HasValue)
                psd.window_width = windowWidth.Value;
            if (windowHeight.HasValue)
                psd.window_height = windowHeight.Value;
            Properties.Settings.Default.Save();
        }
    }
}
