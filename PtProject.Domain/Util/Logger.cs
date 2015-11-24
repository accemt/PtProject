﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PtProject.Domain.Util
{
    public class Logger
    {
        private static readonly string RootHosting;
        private static readonly object SyncObj = new object();
        public static bool IsConsole = true;

        static Logger()
        {
            RootHosting = Environment.CurrentDirectory;
            WriteMessage("Program started.");
        }

        public static void Log(string message)
        {
            if (RootHosting == null) return;

            string methodname = GetMethodName();
            var sb = new StringBuilder();
            sb.AppendLine(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss: ") + methodname + ": " + message);

            lock (SyncObj)
            {
                WriteMessage(sb.ToString());
            }
        }

        public static void Log(Exception e)
        {
            if (RootHosting == null) return;

            string methodname = GetMethodName();
            var sb = new StringBuilder();
            sb.AppendLine(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss: ") + methodname + ": EXCEPTION: "  + e.Message);
            if (e.InnerException != null)
            {
                sb.AppendLine(DateTime.Now.ToString("\t\t\t: ") + methodname + ": " + e.InnerException.Message);
            }

            lock (SyncObj)
            {
                WriteMessage(sb.ToString());
            }
        }

        private static void WriteMessage(string message)
        {
            var dinfo = new DirectoryInfo(RootHosting);
            var sw = new StreamWriter(
                new FileStream(dinfo.FullName + "\\log.txt", FileMode.Append, FileAccess.Write),
                Encoding.GetEncoding(1251));

            if (message != null) message = message.Trim();

            sw.WriteLine(message);
            sw.Close();

            if (IsConsole)
            {
                Console.WriteLine(message);
            }
        }

        private static string GetMethodName()
        {
            var stackTrace = new StackTrace();
            var frame = stackTrace.GetFrame(2);
            var method = frame.GetMethod();
            string methodname = (method.ReflectedType == null ? "" : method.ReflectedType.Name) + "." + method.Name;
            return methodname;
        }
    }
}