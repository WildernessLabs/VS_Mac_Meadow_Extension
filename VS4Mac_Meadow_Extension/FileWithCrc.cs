using System;
namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    public class FileWithCrc
    {
        public string FileName { get; set; }
        public UInt32 CRC { get; set; }

        public FileWithCrc(string fileName, uint crc)
        {
            CRC = crc;
            FileName = fileName;
        }
    }
}