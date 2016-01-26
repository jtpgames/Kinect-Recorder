using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectRecorder
{
    static class LogConsole
    {
        private static ObservableCollection<string> logs = new ObservableCollection<string>();
        public static ObservableCollection<string> Logs
        {
            get { return logs; }
            private set
            {
                logs = value;
            }
        }

        public static void WriteLine(string format, params object[] arg)
        {
            if (arg != null)
            {
                var sb = new StringBuilder();
                sb.AppendFormat(format, arg);
                logs.Add(sb.ToString());
            }
            else
            {
                logs.Add(format);
            }

            Console.WriteLine(format, arg);
        }
    }
}
