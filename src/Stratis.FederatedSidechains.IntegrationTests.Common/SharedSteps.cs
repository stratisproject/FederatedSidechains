using System;
using System.IO;
using System.Threading;
using Stratis.Bitcoin.IntegrationTests.Common;

namespace Stratis.FederatedSidechains.IntegrationTests.Common
{
    public static class SharedStepsExtensions
    {
        public static void ShellCleanupFolder(this SharedSteps sharedSteps, string testFolderPath, int timeout = 30000)
        {
            if (!Directory.Exists(testFolderPath)) return;

            using (var fw = new FileSystemWatcher(Path.GetDirectoryName(testFolderPath)))
            using (var mre = new ManualResetEventSlim())
            {
                fw.EnableRaisingEvents = true;
                fw.Deleted += (s, e) =>
                {
                    mre.Set();
                };
                Directory.Delete(testFolderPath, true);
                mre.Wait(timeout);
            }
        }
    }
}
