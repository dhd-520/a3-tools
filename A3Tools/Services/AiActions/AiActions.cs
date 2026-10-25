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
    public string Description => "按账套编码启动 A3 程序。可指定启动哪些组件的任意组合：客户端(client) / 开发工具(devtools) / ERP网页(erp) / 企业微信网页(wechat)";
    public AiActionPermission Permission => AiActionPermission.Process;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码（如 001）" },
            ["target"] = new
            {
                type = "string",
                description = "★ 可选：要启动哪些组件，多个用 + 分隔（如 \"client+devtools\"）。" +
                              "client=客户端(君则A3.exe)，devtools=集成开发工具，erp=ERP网页版(/h5comerp)，wechat=企业微信网页版(/h5apperp)，all=全部启动。" +
                              "不传=按上次设置(client+erp)。",
                @enum = new[] { "client", "devtools", "erp", "wechat", "all", "client+devtools", "client+erp", "client+devtools+erp", "client+devtools+erp+wechat" }
            }
        },
        required = new string[] { "code" }
    };

    public bool RequiresConfirmation => false;  // ★ 2026-08-27 陛下要求：AI 启动账套不再弹确认框（启动选项对话框本身就有保护作用）
    public string? ConfirmationPrompt => null;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        var target = ReadTargetArgument(arguments);
        string what = target == null
            ? "A3 客户端 / 开发工具 / 网页"
            : (LaunchTargetSpec.TryParse(target, out var spec, out _) ? spec.Describe() : target);
        return $"启动账套 [{code}] 的 {what}";
    }

    /// <summary>
    /// 读取 target 参数。AI 可能传字符串（"client+devtools"）或数组（["client","devtools"]），两种都支持。
    /// </summary>
    private static string? ReadTargetArgument(Dictionary<string, object?> arguments)
    {
        if (!arguments.TryGetValue("target", out var raw) || raw == null) return null;

        if (raw is string s)
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // JsonElement 字符串
        if (raw is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                var v = je.GetString();
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
            // JsonElement 数组
            if (je.ValueKind == JsonValueKind.Array)
            {
                var items = je.EnumerateArray()
                    .Select(x => x.ToString().Trim())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList();
                return items.Count > 0 ? string.Join("+", items) : null;
            }
        }

        // 已经是字符串集合
        if (raw is System.Collections.IEnumerable seq && raw is not string)
        {
            var items = seq.Cast<object?>()
                .Select(x => x?.ToString()?.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToList();
            return items.Count > 0 ? string.Join("+", items) : null;
        }

        var t = raw.ToString();
        return string.IsNullOrWhiteSpace(t) ? null : t.Trim();
    }

    public Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            if (A3Tools.Forms.MainForm.Instance == null)
                return Task.FromResult(AiActionResult.Fail("MainForm 未初始化"));

            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(code)) return Task.FromResult(AiActionResult.Fail("请提供账套编码"));

            // ★ 2026-10-24 陛下需求：AI 可按参数任意组合启动
            //   target 缺失 → 走默认组合（客户端 + ERP网页，与旧行为一致）
            //   target 非法 → 把错误回传给 AI，让模型自己纠正后重试，而不是静默启动错的东西
            string? target = ReadTargetArgument(arguments);
            string describe = "按上次设置";
            if (target != null)
            {
                if (!LaunchTargetSpec.TryParse(target, out var spec, out var parseError))
                    return Task.FromResult(AiActionResult.Fail($"target 参数无效：{parseError}"));
                describe = spec.Describe();
            }

            var tcs = new TaskCompletionSource<AiActionResult>();
            var inst = A3Tools.Forms.MainForm.Instance;
            Action launchAction = () =>
            {
                try
                {
                    bool ok = A3Tools.Forms.MainForm.Instance!.LaunchAccountByCode(code, target);
                    tcs.SetResult(ok
                        ? AiActionResult.Ok($"账套 [{code}] 启动指令已发出（{describe}）")
                        : AiActionResult.Fail($"账套 [{code}] 启动失败（检查 A3 程序目录设置）"));
                }
                catch (Exception ex)
                {
                    tcs.SetResult(AiActionResult.Fail(ex.Message));
                }
            };
            // ★ 2026-10-12 修复：IsHandleCreated 检查，避免「在创建窗口句柄之前调用 Invoke」
            if (inst.IsHandleCreated && !inst.IsDisposed)
                inst.Invoke(launchAction);
            else
                launchAction();
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
            var inst = A3Tools.Forms.MainForm.Instance;
            Action stopAction = () =>
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
            };
            // ★ 2026-10-12 修复：IsHandleCreated 检查，避免「在创建窗口句柄之前调用 Invoke」
            if (inst.IsHandleCreated && !inst.IsDisposed)
                inst.Invoke(stopAction);
            else
                stopAction();
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
    public string? ConfirmationPrompt => null;  // ★ 2026-10-06 陛下要求：高危操作改成只点确认，不要输入

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
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

            // ★ 2026-10-12 修复：IsHandleCreated 检查，避免「在创建窗口句柄之前调用 Invoke」
            var instDel = A3Tools.Forms.MainForm.Instance;
            if (instDel != null)
            {
                Action reloadAction = () =>
                {
                    var m = typeof(A3Tools.Forms.MainForm).GetMethod("LoadAccounts",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    m?.Invoke(A3Tools.Forms.MainForm.Instance, null);
                };
                if (instDel.IsHandleCreated && !instDel.IsDisposed)
                    instDel.Invoke(reloadAction);
                else
                    reloadAction();
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

            // ★ 2026-10-12 修复：IsHandleCreated 检查，避免「在创建窗口句柄之前调用 Invoke」
            var instAdd = A3Tools.Forms.MainForm.Instance;
            if (instAdd != null)
            {
                Action reloadAction = () =>
                {
                    var m = typeof(A3Tools.Forms.MainForm).GetMethod("LoadAccounts",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    m?.Invoke(A3Tools.Forms.MainForm.Instance, null);
                };
                if (instAdd.IsHandleCreated && !instAdd.IsDisposed)
                    instAdd.Invoke(reloadAction);
                else
                    reloadAction();
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
            // ★ 2026-10-12 修复：IsHandleCreated 检查，避免「在创建窗口句柄之前调用 Invoke」
            var instLink = A3Tools.Forms.MainForm.Instance;
            Action linkAction = () =>
            {
                Button? targetBtn = null;
                foreach (Control c in A3Tools.Forms.MainForm.Instance!.Controls)
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
            };
            if (instLink != null && instLink.IsHandleCreated && !instLink.IsDisposed)
                instLink.Invoke(linkAction);
            else
                linkAction();
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
/// ★ 2026-09-20 陛下需求：对比两个表的结构（找差异列 / 类型不一致 / 同名字段）
///   用途：排查表结构漂移、找 JOIN 字段、确认两表字段映射关系
/// </summary>
public class CompareTableSchemasAction : IAiAction
{
    public string Name => "compare_table_schemas";
    public string Description => "对比两个表的列结构差异：列出 A 独有 / B 独有 / 类型不一致的列，并返回同名列清单（用于提示 JOIN 候选），常用于排查表结构漂移或找 JOIN 字段";
    public AiActionPermission Permission => AiActionPermission.ReadLocal;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码" },
            ["table_a"] = new { type = "string", description = "第一个表名（如 S_SCM_CUSTOMER）" },
            ["table_b"] = new { type = "string", description = "第二个表名（如 S_SCM_MEMBER）" }
        },
        required = new string[] { "code", "table_a", "table_b" }
    };

    public bool RequiresConfirmation => false;
    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        var ta = arguments.TryGetValue("table_a", out var tao) ? tao?.ToString() ?? "" : "";
        var tb = arguments.TryGetValue("table_b", out var tbo) ? tbo?.ToString() ?? "" : "";
        return $"对比账套 [{code}] 中表 [{ta}] 和 [{tb}] 的列结构（只读）";
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            string tableA = arguments.TryGetValue("table_a", out var tao) ? tao?.ToString() ?? "" : "";
            string tableB = arguments.TryGetValue("table_b", out var tbo) ? tbo?.ToString() ?? "" : "";
            if (string.IsNullOrEmpty(code)) return AiActionResult.Fail("请提供账套编码");
            if (string.IsNullOrEmpty(tableA)) return AiActionResult.Fail("请提供第一个表名");
            if (string.IsNullOrEmpty(tableB)) return AiActionResult.Fail("请提供第二个表名");

            var ds = new DataService();
            var account = ds.FindAccount(code);
            if (account == null) return AiActionResult.Fail($"账套 [{code}] 不存在");

            var dataAccess = DataAccessFactory.Create(account);
            // 并行获取两个表的 schema（避免串行等待）
            var taskA = dataAccess.GetTableSchemaAsync(tableA, ct);
            var taskB = dataAccess.GetTableSchemaAsync(tableB, ct);
            await Task.WhenAll(taskA, taskB);

            var colsA = taskA.Result;
            var colsB = taskB.Result;

            // 列名大小写不敏感比较
            var mapA = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in colsA) mapA[col.Name] = col.TypeName;
            var mapB = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in colsB) mapB[col.Name] = col.TypeName;

            // A 独有
            var onlyInA = colsA.Where(c => !mapB.ContainsKey(c.Name))
                .Select(c => new { c.Name, type = c.TypeName }).ToList();
            // B 独有
            var onlyInB = colsB.Where(c => !mapA.ContainsKey(c.Name))
                .Select(c => new { c.Name, type = c.TypeName }).ToList();
            // 类型不一致 + 同名列（同时收集）
            var typeMismatch = new List<object>();
            var commonColumns = new List<object>();
            foreach (var kvA in mapA)
            {
                if (mapB.TryGetValue(kvA.Key, out var typeB))
                {
                    if (string.Equals(kvA.Value, typeB, StringComparison.OrdinalIgnoreCase))
                    {
                        commonColumns.Add(new { name = kvA.Key, type = kvA.Value });
                    }
                    else
                    {
                        typeMismatch.Add(new { name = kvA.Key, type_a = kvA.Value, type_b = typeB });
                    }
                }
            }

            return AiActionResult.Ok(
                $"对比完成：A 独有 {onlyInA.Count} 列 / B 独有 {onlyInB.Count} 列 / 类型不一致 {typeMismatch.Count} 列 / 同名列 {commonColumns.Count} 列",
                new
                {
                    code,
                    table_a = new { name = tableA, count = colsA.Count },
                    table_b = new { name = tableB, count = colsB.Count },
                    only_in_a = onlyInA,
                    only_in_b = onlyInB,
                    type_mismatch = typeMismatch,
                    common_columns = commonColumns
                });
        }
        catch (Exception ex)
        {
            return AiActionResult.Fail($"对比表结构失败：{ex.Message}");
        }
    }
}

/// <summary>
/// ★ 2026-08-26 R2-D 业务查询：执行 SQL 查询（高危）
/// </summary>
public class ExecuteSqlAction : IAiAction
{
    public string Name => "execute_sql";
    public string Description => "在指定账套上执行 SQL（★ 只读查询 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN 直接执行；★ INSERT 允许写入数据（会弹确认框）；★ UPDATE/DELETE 不自动执行，只把语句返回给陛下手动执行；★ DDL 请用 execute_ddl）";
    public AiActionPermission Permission => AiActionPermission.Network;
    public object ParametersSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["code"] = new { type = "string", description = "账套编码" },
            ["sql"] = new { type = "string", description = "SQL 语句：只读查询用 SELECT/WITH/SHOW/DESCRIBE/EXPLAIN；新增数据用 INSERT INTO 表(列) VALUES(...)，单次最多 5000 行；UPDATE/DELETE 不会执行，只会回传语句给陛下手动跑" },
            ["limit"] = new { type = "integer", description = "最大行数，默认 1000，最大 5000（查询=最大返回行数，INSERT=单次最大写入行数）" }
        },
        required = new string[] { "code", "sql" }
    };

    // ★ 2026-08-27 陛下要求：execute_sql 只读查询不弹确认框（安全性靠白名单/黑名单保证）
    // ★ 2026-10-25 陛下开放 INSERT：写入属于写操作 → 走 RequiresConfirmationFor 按 SQL 内容判定要不要弹框
    public bool RequiresConfirmation => false;
    public string? ConfirmationPrompt => null;

    /// <summary>
    /// ★ 2026-10-25：按 SQL 内容决定是否弹二次确认
    ///   - INSERT（写数据）→ 需要陛下确认（确认框会显示完整 SQL）
    ///   - SELECT/WITH（只读）→ 不弹框（保住 2026-08-27 的决定）
    /// </summary>
    public bool RequiresConfirmationFor(Dictionary<string, object?> arguments)
    {
        string sql = arguments.TryGetValue("sql", out var s) ? s?.ToString() ?? "" : "";
        var analysis = SqlStatementAnalyzer.Analyze(sql, ReadLimit(arguments));
        return analysis.Kind == SqlStatementKind.Insert;
    }

    public string GetImpactDescription(Dictionary<string, object?> arguments)
    {
        var code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
        var sql = arguments.TryGetValue("sql", out var s) ? s?.ToString() ?? "" : "";
        var analysis = SqlStatementAnalyzer.Analyze(sql, ReadLimit(arguments));
        if (analysis.Kind == SqlStatementKind.Insert)
        {
            string snippet = sql.Length > 120 ? sql.Substring(0, 120) + "..." : sql;
            return $"在账套 [{code}] 上【写入数据】约 {analysis.InsertRowEstimate} 行（INSERT）：{snippet}";
        }
        return $"在账套 [{code}] 上执行 SQL：{sql}";
    }

    /// <summary>读取 limit 参数（默认 1000，上限 5000）</summary>
    private static int ReadLimit(Dictionary<string, object?> arguments)
    {
        int limit = 1000;
        if (arguments.TryGetValue("limit", out var l) && int.TryParse(l?.ToString(), out var n))
            limit = Math.Clamp(n, 1, 5000);
        return limit;
    }

    public async Task<AiActionResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken ct = default)
    {
        try
        {
            string code = arguments.TryGetValue("code", out var c) ? c?.ToString() ?? "" : "";
            string sql = arguments.TryGetValue("sql", out var s) ? s?.ToString() ?? "" : "";
            int limit = ReadLimit(arguments);

            if (string.IsNullOrEmpty(code)) return AiActionResult.Fail("请提供账套编码");
            if (string.IsNullOrEmpty(sql)) return AiActionResult.Fail("请提供 SQL");

            // ★ 2026-10-25 陛下要求：开放 INSERT；UPDATE/DELETE 不执行只回传语句
            var analysis = SqlStatementAnalyzer.Analyze(sql, limit);
            if (!analysis.Allowed)
                return AiActionResult.Fail($"SQL 安全检查未通过：{analysis.Reason}");

            var ds = new DataService();
            var account = ds.FindAccount(code);
            if (account == null) return AiActionResult.Fail($"账套 [{code}] 不存在");

            var dataAccess = DataAccessFactory.Create(account);

            // ── UPDATE / DELETE：陛下要求「不自动执行，但把语句给出来」──
            if (analysis.Kind == SqlStatementKind.Update || analysis.Kind == SqlStatementKind.Delete)
            {
                string verbCn = analysis.Kind == SqlStatementKind.Update ? "更新" : "删除";
                return AiActionResult.Ok(
                    $"已生成{verbCn}语句（未执行）",
                    new
                    {
                        code,
                        executed = false,
                        kind = analysis.Kind.ToString(),
                        sql = analysis.Statement,
                        message = $"execute_sql 不自动执行{verbCn}语句。请将下面这条语句交给陛下，由陛下在数据库客户端手工执行：\n\n{analysis.Statement}"
                    });
            }

            // ── INSERT：真正写入 ──
            if (analysis.Kind == SqlStatementKind.Insert)
            {
                int affected = await dataAccess.ExecuteNonQueryAsync(sql, ct);
                return AiActionResult.Ok(
                    $"INSERT 成功：写入 {affected} 行",
                    new
                    {
                        code,
                        executed = true,
                        kind = "Insert",
                        affected_rows = affected,
                        estimated_rows = analysis.InsertRowEstimate,
                        sql
                    });
            }

            // ── SELECT：只读查询 ──
            string finalSql = WrapWithLimit(sql, limit);
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
