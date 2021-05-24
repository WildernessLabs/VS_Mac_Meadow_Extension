using System;
using System.Diagnostics;
using System.IO;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    public static class ManualLink
    {
        // monolinker -l all -c link -o ./linked -a ./Meadow.dll -a ./app.exe]

        public static string LinkFolder => "linked";

        public static void LinkApp(string path)
        {
            var psi = new ProcessStartInfo
            {
                WorkingDirectory = path,
                FileName = "monolinker",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Arguments = "-l all -c link -o ./linked -a ./Meadow.dll -a ./App.exe"
            };

          //  string output = string.Empty;

            using (var p = Process.Start(psi))
            {
                if (p != null)
                {
                //    output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                }
            }
        }


    }
}
