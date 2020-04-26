using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Sdks.IdeExtensions.Vs4Mac
{
    static public class Helper
    {
        
        public static String BytesToString(this long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }
        
               
        
        
        public class AsyncLock : IDisposable
        {
            private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
     
            public async Task<AsyncLock> LockAsync()
            {
                await _semaphoreSlim.WaitAsync();
                return this;
            }
     
            public void Dispose()
            {
                _semaphoreSlim.Release();
            }
        }
        
        
    }
    
    
}
