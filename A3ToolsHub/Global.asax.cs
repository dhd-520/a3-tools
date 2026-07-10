using System;
using System.Diagnostics;
using System.IO;
using System.Web;
using System.Web.Http;

namespace A3ToolsHub
{
    /// <summary>
    /// ASP.NET Web API 启动入口。
    /// IIS 加载 Global.asax 后会创建此类型，并在应用启动时注册 Web API 路由。
    /// </summary>
    public class WebApiApplication : HttpApplication
    {
        private const string LogSubDir = "logs";
        private const string LogFileName = "startup.log";
        private static readonly object _logLock = new object();
        private static string _logFilePath;

        /// <summary>
        /// 日志文件路径：部署目录/logs/startup.log
        /// （采用 AppDomain.BaseDirectory，避免在 C 盘固定位置）
        /// 部署目录无写权限时退回到 %TEMP%
        /// </summary>
        private static string LogFilePath
        {
            get
            {
                if (_logFilePath != null) return _logFilePath;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string primary = Path.Combine(baseDir, LogSubDir);
                try
                {
                    if (!Directory.Exists(primary)) Directory.CreateDirectory(primary);
                    string probe = Path.Combine(primary, ".__write_probe");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    _logFilePath = Path.Combine(primary, LogFileName);
                    return _logFilePath;
                }
                catch
                {
                    string fallback = Path.Combine(Path.GetTempPath(), "A3ToolsHub-logs");
                    try { if (!Directory.Exists(fallback)) Directory.CreateDirectory(fallback); } catch { }
                    _logFilePath = Path.Combine(fallback, LogFileName);
                    return _logFilePath;
                }
            }
        }

        protected void Application_Start()
        {
            try
            {
                Log("Application_Start begin (log=" + LogFilePath + ")");

                GlobalConfiguration.Configure(WebApiConfig.Register);

                Log("Application_Start OK (routes registered)");
            }
            catch (Exception ex)
            {
                Log("Application_Start FAILED: " + ex);
                ThrowWithDiagnosableMessage(ex);
            }
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            try
            {
                Exception ex = Server.GetLastError();
                if (ex == null) return;
                Log("Application_Error: " + ex);
            }
            catch { }
        }

        private static void Log(string msg)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}{Environment.NewLine}";
            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(LogFilePath, line);
                }
            }
            catch { }
            try { EventLog.WriteEntry("Application", "[A3ToolsHub] " + msg, EventLogEntryType.Information); } catch { }
        }

        private static void ThrowWithDiagnosableMessage(Exception ex)
        {
            try { HttpContext.Current.Response.TrySkipIisCustomErrors = true; } catch { }
            throw ex;
        }
    }
}
