using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using A3Tools.Models;
using A3Tools.Services;
using A3Tools.Common.DataAccess;

namespace A3Tools.Services.AiActions;

/// <summary>
/// 列出所有账套（不含密码字段）
/// </summary>
public class ListAccountsAction : IAiAction
{
    public string Name => "list_accounts";
    public string Description => "列出 A3Tools 中所有已配置账套（只返回名称、编码、服务器、备注，不含密码）";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["keyword"] = new { type = "string", description = "可选，模糊匹配账套名 / 编码 / 服务器" }
        },
        required = new string[] { }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "读取账套列表（不包含密码）";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var ds = new DataService();
            var accounts = ds.LoadAccounts();
            string? keyword = arguments.TryGetValue("keyword", out var kw) ? kw?.ToString() : null;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string k = keyword.ToLower();
                accounts = accounts.Where(a =>
                    a.Name.ToLower().Contains(k) ||
                    a.Code.ToLower().Contains(k) ||
                    a.Server.ToLower().Contains(k)
                ).ToList();
            }

            var data = accounts.Select(a => new
            {
                a.Code,
                a.Name,
                a.Server,
                Database = "***",
                a.Remark
            }).ToList();

            return Task.FromResult(AiActionResult.Ok($"找到 {data.Count} 个账套", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 获取 A3Tools 应用信息
/// </summary>
public class GetAppInfoAction : IAiAction
{
    public string Name => "get_app_info";
    public string Description => "获取 A3Tools launcher 的版本、A3 程序目录、当前设置摘要";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>(),
        required = new string[] { }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "读取应用信息（版本 / 目录）";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var data = new
            {
                LauncherVersion = UpdateService.CurrentVersion,
                AppDirectory = AppContext.BaseDirectory,
                DataDirectory = Path.Combine(AppContext.BaseDirectory, "DATA"),
                WorkingDirectory = Directory.GetCurrentDirectory(),
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                OsVersion = Environment.OSVersion.VersionString,
                DotNetVersion = Environment.Version.ToString()
            };
            return Task.FromResult(AiActionResult.Ok("应用信息已获取", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 检查 launcher 是否有新版本
/// </summary>
public class CheckUpdateAction : IAiAction
{
    public string Name => "check_update";
    public string Description => "联网检查 A3Tools launcher 是否有新版本（不弹窗、不下载，仅返回信息）";
    public AiActionPermission Permission => AiActionPermission.Safe;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>(),
        required = new string[] { }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "联网查询 launcher 更新（只读）";

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var info = await UpdateService.CheckForUpdateAsync();
            if (info == null)
            {
                return AiActionResult.Ok("检查完成：已是最新版本", new
                {
                    CurrentVersion = UpdateService.CurrentVersion,
                    HasUpdate = false,
                    LatestVersion = UpdateService.CurrentVersion
                });
            }
            return AiActionResult.Ok("发现新版本", new
            {
                CurrentVersion = UpdateService.CurrentVersion,
                LatestVersion = info.Version,
                HasUpdate = true,
                ReleaseNotes = info.Body,
                AssetUrl = info.DownloadUrl
            });
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"检查更新失败：{ex.Message}");
        }
    }
}

/// <summary>
/// 读取日志
/// </summary>
public class ReadLogAction : IAiAction
{
    public string Name => "read_log";
    public string Description => "读取 A3Tools 最近 N 行日志（用于排查启动 / 运行故障），默认 50 行";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["lines"] = new { type = "integer", description = "读取行数，默认 50，最大 500" },
            ["date"] = new { type = "string", description = "可选，指定日期 YYYY-MM-DD，不传则读最新的" }
        },
        required = new string[] { }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "读取日志文件（只读）";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            int lines = 50;
            if (arguments.TryGetValue("lines", out var l) && int.TryParse(l?.ToString(), out var n))
                lines = Math.Clamp(n, 1, 500);

            string logDir = Path.Combine(AppContext.BaseDirectory, "DATA", "logs");
            if (!Directory.Exists(logDir))
                return Task.FromResult(AiActionResult.Ok("日志目录不存在", new { logDir, files = Array.Empty<string>() }));

            string? targetFile = null;
            if (arguments.TryGetValue("date", out var d) && d != null)
            {
                string dateStr = d.ToString() ?? string.Empty;
                string candidate = Path.Combine(logDir, $"{dateStr}.log");
                if (File.Exists(candidate)) targetFile = candidate;
            }
            if (targetFile == null)
            {
                var files = Directory.GetFiles(logDir, "*.log").OrderByDescending(f => f).ToList();
                if (files.Count > 0) targetFile = files[0];
            }
            if (targetFile == null)
                return Task.FromResult(AiActionResult.Ok("没有日志文件", new { logDir }));

            var allLines = File.ReadAllLines(targetFile);
            var tailLines = allLines.TakeLast(lines).ToList();

            return Task.FromResult(AiActionResult.Ok($"读取 {tailLines.Count} 行（来自 {Path.GetFileName(targetFile)}）", new
            {
                File = Path.GetFileName(targetFile),
                TotalLines = allLines.Length,
                ReturnedLines = tailLines.Count,
                Content = string.Join("\n", tailLines)
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 获取系统状态
/// </summary>
public class GetSystemStatusAction : IAiAction
{
    public string Name => "get_system_status";
    public string Description => "获取当前进程和系统状态（CPU 占用、内存占用、A3Tools 目录磁盘空间等）";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>(),
        required = new string[] { }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "读取系统状态（只读）";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var current = System.Diagnostics.Process.GetCurrentProcess();
            var data = new
            {
                LauncherMemoryMB = current.WorkingSet64 / 1024 / 1024,
                LauncherThreads = current.Threads.Count,
                LauncherUptimeSeconds = (DateTime.Now - current.StartTime).TotalSeconds,
                LauncherStartTime = current.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                SystemDrive = new
                {
                    Drive = Path.GetPathRoot(AppContext.BaseDirectory),
                    TotalGB = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\").TotalSize / 1024d / 1024d / 1024d,
                    FreeGB = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\").AvailableFreeSpace / 1024d / 1024d / 1024d
                },
                AppDirectory = AppContext.BaseDirectory,
                DataDirectoryExists = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "DATA"))
            };

            return Task.FromResult(AiActionResult.Ok("系统状态已获取", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 获取账套详细信息
/// </summary>
public class GetAccountDetailAction : IAiAction
{
    public string Name => "get_account_detail";
    public string Description => "根据账套编码获取详细信息（含服务器、数据库名等，不含密码字段）";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码（如 001）" }
        },
        required = new string[] { "code" }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        return $"读取账套 [{code}] 详细信息（不含密码）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(code)) return Task.FromResult(AiActionResult.Fail("请提供账套编码"));
            var ds = new DataService();
            var account = ds.FindAccount(code);
            if (account == null) return Task.FromResult(AiActionResult.Fail($"账套 [{code}] 不存在"));

            var data = new
            {
                account.Code,
                account.Name,
                account.Server,
                Database = "***",
                account.Remark,
                ConnectionMode = account.ConnectionMode.ToString()
            };
            return Task.FromResult(AiActionResult.Ok($"账套 [{code}] 信息", data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 列出正在运行的账套
/// </summary>
public class ListRunningAccountsAction : IAiAction
{
    public string Name => "list_running_accounts";
    public string Description => "列出当前正在运行的账套";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>(),
        required = new string[] { }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "读取正在运行的账套列表";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            if (A3Tools.Forms.MainForm.Instance == null)
                return Task.FromResult(AiActionResult.Ok("MainForm 未初始化", new { running = Array.Empty<object>() }));

            var running = new List<object>();
            var field = typeof(A3Tools.Forms.MainForm).GetField("_accountStatuses",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return Task.FromResult(AiActionResult.Fail("无法读取账套状态"));
            var dict = field.GetValue(A3Tools.Forms.MainForm.Instance) as System.Collections.IDictionary;
            if (dict == null) return Task.FromResult(AiActionResult.Ok("当前无运行中的账套", new { running }));

            foreach (var key in dict.Keys)
            {
                string code = key?.ToString() ?? "";
                if (!string.IsNullOrEmpty(code)) running.Add(new { code });
            }
            return Task.FromResult(AiActionResult.Ok($"当前 {running.Count} 个账套在跑", new { running }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 启动账套（普通确认）
/// </summary>
public class StartAccountAction : IAiAction
{
    public string Name => "start_account";
    public string Description => "按账套编码启动 A3 程序（客户端 + 开发工具 + 网页）";
    public AiActionPermission Permission => AiActionPermission.Process;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码（如 001）" }
        },
        required = new string[] { "code" }
    };

    public bool RequiresConfirmation => false;  // ★ 2026-08-27 陛下要求：AI 启动账套不再弹确认框（启动选项对话框本身就有保护作用）
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        return $"启动账套 [{code}] 的 A3 客户端 / 开发工具 / 网页";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            if (A3Tools.Forms.MainForm.Instance == null)
                return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(code)) return Task.FromResult(AiActionResult.Fail("请提供账套编码"));

            var tcs = new TaskCompletionSource<AiActionResult>();
            A3Tools.Forms.MainForm.Instance.Invoke(() =>
            {
                try
                {
                    bool ok = A3Tools.Forms.MainForm.Instance.LaunchAccountByCode(code);
                    tcs.SetResult(ok
                        ? AiActionResult.Ok($"账套 [{code}] 启动指令已发出")
                        : AiActionResult.Fail($"账套 [{code}] 启动失败（检查 A3 程序目录设置）"));
                }
                catch (Exception ex)
                {
                    tcs.SetResult(AiActionResult.Fail(ex.Message));
                }
            });
            return tcs.Task;
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 停止账套（普通确认）
/// </summary>
public class StopAccountAction : IAiAction
{
    public string Name => "stop_account";
    public string Description => "停止指定账套的 A3 进程（客户端 + 开发工具）";
    public AiActionPermission Permission => AiActionPermission.Process;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码（如 001）" }
        },
        required = new string[] { "code" }
    };

    public bool RequiresConfirmation => false;  // ★ 2026-08-27 同步：停止账套也不再弹确认框（陛下明确要求激进模式）
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        return $"停止账套 [{code}] 的 A3 进程（数据未保存会丢失）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            if (A3Tools.Forms.MainForm.Instance == null)
                return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(code)) return Task.FromResult(AiActionResult.Fail("请提供账套编码"));

            var tcs = new TaskCompletionSource<AiActionResult>();
            A3Tools.Forms.MainForm.Instance.Invoke(() =>
            {
                try
                {
                    var ds = new DataService();
                    var account = ds.FindAccount(code);
                    if (account == null) { tcs.SetResult(AiActionResult.Fail($"账套 [{code}] 不存在")); return; }

                    int killed = 0;
                    var field = typeof(A3Tools.Forms.MainForm).GetField("_accountStatuses",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var dict = field?.GetValue(A3Tools.Forms.MainForm.Instance) as System.Collections.IDictionary;
                    var pids = new List<int>();
                    if (dict != null && dict[code] != null)
                    {
                        var statusObj = dict[code];
                        var pidsProp = statusObj.GetType().GetProperty("ProcessIds");
                        if (pidsProp != null)
                        {
                            var plist = pidsProp.GetValue(statusObj) as System.Collections.IEnumerable;
                            if (plist != null)
                                foreach (var p in plist) if (p is int pid) pids.Add(pid);
                        }
                    }

                    foreach (var pid in pids)
                    {
                        try
                        {
                            var proc = System.Diagnostics.Process.GetProcessById(pid);
                            proc.Kill();
                            killed++;
                        }
                        catch { }
                    }
                    tcs.SetResult(AiActionResult.Ok($"账套 [{code}] 已停止（杀 {killed} 个进程）"));
                }
                catch (Exception ex)
                {
                    tcs.SetResult(AiActionResult.Fail(ex.Message));
                }
            });
            return tcs.Task;
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 删除账套（高危：输入账套编码确认）
/// </summary>
public class DeleteAccountAction : IAiAction
{
    public string Name => "delete_account";
    public string Description => "永久删除指定账套（含密码信息）";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码（如 001）" }
        },
        required = new string[] { "code" }
    };

    public bool RequiresConfirmation => true;
    private string? _confirmCode;
    public string? ConfirmationPrompt => _confirmCode;

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        _confirmCode = code;
        return $"永久删除账套 [{code}]（含密码），删除后无法恢复！";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(code)) return Task.FromResult(AiActionResult.Fail("请提供账套编码"));
            var ds = new DataService();
            var account = ds.FindAccount(code);
            if (account == null) return Task.FromResult(AiActionResult.Fail($"账套 [{code}] 不存在"));

            ds.DeleteAccount(code);

            if (A3Tools.Forms.MainForm.Instance != null)
            {
                A3Tools.Forms.MainForm.Instance.Invoke(() =>
                {
                    var m = typeof(A3Tools.Forms.MainForm).GetMethod("LoadAccounts",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    m?.Invoke(A3Tools.Forms.MainForm.Instance, null);
                });
            }

            return Task.FromResult(AiActionResult.Ok($"账套 [{code}] 已永久删除"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 新增账套（普通确认）
/// </summary>
public class AddAccountAction : IAiAction
{
    public string Name => "add_account";
    public string Description => "新增账套（含数据库连接信息）";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码，如 001" },
            ["name"] = new { type = "string", description = "账套名称，如 临沂总账" },
            ["server"] = new { type = "string", description = "数据库服务器地址" },
            ["database"] = new { type = "string", description = "数据库名" },
            ["db_user"] = new { type = "string", description = "数据库用户名" },
            ["db_password"] = new { type = "string", description = "数据库密码（自动加密保存）" },
            ["remark"] = new { type = "string", description = "备注（可选）" }
        },
        required = new string[] { "code", "name", "server", "database", "db_user", "db_password" }
    };

    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        var name = arguments.TryGetValue("name", out var n) ? n?.ToString() ?? "" : "";
        return $"新增账套 [{code}] {name}（密码自动加密保存）";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            var ds = new DataService();
            string code = arguments["code"]?.ToString() ?? "";
            string name = arguments["name"]?.ToString() ?? "";
            string server = arguments["server"]?.ToString() ?? "";
            string database = arguments["database"]?.ToString() ?? "";
            string dbUser = arguments["db_user"]?.ToString() ?? "";
            string dbPwd = arguments["db_password"]?.ToString() ?? "";
            string remark = arguments.TryGetValue("remark", out var r) ? r?.ToString() ?? "" : "";

            if (ds.FindAccount(code) != null)
                return Task.FromResult(AiActionResult.Fail($"账套编码 [{code}] 已存在"));

            var account = new Account
            {
                Code = code,
                Name = name,
                Server = server,
                Database = database,
                DbUser = dbUser,
                DbPassword = dbPwd,
                Remark = remark
            };
            ds.AddAccount(account);

            if (A3Tools.Forms.MainForm.Instance != null)
            {
                A3Tools.Forms.MainForm.Instance.Invoke(() =>
                {
                    var m = typeof(A3Tools.Forms.MainForm).GetMethod("LoadAccounts",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    m?.Invoke(A3Tools.Forms.MainForm.Instance, null);
                });
            }

            return Task.FromResult(AiActionResult.Ok($"账套 [{code}] {name} 已新增"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 启动链接数据库
/// </summary>
public class LaunchLinkDbAction : IAiAction
{
    public string Name => "launch_link_db";
    public string Description => "启动数据库查询工具（SSMS 或内置 SQL），连接到指定账套";
    public AiActionPermission Permission => AiActionPermission.Process;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码" }
        },
        required = new string[] { "code" }
    };

    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        return $"启动数据库工具，连接到账套 [{code}]";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            if (A3Tools.Forms.MainForm.Instance == null)
                return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(code)) return Task.FromResult(AiActionResult.Fail("请提供账套编码"));

            var tcs = new TaskCompletionSource<AiActionResult>();
            A3Tools.Forms.MainForm.Instance.Invoke(() =>
            {
                Button? targetBtn = null;
                foreach (Control c in A3Tools.Forms.MainForm.Instance.Controls)
                {
                    if (c is TabControl tc)
                    {
                        foreach (TabPage page in tc.TabPages)
                        {
                            foreach (Control child in page.Controls)
                            {
                                if (child is Button b && (b.Text.Contains("链接数据库") || b.Name.Contains("LinkDb")))
                                {
                                    targetBtn = b;
                                    break;
                                }
                            }
                            if (targetBtn != null) break;
                        }
                    }
                    if (targetBtn != null) break;
                }

                if (targetBtn != null)
                {
                    targetBtn.PerformClick();
                    tcs.SetResult(AiActionResult.Ok($"已触发链接数据库（账套 [{code}]）"));
                }
                else
                {
                    tcs.SetResult(AiActionResult.Fail("找不到链接数据库按钮"));
                }
            });
            return tcs.Task;
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 打开 DATA 目录
/// </summary>
public class OpenDataFolderAction : IAiAction
{
    public string Name => "open_data_folder";
    public string Description => "用资源管理器打开 A3Tools 的 DATA 目录";
    public AiActionPermission Permission => AiActionPermission.Process;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>(),
        required = new string[] { }
    };

    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments) => "用资源管理器打开 DATA 目录";

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string dataDir = Path.Combine(AppContext.BaseDirectory, "DATA");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            System.Diagnostics.Process.Start("explorer.exe", dataDir);
            return Task.FromResult(AiActionResult.Ok($"已打开 {dataDir}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// 清理日志（普通确认）
/// </summary>
public class CleanLogsAction : IAiAction
{
    public string Name => "clean_logs";
    public string Description => "清理 DATA/logs/ 下的日志文件";
    public AiActionPermission Permission => AiActionPermission.WriteLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["older_than_days"] = new { type = "integer", description = "只清理 N 天前的日志（默认 0 = 清全部）" }
        },
        required = new string[] { }
    };

    public bool RequiresConfirmation => true;
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        int days = arguments.TryGetValue("older_than_days", out var d) && int.TryParse(d?.ToString(), out var n) ? n : 0;
        return days > 0 ? $"清理 {days} 天前的日志" : "清空 DATA/logs/ 全部日志";
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string logDir = Path.Combine(AppContext.BaseDirectory, "DATA", "logs");
            if (!Directory.Exists(logDir)) return Task.FromResult(AiActionResult.Ok("日志目录不存在，无需清理"));

            int days = arguments.TryGetValue("older_than_days", out var d) && int.TryParse(d?.ToString(), out var n) ? n : 0;
            var files = Directory.GetFiles(logDir, "*.log");
            int deleted = 0;
            foreach (var f in files)
            {
                if (days > 0 && File.GetLastWriteTime(f) > DateTime.Now.AddDays(-days)) continue;
                try { File.Delete(f); deleted++; } catch { }
            }
            return Task.FromResult(AiActionResult.Ok($"清理完成：删除 {deleted} 个日志"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AiActionResult.Fail(ex.Message));
        }
    }
}

/// <summary>
/// ★ 2026-08-26 R2-D 业务查询：列出账套下所有表
/// </summary>
public class ListTablesAction : IAiAction
{
    public string Name => "list_tables";
    public string Description => "列出账套数据库下所有用户表（AI 探索库结构用，写 SQL 前建议先查这个）";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码（如 8088）" }
        },
        required = new string[] { "code" }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        return $"读取账套 [{code}] 的所有表名（只读）";
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(code)) return AiActionResult.Fail("请提供账套编码");

            var ds = new DataService();
            var account = ds.FindAccount(code);
            if (account == null) return AiActionResult.Fail($"账套 [{code}] 不存在");

            var dataAccess = DataAccessFactory.Create(account);
            var tables = await dataAccess.GetTablesAsync(null, ct);
            var tableNames = tables.Select(t => t.Name).ToList();

            return AiActionResult.Ok($"账套 [{code}] 共有 {tableNames.Count} 张表", new
            {
                code,
                count = tableNames.Count,
                tables = tableNames
            });
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"列表失败：{ex.Message}");
        }
    }
}

/// <summary>
/// ★ 2026-08-26 R2-D 业务查询：获取表结构
/// </summary>
public class GetTableSchemaAction : IAiAction
{
    public string Name => "get_table_schema";
    public string Description => "获取指定表的列结构（列名 + 数据类型），AI 写 SQL 前用这个确认字段名";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码" },
            ["table_name"] = new { type = "string", description = "表名（如 销售订单）" }
        },
        required = new string[] { "code", "table_name" }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        var table = arguments.TryGetValue("table_name", out var t) ? t?.ToString() ?? "" : "";
        return $"读取表 [{table}] 的列结构（账套 [{code}]，只读）";
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            string tableName = arguments.TryGetValue("table_name", out var t) ? t?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(code)) return AiActionResult.Fail("请提供账套编码");
            if (string.IsNullOrEmpty(tableName)) return AiActionResult.Fail("请提供表名");

            var ds = new DataService();
            var account = ds.FindAccount(code);
            if (account == null) return AiActionResult.Fail($"账套 [{code}] 不存在");

            var dataAccess = DataAccessFactory.Create(account);
            var columns = await dataAccess.GetTableSchemaAsync(tableName, ct);
            var cols = columns.Select(col => new
            {
                col.Name,
                col.TypeName
            }).ToList();

            return AiActionResult.Ok($"表 [{tableName}] 共有 {cols.Count} 列", new
            {
                code,
                table_name = tableName,
                count = cols.Count,
                columns = cols
            });
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"读取表结构失败：{ex.Message}");
        }
    }
}

/// <summary>
/// ★ 2026-08-26 R2-D 业务查询：执行 SQL 查询（高危）
/// </summary>
public class ExecuteSqlAction : IAiAction
{
    public string Name => "execute_sql";
    public string Description => "在指定账套上执行 SELECT 查询（拒绝写操作），返回表格数据";
    public AiActionPermission Permission => AiActionPermission.Network;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码" },
            ["sql"] = new { type = "string", description = "SQL 语句，必须是 SELECT/WITH" },
            ["limit"] = new { type = "integer", description = "最大返回行数，默认 1000，最大 5000" }
        },
        required = new string[] { "code", "sql" }
    };

    public bool RequiresConfirmation => false;  // ★ 2026-08-27 陛下要求：execute_sql 只走只读查询，安全性靠白名单/黑名单保证，不再弹任何确认框
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        var sql = arguments.TryGetValue("sql", out var s) ? s?.ToString() ?? "" : "";
        return $"在账套 [{code}] 上执行 SQL：{sql}";
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            string sql = arguments.TryGetValue("sql", out var s) ? s?.ToString() ?? "" : "";
            int limit = 1000;
            if (arguments.TryGetValue("limit", out var l) && int.TryParse(l?.ToString(), out var n))
                limit = Math.Clamp(n, 1, 5000);

            if (string.IsNullOrEmpty(code)) return AiActionResult.Fail("请提供账套编码");
            if (string.IsNullOrEmpty(sql)) return AiActionResult.Fail("请提供 SQL");

            var safety = CheckSqlSafety(sql);
            if (!safety.Safe) return AiActionResult.Fail($"SQL 安全检查未通过：{safety.Reason}");

            var ds = new DataService();
            var account = ds.FindAccount(code);
            if (account == null) return AiActionResult.Fail($"账套 [{code}] 不存在");

            string finalSql = WrapWithLimit(sql, limit);

            var dataAccess = DataAccessFactory.Create(account);
            var queryResult = await dataAccess.ExecuteQueryAsync(finalSql, ct);

            if (!queryResult.Success)
                return AiActionResult.Fail($"查询失败：{queryResult.Message}");

            if (queryResult.Tables == null || queryResult.Tables.Count == 0)
                return AiActionResult.Ok("查询无返回数据", new { rows = Array.Empty<object>() });

            var table = queryResult.Tables[0];
            var rows = table.Rows.Select(row =>
            {
                var dict = new Dictionary<string, object?>();
                for (int i = 0; i < table.Columns.Count && i < row.Length; i++)
                    dict[table.Columns[i].Name] = row[i];
                return dict;
            }).ToList();

            return AiActionResult.Ok(
                $"查询成功：{rows.Count} 行" + (table.Truncated ? "（已截断）" : ""),
                new
                {
                    code,
                    row_count = rows.Count,
                    truncated = table.Truncated,
                    elapsed_ms = queryResult.ElapsedMs,
                    rows
                });
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"执行 SQL 失败：{ex.Message}");
        }
    }

    /// <summary>
    /// SQL 安全检查：只允许 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN
    /// </summary>
    private static (bool Safe, string Reason) CheckSqlSafety(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return (false, "SQL 为空");

        // 去掉注释
        string cleaned = Regex.Replace(sql, @"--.*?$", "", RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"/\*.*?\*/", "", RegexOptions.Singleline);
        cleaned = cleaned.Trim().TrimEnd(';').Trim();

        string upper = cleaned.ToUpperInvariant();

        // 危险关键字（用词边界匹配，避免误判 UPDATED_AT 等列名）
        string[] dangerous = {
            "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
            "TRUNCATE", "EXEC", "EXECUTE", "MERGE", "GRANT", "REVOKE",
            "BACKUP", "RESTORE", "SHUTDOWN", "KILL"
        };
        foreach (var kw in dangerous)
        {
            if (Regex.IsMatch(upper, $@"\b{kw}\b"))
                return (false, $"包含禁止的关键字 [{kw}]，只允许 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN");
        }

        // 必须以 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN 开头
        string[] allowedStarts = { "SELECT", "WITH", "SHOW", "DESCRIBE", "DESC", "EXPLAIN" };
        bool startsOk = false;
        foreach (var prefix in allowedStarts)
        {
            if (upper.StartsWith(prefix + " ") || upper == prefix)
            {
                startsOk = true;
                break;
            }
        }
        if (!startsOk) return (false, "必须以 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN 开头");

        // 拦截多语句
        if (cleaned.Contains(";"))
        {
            var after = cleaned.Substring(cleaned.IndexOf(';') + 1).Trim();
            if (!string.IsNullOrEmpty(after))
                return (false, "SQL 不允许多语句（分号后还有内容）");
        }

        return (true, "");
    }

    /// <summary>
    /// 自动给 SELECT 加 TOP N（SQL Server 语法）
    /// </summary>
    private static string WrapWithLimit(string sql, int limit)
    {
        string trimmed = sql.Trim().TrimEnd(';').Trim();
        // 已有限制则不加
        if (Regex.IsMatch(trimmed, @"\bTOP\s+\d+", RegexOptions.IgnoreCase)) return trimmed;
        if (Regex.IsMatch(trimmed, @"\bOFFSET\s+\d+", RegexOptions.IgnoreCase)) return trimmed;

        return Regex.Replace(
            trimmed,
            @"^\s*SELECT\s+(DISTINCT\s+)?",
            m => $"{m.Value}TOP {limit} ",
            RegexOptions.IgnoreCase
        );
    }
}
