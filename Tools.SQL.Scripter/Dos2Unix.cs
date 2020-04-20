using System.Diagnostics;

namespace Tools.SQL.Scripter {
    public static class Dos2Unix {
        public static void Convert(string path) {
            // fix encoding 
            try {
                var process = new Process ()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dos2unix",    // must ber installed on your system path
                        Arguments = $"{path}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = false,
                        RedirectStandardInput = false,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                process.Start ();
                process.WaitForExit ();
            }
            catch {
                // ignore error
            }
            
        }
    }
}
