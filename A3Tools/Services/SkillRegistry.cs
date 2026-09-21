using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using A3Tools.Models;

namespace A3Tools.Services;

/// <summary>
/// ★ 2026-09-20 陛下需求：Skill 注册表
///   从 DATA/skills/*.md 加载所有 Skill（Markdown + 简易 frontmatter）
///   单例、5 秒缓存、自动热加载（陛下编辑后下次调用自动重载）
/// </summary>
public class SkillRegistry
{
    private static readonly Lazy<SkillRegistry> _instance = new(() => new SkillRegistry());
    public static SkillRegistry Instance => _instance.Value;

    private readonly Dictionary<string, SkillDefinition> _skills = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastLoadTime = DateTime.MinValue;
    private readonly object _lock = new();

    public string SkillsDirectory => Path.Combine(AppContext.BaseDirectory, "DATA", "skills");

    /// <summary>
    /// 获取所有 Skill（按分类、名称排序）
    /// </summary>
    public List<SkillDefinition> List()
    {
        ReloadIfStale();
        lock (_lock)
        {
            return _skills.Values
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .ToList();
        }
    }

    /// <summary>
    /// 按 name 获取 Skill
    /// </summary>
    public SkillDefinition? Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        ReloadIfStale();
        lock (_lock)
        {
            return _skills.TryGetValue(name, out var s) ? s : null;
        }
    }

    /// <summary>
    /// 强制重载（陛下编辑文件后立即调，或测试用）
    /// </summary>
    public void Reload()
    {
        lock (_lock)
        {
            _skills.Clear();
            try
            {
                if (!Directory.Exists(SkillsDirectory))
                {
                    Directory.CreateDirectory(SkillsDirectory);
                }

                foreach (var file in Directory.GetFiles(SkillsDirectory, "*.md"))
                {
                    try
                    {
                        var skill = ParseSkillFile(file);
                        if (skill != null && !string.IsNullOrEmpty(skill.Name))
                        {
                            _skills[skill.Name] = skill;
                        }
                    }
                    catch
                    {
                        // 单个文件解析失败不影响其他文件
                    }
                }
            }
            catch
            {
                // 目录扫描失败不影响系统运行
            }
            _lastLoadTime = DateTime.UtcNow;
        }
    }

    private void ReloadIfStale()
    {
        // ★ 简单缓存：5 秒重载一次（避免每次 List 都扫盘）
        //   陛下编辑文件后 5 秒内下次 AI 调用就会看到最新内容
        if ((DateTime.UtcNow - _lastLoadTime).TotalSeconds > 5)
        {
            Reload();
        }
    }

    /// <summary>
    /// 解析单个 .md 文件
    /// 文件格式：
    ///   ---
    ///   name: upgrade_account
    ///   title: 升级账户
    ///   category: 账套升级
    ///   requires_confirmation: true
    ///   description: 简述...
    ///   parameters: source_code=标准账套; target_code=目标账套
    ///   ---
    ///   # 正文 markdown...
    /// </summary>
    private static SkillDefinition? ParseSkillFile(string path)
    {
        var content = File.ReadAllText(path);
        var skill = new SkillDefinition
        {
            FilePath = path,
            Name = Path.GetFileNameWithoutExtension(path)
        };

        // 必须以 --- 开头表示有 frontmatter
        if (!content.TrimStart('\r', '\n').StartsWith("---"))
        {
            skill.Body = content;
            return skill;
        }

        var lines = content.Split('\n');
        int endLine = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                endLine = i;
                break;
            }
        }

        if (endLine < 0)
        {
            skill.Body = content;
            return skill;
        }

        // 正文 = --- 之后的所有行
        skill.Body = string.Join('\n', lines.Skip(endLine + 1)).TrimStart('\n', '\r');

        // 解析 frontmatter（顶层 key: value + 缩进续行）
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string currentKey = "";
        var currentValue = new StringBuilder();

        for (int i = 1; i < endLine; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // 顶层 key（无前导空格）
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && line.Contains(':'))
            {
                if (!string.IsNullOrEmpty(currentKey))
                {
                    properties[currentKey] = currentValue.ToString().Trim();
                }
                var colonIdx = line.IndexOf(':');
                currentKey = line.Substring(0, colonIdx).Trim();
                var value = line.Substring(colonIdx + 1).Trim();
                currentValue.Clear();
                currentValue.Append(value);
            }
            else if (!string.IsNullOrEmpty(currentKey) && (line.StartsWith("  ") || line.StartsWith("\t")))
            {
                // 缩进续行（多行值）
                if (currentValue.Length > 0) currentValue.Append(' ');
                currentValue.Append(trimmed);
            }
        }
        // flush 最后一个
        if (!string.IsNullOrEmpty(currentKey))
        {
            properties[currentKey] = currentValue.ToString().Trim();
        }

        // 应用属性
        if (properties.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
            skill.Name = name;
        if (properties.TryGetValue("title", out var title)) skill.Title = title;
        if (properties.TryGetValue("category", out var cat)) skill.Category = cat;
        if (properties.TryGetValue("description", out var desc)) skill.Description = desc;
        if (properties.TryGetValue("requires_confirmation", out var rc) &&
            bool.TryParse(rc, out var b))
            skill.RequiresConfirmation = b;
        if (properties.TryGetValue("parameters", out var parms))
        {
            // 格式：key1=描述1; key2=描述2
            foreach (var kv in parms.Split(';'))
            {
                var eq = kv.IndexOf('=');
                if (eq > 0)
                {
                    var k = kv.Substring(0, eq).Trim();
                    var v = kv.Substring(eq + 1).Trim();
                    if (!string.IsNullOrEmpty(k))
                        skill.Parameters[k] = v;
                }
            }
        }

        return skill;
    }
}