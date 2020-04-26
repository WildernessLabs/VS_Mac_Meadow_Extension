using MeadowCLI.DeviceManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.IO.Compression;
using DfuSharp;
using System.Threading;
using MeadowCLI;
using Mono.Unix.Native;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{

    /// <summary>
    /// Interaction logic for MeadowWindowControl.
    /// </summary>
    public class MeadowOS
    {
        readonly string osFileName = "Meadow.OS.bin";
        static readonly string versionCheckUrl = "https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/Meadow_Beta/latest.json";
        string latestJson = "latest.json";
        
        static public string     FirmareVersion              { get; private set;}
        static public Uri        FirmareVersionUrl           { get; private set;}
        static public DateTime?  FirmareVersionLastChecked   { get; private set;}        
        const  uint              FirmwareCheckEveryMinutes = 2 * 60;
                
        static public string FirmwarePath { get; private set;} 
                
        MeadowPad meadowPad;

        //[SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        //[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]

        public MeadowOS(MeadowPad meadowPad)
        {
            this.meadowPad = meadowPad;
        }


        public async Task<int> FlashFirmware(int timeout = 3 * 60 * 1000)
        {
            if (String.IsNullOrEmpty(FirmwarePath)) await Download_Firmware();
            
            var osFile = Path.Combine(FirmwarePath, osFileName);
            
            if (!File.Exists(osFile))
            {
                meadowPad.control.WriteToConsole("Firmware not found\n");
                return 100;
            }

            await Task.Run(() =>
            {
                try
                {
                    int i;
                    for (i = 0; i < 15; i++)
                    {
                        if (Meadow.CLI.Internals.Udev.Udev.GetSerialNumbers("0483", "df11", "usb").Contains(meadowPad.Target.SerialNumber)) break;

                        if ( i % 10 == 0)  meadowPad.control.WriteToConsole("Waiting for BOOT mode.\n");
                        Thread.Sleep(2000);
                    }
                    if (i == 15)
                    {
                        meadowPad.control.WriteToConsole("No devices found.\n");
                        return 100;
                    }
                
                
                    Process p = new Process();
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.FileName = "dfu-util";
                    p.StartInfo.Arguments = $"-a 0 -S {meadowPad.Target.SerialNumber} -D {osFile} -s 0x08000000";
                    p.Start();
                    meadowPad.control.WriteToConsole($">dfu-util {p.StartInfo.Arguments}\n");
                    p.OutputDataReceived += (sender, e) =>
                    {
                        meadowPad.control.WriteToConsole($"{e.Data}\n");
                    };

                    p.BeginOutputReadLine();

                    if (!p.WaitForExit(timeout))
                    {
                        meadowPad.control.WriteToConsole("Sending Ctrl-C\n");
                        Syscall.kill(p.Id, Signum.SIGABRT);
                        if (!p.WaitForExit(1000))
                        {
                            meadowPad.control.WriteToConsole("Sending SIGKILL\n");
                            Syscall.kill(p.Id, Signum.SIGKILL);
                            if (!p.WaitForExit(1000))
                            {
                                meadowPad.control.WriteToConsole("Failed to KILL\n");
                            }
                        }
                    }
                    meadowPad.control.WriteToConsole(">\n");
                    return p.ExitCode;
                }
                catch (Exception ex)
                {

                    if (ex.Message.Contains("Cannot find"))
                    {
                        meadowPad.control.WriteToConsole("dfu-util not found.  Please install it.");
                    }
                    else
                    {
                        meadowPad.control.WriteToConsole(ex.Message);
                    }
                }
                return 100;
            });
            return 100;            
        }

        public async Task Download_Firmware()
        {
            try
            {                
                await GetFirmwareVersion(1);
                var filenameZip = System.IO.Path.GetFileName(FirmareVersionUrl.LocalPath);
                var pathZip = Path.Combine(Path.GetTempPath(),filenameZip);
                
                var dirName = filenameZip.Replace(".zip", "");
                FirmwarePath = Path.Combine(Path.GetTempPath(),dirName);
                
                if (Directory.Exists(FirmwarePath))
                {
                    meadowPad.control.WriteToConsole($"Latest version already downloaded.\n");
                }
                else
                {
                    meadowPad.control.WriteToConsole($"Downloading {dirName}...");
                    HttpClient http = new HttpClient();
                    var response = await http.GetAsync(FirmareVersionUrl);
                    response.EnsureSuccessStatusCode();

                    using (var httpClient = new HttpClient())
                    {
                        using (var request = new HttpRequestMessage(HttpMethod.Get, FirmareVersionUrl))
                        {
                            using (Stream contentStream = await (await httpClient.SendAsync(request)).Content.ReadAsStreamAsync(),
                                   stream = new FileStream(pathZip, FileMode.Create, FileAccess.Write, FileShare.None, 10000, true))
                            {
                                await contentStream.CopyToAsync(stream);
                                
                                meadowPad.control.WriteToConsole($"...complete {contentStream.Length.BytesToString()}.\n");
                            }                            
                        }
                    }
                    
                    meadowPad.control.WriteToConsole($"Extracting files...");
                    ZipFile.ExtractToDirectory(pathZip, FirmwarePath);
                    meadowPad.control.WriteToConsole($"Download complete.\n");
                }
            }
            catch(Exception ex)
            {
                meadowPad.control.WriteToConsole($"\nError occurred while downloading latest OS. Please try again later.");
            }
            
        }

        private static readonly Helper.AsyncLock _versionLock = new Helper.AsyncLock();
        public static async Task<System.Version> GetFirmwareVersion(uint cacheMinutes = FirmwareCheckEveryMinutes )
        {
            using(await _versionLock.LockAsync())
            {
                if (!FirmareVersionLastChecked.HasValue ||
                     (DateTime.UtcNow - FirmareVersionLastChecked.Value).TotalMinutes >= cacheMinutes)
                {
                    using (HttpClient httpClient = new HttpClient())
                    {
                        httpClient.Timeout = TimeSpan.FromMilliseconds(5000);

                        var payload = await httpClient.GetStringAsync(versionCheckUrl);
                        if (!String.IsNullOrEmpty(payload))
                        {
                            FirmareVersion = GetVersionFromPayload(payload);
                            FirmareVersionUrl = new Uri(GetDownloadUrlFromPayload(payload));
                            FirmareVersionLastChecked = DateTime.UtcNow;
                        }
                    }
                }
            }
            
            return new System.Version(FirmareVersion);
        }
        
        static private string GetVersionFromPayload(string payload)
        {
            var json = JObject.Parse(payload);
            return json["version"].Value<string>();
        }

        static private string GetDownloadUrlFromPayload(string payload)
        {
            var json = JObject.Parse(payload);
            return json["downloadUrl"].Value<string>();
        }
       
    }
}
